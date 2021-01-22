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
        [InlineData(" \\u1234 ", " \u1234 ")]

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
    }
}
