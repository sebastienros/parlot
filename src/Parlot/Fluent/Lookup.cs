using Parlot.Rewriting;
using System;

namespace Parlot.Fluent;

internal sealed class Lookup : Parser<TextSpan>, ISeekable
{
    private readonly CharMap<object> _map = new();
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _hasNewLine;

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public Lookup(string values, int minSize = 1, int maxSize = 0)
    {
        foreach (var c in values)
        {
            _map.Set(c, new object());

            if (Character.IsNewLine(c))
            {
                _hasNewLine = true;
            }
        }

        ExpectedChars = values.ToCharArray();
        _minSize = minSize;
        _maxSize = maxSize;
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
            if (_map[span[i]] == null)
            {
                break;
            }

            size++;
        }

        if (size < _minSize)
        {
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

        return true;
    }
}
