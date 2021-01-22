using System;
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
            Assert.Equal(expected, result.Text);
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
            Assert.Equal(expected, result.Text);
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
            Assert.Equal(14, s.Cursor.Position.Offset);

            Assert.True(s.SkipWhiteSpaceOrNewLine());
            Assert.Equal(19, s.Cursor.Position.Offset);

            Assert.True(s.Cursor.Eof);

            Assert.False(new Scanner("a").SkipWhiteSpaceOrNewLine());
        }

        [Fact]
        public void ReadIdentifierShouldReadIdentifier()
        {
            Scanner s = new("a $abc 123");
            var result = new TokenResult();

            Assert.True(s.ReadIdentifier(result));
            Assert.Equal("a", result.Text);

            s.SkipWhiteSpace();

            Assert.True(s.ReadIdentifier(result));
            Assert.Equal("$abc", result.Text);

            s.SkipWhiteSpace();

            Assert.False(s.ReadIdentifier(result));
        }

        [Fact]
        public void ReadCharShouldReadSingleChar()
        {
            Scanner s = new("aaa");
            var result = new TokenResult();

            Assert.True(s.ReadChar('a', result));
            Assert.Equal("a", result.Text);

            Assert.True(s.ReadChar('a', result));
            Assert.Equal("a", result.Text);

            Assert.True(s.ReadChar('a', result));
            Assert.Equal("a", result.Text);

            Assert.False(s.ReadChar('a', result));
        }

        [Fact]
        public void ReadTextShouldBeCaseSensitiveByDefault()
        {
            Scanner s = new("abcd");
            var result = new TokenResult();

            // We test each char position because to verify specific optimizations
            // in the implementation.
            Assert.False(s.ReadText("Abcd", result: result));
            Assert.False(s.ReadText("aBcd", result: result));
            Assert.False(s.ReadText("abCd", result: result));
            Assert.False(s.ReadText("abcD", result: result));
            Assert.True(s.ReadText("ABCD", comparer: StringComparer.OrdinalIgnoreCase, result: result));
            Assert.Equal("abcd", result.Text);
        }

        [Fact]
        public void ReadTextShouldReadTheFullTextOrNothing()
        {
            Scanner s = new("abcd");
            var result = new TokenResult();

            Assert.False(s.ReadText("abcde", result: result));
            Assert.False(s.ReadText("abd", result: result));

            Assert.True(s.ReadText("abc", result: result));
            Assert.Equal("abc", result.Text);

            Assert.True(s.ReadText("d", result: result));
            Assert.Equal("d", result.Text);
        }

        [Fact]
        public void ReadSingleQuotedStringShouldReadSingleQuotedStrings()
        {
            var result = new TokenResult();

            new Scanner("'abcd'").ReadSingleQuotedString(result);
            Assert.Equal("'abcd'", result.Text);

            new Scanner("'a\\nb'").ReadSingleQuotedString(result);
            Assert.Equal("'a\\nb'", result.Text);

            Assert.False(new Scanner("'abcd").ReadSingleQuotedString(result));
            Assert.False(new Scanner("abcd'").ReadSingleQuotedString(result));
            Assert.False(new Scanner("ab\\'cd").ReadSingleQuotedString(result));

        }

        [Fact]
        public void ReadDoubleQuotedStringShouldReadDoubleQuotedStrings()
        {
            var result = new TokenResult();

            new Scanner("\"abcd\"").ReadDoubleQuotedString(result);
            Assert.Equal("\"abcd\"", result.Text);

            new Scanner("\"a\\nb\"").ReadDoubleQuotedString(result);
            Assert.Equal("\"a\\nb\"", result.Text);

            Assert.False(new Scanner("\"abcd").ReadDoubleQuotedString());
            Assert.False(new Scanner("abcd\"").ReadDoubleQuotedString());
            Assert.False(new Scanner("\"ab\\\"cd").ReadDoubleQuotedString(result));
        }

        [Theory]
        [InlineData("1", "1")]
        [InlineData("123", "123")]
        [InlineData("123a", "123")]
        [InlineData("123.0", "123.0")]
        [InlineData("123.0a", "123.0")]
        [InlineData("123.01", "123.01")]
        public void ShouldReadValidDecimal(string text, string expected)
        {
            var result = new TokenResult();

            Assert.True(new Scanner(text).ReadDecimal(result));
            Assert.Equal(expected, result.Text);
        }

        [Theory]
        [InlineData(" 1")]
        [InlineData("123.")]
        public void ShouldNotReadInvalidDecimal(string text)
        {
            Assert.False(new Scanner(text).ReadDecimal());
        }

        [Theory]
        [InlineData("'a\nb' ", "'a\nb'")]
        [InlineData("'a\r\nb' ", "'a\r\nb'")]
        public void ShouldReadStringsWithLineBreaks(string text, string expected)
        {
            var result = new TokenResult();

            Assert.True(new Scanner(text).ReadSingleQuotedString(result));
            Assert.Equal(expected, result.Text);
        }

        [Theory]
        [InlineData("'a\\bc'", "'a\\bc'")]
        [InlineData("'\\xa0'", "'\\xa0'")]
        [InlineData("'\\xfh'", "'\\xfh'")]
        [InlineData("'\\u1234'", "'\\u1234'")]
        [InlineData("' a\\bc ' ", "' a\\bc '")]
        [InlineData("' \\xa0 ' ", "' \\xa0 '")]
        [InlineData("' \\xfh ' ", "' \\xfh '")]
        [InlineData("' \\u1234 ' ", "' \\u1234 '")]

        public void ShouldReadUnicodeSequence(string text, string expected)
        {
            var result = new TokenResult();

            new Scanner(text).ReadQuotedString(result);
            Assert.Equal(expected, result.Text);
        }
    }
}
