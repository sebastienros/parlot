#if NET8_0_OR_GREATER
using System;
using System.Buffers;

namespace Parlot.Fluent;

internal sealed class IdentifierLiteral : Parser<TextSpan>
{
    private readonly SearchValues<char> _startSearchValues;
    private readonly SearchValues<char> _partSearchValues;

    public IdentifierLiteral(SearchValues<char> startSearchValues, SearchValues<char> partSearchValues)
    {
        _startSearchValues = startSearchValues;
        _partSearchValues = partSearchValues;

        // Since we assume these can't container new lines, we can check this here.
        if (partSearchValues.Contains('\n') || startSearchValues.Contains('\r'))
        {
            throw new InvalidOperationException("Identifiers cannot contain new lines.");
        }

        Name = "IdentifierLiteral";
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var span = context.Scanner.Cursor.Span;

        if (span.Length == 0 || !_startSearchValues.Contains(span[0]))
        {
            context.ExitParser(this);
            return false;
        }

        var index = span.Slice(1).IndexOfAnyExcept(_partSearchValues);

        // If index == -1 the whole input is a match
        var size = index == -1 ? span.Length : index + 1;

        var start = context.Scanner.Cursor.Position.Offset;
        context.Scanner.Cursor.AdvanceNoNewLines(size);
        result.Set(start, start + size, new TextSpan(context.Scanner.Buffer, start, size));

        context.ExitParser(this);
        return true;
    }
}
#endif
