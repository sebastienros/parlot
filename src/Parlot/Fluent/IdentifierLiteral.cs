#if NET8_0_OR_GREATER
using Parlot.Compilation;
using System;
using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

internal sealed class IdentifierLiteral : Parser<TextSpan>, ICompilable
{
    private static readonly MethodInfo _parseSpanMethodInfo = typeof(IdentifierLiteral).GetMethod(nameof(ParseSpan), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly SearchValues<char> _startSearchValues;
    private readonly SearchValues<char> _partSearchValues;

    public IdentifierLiteral(SearchValues<char> startSearchValues, SearchValues<char> partSearchValues)
    {
        _startSearchValues = startSearchValues;
        _partSearchValues = partSearchValues;

        // Since we assume these can't container new lines, we can check this here.
        if (startSearchValues.Contains('\n') || startSearchValues.Contains('\r') || partSearchValues.Contains('\n') || partSearchValues.Contains('\r'))
        {
            throw new InvalidOperationException("Identifiers cannot contain new lines.");
        }

        Name = "IdentifierLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (ParseSpan(context.Scanner.Cursor, _startSearchValues, _partSearchValues, out var span))
        {
            result.Set(span.Offset, span.Offset + span.Length, span);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    private static bool ParseSpan(Cursor cursor, SearchValues<char> startSearchValues, SearchValues<char> partSearchValues, out TextSpan span)
    {
        var cursorSpan = cursor.Span;

        if (cursorSpan.Length == 0 || !startSearchValues.Contains(cursorSpan[0]))
        {
            span = default;
            return false;
        }

        var index = cursorSpan.Slice(1).IndexOfAnyExcept(partSearchValues);

        // If index == -1 the whole input is a match
        var size = index == -1 ? cursorSpan.Length : index + 1;

        var start = cursor.Offset;
        cursor.AdvanceNoNewLines(size);
        span = new TextSpan(cursor.Buffer, start, size);
        return true;
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
                    Expression.Constant(_startSearchValues),
                    Expression.Constant(_partSearchValues),
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
#endif
