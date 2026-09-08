#nullable enable

using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class ComprehensiveGeneratedParserTests
{
    [Theory]
    [InlineData("hello", true)]
    [InlineData("hello world", true)]
    [InlineData("  hello", true)]
    [InlineData("world", false)]
    public void Terms_Text_Skips_Whitespace(string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseHello(input, out var value));
        Assert.Equal(expectedSuccess ? "hello" : null, value);
    }

    [Theory]
    [InlineData("h", true, 'h')]
    [InlineData("  h", true, 'h')]
    [InlineData("x", false, '\0')]
    public void Terms_Char_Skips_Whitespace(string input, bool expectedSuccess, char expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseTermsChar(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("'hello'", true, "hello")]
    [InlineData("\"world\"", true, "world")]
    [InlineData("''", true, "")]
    [InlineData("'unterminated", false, null)]
    public void String_Literals_Are_Converted_To_Bcl_Strings(
        string input,
        bool expectedSuccess,
        string? expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseTermsString(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc123", true, "abc")]
    [InlineData("123", false, null)]
    public void Pattern_Results_Are_Converted_To_Bcl_Strings(
        string input,
        bool expectedSuccess,
        string? expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseTermsPattern(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("foo123", true, "foo123")]
    [InlineData("  _bar", true, "_bar")]
    [InlineData("123foo", false, null)]
    public void Identifier_Results_Are_Converted_To_Bcl_Strings(
        string input,
        bool expectedSuccess,
        string? expected)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseTermsIdentifier(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Whitespace_And_NonWhitespace_Results_Are_Strings()
    {
        Assert.True(Grammars.TryParseTermsWhiteSpace(" \tfoo", out var whitespace));
        Assert.Equal(" \t", whitespace);
        Assert.True(Grammars.TryParseTermsNonWhiteSpace("  hello world", out var text));
        Assert.Equal("hello", text);
    }

    [Theory]
    [InlineData("if ", true)]
    [InlineData("if(", true)]
    [InlineData("ifx", false)]
    public void Keyword_Predicate_Is_Generated(string input, bool expectedSuccess)
    {
        Assert.Equal(expectedSuccess, Grammars.TryParseTermsKeyword(input, out var value));
        Assert.Equal(expectedSuccess ? "if" : null, value);
    }

    [Fact]
    public void Literal_Parsers_Do_Not_Skip_Whitespace()
    {
        Assert.True(Grammars.TryParseLiteralsText("hello", out var text));
        Assert.Equal("hello", text);
        Assert.False(Grammars.TryParseLiteralsText(" hello", out _));
        Assert.True(Grammars.TryParseLiteralsChar("hello", out var character));
        Assert.Equal('h', character);
        Assert.False(Grammars.TryParseLiteralsChar(" hello", out _));
    }

    [Fact]
    public void Character_Literals_Are_Escaped()
    {
        Assert.True(Grammars.TryParseLiteralsQuote("'", out var quote));
        Assert.Equal('\'', quote);
        Assert.True(Grammars.TryParseLiteralsBackslash("\\", out var backslash));
        Assert.Equal('\\', backslash);
        Assert.True(Grammars.TryParseLiteralsNewLine("\n", out var newLine));
        Assert.Equal('\n', newLine);
    }

    [Fact]
    public void Sequence_And_Skip_Combinators_Work()
    {
        Assert.True(Grammars.TryParseSequence(" hi !", out var sequence));
        Assert.Equal(("hi", '!'), sequence);
        Assert.True(Grammars.TryParseSkipAnd("hi!", out var skippedLeft));
        Assert.Equal('!', skippedLeft);
        Assert.True(Grammars.TryParseAndSkip("!hi", out var skippedRight));
        Assert.Equal('!', skippedRight);
        Assert.False(Grammars.TryParseSequence("hi?", out _));
    }

    [Fact]
    public void Optional_Results_Are_Mapped_Before_Crossing_The_Api()
    {
        Assert.True(Grammars.TryParseOptionalText("hi", out var some));
        Assert.Equal("hi", some);
        Assert.True(Grammars.TryParseOptionalText("other", out var none));
        Assert.Null(none);
    }

    [Fact]
    public void Repetition_And_Choice_Work()
    {
        Assert.True(Grammars.TryParseZeroOrManyChars("aaab", out var values));
        Assert.Equal(new[] { 'a', 'a', 'a' }, values);
        Assert.True(Grammars.TryParseZeroOrOneChar("b", out var defaultValue));
        Assert.Equal('x', defaultValue);
        Assert.True(Grammars.TryParseOneOfChar("b", out var choice));
        Assert.Equal('b', choice);
        Assert.False(Grammars.TryParseOneOfChar("c", out _));
    }

    [Fact]
    public void End_Of_Input_Is_Only_Enforced_When_Explicit()
    {
        Assert.True(Grammars.TryParseEofText("end", out var value));
        Assert.Equal("end", value);
        Assert.False(Grammars.TryParseEofText("end!", out value));
        Assert.Null(value);
        Assert.True(Grammars.TryParseHello("hello!", out _));
    }

    [Fact]
    public void Capture_And_Between_Map_Internal_Spans_To_Strings()
    {
        Assert.True(Grammars.TryParseCaptureChar("  z", out var captured));
        Assert.Equal("  z", captured);
        Assert.True(Grammars.TryParseBetweenIdentifier("( value )", out var identifier));
        Assert.Equal("value", identifier);
    }

    [Fact]
    public void Separated_Unary_And_LeftAssociative_Work()
    {
        Assert.True(Grammars.TryParseSeparatedDecimals("1, 2, 3", out var values));
        Assert.Equal(new decimal[] { 1m, 2m, 3m }, values);
        Assert.True(Grammars.TryParseUnaryDecimal("--5", out var unary));
        Assert.Equal(5m, unary);
        Assert.True(Grammars.TryParseLeftAssociativeDecimal("1+2+3", out var sum));
        Assert.Equal(6m, sum);
    }

    [Fact]
    public void List_Of_Chars_Parsers_Work()
    {
        Assert.True(Grammars.TryParseAnyOfDigits("123abc", out var digits));
        Assert.Equal("123", digits);
        Assert.True(Grammars.TryParseAnyOfLetters("abcdefghijklmnop", out var letters));
        Assert.Equal("abcdefghij", letters);
        Assert.False(Grammars.TryParseAnyOfLetters("a", out _));
        Assert.True(Grammars.TryParseNoneOfWhitespace("hello world", out var nonWhitespace));
        Assert.Equal("hello", nonWhitespace);
    }

    [Fact]
    public void Block_Lambda_And_Lookahead_Combinators_Work()
    {
        Assert.True(Grammars.TryParseBlockLambda("hello", out var transformed));
        Assert.Equal("Result: HELLO", transformed);
        Assert.True(Grammars.TryParseNotX("a", out _));
        Assert.False(Grammars.TryParseNotX("x", out _));
        Assert.True(Grammars.TryParseHelloNotBang("hello", out _));
        Assert.False(Grammars.TryParseHelloNotBang("hello!", out _));
        Assert.True(Grammars.TryParseHelloBang("hello!", out _));
        Assert.False(Grammars.TryParseHelloBang("hello", out _));
    }
}
