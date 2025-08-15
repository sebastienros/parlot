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
    private readonly bool _negate;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public SearchValuesCharLiteral(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _searchValues = searchValues ?? throw new ArgumentNullException(nameof(searchValues));
        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;
    }

    public SearchValuesCharLiteral(ReadOnlySpan<char> searchValues, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _searchValues = SearchValues.Create(searchValues);
        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;

        if (minSize > 0 && !_negate)
        {
            CanSeek = true;
            ExpectedChars = searchValues.ToArray();
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var span = context.Scanner.Cursor.Span;

        if (_minSize > span.Length)
        {
            return false;
        }

        // First char not matching the searched values
        var index = _negate ? span.IndexOfAny(_searchValues) : span.IndexOfAnyExcept(_searchValues);

        var size = 0;

        if (index != -1)
        {
            // Too small?
            if (index < _minSize)
            {
                context.ExitParser(this);
                return false;
            }

            size = index;
        }
        else
        {
            // If index == -1 the whole input is a match
            size = span.Length;
        }

        // Too large? Take only the request size
        if (_maxSize > 0 && size > _maxSize)
        {
            size = _maxSize;
        }

        var start = context.Scanner.Cursor.Position.Offset;
        context.Scanner.Cursor.Advance(size);
        result.Set(start, start + size, new TextSpan(context.Scanner.Buffer, start, size));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"AnyOf({_searchValues})";
}
#endif
