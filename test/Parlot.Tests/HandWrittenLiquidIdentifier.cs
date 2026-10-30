using Parlot.Fluent;
using Parlot.Rewriting;

namespace Parlot.Tests;

/// <summary>
/// The Liquid identifier written as a <see cref="Parser{T}"/> subclass, as Fluid's IdentifierParser does.
/// </summary>
internal sealed class HandWrittenLiquidIdentifier : Parser<TextSpan>, ISeekable
{
    public bool CanSeek => true;

    public char[] ExpectedChars { get; } = LiquidScanners.StartChars.ToCharArray();

    public bool SkipWhitespace => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var current = cursor.Current;
        var nonDigits = 0;
        var lastIsDash = false;
        var start = cursor.Position;
        var lastDashPosition = cursor.Position;

        if (IsNonDigitStart(current))
        {
            nonDigits++;
        }
        else if (!IsDigit(current))
        {
            context.ExitParser(this);
            return false;
        }

        cursor.Advance();

        while (!cursor.Eof)
        {
            current = cursor.Current;

            if (IsNonDigitStart(current))
            {
                nonDigits++;
                lastIsDash = false;
            }
            else if (current == '-')
            {
                lastDashPosition = cursor.Position;
                nonDigits++;
                lastIsDash = true;
            }
            else if (IsDigit(current))
            {
                lastIsDash = false;
            }
            else
            {
                break;
            }

            cursor.Advance();
        }

        var end = cursor.Offset;
        current = cursor.Current;

        if (lastIsDash && !cursor.Eof && (current == '%' || current == '}'))
        {
            nonDigits--;
            end--;
            cursor.ResetPosition(lastDashPosition);
        }

        if (nonDigits == 0)
        {
            cursor.ResetPosition(start);
            context.ExitParser(this);
            return false;
        }

        result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, end - start.Offset));

        context.ExitParser(this);
        return true;
    }

    private static bool IsNonDigitStart(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';

    private static bool IsDigit(char c) => c >= '0' && c <= '9';
}
