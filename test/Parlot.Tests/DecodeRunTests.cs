using System;
using System.Linq;
using Xunit;

namespace Parlot.Tests;

public class DecodeRunTests
{
    [Fact]
    public void DecodePreservesDenseSparseUnicodeAndBoundaryRuns()
    {
        foreach (var length in new[] { 0, 1, 15, 16, 17, 31, 32, 127, 128, 129, 1024 })
        {
            foreach (var (encoded, decoded) in new[]
            {
                ("\\n", "\n"), ("\\r", "\r"), ("\\t", "\t"), ("\\0", "\0"),
                ("\\a", "\a"), ("\\b", "\b"), ("\\f", "\f"), ("\\v", "\v"),
                ("\\\\", "\\"), ("\\\"", "\""), ("\\'", "'"),
                ("\\u1234", "\u1234"), ("\\x41", "A")
            })
            {
                var plain = new string('\u0122', length);
                var input = string.Concat(Enumerable.Repeat(plain + encoded + "g", 4));
                var expected = string.Concat(Enumerable.Repeat(plain + decoded + "g", 4));
                Assert.Equal(expected, Character.DecodeStringInternal(input));
                var framed = "prefix" + input + "suffix";
                Assert.Equal(expected, Character.DecodeString(new TextSpan(framed, 6, input.Length)).ToString());
            }
        }
    }
}
