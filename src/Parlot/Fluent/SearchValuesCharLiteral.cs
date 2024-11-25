#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using System;
using System.Buffers;

namespace Parlot.Fluent;

internal sealed class SearchValuesCharLiteral : Parser<TextSpan>, ISeekable
{
    private readonly SearchValues<char> _searchValues;
    private readonly int _minSize;
    private readonly int _maxSize;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public SearchValuesCharLiteral(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0)
    {
        _searchValues = searchValues ?? throw new ArgumentNullException(nameof(searchValues));
        _minSize = minSize;
        _maxSize = maxSize;
    }

    public SearchValuesCharLiteral(ReadOnlySpan<char> searchValues, int minSize = 1, int maxSize = 0)
    {
        _searchValues = SearchValues.Create(searchValues);
        _minSize = minSize;
        _maxSize = maxSize;

        CanSeek = true;
        ExpectedChars = searchValues.ToArray();
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var span = context.Scanner.Cursor.Span;

        // First char not matching the searched values
        var index = span.IndexOfAnyExcept(_searchValues);

        if (index != -1)
        {
            // Too small?
            if (index == 0 || index < _minSize)
            {
                return false;
            }

            // Too large?
            if (_maxSize > 0 && index > _maxSize)
            {
                return false;
            }
        }

        // If index == -1 the whole input is a match
        var size = index == -1 ? span.Length : index;

        var start = context.Scanner.Cursor.Position.Offset;
        context.Scanner.Cursor.Advance(size);
        result.Set(start, start + size, new TextSpan(context.Scanner.Buffer, start, size));
        return true;
    }
}
#endif
