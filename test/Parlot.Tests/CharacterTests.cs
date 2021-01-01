using System;
using Xunit;

namespace Parlot.Tests
{
    public class CharacterTests
    {
        [Theory]
        [InlineData("a\\bc", "a\bc")]
        public void ShouldDescodeString(string text, string expected)
        {
            Assert.Equal(expected, Character.DecodeString(text.AsSpan()).ToString());
        }

        [Theory]
        [InlineData("\\u0647", '\u0647')]
        [InlineData("\\u03BD", '\u03BD')]
        [InlineData("\\u03BH", '\0')]
        public void ScanHexEscape(string text, char expected)
        {
            Assert.Equal(expected, Character.ScanHexEscape(text.AsSpan(), 1));
        }
    }
}
