using System;
using Xunit;

namespace Parlot.Tests;

public class CursorBulkAdvanceTests
{
    [Fact]
    public void BulkAdvanceMatchesSingleStepsAtEveryBoundary()
    {
        // Exhaustive short inputs include consecutive CR/LF and a non-ASCII character.
        for (var length = 0; length <= 5; length++)
        {
            var combinations = 1 << (length * 2);
            for (var bits = 0; bits < combinations; bits++)
            {
                var chars = new char[length];
                for (var i = 0; i < length; i++) chars[i] = "a\r\n\u0122"[(bits >> (i * 2)) & 3];
                Verify(new string(chars));
            }
        }

        // Exercise vector-sized spans, EOF, resets, and specials on either end.
        foreach (var length in new[] { 15, 16, 17, 31, 32, 33, 63, 64, 65, 128 })
        {
            Verify(new string('a', length));
            foreach (var special in new[] { '\r', '\n', '\v', '\u0122' })
            {
                foreach (var index in new[] { 0, 1, length / 2, length - 1 })
                {
                    var chars = new string('a', length).ToCharArray();
                    chars[index] = special;
                    Verify(new string(chars));
                }
            }
        }
    }

    private static void Verify(string text)
    {
        var bulk = new Cursor(text);
        var scalar = new Cursor(text);
        for (var offset = 0; offset <= text.Length; offset++)
        {
            var start = new Cursor(text);
            while (start.Offset < offset) start.Advance();
            for (var iteration = 0; iteration <= text.Length + 3; iteration++)
            {
                var count = iteration == text.Length + 3 ? 64 : iteration;
                bulk.ResetPosition(TextPosition.Start);
                scalar.ResetPosition(TextPosition.Start);
                bulk.ResetPosition(start.Position);
                scalar.ResetPosition(start.Position);
                bulk.Advance(count);
                for (var i = 0; i < count && !scalar.Eof; i++) scalar.Advance();
                Assert.Equal(scalar.Position, bulk.Position);
                Assert.Equal(scalar.Current, bulk.Current);
                Assert.Equal(scalar.Eof, bulk.Eof);
            }
        }
    }
}
