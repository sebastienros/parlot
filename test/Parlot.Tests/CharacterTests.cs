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
        public void ShouldDescodeString(string text, string expected)
        {
            Assert.Equal(expected, Character.DecodeString(text));
        }
    }
}
