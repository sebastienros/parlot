using Xunit;

namespace Parlot.Tests;

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
    [InlineData(" \\a ", " \a ")]
    [InlineData(" \\0hello ", " \0hello ")]
    public void ShouldDecodeString(string text, string expected)
    {
        Assert.Equal(expected, Character.DecodeString(new TextSpan(text)).ToString());
        Assert.Equal(expected, Character.DecodeString(text).ToString());
    }

    [Fact]
    public void ShouldDecodeStringWithOffset()
    {
        Assert.Equal(" \0hello ", Character.DecodeString("AAAA \\0hello BBBB", 4, 9).ToString());
        Assert.Equal("AAAA \0hello ", Character.DecodeString("AAAA \\0hello BBBB", 0, 13).ToString());
        Assert.Equal(" \0hello BBBB", Character.DecodeString("AAAA \\0hello BBBB", 4, 13).ToString());
        Assert.Equal("AAAA \0hello BBBB", Character.DecodeString("AAAA \\0hello BBBB", 0, 17).ToString());
    }

    [Fact]
    public void ShouldDecodeStringInBuffer()
    {
        var span = new TextSpan("   a\\nbc   ", 3, 5);
        Assert.Equal("a\nbc", Character.DecodeString(span).ToString());
    }

    [Theory]
    [InlineData('\x09', true)]
    [InlineData('\x20', true)]
    [InlineData('\xa0', true)]
    [InlineData('\f', true)]
    [InlineData('\v', false)]
    [InlineData('\n', false)]
    [InlineData('\r', false)]
    [InlineData('a', false)]
    public void ShouldDetectWhiteSpace(char c, bool isWhiteSpace)
    {
        Assert.Equal(isWhiteSpace, Character.IsWhiteSpace(c));
    }

    [Theory]
    [InlineData('\t', true)]
    [InlineData('\x20', true)]
    [InlineData('\xa0', true)]
    [InlineData('\f', true)]
    [InlineData('\v', true)]
    [InlineData('\n', true)]
    [InlineData('\r', true)]
    [InlineData('a', false)]
    public void ShouldDetectWhiteSpaceOrNewLines(char c, bool isWhiteSpace)
    {
        Assert.Equal(isWhiteSpace, Character.IsWhiteSpaceOrNewLine(c));
    }

    [Fact]
    public void IsDecimalDigitShouldMatchDecimalDigitsSetForEveryChar()
    {
        // Character.IsDecimalDigit is implemented as char.IsAsciiDigit, which is the BCL member on
        // net8.0+ and a polyfill on net472/netstandard2.0. Both must agree with the set the
        // SearchValues instances are built from, for every char, on every target framework.
        for (var c = 0; c <= char.MaxValue; c++)
        {
            Assert.Equal(Character.DecimalDigits.IndexOf((char)c) >= 0, Character.IsDecimalDigit((char)c));
        }
    }

    [Fact]
    public void IsHexDigitShouldMatchHexDigitsSetForEveryChar()
    {
        for (var c = 0; c <= char.MaxValue; c++)
        {
            Assert.Equal(Character.HexDigits.IndexOf((char)c) >= 0, Character.IsHexDigit((char)c));
        }
    }
}
