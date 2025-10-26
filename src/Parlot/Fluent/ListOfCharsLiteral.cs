#if !NET8_0_OR_GREATER
using Parlot.Rewriting;
using System;

namespace Parlot.Fluent;

internal sealed class ListOfChars : Parser<TextSpan>, ISeekable
{
    private readonly CharMap<object> _map = new();
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;
    private readonly bool _hasNewLine;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public ListOfChars(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        foreach (var c in values)
        {
            _map.Set(c, new object());

            if (Character.IsNewLine(c))
            {
                _hasNewLine = true;
            }
        }

        if (_minSize > 0 && !_negate)
        {
            ExpectedChars = values.ToString().ToCharArray();
            CanSeek = true;
        }

        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var span = cursor.Span;
        var start = cursor.Offset;

        var size = 0;
        var maxLength = _maxSize > 0 ? Math.Min(span.Length, _maxSize) : span.Length;

        for (var i = 0; i < maxLength; i++)
        {
            if (_map[span[i]] == null != _negate)
            {
                break;
            }

            size++;
        }

        if (size < _minSize)
        {
            context.ExitParser(this);
            return false;
        }

        if (_hasNewLine)
        {
            cursor.Advance(size);
        }
        else
        {
            cursor.AdvanceNoNewLines(size);
        }

        result.Set(start, start + size, new TextSpan(context.Scanner.Buffer, start, size));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"AnyOf([{string.Join(", ", ExpectedChars)}])";
}
#endif
