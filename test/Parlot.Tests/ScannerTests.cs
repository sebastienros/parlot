using Xunit;

namespace Parlot.Tests
{
    public class ScannerTests
    {
        [Theory]
        [InlineData("Lorem ipsum")]
        [InlineData("'Lorem ipsum")]
        [InlineData("Lorem ipsum'")]
        [InlineData("\"Lorem ipsum")]
        [InlineData("Lorem ipsum\"")]
        [InlineData("'Lorem ipsum\"")]
        [InlineData("\"Lorem ipsum'")]
        public void ShouldNotReadEscapedStringWithoutMatchingQuotes(string text)
        {
            Scanner s = new(text);
            Assert.False(s.ReadQuotedString(out _));
        }

        [Theory]
        [InlineData("'Lorem ipsum'", "'Lorem ipsum'")]
        [InlineData("\"Lorem ipsum\"", "\"Lorem ipsum\"")]
        public void ShouldReadEscapedStringWithMatchingQuotes(string text, string expected)
        {
            Scanner s = new(text);
            var result = s.ReadQuotedString(out var token);
            Assert.True(result);
            Assert.Equal(expected, token.Text);
        }

        [Theory]
        [InlineData("'Lorem \\n ipsum'", "'Lorem \\n ipsum'")]
        [InlineData("\"Lorem \\n ipsum\"", "\"Lorem \\n ipsum\"")]
        [InlineData("\"Lo\\trem \\n ipsum\"", "\"Lo\\trem \\n ipsum\"")]
        [InlineData("'Lorem \\u1234 ipsum'", "'Lorem \\u1234 ipsum'")]
        [InlineData("'Lorem \\xabcd ipsum'", "'Lorem \\xabcd ipsum'")]
        public void ShouldReadStringWithEscapes(string text, string expected)
        {
            Scanner s = new(text);
            var result = s.ReadQuotedString(out var token);
            Assert.True(result);
            Assert.Equal(expected, token.Text);
        }

        [Theory]
        [InlineData("'Lorem \\w ipsum'")]
        [InlineData("'Lorem \\u12 ipsum'")]
        [InlineData("'Lorem \\xg ipsum'")]
        public void ShouldNotReadStringWithInvalidEscapes(string text)
        {
            Scanner s = new(text);
            Assert.False(s.ReadQuotedString(out _));
        }
    }
}
