using System;

namespace Parlot.Tests;

/// <summary>
/// Text scanners which match only at the start of the input or of a line, reading the char before their token.
/// Linked into the source generator tests.
/// </summary>
internal static class LineStartScanners
{
    /// <summary>
    /// An HTML-like close comment of JavaScript (Annex B): <c>--&gt;</c> up to the end of the line, only at the start of the
    /// input or of a line. <c>x--&gt;y</c> is <c>x-- &gt; y</c>.
    /// </summary>
    public static int HtmlCloseComment(ReadOnlySpan<char> text, int position)
    {
        var span = text.Slice(position);

        if (!span.StartsWith("-->".AsSpan()))
        {
            // More text could complete a "-->"
            return span.Length < 3 && "-->".AsSpan().StartsWith(span) ? -1 : 0;
        }

        // Checked after the token, which can be scanned again with more text: the text before must still be there
        if (position > 0 && text[position - 1] is not ('\n' or '\r'))
        {
            return 0;
        }

        var end = span.Slice(3).IndexOfAny('\n', '\r');

        // A comment reaching the end of the text is scanned again when more text follows
        return end < 0 ? span.Length : 3 + end;
    }
}
