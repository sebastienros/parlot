using System;
using Parlot.Fluent;
using Parlot.Rewriting;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class NumberLiteralTests
{
    [Theory]
    [InlineData(NumberOptions.None, '.', ',', "0123456789")]
    [InlineData(NumberOptions.AllowLeadingSign, '.', ',', "0123456789+-")]
    [InlineData(NumberOptions.AllowDecimalSeparator, '.', ',', "0123456789.")]
    [InlineData(NumberOptions.AllowGroupSeparators, '.', ',', "0123456789")]
    [InlineData(NumberOptions.AllowExponent, '.', ',', "0123456789")]
    [InlineData(NumberOptions.Float, '.', ',', "0123456789+-.")]
    [InlineData(NumberOptions.Number, '.', ',', "0123456789+-.")]
    [InlineData(NumberOptions.Any, '.', ',', "0123456789+-.")]
    [InlineData(NumberOptions.Any, ',', '.', "0123456789+-,")]
    [InlineData(NumberOptions.Any, ',', '_', "0123456789+-,")]
    public void ExpectedCharsShouldBeTheCharsStartingANumber(NumberOptions options, char decimalSeparator, char groupSeparator, string expected)
    {
        // The chars a number can start with are the digits, the signs and the decimal separator. A group
        // separator or an exponent only follow digits, so they can't be the first char. The runtime parser and
        // the one the source generator builds the lookup tables from have to agree on the set.
        var parsers = new System.Collections.Generic.List<ISeekable>
        {
            (ISeekable)new TestNumberLiteral(options, decimalSeparator, groupSeparator),
            (ISeekable)Literals.Number<decimal>(options, decimalSeparator, groupSeparator),
            (ISeekable)Literals.Number<long>(options, decimalSeparator, groupSeparator),
        };

        foreach (var parser in parsers)
        {
            Assert.True(parser.CanSeek);
            Assert.False(parser.SkipWhitespace);
            Assert.Equal(expected.ToCharArray(), parser.ExpectedChars);
        }

        var terms = (ISeekable)Terms.Number<decimal>(options, decimalSeparator, groupSeparator);

        Assert.True(terms.SkipWhitespace);
        Assert.Equal(expected.ToCharArray(), terms.ExpectedChars);
    }

    [Theory]
    [InlineData(NumberOptions.Any, "1,234.5e2", 123450)]
    [InlineData(NumberOptions.Any, "1e2", 100)]
    [InlineData(NumberOptions.Any, ".5", 0.5)]
    [InlineData(NumberOptions.Any, "-.5", -0.5)]
    public void TrimmedExpectedCharsShouldStillLetTheChoiceParseNumbers(NumberOptions options, string input, decimal expected)
    {
        // Only a number parser is tried for a digit, a sign or the decimal separator, and that is enough
        var parser = OneOf(Literals.Number<decimal>(options).Then(static x => (object)x), Literals.Text("e").Then(static x => (object)x), Literals.Text(",").Then(static x => (object)x));

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal("e", parser.Parse("e2"));
        Assert.Equal(",", parser.Parse(",5"));
    }

    [Theory]
    [InlineData(NumberOptions.AllowUnderscore, '.', ',', "0123456789")]
    [InlineData(NumberOptions.Any | NumberOptions.AllowUnderscore, '.', ',', "0123456789+-.")]
    public void UnderscoresShouldNotStartANumber(NumberOptions options, char decimalSeparator, char groupSeparator, string expected)
    {
        ExpectedCharsShouldBeTheCharsStartingANumber(options, decimalSeparator, groupSeparator, expected);
    }

    [Theory]
    [InlineData("1_000", 1000)]
    [InlineData("1__000", 1000)]
    [InlineData("-1_234.567_8", -1234.5678)]
    [InlineData("1,000_000.5", 1000000.5)]
    [InlineData("1_2.3_4e1_0", 123400000000)]
    [InlineData("1_2e-1", 1.2)]
    public void ShouldParseNumbersWithUnderscores(string text, decimal expected)
    {
        var options = NumberOptions.Any | NumberOptions.AllowUnderscore;
        var parsers = new Parser<decimal>[]
        {
            Literals.Number<decimal>(options),
            Terms.Number<decimal>(options),
            new TestNumberLiteral(options, '.', ','),
        };

        foreach (var parser in parsers)
        {
            var context = new ParseContext(new Scanner(text));
            var result = new ParseResult<decimal>();

            Assert.True(parser.Parse(context, ref result));
            Assert.Equal(expected, result.Value);
            Assert.Equal(text.Length, context.Scanner.Cursor.Offset);
            Assert.Equal(text.Length, result.End);
        }
    }

    [Fact]
    public void ShouldParseNumbersWithUnderscoresOfAnyType()
    {
        Assert.Equal((byte)255, Literals.Number<byte>(NumberOptions.AllowUnderscore).Parse("2_5_5"));
        Assert.Equal(-1_000, Literals.Number<int>(NumberOptions.Integer | NumberOptions.AllowUnderscore).Parse("-1_000"));
        Assert.Equal(long.MaxValue, Literals.Number<long>(NumberOptions.AllowUnderscore).Parse("9_223_372_036_854_775_807"));
        Assert.Equal(ulong.MaxValue, Literals.Number<ulong>(NumberOptions.AllowUnderscore).Parse("18_446_744_073_709_551_615"));
        Assert.Equal(1.5e10, Literals.Number<double>(NumberOptions.Float | NumberOptions.AllowUnderscore).Parse("1.5e1_0"));
        Assert.Equal(1_000.25f, Literals.Number<float>(NumberOptions.Float | NumberOptions.AllowUnderscore).Parse("1_000.2_5"));

        // Longer than the buffer the digits are first copied to
        var digits = string.Join("_", Enumerable.Repeat("1234567890", 20));
        Assert.Equal(BigInteger.Parse(digits.Replace("_", "")), Literals.Number<BigInteger>(NumberOptions.AllowUnderscore).Parse(digits));
    }

    [Theory]
    [InlineData("1_000", 1)]
    [InlineData("1.5_5", 1.5)]
    public void ShouldNotReadUnderscoresWithoutTheOption(string text, decimal expected)
    {
        Assert.Equal(expected, Literals.Number<decimal>(NumberOptions.Any).Parse(text));
        Assert.Equal(expected, new TestNumberLiteral(NumberOptions.Any, '.', ',').Parse(text));
    }

    [Theory]
    [InlineData("1_", 1, 1)]
    [InlineData("1__x", 1, 1)]
    [InlineData("1_.5", 1, 1)]
    [InlineData("1._5", 1, 2)]
    [InlineData("1e_5", 1, 1)]
    [InlineData("1_e5", 1, 1)]
    public void ShouldStopBeforeUnderscoresNotFollowedByADigit(string text, decimal expected, int end)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<decimal>();

        Assert.True(Literals.Number<decimal>(NumberOptions.Any | NumberOptions.AllowUnderscore).Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(end, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("_1")]
    [InlineData("-_1")]
    [InlineData("2_5_6")]
    public void ShouldRestoreTheCursorWhenANumberWithUnderscoresFails(string text)
    {
        var parsers = new Parser<byte>[]
        {
            Literals.Number<byte>(NumberOptions.Integer | NumberOptions.AllowUnderscore),
            new TestByteNumberLiteral(NumberOptions.Integer | NumberOptions.AllowUnderscore),
        };

        foreach (var parser in parsers)
        {
            var context = new ParseContext(new Scanner(text));
            var result = new ParseResult<byte>();

            Assert.False(parser.Parse(context, ref result));
            Assert.Equal(0, context.Scanner.Cursor.Offset);
        }
    }

    [Fact]
    public void CustomNumberParsersShouldReceiveTheDigitsWithoutUnderscores()
    {
        var parser = new RecordingNumberLiteral(NumberOptions.Float | NumberOptions.AllowUnderscore);

        Assert.Equal(1234.5m, parser.Parse("1_234.5"));
        Assert.Equal("1234.5", parser.LastText);

        // The text is passed as is when there are no underscores
        Assert.Equal(12m, parser.Parse("12"));
        Assert.Equal("12", parser.LastText);
    }

    private sealed class RecordingNumberLiteral : NumberLiteralBase<decimal>
    {
        public RecordingNumberLiteral(NumberOptions options) : base(options)
        {
        }

        public string LastText { get; private set; } = "";

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
        {
            LastText = s.ToString();
            return decimal.TryParse(LastText, style, provider, out value);
        }
    }

    private sealed class TestByteNumberLiteral : NumberLiteralBase<byte>
    {
        public TestByteNumberLiteral(NumberOptions options) : base(options)
        {
        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out byte value)
            => byte.TryParse(s.ToString(), style, provider, out value);
    }

    private sealed class TestNumberLiteral : NumberLiteralBase<decimal>
    {
        public TestNumberLiteral(NumberOptions options, char decimalSeparator, char groupSeparator) : base(options, decimalSeparator, groupSeparator)
        {
        }

        public override bool TryParseNumber(ReadOnlySpan<char> s, NumberStyles style, IFormatProvider provider, out decimal value)
            => decimal.TryParse(s.ToString(), style, provider, out value);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void GenericRuntimeTryParseShouldStillHonorNumberStyles()
    {
        Assert.True(Numbers.TryParse<int>("2A", NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value));
        Assert.Equal(42, value);
        Assert.False(Numbers.TryParse<int>("-42", NumberStyles.None, CultureInfo.InvariantCulture, out _));
    }
#endif

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

    [Theory]
    [InlineData("1.e5", 100000, 4)]
    [InlineData("1.E-2", 0.01, 5)]
    [InlineData("1.e", 1, 2)]
    [InlineData("1.", 1, 2)]
    public void NumbersShouldReadAnExponentAfterATrailingDecimalSeparator(string text, double expected, int end)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<double>();

        Assert.True(Literals.Number<double>(NumberOptions.Float).Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(end, context.Scanner.Cursor.Offset);

        Assert.Equal((decimal)expected, Literals.Number<decimal>(NumberOptions.Float).Parse(text));
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

#if NET8_0_OR_GREATER
    [Fact]
    public void GenericRadixNumbersShouldFailOnOverflowWithoutAnException()
    {
        // These types go through the generic TryParseRadix<TNumber>, which must not throw on overflow
        Assert.Equal(Int128.MaxValue, Literals.Hexadecimal<Int128>().Parse("7fffffffffffffffffffffffffffffff"));
        Assert.False(Literals.Hexadecimal<Int128>().TryParse("80000000000000000000000000000000", out _));
        Assert.Equal(UInt128.MaxValue, Literals.Hexadecimal<UInt128>().Parse("ffffffffffffffffffffffffffffffff"));
        Assert.False(Literals.Hexadecimal<UInt128>().TryParse("100000000000000000000000000000000", out _));
        Assert.Equal(Int128.MaxValue, Literals.Binary<Int128>().Parse(new string('1', 127)));
        Assert.False(Literals.Binary<Int128>().TryParse(new string('1', 128), out _));
        Assert.Equal((Int128)0x1FF, Literals.Octal<Int128>().Parse("777"));

        if (IntPtr.Size == 8)
        {
            Assert.Equal(nint.MaxValue, Literals.Hexadecimal<nint>().Parse("7fffffffffffffff"));
            Assert.False(Literals.Hexadecimal<nint>().TryParse("8000000000000000", out _));
            Assert.Equal(nuint.MaxValue, Literals.Hexadecimal<nuint>().Parse("ffffffffffffffff"));
            Assert.False(Literals.Hexadecimal<nuint>().TryParse("10000000000000000", out _));
        }

        // The generic method called directly with a type that has its own overload
        Assert.True(Numbers.TryParseRadix<ulong>("ffffffffffffffff", 16, out var charsRead, out var unsignedValue));
        Assert.Equal(ulong.MaxValue, unsignedValue);
        Assert.Equal(16, charsRead);
        Assert.False(Numbers.TryParseRadix<ulong>("10000000000000000", 16, out charsRead, out unsignedValue));
        Assert.Equal(0, charsRead);
        Assert.Equal(0ul, unsignedValue);
        Assert.True(Numbers.TryParseRadix<long>("7fffffffffffffff", 16, out _, out var signedValue));
        Assert.Equal(long.MaxValue, signedValue);
        Assert.False(Numbers.TryParseRadix<long>("8000000000000000", 16, out _, out _));
        Assert.False(Numbers.TryParseRadix<sbyte>("80", 16, out _, out _));
        Assert.True(Numbers.TryParseRadix<sbyte>("7f", 16, out _, out var small));
        Assert.Equal(sbyte.MaxValue, small);

        // A type without a largest value never overflows
        Assert.True(Numbers.TryParseRadix<BigInteger>("ffffffffffffffffffffffffffffffffffffffff", 16, out charsRead, out var big));
        Assert.Equal(BigInteger.Parse("1461501637330902918203684832716283019655932542975"), big);
        Assert.Equal(40, charsRead);
    }
#endif

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
