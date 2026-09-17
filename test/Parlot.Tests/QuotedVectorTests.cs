using System;
using Xunit;

namespace Parlot.Tests;

public class QuotedVectorTests
{
    [Fact]
    public void PreservesShortTailsCustomQuotesUnicodeAndFailurePosition()
    {
        foreach (var quote in new[] { '\'', '"', '`', '\u0122' })
        {
            for (var length = 0; length <= 65; length++)
            {
                var text = quote + new string('\u0422', length) + quote;
                var scanner = new Scanner("a\n" + text + "!");
                scanner.Cursor.Advance(2);
                Assert.True(scanner.ReadQuotedString(quote, out var value));
                Assert.Equal(text, value.ToString());
                Assert.Equal('!', scanner.Cursor.Current);
                scanner = new Scanner("a\n" + quote + new string('\u0422', length) + "\\q" + quote);
                scanner.Cursor.Advance(2);
                var start = scanner.Cursor.Position;
                Assert.False(scanner.ReadQuotedString(quote, out _));
                Assert.Equal(start, scanner.Cursor.Position);
                scanner = new Scanner(quote + new string('\u0422', length));
                Assert.False(scanner.ReadQuotedString(quote, out _));
                Assert.Equal(TextPosition.Start, scanner.Cursor.Position);
            }
        }
    }
}
