using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class Identifier : Parser<TextSpan>, ICompilable, ISeekable
{
    private static readonly MethodInfo _parseSpanMethodInfo = typeof(Identifier).GetMethod(nameof(ParseSpan), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Func<char, bool>? _extraStart;
    private readonly Func<char, bool>? _extraPart;

    public Identifier(Func<char, bool>? extraStart = null, Func<char, bool>? extraPart = null)
    {
        _extraStart = extraStart;
        _extraPart = extraPart;

        // The chars an extra start predicate accepts are unknown
        if (extraStart == null)
        {
            CanSeek = true;
            ExpectedChars = Character._identifierStartChars;
        }

        Name = "Identifier";
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (ParseSpan(context.Scanner.Cursor, _extraStart, _extraPart, out var span))
        {
            result.Set(span.Offset, span.Offset + span.Length, span);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    private static bool ParseSpan(Cursor cursor, Func<char, bool>? extraStart, Func<char, bool>? extraPart, out TextSpan span)
    {
        var first = cursor.Current;

        if (Character.IsIdentifierStart(first) || (extraStart != null && extraStart(first)))
        {
            var start = cursor.Offset;

            // At this point we have an identifier, read while it's an identifier part.
            // The chars are counted first such that the cursor only moves once.

            var cursorSpan = cursor.Span;

            if (cursorSpan.IsEmpty)
            {
                // A custom start predicate accepted the end of the text
                cursor.AdvanceNoNewLines(1);
            }
            else
            {
                var size = 1;

                while (size < cursorSpan.Length)
                {
                    var c = cursorSpan[size];

                    if (!Character.IsIdentifierPart(c) && (extraPart == null || !extraPart(c)))
                    {
                        break;
                    }

                    size++;
                }

                cursor.AdvanceNoNewLines(size);
            }

            var end = cursor.Offset;

            span = new TextSpan(cursor.Buffer, start, end - start);
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
                    Expression.Constant(_extraStart, typeof(Func<char, bool>)),
                    Expression.Constant(_extraPart, typeof(Func<char, bool>)),
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
