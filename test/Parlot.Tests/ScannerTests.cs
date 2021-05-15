using System;
using Xunit;
using Scanner = Parlot.Scanner<char>;

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
            Scanner s = new(text.ToCharArray());
            Assert.False(s.ReadQuotedString());
        }

        [Theory]
        [InlineData("'Lorem ipsum'", "'Lorem ipsum'")]
        [InlineData("\"Lorem ipsum\"", "\"Lorem ipsum\"")]
        public void ShouldReadEscapedStringWithMatchingQuotes(string text, string expected)
        {
            Scanner s = new(text.ToCharArray());
            var success = s.ReadQuotedString(out var result);
            Assert.True(success);
            Assert.Equal(expected, result.ToString());
        }

        [Theory]
        [InlineData("'Lorem \\n ipsum'", "'Lorem \\n ipsum'")]
        [InlineData("\"Lorem \\n ipsum\"", "\"Lorem \\n ipsum\"")]
        [InlineData("\"Lo\\trem \\n ipsum\"", "\"Lo\\trem \\n ipsum\"")]
        [InlineData("'Lorem \\u1234 ipsum'", "'Lorem \\u1234 ipsum'")]
        [InlineData("'Lorem \\xabcd ipsum'", "'Lorem \\xabcd ipsum'")]
        public void ShouldReadStringWithEscapes(string text, string expected)
        {
            Scanner s = new(text.ToCharArray());
            var success = s.ReadQuotedString(out var result);
            Assert.True(success);
            Assert.Equal(expected, result.ToString());
        }

        [Theory]
        [InlineData("'Lorem \\w ipsum'")]
        [InlineData("'Lorem \\u12 ipsum'")]
        [InlineData("'Lorem \\xg ipsum'")]
        public void ShouldNotReadStringWithInvalidEscapes(string text)
        {
            Scanner s = new(text.ToCharArray());
            Assert.False(s.ReadQuotedString());
        }

        [Fact]
        public void SkipWhitespaceShouldSkipWhitespace()
        {
            // New lines are not considered white spaces
            Scanner s = new("Lorem ipsum   \r\n   ".ToCharArray());

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

            Assert.False(new Scanner("a".ToCharArray()).SkipWhiteSpaceOrNewLine());
        }

        [Fact]
        public void ReadIdentifierShouldReadIdentifier()
        {
            Scanner s = new("a $abc 123".ToCharArray());

            Assert.True(s.ReadIdentifier(out var result));
            Assert.Equal("a", result.ToString());

            s.SkipWhiteSpace();

            Assert.True(s.ReadIdentifier(out result));
            Assert.Equal("$abc", result.ToString());

            s.SkipWhiteSpace();

            Assert.False(s.ReadIdentifier());
        }

        [Fact]
        public void ReadCharShouldReadSingleChar()
        {
            Scanner s = new("aaa".ToCharArray());

            Assert.True(s.ReadChar('a', out var result));
            Assert.Equal("a", result.ToString());

            Assert.True(s.ReadChar('a', out result));
            Assert.Equal("a", result.ToString());

            Assert.True(s.ReadChar('a', out result));
            Assert.Equal("a", result.ToString());

            Assert.False(s.ReadChar('a'));
        }

        [Fact]
        public void ReadTextShouldBeCaseSensitiveByDefault()
        {
            Scanner s = new("abcd".ToCharArray());

            // We test each char position because to verify specific optimizations
            // in the implementation.
            Assert.False(s.ReadText("Abcd"));
            Assert.False(s.ReadText("aBcd"));
            Assert.False(s.ReadText("abCd"));
            Assert.False(s.ReadText("abcD"));
            Assert.True(s.ReadText("ABCD", comparer: StringComparer.OrdinalIgnoreCase, out var result));
            Assert.Equal("abcd", result.ToString());
        }

        [Fact]
        public void ReadTextShouldReadTheFullTextOrNothing()
        {
            Scanner s = new("abcd".ToCharArray());

            Assert.False(s.ReadText("abcde"));
            Assert.False(s.ReadText("abd"));

            Assert.True(s.ReadText("abc", out var result));
            Assert.Equal("abc", result.ToString());

            Assert.True(s.ReadText("d", out result));
            Assert.Equal("d", result.ToString());
        }

        [Fact]
        public void ReadSingleQuotedStringShouldReadSingleQuotedStrings()
        {
            new Scanner("'abcd'".ToCharArray()).ReadSingleQuotedString(out var result);
            Assert.Equal("'abcd'", result.ToString());

            new Scanner("'a\\nb'".ToCharArray()).ReadSingleQuotedString(out result);
            Assert.Equal("'a\\nb'", result.ToString());

            Assert.False(new Scanner("'abcd".ToCharArray()).ReadSingleQuotedString());
            Assert.False(new Scanner("abcd'".ToCharArray()).ReadSingleQuotedString());
            Assert.False(new Scanner("ab\\'cd".ToCharArray()).ReadSingleQuotedString());
        }

        [Fact]
        public void ReadDoubleQuotedStringShouldReadDoubleQuotedStrings()
        {
            new Scanner("\"abcd\"".ToCharArray()).ReadDoubleQuotedString(out var result);
            Assert.Equal("\"abcd\"", result.ToString());

            new Scanner("\"a\\nb\"".ToCharArray()).ReadDoubleQuotedString(out result);
            Assert.Equal("\"a\\nb\"", result.ToString());

            Assert.False(new Scanner("\"abcd".ToCharArray()).ReadDoubleQuotedString());
            Assert.False(new Scanner("abcd\"".ToCharArray()).ReadDoubleQuotedString());
            Assert.False(new Scanner("\"ab\\\"cd".ToCharArray()).ReadDoubleQuotedString());
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
            Assert.True(new Scanner(text.ToCharArray()).ReadDecimal(out var result));
            Assert.Equal(expected, result.ToString());
        }

        [Theory]
        [InlineData(" 1")]
        [InlineData("123.")]
        public void ShouldNotReadInvalidDecimal(string text)
        {
            Assert.False(new Scanner(text.ToCharArray()).ReadDecimal());
        }

        [Theory]
        [InlineData("'a\nb' ", "'a\nb'")]
        [InlineData("'a\r\nb' ", "'a\r\nb'")]
        public void ShouldReadStringsWithLineBreaks(string text, string expected)
        {
            Assert.True(new Scanner(text.ToCharArray()).ReadSingleQuotedString(out var result));
            Assert.Equal(expected, result.ToString());
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
            new Scanner(text.ToCharArray()).ReadQuotedString(out var result);
            Assert.Equal(expected, result.ToString());
        }
    }
}
