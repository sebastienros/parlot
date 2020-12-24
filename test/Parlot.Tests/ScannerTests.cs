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
            Assert.False(s.ReadQuotedString());
        }

        [Theory]
        [InlineData("'Lorem ipsum'", "'Lorem ipsum'")]
        [InlineData("\"Lorem ipsum\"", "\"Lorem ipsum\"")]
        public void ShouldReadEscapedStringWithMatchingQuotes(string text, string expected)
        {
            Scanner s = new(text);
            var result = new TokenResult();
            var success = s.ReadQuotedString(result);
            Assert.True(success);
            Assert.Equal(expected, result.Token.Text);
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
            var result = new TokenResult();
            var success = s.ReadQuotedString(result);
            Assert.True(success);
            Assert.Equal(expected, result.Token.Text);
        }

        [Theory]
        [InlineData("'Lorem \\w ipsum'")]
        [InlineData("'Lorem \\u12 ipsum'")]
        [InlineData("'Lorem \\xg ipsum'")]
        public void ShouldNotReadStringWithInvalidEscapes(string text)
        {
            Scanner s = new(text);
            Assert.False(s.ReadQuotedString());
        }

        [Fact]
        public void SkipWhitespaceShouldSkipWhitespace()
        {
            Scanner s = new("Lorem ipsum   \r\n   ");

            Assert.False(s.SkipWhiteSpace());

            s.ReadNonWhiteSpace();

            Assert.True(s.SkipWhiteSpace());
            Assert.Equal(6, s.Cursor.Position.Offset);

            s.ReadNonWhiteSpace();

            Assert.True(s.SkipWhiteSpace());
            Assert.Equal(15, s.Cursor.Position.Offset);

            Assert.True(s.SkipWhiteSpaceOrNewLine());
            Assert.Equal(19, s.Cursor.Position.Offset);

            Assert.True(s.Cursor.Eof);
        }

    }
}
