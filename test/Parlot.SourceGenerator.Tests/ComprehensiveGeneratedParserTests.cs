#nullable enable

using System.Collections.Generic;
using Parlot;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Comprehensive tests for source-generated parsers covering various input scenarios
/// and parser combinator combinations.
/// </summary>
public class ComprehensiveGeneratedParserTests
{
    #region Terms.Text Parser Tests

    [Theory]
    [InlineData("hello", true)]
    [InlineData("hello world", true)]
    [InlineData("  hello", true)]  // Terms skip whitespace
    [InlineData("world", false)]
    [InlineData("", false)]
    [InlineData("Hell", false)]
    public void TermsText_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseTermsText;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("hello", value);
        }
    }

    #endregion

    #region Terms.Char Parser Tests

    [Theory]
    [InlineData("h", true, 'h')]
    [InlineData("  h", true, 'h')]  // Terms skip whitespace
    [InlineData("hello", true, 'h')]
    [InlineData("x", false, default(char))]
    [InlineData("", false, default(char))]
    public void TermsChar_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.ParseTermsChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region String Literal Parser Tests

    [Theory]
    [InlineData("'hello'", true, "hello")]
    [InlineData("\"world\"", true, "world")]
    [InlineData("''", true, "")]
    [InlineData("\"\"", true, "")]
    [InlineData("'a'", true, "a")]
    [InlineData("'unterminated", false, null)]
    [InlineData("hello", false, null)]
    public void TermsString_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseTermsString;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region Pattern Parser Tests

    [Theory]
    [InlineData("abc", true, "abc")]
    [InlineData("abcdef", true, "abcdef")]
    [InlineData("a", true, "a")]
    [InlineData("123", false, null)]
    [InlineData("abc123", true, "abc")]  // Stops at first non-letter
    [InlineData("", false, null)]
    public void TermsPattern_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseTermsPattern;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region Identifier Parser Tests

    [Theory]
    [InlineData("foo", true, "foo")]
    [InlineData("  bar123", true, "bar123")]
    [InlineData("_private", true, "_private")]
    [InlineData("CamelCase", true, "CamelCase")]
    [InlineData("with_underscore", true, "with_underscore")]
    [InlineData("123start", false, null)]  // Can't start with digit
    [InlineData("", false, null)]
    public void TermsIdentifier_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseTermsIdentifier;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region WhiteSpace Parser Tests

    [Theory]
    [InlineData("   ", true, "   ")]
    [InlineData("\t\t", true, "\t\t")]
    [InlineData(" \t \n", true, " \t \n")]
    [InlineData("hello", false, null)]
    [InlineData("", false, null)]
    public void TermsWhiteSpace_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseTermsWhiteSpace;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region NonWhiteSpace Parser Tests

    [Theory]
    [InlineData("hello", true, "hello")]
    [InlineData("hello world", true, "hello")]
    [InlineData("  abc", true, "abc")]
    [InlineData("   ", false, null)]
    [InlineData("", false, null)]
    public void TermsNonWhiteSpace_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseTermsNonWhiteSpace;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region Decimal Parser Tests

    [Theory]
    [InlineData("123", true)]
    [InlineData("-456", true)]
    [InlineData("0", true)]
    [InlineData("3.14", true)]
    [InlineData("-2.5", true)]
    [InlineData("  42", true)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void TermsDecimal_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseTermsDecimal;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
    }

    #endregion

    #region Keyword Parser Tests

    [Theory]
    [InlineData("if ", true)]
    [InlineData("  if ", true)]
    [InlineData("if123", true)]  // Succeeds - keyword followed by digit is valid
    [InlineData("ifx", false)]
    [InlineData("iffy", false)]
    [InlineData("if", true)]  // EOF counts as valid boundary
    public void TermsKeyword_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseTermsKeyword;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("if", value);
        }
    }

    #endregion

    #region Literals Parser Tests (No Whitespace Skipping)

    [Theory]
    [InlineData("hello", true)]
    [InlineData(" hello", false)]  // Literals don't skip whitespace
    [InlineData("hello ", true)]
    [InlineData("world", false)]
    public void LiteralsText_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseLiteralsText;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("hello", value);
        }
    }

    [Theory]
    [InlineData("hello", true, 'h')]
    [InlineData(" h", false, default(char))]  // Literals don't skip whitespace
    [InlineData("x", false, default(char))]
    public void LiteralsChar_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.ParseLiteralsChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Sequence Combinator Tests

    [Theory]
    [InlineData("hi!", true, "hi", '!')]
    [InlineData("  hi!", true, "hi", '!')]  // Terms skip whitespace
    [InlineData("hi?", false, null, default(char))]
    [InlineData("hello!", false, null, default(char))]
    [InlineData("hi", false, null, default(char))]
    public void SequenceTextChar_VariousInputs(string input, bool shouldSucceed, string? expectedText, char expectedChar)
    {
        var parser = Grammars.ParseSequenceTextChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expectedText, value.Item1);
            Assert.Equal(expectedChar, value.Item2);
        }
    }

    #endregion

    #region SkipAnd / AndSkip Combinator Tests

    [Theory]
    [InlineData("hi!", true, '!')]
    [InlineData("  hi!", true, '!')]
    [InlineData("hi?", false, default(char))]
    [InlineData("hello!", false, default(char))]
    public void SkipAnd_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.ParseSkipAnd;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("!hi", true, '!')]
    [InlineData("  !hi", true, '!')]
    [InlineData("!hello", false, default(char))]
    [InlineData("?hi", false, default(char))]
    public void AndSkip_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.ParseAndSkip;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Optional Combinator Tests

    [Theory]
    [InlineData("hi", true, "hi")]
    [InlineData("  hi", true, "hi")]
    [InlineData("hello", false, null)]
    [InlineData("", false, null)]
    public void Optional_VariousInputs(string input, bool hasValue, string? expected)
    {
        var parser = Grammars.ParseOptionalText;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);  // Optional always succeeds
        Assert.Equal(hasValue, value.HasValue);
        if (hasValue)
        {
            Assert.Equal(expected, value.Value);
        }
    }

    #endregion

    #region ZeroOrMany Combinator Tests

    [Theory]
    [InlineData("aaa", 3)]
    [InlineData("aaabbb", 3)]
    [InlineData("a", 1)]
    [InlineData("bbb", 0)]
    [InlineData("", 0)]
    public void ZeroOrMany_VariousInputs(string input, int expectedCount)
    {
        var parser = Grammars.ParseZeroOrManyChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);  // ZeroOrMany always succeeds
        Assert.Equal(expectedCount, value!.Count);
    }

    #endregion

    #region ZeroOrOne Combinator Tests

    [Theory]
    [InlineData("a", 'a')]
    [InlineData("  a", 'a')]
    [InlineData("b", 'x')]  // Default value
    [InlineData("", 'x')]  // Default value
    public void ZeroOrOne_VariousInputs(string input, char expected)
    {
        var parser = Grammars.ParseZeroOrOneChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);  // ZeroOrOne always succeeds
        Assert.Equal(expected, value);
    }

    #endregion

    #region Eof Combinator Tests

    [Theory]
    [InlineData("end", true)]
    [InlineData("  end", true)]
    [InlineData("end ", false)]  // Trailing content
    [InlineData("end!", false)]
    [InlineData("ending", false)]
    public void Eof_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseEofText;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("end", value);
        }
    }

    #endregion

    #region Capture Tests

    [Theory]
    [InlineData("z", true, "z")]
    [InlineData("  z", true, "  z")]  // Capture includes whitespace
    [InlineData("a", false, null)]
    [InlineData("", false, null)]
    public void Capture_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseCaptureChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region OneOf Combinator Tests

    [Theory]
    [InlineData("a", true, 'a')]
    [InlineData("b", true, 'b')]
    [InlineData("  a", true, 'a')]
    [InlineData("  b", true, 'b')]
    [InlineData("c", false, default(char))]
    [InlineData("", false, default(char))]
    public void OneOf_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.ParseOneOfChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Between Combinator Tests

    [Theory]
    [InlineData("(foo)", true, "foo")]
    [InlineData("  (bar)", true, "bar")]
    [InlineData("(a)", true, "a")]
    [InlineData("(foo", false, null)]  // Missing closing paren
    [InlineData("foo)", false, null)]  // Missing opening paren
    [InlineData("foo", false, null)]
    public void Between_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.ParseBetweenParensIdentifier;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region Separated Combinator Tests

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1,2", 2)]
    [InlineData("1,2,3", 3)]
    [InlineData("  1, 2  , 3  ", 3)]
    [InlineData("abc", 0)]  // Returns empty list
    public void Separated_VariousInputs(string input, int expectedCount)
    {
        var parser = Grammars.ParseSeparatedDecimals;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);  // Separated returns empty list on failure
        Assert.Equal(expectedCount, value!.Count);
    }

    #endregion

    #region Unary Combinator Tests

    [Theory]
    [InlineData("-1", true, -1)]
    [InlineData("2", true, 2)]
    [InlineData("  -5", true, -5)]
    [InlineData("  10", true, 10)]
    [InlineData("abc", false, 0)]
    public void Unary_VariousInputs(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.ParseUnaryNegateDecimal;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region LeftAssociative Combinator Tests

    [Theory]
    [InlineData("1", 1)]
    [InlineData("1+2", 3)]
    [InlineData("1+2+3", 6)]
    [InlineData("10+20+30", 60)]
    [InlineData("  1 + 2 + 3  ", 6)]
    public void LeftAssociative_VariousInputs(string input, decimal expected)
    {
        var parser = Grammars.ParseLeftAssociativeAddition;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Not Combinator Tests

    [Theory]
    [InlineData("a", true)]
    [InlineData("b", true)]
    [InlineData("  y", true)]
    [InlineData("x", false)]
    [InlineData("  x", false)]
    public void Not_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseNotXChar;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
    }

    #endregion

    #region WhenNotFollowedBy Tests

    [Theory]
    [InlineData("hello", true)]
    [InlineData("hello world", true)]
    [InlineData("hello!", false)]
    [InlineData("  hello", true)]
    public void WhenNotFollowedBy_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseWhenNotFollowedByHelloBang;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("hello", value);
        }
    }

    #endregion

    #region WhenFollowedBy Tests

    [Theory]
    [InlineData("hello!", true)]
    [InlineData("  hello!", true)]
    [InlineData("hello", false)]
    [InlineData("hello world", false)]
    public void WhenFollowedBy_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.ParseWhenFollowedByHelloBang;
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("hello", value);
        }
    }

    #endregion

    #region LeftAssociative Multi-Operator Tests

    [Theory]
    [InlineData("5", 5.0)]
    [InlineData("1+2", 3.0)]
    [InlineData("10-3", 7.0)]
    [InlineData("1+2-1", 2.0)]
    [InlineData("10-2-3", 5.0)]
    public void LeftAssociativeParser_VariousInputs(string input, double expected)
    {
        var parser = Grammars.ParseLeftAssociative;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("2*3", 6.0)]
    [InlineData("2*3+1", 7.0)]  // Multiplication and addition precedence
    [InlineData("10/2", 5.0)]
    [InlineData("2*3/2", 3.0)]
    public void NestedLeftAssociative_VariousInputs(string input, double expected)
    {
        var parser = Grammars.ParseNestedLeftAssociative;
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Edge Cases and Boundary Conditions

    [Fact]
    public void EmptyInput_AllParsers()
    {
        // Test that appropriate parsers handle empty input correctly
        Assert.False(Grammars.ParseTermsText.TryParse("", out _));
        Assert.False(Grammars.ParseTermsChar.TryParse("", out _));
        Assert.False(Grammars.ParseTermsDecimal.TryParse("", out _));
        Assert.True(Grammars.ParseZeroOrManyChar.TryParse("", out _));  // Should succeed with empty list
        Assert.True(Grammars.ParseOptionalText.TryParse("", out _));    // Should succeed with no value
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData(" \t\n ")]
    public void WhitespaceOnly_TermsParsers(string input)
    {
        // Terms parsers skip whitespace, so whitespace-only input should fail for most
        Assert.False(Grammars.ParseTermsText.TryParse(input, out _));
        Assert.False(Grammars.ParseTermsChar.TryParse(input, out _));
        
        // But whitespace parser should succeed
        Assert.True(Grammars.ParseTermsWhiteSpace.TryParse(input, out _));
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("HELLO")]
    [InlineData("HeLLo")]
    public void CaseSensitivity_TextParsers(string input)
    {
        var parser = Grammars.ParseTermsText;
        
        // Should only match "hello" (case-sensitive)
        var result = parser.TryParse(input, out _);
        Assert.Equal(input == "hello", result);
    }

    [Fact]
    public void VeryLongInput_Parsers()
    {
        // Test with very long strings to ensure no overflow issues
        var longIdentifier = new string('a', 10000);
        var parser = Grammars.ParseTermsIdentifier;
        
        Assert.True(parser.TryParse(longIdentifier, out var value));
        Assert.Equal(longIdentifier, value.ToString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("-1")]
    [InlineData("999999999")]
    [InlineData("-999999999")]
    public void NumericBoundaries_DecimalParser(string input)
    {
        var parser = Grammars.ParseTermsDecimal;
        Assert.True(parser.TryParse(input, out var value));
        Assert.Equal(decimal.Parse(input), value);
    }

    #endregion
}
