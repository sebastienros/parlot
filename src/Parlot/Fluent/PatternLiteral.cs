using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class PatternLiteral : Parser<TextSpan>, ICompilable
{
    private readonly Func<char, bool> _predicate;
    private readonly int _minSize;
    private readonly int _maxSize;

    public PatternLiteral(Func<char, bool> predicate, int minSize = 1, int maxSize = 0)
    {
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        _minSize = minSize;
        _maxSize = maxSize;

        Name = "PatternLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var predicate = _predicate;

        if (cursor.Eof || !predicate(cursor.Current))
        {
            context.ExitParser(this);
            return false;
        }

        // The chars are counted first such that the cursor only moves once, and not at all when the size constraint is not met.

        var span = cursor.Span;
        var limit = _maxSize > 0 && _maxSize < span.Length ? _maxSize : span.Length;
        var size = 1;
        var newLines = span[0] is '\n' or '\r';

        while (size < limit)
        {
            var c = span[size];

            if (!predicate(c))
            {
                break;
            }

            newLines |= c is '\n' or '\r';
            size++;
        }

        if (size >= _minSize)
        {
            var start = cursor.Offset;

            // The line and column only need to be tracked char by char when there are new lines,
            // including the char the cursor moves to since a '\r' doesn't count as a column.
            if (newLines || (size < span.Length && span[size] is '\n' or '\r'))
            {
                cursor.Advance(size);
            }
            else
            {
                cursor.AdvanceNoNewLines(size);
            }

            result.Set(start, start + size, new TextSpan(cursor.Buffer, start, size));

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        // var start = context.Scanner.Cursor.Position;

        var start = Expression.Variable(typeof(TextPosition), $"start{context.NextNumber}");
        result.Variables.Add(start);

        result.Body.Add(Expression.Assign(start, context.Position()));

        // var size = 0;

        var size = Expression.Variable(typeof(int), $"size{context.NextNumber}");
        result.Variables.Add(size);
        result.Body.Add(Expression.Assign(size, Expression.Constant(0, typeof(int))));

        // while (true)
        // {
        //     if (context.Scanner.Cursor.Eof)
        //     {
        //        break;
        //     }
        //
        //     if (!_predicate(context.Scanner.Cursor.Current))
        //     {
        //        break;
        //     }
        //
        //     context.Scanner.Cursor.Advance();
        // 
        //     size++;
        //
        //     #if _maxSize > 0 ?
        //     if (size == _maxSize)
        //     {
        //        break;
        //     }
        //     #endif
        // }

        var breakLabel = Expression.Label($"break{context.NextNumber}");

        result.Body.Add(
            Expression.Loop(
                Expression.Block(
                    Expression.IfThen(
                        context.Eof(),
                        Expression.Break(breakLabel)
                    ),
                    Expression.IfThen(
                        Expression.Not(Expression.Invoke(Expression.Constant(_predicate), context.Current())),
                        Expression.Break(breakLabel)
                    ),
                    context.Advance(),
                    Expression.Assign(size, Expression.Add(size, Expression.Constant(1))),
                    _maxSize == 0
                    ? Expression.Empty()
                    : Expression.IfThen(
                        Expression.Equal(size, Expression.Constant(_maxSize)),
                        Expression.Break(breakLabel)
                        )
                ),
                breakLabel)
            );


        // if (size < _minSize)
        // {
        //     context.Scanner.Cursor.ResetPosition(startPosition);
        // }
        // else
        // {
        //     value = new TextSpan(context.Scanner.Buffer, start, end - start);
        //     success = true;
        // }

        var startOffset = Expression.Field(start, nameof(TextPosition.Offset));

        result.Body.Add(
            Expression.IfThenElse(
                Expression.LessThan(size, Expression.Constant(_minSize)),
                context.ResetPosition(start),
                Expression.Block(
                    context.DiscardResult
                    ? Expression.Empty()
                    : Expression.Assign(result.Value,
                        context.NewTextSpan(
                            context.Buffer(),
                            startOffset,
                            Expression.Subtract(context.Offset(), startOffset)
                            )),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                    )
                )
            );

        return result;
    }
}
