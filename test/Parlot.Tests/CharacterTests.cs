using Xunit;

namespace Parlot.Tests
{
    public class CharacterTests
    {
        [Theory]
        [InlineData("a\\bc", "a\bc")]
        [InlineData("\\xa0", "\xa0")]
        [InlineData("\\xfh", "\xfh")]
        [InlineData("\\u1234", "\u1234")]

        [InlineData(" a\\bc ", " a\bc ")]
        [InlineData(" \\xa0 ", " \xa0 ")]
        [InlineData(" \\xfh ", " \xfh ")]
        [InlineData(" \u03B2 ", " β ")]

        public void ShouldDescodeString(string text, string expected)
        {
            Assert.Equal(expected, Character.DecodeString(new TextSpan(text)).ToString());
        }

        [Fact]
        public void ShouldDescodeStringInBuffer()
        {
            var span = new TextSpan("   a\\nbc   ", 3, 5);
            Assert.Equal("a\nbc", Character.DecodeString(span).ToString());
        }

        [Theory]
        [InlineData('\x09', true)]
        [InlineData('\x20', true)]
        [InlineData('\xa0', true)]
        [InlineData('\v', false)]
        [InlineData('\n', false)]
        [InlineData('\r', false)]
        [InlineData('a', false)]
        public void ShouldDetectWhiteSpace(char c, bool isWhiteSpace)
        {
            Assert.Equal(isWhiteSpace, Character.IsWhiteSpace(c));
        }

        [Theory]
        [InlineData('\x09', true)]
        [InlineData('\x20', true)]
        [InlineData('\xa0', true)]
        [InlineData('\v', true)]
        [InlineData('\n', true)]
        [InlineData('\r', true)]
        [InlineData('a', false)]
        public void ShouldDetectWhiteSpaceOrNewLines(char c, bool isWhiteSpace)
        {
            Assert.Equal(isWhiteSpace, Character.IsWhiteSpace(c));
        }
    }
}
