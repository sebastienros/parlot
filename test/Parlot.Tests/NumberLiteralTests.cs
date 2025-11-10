using Parlot.Fluent;
using System.Numerics;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class NumberLiteralTests
{
    [Fact]
    public void ByteNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<byte>();

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal((byte)0, result1);

        Assert.True(parser.TryParse("123", out var result2));
        Assert.Equal((byte)123, result2);

        Assert.True(parser.TryParse("255", out var result3));
        Assert.Equal((byte)255, result3);
    }

    [Fact]
    public void ByteNumberLiteralShouldFailOnInvalidNumbers()
    {
        var parser = Literals.Number<byte>();

        // Out of range
        Assert.False(parser.TryParse("256", out _));
        Assert.False(parser.TryParse("-1", out _));

        // Invalid format
        Assert.False(parser.TryParse("abc", out _));
        Assert.False(parser.TryParse("", out _));
    }

    [Fact]
    public void SByteNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<sbyte>(NumberOptions.AllowLeadingSign);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal((sbyte)0, result1);

        Assert.True(parser.TryParse("-128", out var result2));
        Assert.Equal((sbyte)-128, result2);

        Assert.True(parser.TryParse("127", out var result3));
        Assert.Equal((sbyte)127, result3);

        Assert.True(parser.TryParse("+50", out var result4));
        Assert.Equal((sbyte)50, result4);
    }

    [Fact]
    public void ShortNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<short>(NumberOptions.AllowLeadingSign);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal((short)0, result1);

        Assert.True(parser.TryParse("-32768", out var result2));
        Assert.Equal((short)-32768, result2);

        Assert.True(parser.TryParse("32767", out var result3));
        Assert.Equal((short)32767, result3);
    }

    [Fact]
    public void UShortNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<ushort>();

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal((ushort)0, result1);

        Assert.True(parser.TryParse("65535", out var result2));
        Assert.Equal((ushort)65535, result2);
    }

    [Fact]
    public void IntNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<int>(NumberOptions.AllowLeadingSign);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(0, result1);

        Assert.True(parser.TryParse("-2147483648", out var result2));
        Assert.Equal(-2147483648, result2);

        Assert.True(parser.TryParse("2147483647", out var result3));
        Assert.Equal(2147483647, result3);
    }

    [Fact]
    public void UIntNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<uint>();

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal((uint)0, result1);

        Assert.True(parser.TryParse("4294967295", out var result2));
        Assert.Equal(4294967295u, result2);
    }

    [Fact]
    public void LongNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<long>(NumberOptions.AllowLeadingSign);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(0L, result1);

        Assert.True(parser.TryParse("-9223372036854775808", out var result2));
        Assert.Equal(-9223372036854775808L, result2);

        Assert.True(parser.TryParse("9223372036854775807", out var result3));
        Assert.Equal(9223372036854775807L, result3);
    }

    [Fact]
    public void ULongNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<ulong>();

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(0UL, result1);

        Assert.True(parser.TryParse("18446744073709551615", out var result2));
        Assert.Equal(18446744073709551615UL, result2);
    }

    [Fact]
    public void DecimalNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<decimal>(NumberOptions.Float);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(0m, result1);

        Assert.True(parser.TryParse("123.456", out var result2));
        Assert.Equal(123.456m, result2);

        Assert.True(parser.TryParse("-123.456", out var result3));
        Assert.Equal(-123.456m, result3);
    }

    [Fact]
    public void DoubleNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<double>(NumberOptions.Float);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(0.0, result1);

        Assert.True(parser.TryParse("123.456", out var result2));
        Assert.Equal(123.456, result2);

        Assert.True(parser.TryParse("-123.456", out var result3));
        Assert.Equal(-123.456, result3);
    }

    [Fact]
    public void FloatNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<float>(NumberOptions.Float);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(0.0f, result1);

        Assert.True(parser.TryParse("123.456", out var result2));
        Assert.Equal(123.456f, result2, 3);

        Assert.True(parser.TryParse("-123.456", out var result3));
        Assert.Equal(-123.456f, result3, 3);
    }

    [Fact]
    public void BigIntegerNumberLiteralShouldParseValidNumbers()
    {
        var parser = Literals.Number<BigInteger>(NumberOptions.AllowLeadingSign);

        Assert.True(parser.TryParse("0", out var result1));
        Assert.Equal(BigInteger.Zero, result1);

        Assert.True(parser.TryParse("123456789012345678901234567890", out var result2));
        Assert.Equal(BigInteger.Parse("123456789012345678901234567890"), result2);

        Assert.True(parser.TryParse("-123456789012345678901234567890", out var result3));
        Assert.Equal(BigInteger.Parse("-123456789012345678901234567890"), result3);
    }

    [Fact]
    public void NumberLiteralsShouldSupportExponent()
    {
        var parser = Literals.Number<double>(NumberOptions.AllowExponent | NumberOptions.AllowDecimalSeparator);

        Assert.True(parser.TryParse("1e2", out var result1));
        Assert.Equal(100.0, result1);

        Assert.True(parser.TryParse("1E2", out var result2));
        Assert.Equal(100.0, result2);

        Assert.True(parser.TryParse("1.5e2", out var result3));
        Assert.Equal(150.0, result3);
    }

    [Fact]
    public void NumberLiteralsShouldSupportGroupSeparators()
    {
        var parser = Literals.Number<int>(NumberOptions.AllowGroupSeparators);

        Assert.True(parser.TryParse("1,000", out var result1));
        Assert.Equal(1000, result1);

        Assert.True(parser.TryParse("1,000,000", out var result2));
        Assert.Equal(1000000, result2);
    }

    [Fact]
    public void NumberLiteralsShouldSupportCustomDecimalSeparator()
    {
        var parser = Literals.Number<decimal>(NumberOptions.AllowDecimalSeparator, decimalSeparator: ',');

        Assert.True(parser.TryParse("123,456", out var result));
        Assert.Equal(123.456m, result);
    }

    [Fact]
    public void NumberLiteralsShouldSupportCustomGroupSeparator()
    {
        var parser = Literals.Number<int>(NumberOptions.AllowGroupSeparators, groupSeparator: '.');

        Assert.True(parser.TryParse("1.000.000", out var result));
        Assert.Equal(1000000, result);
    }
}
