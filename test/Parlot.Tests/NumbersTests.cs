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

    [Theory]
    [InlineData("-1", -1)]
    [InlineData("-42", -42)]
    [InlineData("-2147483648", int.MinValue)]
    public void TryParseNumberShouldParseNegativeIntegers(string text, int expected)
    {
        Assert.True(Numbers.TryParseNumber<int>(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value));
        Assert.Equal(expected, value);
        Assert.False(Numbers.TryParseNumber<int>(text, NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void TryParseNumberShouldRejectNegativeForUnsignedTypes()
    {
        Assert.False(Numbers.TryParseNumber<uint>("-1", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
        Assert.False(Numbers.TryParseNumber<ulong>("-1", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
        Assert.False(Numbers.TryParseNumber<byte>("-1", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
        Assert.False(Numbers.TryParseNumber<ushort>("-1", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
        Assert.False(Numbers.TryParseNumber<sbyte>("-129", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
        Assert.True(Numbers.TryParseNumber<sbyte>("-128", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var sb));
        Assert.Equal(sbyte.MinValue, sb);
        Assert.False(Numbers.TryParseNumber<int>("-", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void TryParseNumberShouldPreserveNegativeZeroForFloatingPoint()
    {
        Assert.True(Numbers.TryParseNumber<double>("-0", NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var d));
        Assert.True(double.IsNegative(d));
        Assert.Equal(0.0, d);
    }

    [Fact]
    public void TryParseNumberShouldRespectCurrentCultureNegativeSignWhenProviderIsNull()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            var custom = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            custom.NumberFormat.NegativeSign = "NEG";
            CultureInfo.CurrentCulture = custom;

            Assert.False(Numbers.TryParseNumber<int>("-5", NumberStyles.AllowLeadingSign, null!, out _));
            Assert.True(Numbers.TryParseNumber<int>("NEG5", NumberStyles.AllowLeadingSign, null!, out var value));
            Assert.Equal(-5, value);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
#endif
