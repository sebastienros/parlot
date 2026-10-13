#if NET8_0_OR_GREATER
using System.Globalization;
using Xunit;

namespace Parlot.Tests;

public class NumbersTests
{
    [Theory]
    [InlineData("10", NumberStyles.Integer, 10)]
    [InlineData("10", NumberStyles.HexNumber, 16)]
    [InlineData("10", NumberStyles.AllowHexSpecifier, 16)]
    [InlineData("1f", NumberStyles.HexNumber, 31)]
    [InlineData("10", NumberStyles.BinaryNumber, 2)]
    [InlineData("10", NumberStyles.AllowBinarySpecifier, 2)]
    public void TryParseNumberShouldUseTheStyle(string text, NumberStyles styles, int expected)
    {
        Assert.True(Numbers.TryParseNumber<int>(text, styles, CultureInfo.InvariantCulture, out var value));
        Assert.Equal(expected, value);

        Assert.True(Numbers.TryParseNumber<long>(text, styles, CultureInfo.InvariantCulture, out var longValue));
        Assert.Equal(expected, longValue);
    }

    [Fact]
    public void TryParseNumberShouldNotReadDecimalDigitsAsBinary()
    {
        Assert.False(Numbers.TryParseNumber<int>("12", NumberStyles.BinaryNumber, CultureInfo.InvariantCulture, out _));
    }
}
#endif
