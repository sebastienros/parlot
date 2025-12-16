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
        var parser = Grammars.TermsTextParser();
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
        var parser = Grammars.TermsCharParser();
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
        var parser = Grammars.TermsStringParser();
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
        var parser = Grammars.TermsPatternParser();
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
    [InlineData("_bar", true, "_bar")]
    [InlineData("foo123", true, "foo123")]
    [InlineData("_123", true, "_123")]
    [InlineData("  foo", true, "foo")]  // Terms skip whitespace
    [InlineData("123foo", false, null)]  // Can't start with digit
    [InlineData("", false, null)]
    public void TermsIdentifier_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.TermsIdentifierParser();
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
    [InlineData("   ", true)]
    [InlineData(" \t\n", true)]
    [InlineData("  foo", true)]  // Parses whitespace before foo
    [InlineData("foo", false)]   // No leading whitespace
    [InlineData("", false)]
    public void TermsWhiteSpace_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.TermsWhiteSpaceParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
    }

    #endregion

    #region NonWhiteSpace Parser Tests

    [Theory]
    [InlineData("hello", true, "hello")]
    [InlineData("hello world", true, "hello")]  // Stops at space
    [InlineData("  hello", true, "hello")]  // Terms skip leading whitespace
    [InlineData("\t\n", false, null)]       // Only whitespace
    public void TermsNonWhiteSpace_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.TermsNonWhiteSpaceParser();
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
    [InlineData("123", true, 123)]
    [InlineData("-456", true, -456)]
    [InlineData("0", true, 0)]
    [InlineData("-0", true, 0)]
    [InlineData("  789", true, 789)]  // Terms skip whitespace
    [InlineData("abc", false, 0)]
    [InlineData("", false, 0)]
    public void TermsDecimal_VariousInputs(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.TermsDecimalParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Keyword Parser Tests

    [Theory]
    [InlineData("if ", true)]      // Followed by space
    [InlineData("if(", true)]      // Followed by paren
    [InlineData("ifx", false)]     // Not a keyword if followed by letter
    [InlineData("  if ", true)]    // Terms skip leading whitespace
    public void TermsKeyword_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.TermsKeywordParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("if", value);
        }
    }

    #endregion

    #region Literals.Text Parser Tests

    [Theory]
    [InlineData("hello", true, "hello")]
    [InlineData(" hello", false, null)]  // Literals don't skip whitespace
    [InlineData("world", false, null)]
    public void LiteralsText_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.LiteralsTextParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Literals.Char Parser Tests

    [Theory]
    [InlineData("h", true, 'h')]
    [InlineData("hello", true, 'h')]
    [InlineData(" h", false, default(char))]  // Literals don't skip whitespace
    [InlineData("x", false, default(char))]
    public void LiteralsChar_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.LiteralsCharParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Sequence Parser Tests

    [Theory]
    [InlineData("hi!", true, "hi", '!')]
    [InlineData("  hi!", true, "hi", '!')]  // Terms skip leading whitespace
    [InlineData("hi ?", false, null, default(char))]
    [InlineData("HI!", false, null, default(char))]  // Case sensitive
    public void SequenceTextChar_VariousInputs(string input, bool shouldSucceed, string? expectedText, char expectedChar)
    {
        var parser = Grammars.SequenceTextCharParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expectedText, value.Item1);
            Assert.Equal(expectedChar, value.Item2);
        }
    }

    #endregion

    #region SkipAnd Parser Tests

    [Theory]
    [InlineData("hi!", true, '!')]
    [InlineData("  hi !", true, '!')]  // Terms skip whitespace
    [InlineData("hi?", false, default(char))]
    public void SkipAnd_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.SkipAndParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region AndSkip Parser Tests

    [Theory]
    [InlineData("!hi", true, '!')]
    [InlineData("  ! hi", true, '!')]  // Terms skip whitespace
    [InlineData("!bye", false, default(char))]  // 'hi' expected after '!'
    public void AndSkip_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.AndSkipParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Optional Parser Tests

    [Theory]
    [InlineData("hi", true, true, "hi")]
    [InlineData("hello", true, false, null)]  // Doesn't match but still succeeds with empty option
    [InlineData("", true, false, null)]
    public void OptionalText_VariousInputs(string input, bool shouldSucceed, bool hasValue, string? expected)
    {
        var parser = Grammars.OptionalTextParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        Assert.Equal(hasValue, value.HasValue);
        if (hasValue)
        {
            Assert.Equal(expected, value.Value);
        }
    }

    #endregion

    #region ZeroOrMany Parser Tests

    [Theory]
    [InlineData("aaaa", new[] { 'a', 'a', 'a', 'a' })]
    [InlineData("aab", new[] { 'a', 'a' })]
    [InlineData("b", new char[0])]
    [InlineData("", new char[0])]
    public void ZeroOrManyChar_VariousInputs(string input, char[] expected)
    {
        var parser = Grammars.ZeroOrManyCharParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);  // ZeroOrMany always succeeds
        Assert.Equal(expected, value);
    }

    #endregion

    #region ZeroOrOne Parser Tests

    [Theory]
    [InlineData("a", 'a')]
    [InlineData("b", 'x')]  // Doesn't match, returns default 'x'
    [InlineData("", 'x')]
    public void ZeroOrOneChar_VariousInputs(string input, char expected)
    {
        var parser = Grammars.ZeroOrOneCharParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);  // ZeroOrOne (with Then returning default) always succeeds
        Assert.Equal(expected, value);
    }

    #endregion

    #region Eof Parser Tests

    [Theory]
    [InlineData("end", true)]
    [InlineData("end!", false)]  // Not at EOF after parsing
    [InlineData("ending", false)]
    public void EofText_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.EofTextParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("end", value);
        }
    }

    #endregion

    #region Capture Parser Tests

    [Theory]
    [InlineData("z", true, "z")]
    [InlineData("  z", true, "  z")]  // Capture includes the whitespace since it captures from start position
    [InlineData("a", false, null)]
    public void CaptureChar_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.CaptureCharParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region OneOf Parser Tests

    [Theory]
    [InlineData("a", true, 'a')]
    [InlineData("b", true, 'b')]
    [InlineData("c", false, default(char))]
    [InlineData("  a", true, 'a')]  // Terms skip whitespace
    public void OneOfChar_VariousInputs(string input, bool shouldSucceed, char expected)
    {
        var parser = Grammars.OneOfCharParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region Between Parser Tests

    [Theory]
    [InlineData("(foo)", true, "foo")]
    [InlineData("( bar )", true, "bar")]  // Terms skip whitespace
    [InlineData("foo", false, null)]
    [InlineData("(foo", false, null)]
    [InlineData("foo)", false, null)]
    public void BetweenParensIdentifier_VariousInputs(string input, bool shouldSucceed, string? expected)
    {
        var parser = Grammars.BetweenParensIdentifierParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value.ToString());
        }
    }

    #endregion

    #region Separated Parser Tests

    [Theory]
    [InlineData("1,2,3", new[] { 1, 2, 3 })]
    [InlineData("1", new[] { 1 })]
    [InlineData("1, 2, 3", new[] { 1, 2, 3 })]  // Terms skip whitespace
    public void SeparatedDecimals_VariousInputs(string input, int[] expected)
    {
        var parser = Grammars.SeparatedDecimalsParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.NotNull(value);
        Assert.Equal(expected.Length, value.Count);
        for (int i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], (int)value[i]);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public void SeparatedDecimals_FailsWhenNoItemsMatch(string input)
    {
        var parser = Grammars.SeparatedDecimalsParser();
        // Separated requires at least one element
        var result = parser.TryParse(input, out var value);
        
        Assert.False(result);
    }

    #endregion

    #region Unary Parser Tests

    [Theory]
    [InlineData("-5", true, -5)]
    [InlineData("--5", true, 5)]  // Double negation
    [InlineData("5", true, 5)]
    [InlineData("abc", false, 0)]
    public void UnaryNegateDecimal_VariousInputs(string input, bool shouldSucceed, decimal expected)
    {
        var parser = Grammars.UnaryNegateDecimalParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal(expected, value);
        }
    }

    #endregion

    #region LeftAssociative Parser Tests

    [Theory]
    [InlineData("1+2", 3)]
    [InlineData("1+2+3", 6)]
    [InlineData("1 + 2 + 3", 6)]  // Terms skip whitespace
    [InlineData("10", 10)]
    public void LeftAssociativeAddition_VariousInputs(string input, decimal expected)
    {
        var parser = Grammars.LeftAssociativeAdditionParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Hello Parser Tests

    [Theory]
    [InlineData("hello", true)]
    [InlineData("hello world", true)]
    [InlineData("  hello", true)]
    [InlineData("world", false)]
    public void HelloParser_VariousInputs(string input, bool shouldSucceed)
    {
        var parser = Grammars.HelloParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.Equal(shouldSucceed, result);
        if (shouldSucceed)
        {
            Assert.Equal("hello", value);
        }
    }

    #endregion

    #region Expression Parser Tests

    [Theory]
    [InlineData("one + two", 3)]
    [InlineData("two + three", 5)]
    [InlineData("one + two + three", 6)]
    public void ExpressionParser_VariousInputs(string input, double expected)
    {
        var parser = Grammars.ExpressionParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.Equal(expected, value);
    }

    #endregion

    #region Calculator Parser Tests

    
    [Theory]
    
    
    
    
    [InlineData("2 * 3", 6)]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    public void CalculatorParser_VariousInputs(string input, decimal expected)
    {
        var parser = Grammars.CalculatorParser();
        var result = parser.TryParse(input, out var value);
        
        Assert.True(result);
        Assert.NotNull(value);
        Assert.Equal(expected, value.Evaluate());
    }

    #endregion
}
