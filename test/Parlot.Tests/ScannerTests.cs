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
            var success = s.ReadQuotedString(out var result);
            Assert.True(success);
            Assert.Equal(expected, result.GetText());
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
            var success = s.ReadQuotedString(out var result);
            Assert.True(success);
            Assert.Equal(expected, result.GetText());
        }

        [Theory]
        [InlineData("'Lorem ipsum'", "'Lorem ipsum'")]
        [InlineData("'Lorem \n ipsum'", "'Lorem \n ipsum'")]
        [InlineData("'Lorem '' ipsum'", "'Lorem '' ipsum'")]
        [InlineData(@"""Lorem """" ipsum""", "\"Lorem \"\" ipsum\"")]
        public void ShouldReadNonEscapableString(string text, string expected)
        {
            Scanner s = new(text);
            var success = s.ReadNonEscapableSequence(text[0], text[text.Length - 1], out var result);
            Assert.True(success);
            Assert.Equal(expected, result.GetText());
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
            // New lines are not considered white spaces
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

            Assert.True(s.ReadIdentifier(out var result));
            Assert.Equal("a", result.GetText());

            s.SkipWhiteSpace();

            Assert.True(s.ReadIdentifier(out result));
            Assert.Equal("$abc", result.GetText());

            s.SkipWhiteSpace();

            Assert.False(s.ReadIdentifier());
        }

        [Fact]
        public void ReadCharShouldReadSingleChar()
        {
            Scanner s = new("aaa");

            Assert.True(s.ReadChar('a', out var result));
            Assert.Equal("a", result.GetText());

            Assert.True(s.ReadChar('a', out result));
            Assert.Equal("a", result.GetText());

            Assert.True(s.ReadChar('a', out result));
            Assert.Equal("a", result.GetText());

            Assert.False(s.ReadChar('a'));
        }

        [Fact]
        public void ReadTextShouldBeCaseSensitiveByDefault()
        {
            Scanner s = new("abcd");

            // We test each char position because to verify specific optimizations
            // in the implementation.
            Assert.False(s.ReadText("Abcd"));
            Assert.False(s.ReadText("aBcd"));
            Assert.False(s.ReadText("abCd"));
            Assert.False(s.ReadText("abcD"));
            Assert.True(s.ReadText("ABCD", comparer: StringComparer.OrdinalIgnoreCase, out var result));
            Assert.Equal("abcd", result.GetText());
        }

        [Fact]
        public void ReadTextShouldReadTheFullTextOrNothing()
        {
            Scanner s = new("abcd");

            Assert.False(s.ReadText("abcde"));
            Assert.False(s.ReadText("abd"));

            Assert.True(s.ReadText("abc", out var result));
            Assert.Equal("abc", result.GetText());

            Assert.True(s.ReadText("d", out result));
            Assert.Equal("d", result.GetText());
        }

        [Fact]
        public void ReadSingleQuotedStringShouldReadSingleQuotedStrings()
        {
            new Scanner("'abcd'").ReadSingleQuotedString(out var result);
            Assert.Equal("'abcd'", result.GetText());

            new Scanner("'a\\nb'").ReadSingleQuotedString(out result);
            Assert.Equal("'a\\nb'", result.GetText());

            Assert.False(new Scanner("'abcd").ReadSingleQuotedString());
            Assert.False(new Scanner("abcd'").ReadSingleQuotedString());
            Assert.False(new Scanner("ab\\'cd").ReadSingleQuotedString());
        }

        [Fact]
        public void ReadDoubleQuotedStringShouldReadDoubleQuotedStrings()
        {
            new Scanner("\"abcd\"").ReadDoubleQuotedString(out var result);
            Assert.Equal("\"abcd\"", result.GetText());

            new Scanner("\"a\\nb\"").ReadDoubleQuotedString(out result);
            Assert.Equal("\"a\\nb\"", result.GetText());

            Assert.False(new Scanner("\"abcd").ReadDoubleQuotedString());
            Assert.False(new Scanner("abcd\"").ReadDoubleQuotedString());
            Assert.False(new Scanner("\"ab\\\"cd").ReadDoubleQuotedString());
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
            Assert.True(new Scanner(text).ReadDecimal(out var result));
            Assert.Equal(expected, result.GetText());
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
            Assert.True(new Scanner(text).ReadSingleQuotedString(out var result));
            Assert.Equal(expected, result.GetText());
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
            new Scanner(text).ReadQuotedString(out var result);
            Assert.Equal(expected, result.GetText());
        }
    }
}
