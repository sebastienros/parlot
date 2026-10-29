using System;
using System.Globalization;
using Parlot.Fluent;
using Xunit;

namespace Parlot.Tests;

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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData('`', "`Lorem ipsum`", "`Lorem ipsum`")]
    [InlineData('|', "|Lorem ipsum|", "|Lorem ipsum|")]
    [InlineData('\'', "'Lorem ipsum'", "'Lorem ipsum'")]
    [InlineData('"', "\"Lorem ipsum\"", "\"Lorem ipsum\"")]
    public void ShouldReadEscapedStringWithCustomMatchingQuotes(char quote, string text, string expected)
    {
        Scanner s = new(text);
        var success = s.ReadQuotedString([quote], out var result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("'Lorem \\n ipsum'", "'Lorem \\n ipsum'")]
    [InlineData("\"Lorem \\n ipsum\"", "\"Lorem \\n ipsum\"")]
    [InlineData("\"Lo\\trem \\n ipsum\"", "\"Lo\\trem \\n ipsum\"")]
    [InlineData("'Lorem \\u1234 ipsum'", "'Lorem \\u1234 ipsum'")]
    [InlineData("'Lorem \\xabcd ipsum'", "'Lorem \\xabcd ipsum'")]
    [InlineData("'\\a ding'", "'\\a ding'")]
    [InlineData("'Lorem ipsum' \\xabcd", "'Lorem ipsum'")]
    public void ShouldReadStringWithEscapes(string text, string expected)
    {
        Scanner s = new(text);
        var success = s.ReadQuotedString(out var result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData('`', "`Lorem \\n ipsum`", "`Lorem \\n ipsum`")]
    [InlineData('`', "`Lo\\trem \\n ipsum`", "`Lo\\trem \\n ipsum`")]
    [InlineData('`', "`Lorem \\u1234 ipsum`", "`Lorem \\u1234 ipsum`")]
    [InlineData('`', "`Lorem \\xabcd ipsum`", "`Lorem \\xabcd ipsum`")]
    [InlineData('`', "`\\a ding`", "`\\a ding`")]
    [InlineData('`', "`Lorem ipsum` \\xabcd", "`Lorem ipsum`")]
    public void ShouldReadCustomStringWithEscapes(char quote, string text, string expected)
    {
        Scanner s = new(text);
        var success = s.ReadQuotedString([quote], out var result);
        Assert.True(success);
        Assert.Equal(expected, result);
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

    [Theory]
    [InlineData("'Lorem \\n ipsum")]
    [InlineData("'C:\\temp\\file")]
    [InlineData("'\\u1234")]
    public void ShouldNotReadStringWithEscapesAndNoEndQuote(string text)
    {
        Scanner s = new(text);

        Assert.False(s.ReadQuotedString());
        Assert.Equal(0, s.Cursor.Offset);
    }

    [Theory]
    // The quote char is also the escape char, so it can't be escaped: the backslash following the
    // start quote is the end quote, and the content is never decoded
    [InlineData("\\n\\rest", true, "\\n\\")]
    [InlineData("\\p\\rest", true, "\\p\\")]
    [InlineData("\\abc\\", true, "\\abc\\")]
    [InlineData("\\\\", true, "\\\\")]
    [InlineData("\\u1234\\rest", true, "\\u1234\\")]
    [InlineData("\\u12\\rest", true, "\\u12\\")]
    [InlineData("\\nrest", false, null)]
    [InlineData("\\", false, null)]
    [InlineData("", false, null)]
    public void ShouldReadStringWhenQuoteCharIsTheEscapeChar(string text, bool success, string expected)
    {
        Scanner s = new(text);

        Assert.Equal(success, s.ReadQuotedString('\\', out var result));

        if (success)
        {
            Assert.Equal(expected, result.ToString());
            Assert.Equal(expected.Length, s.Cursor.Offset);
        }
        else
        {
            Assert.Equal(0, s.Cursor.Offset);
        }
    }

    [Fact]
    public void ShouldNotReadNullQuotedStringAtEof()
    {
        // The cursor reports the null char at the end of the text, which is not a quote
        var s = new Scanner("");

        Assert.False(s.ReadQuotedString('\0', out _));
        Assert.Equal(0, s.Cursor.Offset);

        s = new Scanner("\0abc\0rest");

        Assert.True(s.ReadQuotedString('\0', out var result));
        Assert.Equal("\0abc\0", result.ToString());
        Assert.Equal(5, s.Cursor.Offset);

        Assert.False(new StringLiteral('\0').TryParse("", out _));
        Assert.Equal("abc", new StringLiteral('\0').Parse("\0abc\0").ToString());
    }

    [Fact]
    public void ShouldReadQuotedStringPartial()
    {
        var s = new Scanner("[h a='\\\"']");
        Assert.True(s.ReadText("[h a="));
        Assert.True(s.ReadSingleQuotedString(out var result));
        Assert.Equal("'\\\"'", result);
    }

    [Theory]
    [InlineData("hello world", "helo", "hello")]
    [InlineData("hello world", "helo wrd", "hello world")]
    [InlineData("hello world", "h", "h")]
    public void ShouldReadAnyOf(string source, string pattern, string expected)
    {
        var s = new Scanner(source);
        Assert.True(s.ReadAnyOf(pattern, out var result));
        Assert.Equal(expected, result);

#if NET8_0_OR_GREATER
        s = new Scanner(source);
        Assert.True(s.ReadAnyOf(SearchValues.Create(pattern), out result));
        Assert.Equal(expected, result);
#endif
    }

    [Theory]
    [InlineData("hello world", "wrd")]
    [InlineData("", "helo wrd")]
    [InlineData("hello world", "")]
    public void ShouldNotReadAnyOf(string source, string pattern)
    {
        var s = new Scanner(source);
        Assert.False(s.ReadAnyOf(pattern, out var result));

#if NET8_0_OR_GREATER
        s = new Scanner(source);
        Assert.False(s.ReadAnyOf(SearchValues.Create(pattern), out result));
#endif
    }

    [Fact]
    public void ReadAnyOfShouldStopAtEofWhenTheNullCharIsExpected()
    {
        // The cursor reports the null char at the end of the text, which must not be read even when expected
        var s = new Scanner("ab");

        Assert.True(s.ReadAnyOf("ab\0", out var result));
        Assert.Equal("ab", result.ToString());
        Assert.Equal(2, s.Cursor.Offset);

        s = new Scanner("a\0b");

        Assert.True(s.ReadAnyOf("a\0", out result));
        Assert.Equal("a\0", result.ToString());
        Assert.Equal(2, s.Cursor.Offset);

        s = new Scanner("");

        Assert.False(s.ReadAnyOf("\0", out _));
        Assert.Equal(0, s.Cursor.Offset);

        s = new Scanner("x");

        Assert.False(s.ReadAnyOf("\0", out _));
        Assert.Equal(0, s.Cursor.Offset);
    }

    [Fact]
    public void ReadNonWhiteSpaceShouldMatchTheCharacterTableForEveryChar()
    {
        // The vectorized scan reads up to a set of chars which must be the one of the Character table
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var c = (char)i;
            var text = c.ToString();

            Assert.Equal(!Character.IsWhiteSpace(c), new Scanner(text).ReadNonWhiteSpace(out var read));
            Assert.Equal(Character.IsWhiteSpace(c) ? 0 : 1, read.Length);

            Assert.Equal(!Character.IsWhiteSpaceOrNewLine(c), new Scanner(text).ReadNonWhiteSpaceOrNewLine(out read));
            Assert.Equal(Character.IsWhiteSpaceOrNewLine(c) ? 0 : 1, read.Length);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(100)]
    public void ReadNonWhiteSpaceShouldReadRunsOfAnyLength(int length)
    {
        // Short runs are read with the char table, longer ones with a vectorized search
        var run = new string('a', length);

        foreach (var text in new[] { run, run + " b", run + "\u3000b" })
        {
            var s = new Scanner(text);
            Assert.True(s.ReadNonWhiteSpace(out var read));
            Assert.Equal(run, read.ToString());
            Assert.Equal(new TextPosition(length, 1, length + 1), s.Cursor.Position);

            s = new Scanner(text);
            Assert.True(s.ReadNonWhiteSpaceOrNewLine(out read));
            Assert.Equal(run, read.ToString());
            Assert.Equal(new TextPosition(length, 1, length + 1), s.Cursor.Position);
        }

        // A new line ends the run or is part of it
        var s2 = new Scanner(run + "\n" + run + " b");
        Assert.True(s2.ReadNonWhiteSpaceOrNewLine(out var read2));
        Assert.Equal(run, read2.ToString());
        Assert.Equal(new TextPosition(length, 1, length + 1), s2.Cursor.Position);

        s2 = new Scanner(run + "\n" + run + " b");
        Assert.True(s2.ReadNonWhiteSpace(out read2));
        Assert.Equal(run + "\n" + run, read2.ToString());
        Assert.Equal(new TextPosition(2 * length + 1, 2, length + 1), s2.Cursor.Position);
    }

    [Fact]
    public void ReadNonWhiteSpaceShouldTrackLinesAndStopAtTheEnd()
    {
        // New lines are not white spaces, the run contains them and the line is tracked
        Scanner s = new("a\nb c");

        Assert.True(s.ReadNonWhiteSpace(out var read));
        Assert.Equal("a\nb", read.ToString());
        Assert.Equal(new TextPosition(3, 2, 2), s.Cursor.Position);
        Assert.False(s.ReadNonWhiteSpace(out _));

        s.SkipWhiteSpace();
        Assert.True(s.ReadNonWhiteSpace(out read));
        Assert.Equal("c", read.ToString());
        Assert.True(s.Cursor.Eof);
        Assert.False(s.ReadNonWhiteSpace(out _));

        // Here the new line stops the run and is left to the cursor
        s = new("a\nb");

        Assert.True(s.ReadNonWhiteSpaceOrNewLine(out read));
        Assert.Equal("a", read.ToString());
        Assert.Equal(new TextPosition(1, 1, 2), s.Cursor.Position);
        Assert.False(s.ReadNonWhiteSpaceOrNewLine(out _));
    }

    [Fact]
    public void SkipWhitespaceShouldSkipWhitespace()
    {
        // New lines are not considered white spaces
        Scanner s = new("Lorem ipsum   \r\n   ");

        Assert.Equal(1, s.Cursor.Position.Line);

        Assert.False(s.SkipWhiteSpace());
        Assert.Equal(1, s.Cursor.Position.Line);

        s.ReadNonWhiteSpace();

        Assert.True(s.SkipWhiteSpace());
        Assert.Equal(6, s.Cursor.Position.Offset);
        Assert.Equal(1, s.Cursor.Position.Line);

        s.ReadNonWhiteSpace();

        Assert.True(s.SkipWhiteSpace());
        Assert.Equal(14, s.Cursor.Position.Offset);
        Assert.Equal(1, s.Cursor.Position.Line);

        Assert.True(s.SkipWhiteSpaceOrNewLine());
        Assert.Equal(19, s.Cursor.Position.Offset);
        Assert.Equal(2, s.Cursor.Position.Line);

        Assert.True(s.Cursor.Eof);
        Assert.Equal(2, s.Cursor.Position.Line);

        Assert.False(new Scanner("a").SkipWhiteSpaceOrNewLine());
        Assert.False(new Scanner("a").SkipWhiteSpace());
    }

    [Theory]
    [InlineData("Loremipsum")]
    [InlineData("a")]
    [InlineData("")]
    [InlineData("\r\n\t")]
    [InlineData("\n\t")]
    public void SkipWhiteSpaceWithNoSpacesReturnsFalse(string text)
    {
        Scanner s = new(text);
        Assert.False(s.SkipWhiteSpace());
    }

    [Theory]
    [InlineData(" Loremipsum")]
    [InlineData("  a")]
    [InlineData("\t")]
    public void SkipWhiteSpaceWithSpacesReturnsTrue(string text)
    {
        Scanner s = new(text);
        Assert.True(s.SkipWhiteSpace(), text);
    }

    [Fact]
    public void ReadIdentifierShouldReadIdentifier()
    {
        #pragma warning disable CS0618 // Type or member is obsolete
        Scanner s = new("a $abc 123");

        Assert.True(s.ReadIdentifier(out var result));
        Assert.Equal("a", result);

        s.SkipWhiteSpace();

        Assert.True(s.ReadIdentifier(out result));
        Assert.Equal("$abc", result);

        s.SkipWhiteSpace();

        Assert.False(s.ReadIdentifier());
        #pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void ReadCharShouldReadSingleChar()
    {
        Scanner s = new("aaa");

        Assert.True(s.ReadChar('a'));
        Assert.True(s.ReadChar('a'));
        Assert.True(s.ReadChar('a'));

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
        Assert.True(s.ReadText("ABCD", StringComparison.OrdinalIgnoreCase, out var result));
        Assert.Equal("abcd", result);
    }

    [Fact]
    public void ReadTextShouldReadTheFullTextOrNothing()
    {
        Scanner s = new("abcd");

        Assert.False(s.ReadText("abcde"));
        Assert.False(s.ReadText("abd"));

        Assert.True(s.ReadText("abc", out var result));
        Assert.Equal("abc", result);

        Assert.True(s.ReadText("d", out result));
        Assert.Equal("d", result);
    }

    [Fact]
    public void ReadSingleQuotedStringShouldReadSingleQuotedStrings()
    {
        new Scanner("'abcd'").ReadSingleQuotedString(out var result);
        Assert.Equal("'abcd'", result);

        new Scanner("'a\\nb'").ReadSingleQuotedString(out result);
        Assert.Equal("'a\\nb'", result);

        Assert.False(new Scanner("'abcd").ReadSingleQuotedString());
        Assert.False(new Scanner("abcd'").ReadSingleQuotedString());
        Assert.False(new Scanner("ab\\'cd").ReadSingleQuotedString());
    }

    [Fact]
    public void ReadDoubleQuotedStringShouldReadDoubleQuotedStrings()
    {
        new Scanner("\"abcd\"").ReadDoubleQuotedString(out var result);
        Assert.Equal("\"abcd\"", result);

        new Scanner("\"a\\nb\"").ReadDoubleQuotedString(out result);
        Assert.Equal("\"a\\nb\"", result);

        Assert.False(new Scanner("\"abcd").ReadDoubleQuotedString());
        Assert.False(new Scanner("abcd\"").ReadDoubleQuotedString());
        Assert.False(new Scanner("\"ab\\\"cd").ReadDoubleQuotedString());
    }

    [Fact]
    public void ReadBacktickStringShouldBacktickQuotedStrings()
    {
        new Scanner("`abcd`").ReadBacktickString(out var result);
        Assert.Equal("`abcd`", result);

        new Scanner("`a\\nb`").ReadBacktickString(out result);
        Assert.Equal("`a\\nb`", result);

        Assert.False(new Scanner("`abcd").ReadBacktickString());
        Assert.False(new Scanner("abcd`").ReadBacktickString());
        Assert.False(new Scanner("`ab\\`cd").ReadBacktickString());

        new Scanner("`a\\`b` c").ReadBacktickString(out result);
        Assert.Equal("`a\\`b`", result);
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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" 1")]
    public void ShouldNotReadInvalidInteger(string text)
    {
        Assert.False(new Scanner(text).ReadInteger());
    }

    [Theory]
    [InlineData("1", "1")]
    [InlineData("123", "123")]
    [InlineData("123a", "123")]
    [InlineData("123.0", "123")]
    [InlineData("123.0a", "123")]
    [InlineData("123.", "123")]
    [InlineData("123 ", "123")]
    public void ShouldReadValidInteger(string text, string expected)
    {
        Assert.True(new Scanner(text).ReadInteger(out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(" 1")]
    [InlineData("abc")]
    [InlineData(".")]
    [InlineData(",")]
    public void ShouldNotReadInvalidDecimal(string text)
    {
        Assert.False(new Scanner(text).ReadDecimal(Fluent.NumberOptions.Any, groupSeparator: ',', decimalSeparator: '.', number: out _));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("-x")]
    [InlineData("-.")]
    [InlineData("-.x")]
    [InlineData("+.x")]
    [InlineData(".x")]
    public void ShouldNotMoveWhenDecimalIsInvalid(string text)
    {
        var scanner = new Scanner(text);

        Assert.False(scanner.ReadDecimal(Fluent.NumberOptions.Any, out _));
        Assert.Equal(TextPosition.Start, scanner.Cursor.Position);
    }

    [Theory]
    [InlineData("'a\nb' ", "'a\nb'")]
    [InlineData("'a\r\nb' ", "'a\r\nb'")]
    public void ShouldReadStringsWithLineBreaks(string text, string expected)
    {
        Assert.True(new Scanner(text).ReadSingleQuotedString(out var result));
        Assert.Equal(expected, result);
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
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("'\\u'")]
    [InlineData("'\\u1'")]
    [InlineData("'\\u12'")]
    [InlineData("'\\u123'")]
    [InlineData("'\\ug'")]
    [InlineData("'\\u1g'")]
    [InlineData("'\\u12g'")]
    [InlineData("'\\u123g'")]
    [InlineData("'\\x'")]
    [InlineData("'\\xg'")]
    public void ShouldNotParseInvalidEscapedNumbers(string input)
    {
        var s = new Scanner(input);
        Assert.False(s.ReadQuotedString());
        Assert.Equal(0, s.Cursor.Position.Offset);
    }

    [Theory]
    [InlineData("'\\u1234'")]
    [InlineData("'\\u12345'")]
    [InlineData("'\\x1'")]
    [InlineData("'\\x12'")]
    [InlineData("'\\x123'")]
    [InlineData("'\\x1234'")]
    [InlineData("'\\x1234g'")]
    public void ShouldParseValidEscapedNumbers(string input)
    {
        var s = new Scanner(input);
        Assert.True(s.ReadQuotedString());
    }

    [Theory]
    [InlineData("123,456", "123,456")]
    [InlineData("123,456,789", "123,456,789")]
    public void ShouldReadNumberWithMultipleGroupSeparators(string input, string expected)
    {
        Scanner s = new(input);

        Assert.True(s.ReadDecimal(Fluent.NumberOptions.AllowGroupSeparators, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("123,123", "123,123")]
    [InlineData("123,a", "123")]
    [InlineData("123,123,a", "123,123")]
    [InlineData("123,123,123", "123,123,123")]
    [InlineData("123,.1", "123")]
    [InlineData("123,.e", "123")]
    [InlineData("123,e", "123")]
    [InlineData("123,", "123")]
    public void ShouldReadDecimalWithGroupSeparator(string input, string expected)
    {
        Scanner s = new(input);

        Assert.True(s.ReadDecimal(Fluent.NumberOptions.AllowGroupSeparators | Fluent.NumberOptions.AllowDecimalSeparator, out var result, groupSeparator: ',', decimalSeparator: '.'));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("123.456", "123.456")]
    [InlineData("123.456a", "123.456")]
    [InlineData("123.a", "123.")]
    [InlineData("123.456.789", "123.456")]
    [InlineData("123.", "123.")]
    public void ShouldReadDecimalWithDecimalSeparator(string input, string expected)
    {
        Scanner s = new(input);

        Assert.True(s.ReadDecimal(Fluent.NumberOptions.AllowDecimalSeparator, out var result, decimalSeparator: '.'));
        Assert.True(decimal.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
        Assert.Equal(expected, result);
    }

    [Theory]
    // The BCL accepts an exponent right after the decimal separator, like it accepts a trailing separator
    [InlineData("1.e5", "1.e5")]
    [InlineData("1.E+5x", "1.E+5")]
    [InlineData("1.e-5", "1.e-5")]
    [InlineData("1.5e5", "1.5e5")]
    [InlineData("1.e", "1.")]
    [InlineData("1.ex", "1.")]
    [InlineData("1.e+", "1.")]
    [InlineData("1.", "1.")]
    public void ShouldReadExponentAfterTrailingDecimalSeparator(string input, string expected)
    {
        Scanner s = new(input);

        Assert.True(s.ReadDecimal(Fluent.NumberOptions.Float, out var result));
        Assert.True(double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out _));
        Assert.Equal(expected, result);
        Assert.Equal(expected.Length, s.Cursor.Offset);
    }

    [Theory]
    [InlineData("123.456", "123")]
    [InlineData("123.456a", "123")]
    [InlineData("123.a", "123")]
    [InlineData("123.456.789", "123")]
    [InlineData("123.", "123")]
    [InlineData("123.e", "123")]
    [InlineData("123.e1", "123")]
    [InlineData("123e", "123")]
    [InlineData("123e1", "123e1")]
    public void ShouldReadIntegerWithExponent(string input, string expected)
    {
        Scanner s = new(input);

        Assert.True(s.ReadDecimal(Fluent.NumberOptions.Integer | Fluent.NumberOptions.AllowExponent, out var result, decimalSeparator: '.'));
        Assert.True(decimal.TryParse(expected, NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out _));
        Assert.Equal(expected, result);
    }
}
