using Parlot.Compilation;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class PatternLiteral : Parser<TextSpan>, ICompilable
{
    private static readonly MethodInfo _parseSpanMethodInfo = typeof(PatternLiteral).GetMethod(nameof(ParseSpan), BindingFlags.NonPublic | BindingFlags.Static)!;

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

        if (ParseSpan(context.Scanner.Cursor, _predicate, _minSize, _maxSize, out var span))
        {
            result.Set(span.Offset, span.Offset + span.Length, span);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    private static bool ParseSpan(Cursor cursor, Func<char, bool> predicate, int minSize, int maxSize, out TextSpan span)
    {
        if (cursor.Eof || !predicate(cursor.Current))
        {
            if (minSize > 0)
            {
                span = default;
                return false;
            }

            // A pattern without a minimum size matches nothing, like AnyOf does, and leaves the cursor in place
            span = new TextSpan(cursor.Buffer, cursor.Offset, 0);
            return true;
        }

        // The chars are counted first such that the cursor only moves once, and not at all when the size constraint is not met.

        var cursorSpan = cursor.Span;
        var limit = maxSize > 0 && maxSize < cursorSpan.Length ? maxSize : cursorSpan.Length;
        var size = 1;
        var newLines = cursorSpan[0] is '\n' or '\r';

        while (size < limit)
        {
            var c = cursorSpan[size];

            if (!predicate(c))
            {
                break;
            }

            newLines |= c is '\n' or '\r';
            size++;
        }

        if (size >= minSize)
        {
            var start = cursor.Offset;

            // The line and column only need to be tracked char by char when there are new lines,
            // including the char the cursor moves to since a '\r' doesn't count as a column.
            if (newLines || (size < cursorSpan.Length && cursorSpan[size] is '\n' or '\r'))
            {
                cursor.Advance(size);
            }
            else
            {
                cursor.AdvanceNoNewLines(size);
            }

            span = new TextSpan(cursor.Buffer, start, size);
            return true;
        }

        span = default;
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        var span = Expression.Variable(typeof(TextSpan), $"span{context.NextNumber}");
        result.Variables.Add(span);

        result.Body.Add(
            Expression.IfThen(
                Expression.Call(
                    _parseSpanMethodInfo,
                    context.Cursor(),
                    Expression.Constant(_predicate),
                    Expression.Constant(_minSize),
                    Expression.Constant(_maxSize),
                    span
                ),
                Expression.Block(
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, span),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                )
            )
        );

        return result;
    }
}
