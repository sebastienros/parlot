#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using System;
using System.Buffers;
using System.Linq;

namespace Parlot.Fluent;

internal sealed class IdentifierLiteral : Parser<TextSpan>, ISeekable
{
    private readonly SearchValues<char> _startSearchValues;
    private readonly SearchValues<char> _partSearchValues;

    // The chars of the start search values can't be listed from them, so they are passed when they are known
    public IdentifierLiteral(SearchValues<char> startSearchValues, SearchValues<char> partSearchValues, char[]? startChars = null)
    {
        _startSearchValues = startSearchValues;
        _partSearchValues = partSearchValues;

        if (startChars is { Length: > 0 })
        {
            CanSeek = true;
            ExpectedChars = startChars.Distinct().ToArray();
        }

        // Since we assume these can't container new lines, we can check this here.
        if (startSearchValues.Contains('\n') || startSearchValues.Contains('\r') || partSearchValues.Contains('\n') || partSearchValues.Contains('\r'))
        {
            throw new InvalidOperationException("Identifiers cannot contain new lines.");
        }

        Name = "IdentifierLiteral";
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

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
        result.Set(start, start + size, context.Scanner.Cursor.CreateSpan(start, size));

        context.ExitParser(this);
        return true;
    }
}
#endif
