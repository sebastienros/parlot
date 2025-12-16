#nullable enable

using System.Globalization;
using Parlot;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Tests for source-generated number literal parsers with various cultures and number styles.
/// </summary>
public class NumberLiteralTests
{
    #region Integer Number Literals

    [Theory]
    [InlineData("123", true, 123L)]
    [InlineData("  456", true, 456L)]  // Terms skip whitespace
    [InlineData("-789", true, -789L)]
    [InlineData("+42", true, 42L)]
    [InlineData("0", true, 0L)]
    [InlineData("abc", false, 0L)]
    [InlineData("", false, 0L)]
    public void IntegerNumberLiteral_VariousInputs(string input, bool shouldSucceed, long expected)
    {
        var parser = Grammars.IntegerNumberLiteralParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Decimal Number Literals

    [Theory]
    [InlineData("123.45", true, 123.45)]
    [InlineData("  678.90", true, 678.90)]
    [InlineData("-12.34", true, -12.34)]
    [InlineData("+56.78", true, 56.78)]
    [InlineData("0.0", true, 0.0)]
    [InlineData("123", true, 123.0)]
    [InlineData("abc", false, 0.0)]
    [InlineData("", false, 0.0)]
    public void DecimalNumberLiteral_VariousInputs(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.DecimalNumberLiteralParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Double Number Literals

    [Theory]
    [InlineData("123.45", true, 123.45)]
    [InlineData("1.23e2", true, 123.0)]
    [InlineData("1.23E+2", true, 123.0)]
    [InlineData("1.23e-2", true, 0.0123)]
    [InlineData("-1.5e3", true, -1500.0)]
    [InlineData("  456.78", true, 456.78)]
    public void DoubleNumberLiteral_WithExponent(string input, bool shouldSucceed, double expected)
    {
        var parser = Grammars.DoubleNumberLiteralWithExponentParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value, 5); // 5 decimal places precision
        }
    }

    #endregion

    #region Custom Decimal Separator

    [Theory]
    [InlineData("123,45", true, 123.45)]
    [InlineData("  678,90", true, 678.90)]
    [InlineData("-12,34", true, -12.34)]
    [InlineData("123", true, 123.0)]
    [InlineData("abc", false, 0.0)]
    public void DecimalNumberLiteral_CustomDecimalSeparator(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.DecimalNumberLiteralWithCommaSeparatorParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Custom Group Separator

    [Theory]
    [InlineData("1_234", true, 1234L)]
    [InlineData("1_234_567", true, 1234567L)]
    [InlineData("  999_999", true, 999999L)]
    [InlineData("123", true, 123L)]
    [InlineData("abc", false, 0L)]
    public void IntegerNumberLiteral_CustomGroupSeparator(string input, bool shouldSucceed, long expected)
    {
        var parser = Grammars.IntegerNumberLiteralWithUnderscoreSeparatorParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region NumberOptions Tests

    [Theory]
    [InlineData("123", true, 123L)]
    [InlineData("  456", true, 456L)]
    [InlineData("-789", false, 0L)]  // Leading sign not allowed
    [InlineData("+42", false, 0L)]   // Leading sign not allowed
    public void IntegerNumberLiteral_NoLeadingSign(string input, bool shouldSucceed, long expected)
    {
        var parser = Grammars.IntegerNumberLiteralNoLeadingSignParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [InlineData("123", true, 123.0)]
    [InlineData("456", true, 456.0)]
    [InlineData("-789", true, -789.0)]
    [InlineData("abc", false, 0.0)]
    public void DecimalNumberLiteral_NoDecimalSeparator(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.DecimalNumberLiteralNoDecimalSeparatorParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Float and Long Number Literals

    [Theory]
    [InlineData("123.45", true, 123.45f)]
    [InlineData("1.23e2", true, 123.0f)]
    [InlineData("-45.67", true, -45.67f)]
    public void FloatNumberLiteral_VariousInputs(string input, bool shouldSucceed, float expected)
    {
        var parser = Grammars.FloatNumberLiteralParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value, 5);
        }
    }

    [Theory]
    [InlineData("9223372036854775807", true, 9223372036854775807)]
    [InlineData("-9223372036854775808", true, -9223372036854775808)]
    [InlineData("  12345", true, 12345L)]
    [InlineData("0", true, 0L)]
    public void LongNumberLiteral_VariousInputs(string input, bool shouldSucceed, long expected)
    {
        var parser = Grammars.LongNumberLiteralParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Combined Custom Culture Tests

    [Theory]
    [InlineData("1_234,56", true, 1234.56)]
    [InlineData("  9_999,99", true, 9999.99)]
    [InlineData("-1_234,56", true, -1234.56)]
    [InlineData("123", true, 123.0)]
    [InlineData("abc", false, 0.0)]
    public void DecimalNumberLiteral_CustomCulture(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.DecimalNumberLiteralCustomCultureParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion
}
