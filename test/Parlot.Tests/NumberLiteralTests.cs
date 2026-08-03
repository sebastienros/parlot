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

    [Theory]
    [InlineData("0", 0)]
    [InlineData("2a", 42)]
    [InlineData("CAFE", 51966)]
    public void HexadecimalNumberLiteralShouldParseValidNumbers(string source, int expected)
    {
        Assert.True(Literals.Hexadecimal<int>().TryParse(source, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("52", 42)]
    [InlineData("177777", 65535)]
    public void OctalNumberLiteralShouldParseValidNumbers(string source, long expected)
    {
        Assert.True(Literals.Octal<long>().TryParse(source, out var result));
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("101010", 42)]
    [InlineData("11111111", 255)]
    public void BinaryNumberLiteralShouldParseValidNumbers(string source, byte expected)
    {
        Assert.True(Literals.Binary<byte>().TryParse(source, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RadixNumberLiteralsShouldSupportDifferentNumericTypes()
    {
        Assert.Equal(255u, Literals.Hexadecimal<uint>().Parse("ff"));
        Assert.Equal(uint.MaxValue, Literals.Hexadecimal<uint>().Parse("ffffffff"));
        Assert.Equal(int.MaxValue, Literals.Hexadecimal<int>().Parse("7fffffff"));
        Assert.Equal((ushort)511, Literals.Octal<ushort>().Parse("777"));
        Assert.Equal(10L, Literals.Binary<long>().Parse("1010"));
        Assert.Equal(new BigInteger(255), Literals.Hexadecimal<BigInteger>().Parse("ff"));
    }

    [Fact]
    public void RadixNumberLiteralsShouldNotConsumePrefixes()
    {
        var parser = Literals.Text("0x").SkipAnd(Literals.Hexadecimal<int>());

        Assert.True(parser.TryParse("0x2a", out var result));
        Assert.Equal(42, result);
    }

    [Fact]
    public void RadixNumberLiteralsShouldFailOnInvalidOrOverflowingNumbers()
    {
        Assert.False(Literals.Hexadecimal<int>().TryParse("xyz", out _));
        Assert.False(Literals.Octal<int>().TryParse("89", out _));
        Assert.False(Literals.Binary<int>().TryParse("2", out _));
        Assert.False(Literals.Hexadecimal<byte>().TryParse("100", out _));
        Assert.False(Literals.Hexadecimal<int>().TryParse("80000000", out _));
        Assert.False(Literals.Octal<byte>().TryParse("400", out _));
        Assert.False(Literals.Binary<byte>().TryParse("100000000", out _));
    }

    [Fact]
    public void RadixNumberLiteralsShouldResetPositionWhenTheyFail()
    {
        var parser = OneOf(
            Literals.Hexadecimal<byte>().Then(static _ => "number"),
            Literals.Text("100").Then(static _ => "text"));

        Assert.True(parser.TryParse("100", out var result));
        Assert.Equal("text", result);
    }

    [Fact]
    public void RadixNumberTermsShouldSkipWhiteSpace()
    {
        Assert.Equal(42, Terms.Hexadecimal<int>().Parse("  2a"));
        Assert.Equal(42, Terms.Octal<int>().Parse("  52"));
        Assert.Equal(42, Terms.Binary<int>().Parse("  101010"));
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

    // Plain sequences of digits are parsed by a dedicated implementation, these cover its boundaries

    [Theory]
    [InlineData("1", 1L)]
    [InlineData("9", 9L)]
    [InlineData("10", 10L)]
    [InlineData("999999999999999999", 999999999999999999L)] // 18 digits, the longest it handles
    [InlineData("1000000000000000000", 1000000000000000000L)] // 19 digits, handled by the fallback
    [InlineData("9223372036854775807", long.MaxValue)]
    public void LongNumberLiteralShouldParseAnyDigitCount(string source, long expected)
    {
        Assert.True(Literals.Number<long>().TryParse(source, out var result));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void LongNumberLiteralShouldFailOverLongMaxValue()
    {
        Assert.False(Literals.Number<long>().TryParse("9223372036854775808", out _));
    }

    [Theory]
    // A number that fits in a long but not in the target type must not be truncated into a valid value
    [InlineData("256")]
    [InlineData("300")]
    [InlineData("65536")]
    [InlineData("4294967296")]
    public void ByteNumberLiteralShouldNotTruncateLargerNumbers(string source)
    {
        Assert.False(Literals.Number<byte>().TryParse(source, out _));
    }

    [Theory]
    [InlineData("65536")]
    [InlineData("4294967296")]
    public void ShortNumberLiteralShouldNotTruncateLargerNumbers(string source)
    {
        Assert.False(Literals.Number<short>().TryParse(source, out _));
        Assert.False(Literals.Number<ushort>().TryParse(source, out _));
    }

    [Fact]
    public void IntNumberLiteralShouldNotTruncateLargerNumbers()
    {
        Assert.False(Literals.Number<int>().TryParse("4294967296", out _));
        Assert.False(Literals.Number<int>().TryParse("2147483648", out _));

        Assert.True(Literals.Number<int>().TryParse("2147483647", out var result));
        Assert.Equal(int.MaxValue, result);
    }

    [Theory]
    [InlineData("2.5", 2.5)]
    [InlineData("0.125", 0.125)]
    [InlineData("12", 12)]
    public void DecimalNumberLiteralShouldParseWithAndWithoutSeparator(string source, decimal expected)
    {
        Assert.True(Literals.Number<decimal>(NumberOptions.Float).TryParse(source, out var result));
        Assert.Equal(expected, result);
    }
}
