using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class NumberLiteralTests
{
    [Theory]
    [InlineData("123", true, 123L)]
    [InlineData("  456", true, 456L)]
    [InlineData("-789", true, -789L)]
    [InlineData("+42", true, 42L)]
    [InlineData("abc", false, 0L)]
    public void Integer_VariousInputs(string input, bool expectedSuccess, long expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseInteger(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("123.45", true, 123.45)]
    [InlineData("  678.90", true, 678.90)]
    [InlineData("-12.34", true, -12.34)]
    [InlineData("123", true, 123.0)]
    [InlineData("abc", false, 0.0)]
    public void Decimal_VariousInputs(string input, bool expectedSuccess, decimal expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseDecimal(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1.23e2", 123.0)]
    [InlineData("1.23E+2", 123.0)]
    [InlineData("1.23e-2", 0.0123)]
    [InlineData("-1.5e3", -1500.0)]
    public void Floating_Point_Exponent_Is_Supported(string input, double expected)
    {
        Assert.True(Grammars.TryParseDoubleExponent(input, out var value));
        Assert.Equal(expected, value, 5);
    }

    [Theory]
    [InlineData("123,45", true, 123.45)]
    [InlineData("-12,34", true, -12.34)]
    [InlineData("123", true, 123.0)]
    [InlineData("abc", false, 0.0)]
    public void Custom_Decimal_Separator_Is_Supported(string input, bool expectedSuccess, decimal expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseCommaDecimal(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1_234", 1234L)]
    [InlineData("1_234_567", 1234567L)]
    [InlineData("123", 123L)]
    public void Custom_Group_Separator_Is_Supported(string input, long expected)
    {
        Assert.True(Grammars.TryParseUnderscoreInteger(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("123", true, 123L)]
    [InlineData("-789", false, 0L)]
    [InlineData("+42", false, 0L)]
    public void Leading_Sign_Can_Be_Disabled(string input, bool expectedSuccess, long expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseUnsignedInteger(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("123", true, 123.0)]
    [InlineData("-789", true, -789.0)]
    [InlineData("12.5", false, 0.0)]
    public void Decimal_Separator_Can_Be_Disabled(string input, bool expectedSuccess, decimal expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseIntegralDecimal(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("123.45", 123.45f)]
    [InlineData("1.23e2", 123.0f)]
    [InlineData("-45.67", -45.67f)]
    public void Float_Is_Supported(string input, float expected)
    {
        Assert.True(Grammars.TryParseFloat(input, out var value));
        Assert.Equal(expected, value, 5);
    }

    [Theory]
    [InlineData("9223372036854775807", 9223372036854775807)]
    [InlineData("-9223372036854775808", -9223372036854775808)]
    public void Long_Bounds_Are_Supported(string input, long expected)
    {
        Assert.True(Grammars.TryParseLong(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("0", true, 0)]
    [InlineData("255", true, 255)]
    [InlineData("256", false, 0)]
    public void Byte_Overflow_Does_Not_Truncate(string input, bool expectedSuccess, byte expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseByte(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1_234,56", true, 1234.56)]
    [InlineData("-1_234,56", true, -1234.56)]
    [InlineData("abc", false, 0.0)]
    public void Combined_Custom_Separators_Are_Supported(string input, bool expectedSuccess, decimal expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseCustomCultureDecimal(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("2a", 42)]
    [InlineData("CAFE", 51966)]
    public void Hexadecimal_Is_Supported(string input, int expected)
    {
        Assert.True(Grammars.TryParseHexadecimal(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("52", 42L)]
    [InlineData("177777", 65535L)]
    public void Octal_Is_Supported(string input, long expected)
    {
        Assert.True(Grammars.TryParseOctal(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("101010", true, 42)]
    [InlineData("11111111", true, 255)]
    [InlineData("100000000", false, 0)]
    [InlineData("2", false, 0)]
    public void Binary_Is_Supported(string input, bool expectedSuccess, byte expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseBinary(input, out var value));
        Assert.Equal(expected, value);
    }
}
