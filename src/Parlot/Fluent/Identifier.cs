using Parlot.Compilation;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class Identifier : Parser<TextSpan>, ICompilable
{
    private static readonly MethodInfo _isIdentifierStartMethodInfo = typeof(Character).GetMethod(nameof(Character.IsIdentifierStart))!;
    private static readonly MethodInfo _isIdentifierPartMethodInfo = typeof(Character).GetMethod(nameof(Character.IsIdentifierPart))!;

    private readonly Func<char, bool>? _extraStart;
    private readonly Func<char, bool>? _extraPart;

    public Identifier(Func<char, bool>? extraStart = null, Func<char, bool>? extraPart = null)
    {
        _extraStart = extraStart;
        _extraPart = extraPart;

        Name = "Identifier";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var first = cursor.Current;

        if (Character.IsIdentifierStart(first) || (_extraStart != null && _extraStart(first)))
        {
            var start = cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.
            // The chars are counted first such that the cursor only moves once.

            var span = cursor.Span;

            if (span.IsEmpty)
            {
                // A custom start predicate accepted the end of the text
                cursor.AdvanceNoNewLines(1);
            }
            else
            {
                var extraPart = _extraPart;
                var size = 1;

                while (size < span.Length)
                {
                    var c = span[size];

                    if (!Character.IsIdentifierPart(c) && (extraPart == null || !extraPart(c)))
                    {
                        break;
                    }

                    size++;
                }

                cursor.AdvanceNoNewLines(size);
            }

            var end = cursor.Offset;

            result.Set(start, end, new TextSpan(cursor.Buffer, start, end - start));

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        // var first = context.Scanner.Cursor.Current;

        var first = Expression.Parameter(typeof(char), $"first{context.NextNumber}");
        result.Body.Add(Expression.Assign(first, context.Current()));
        result.Variables.Add(first);

        //
        // success = false;
        // TextSpan value;
        // 
        // if (Character.IsIdentifierStart(first) [_extraStart != null] || _extraStart(first))
        // {
        //    var start = context.Scanner.Cursor.Offset;
        //
        //    context.Scanner.Cursor.Advance();
        //    
        //    while (!context.Scanner.Cursor.Eof && (Character.IsIdentifierPart(context.Scanner.Cursor.Current) || (_extraPart != null && _extraPart(context.Scanner.Cursor.Current))))
        //    {
        //        context.Scanner.Cursor.Advance();
        //    }
        //    
        //    value = new TextSpan(context.Scanner.Buffer, start, context.Scanner.Cursor.Offset - start);
        //    success = true;
        // }

        var start = Expression.Parameter(typeof(int), $"start{context.NextNumber}");

        var breakLabel = Expression.Label($"break_{context.NextNumber}");

        var block = Expression.Block(
            Expression.IfThen(
                Expression.OrElse(
                    Expression.Call(_isIdentifierStartMethodInfo, first),
                    _extraStart != null
                        ? Expression.Invoke(Expression.Constant(_extraStart), first)
                        : Expression.Constant(false, typeof(bool))
                        ),
                Expression.Block(
                    [start],
                    Expression.Assign(start, context.Offset()),
                    context.AdvanceNoNewLine(Expression.Constant(1)),
                    Expression.Loop(
                        Expression.IfThenElse(
                            /* if */ Expression.AndAlso(
                                Expression.Not(context.Eof()),
                                    Expression.OrElse(
                                        Expression.Call(_isIdentifierPartMethodInfo, context.Current()),
                                        _extraPart != null
                                            ? Expression.Invoke(Expression.Constant(_extraPart), context.Current())
                                            : Expression.Constant(false, typeof(bool))
                                        )
                                ),
                            /* then */ context.AdvanceNoNewLine(Expression.Constant(1)),
                            /* else */ Expression.Break(breakLabel)
                            ),
                        breakLabel
                        ),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(context.Offset(), start))),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                )
            )
        );

        result.Body.Add(block);

        return result;
    }
}
