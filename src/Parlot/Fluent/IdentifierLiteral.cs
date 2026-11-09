#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using System;
using System.Buffers;
using System.Collections.Generic;

namespace Parlot.Fluent;

internal sealed class IdentifierLiteral : Parser<TextSpan>, ISeekable, ILoopAnalyzable
{
    private readonly SearchValues<char> _startSearchValues;
    private readonly SearchValues<char> _partSearchValues;

    // The chars of the start search values can't be listed from them, so they are passed when they are known, and found
    // by testing every char otherwise
    public IdentifierLiteral(SearchValues<char> startSearchValues, SearchValues<char> partSearchValues, char[]? startChars = null)
    {
        _startSearchValues = startSearchValues;
        _partSearchValues = partSearchValues;

        // A large set of non-ASCII chars is declared as any non-ASCII char, see SeekableHints.ExpectedChars()
        var expectedChars = startChars != null ? SeekableHints.ExpectedChars(startChars) : FindStartChars(startSearchValues);

        if (expectedChars.Length > 0)
        {
            CanSeek = true;
            ExpectedChars = expectedChars;
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

    private static char[] FindStartChars(SearchValues<char> startSearchValues)
    {
        var chars = new List<char>();
        var nonAscii = 0;

        for (var c = 0; c <= char.MaxValue; c++)
        {
            if (startSearchValues.Contains((char)c))
            {
                if (c < 0x80)
                {
                    chars.Add((char)c);
                }
                else if (++nonAscii <= SeekableHints.MaxNonAsciiChars)
                {
                    chars.Add((char)c);
                }
                else
                {
                    // The ASCII chars are all listed, the non-ASCII ones are any of them
                    chars.RemoveAll(static x => x >= 0x80);
                    chars.Add(OneOf<object>.NonAsciiSeekableChar);
                    break;
                }
            }
        }

        return chars.ToArray();
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

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

        var start = context.Scanner.Cursor.Offset;
        context.Scanner.Cursor.AdvanceNoNewLines(size);
        result.Set(start, start + size, context.Scanner.Cursor.CreateSpan(start, size));

        context.ExitParser(this);
        return true;
    }
}
#endif
