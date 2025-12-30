using Parlot.Tests.Calc;
using Parlot;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class GrammarsTests
{
    [Fact]
    public void ParserWithNoName_InterceptsMethodCall()
    {
        // This call will be intercepted and return the source-generated parser
        var parser = Grammars.ParserWithNoName();

        var result = parser.Parse("hello");
        Assert.Equal("hello", result);

        result = parser.Parse("world");
        Assert.Null(result);
    }

    [Fact]
    public void HelloParser_InterceptsMethodCall()
    {
        // This call will be intercepted and return the source-generated parser
        var parser = Grammars.HelloParser();

        var result = parser.Parse("hello");
        Assert.Equal("hello", result);

        result = parser.Parse("world");
        Assert.Null(result);
    }

    [Fact]
    public void ExpressionParser_InterceptsMethodCall()
    {
        var parser = Grammars.ExpressionParser();

        // Test basic values
        var result = parser.Parse("one");
        Assert.Equal(1.0, result);

        result = parser.Parse("two");
        Assert.Equal(2.0, result);

        result = parser.Parse("three");
        Assert.Equal(3.0, result);

        // Test additions
        result = parser.Parse("one + two");
        Assert.Equal(3.0, result);

        result = parser.Parse("one + two + three");
        Assert.Equal(6.0, result);

        // Test invalid input returns default (0.0) when parsing fails
        result = parser.Parse("four");
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void LeftAssociativeParser_InterceptsMethodCall()
    {
        var parser = Grammars.LeftAssociativeParser();

        // Test basic number
        var result = parser.Parse("5");
        Assert.Equal(5.0, result);

        // Test addition
        result = parser.Parse("3 + 2");
        Assert.Equal(5.0, result);

        // Test subtraction
        result = parser.Parse("10 - 4");
        Assert.Equal(6.0, result);

        // Test multiple operators (left-associative)
        result = parser.Parse("10 - 4 - 2");
        Assert.Equal(4.0, result); // (10 - 4) - 2 = 4

        result = parser.Parse("1 + 2 + 3");
        Assert.Equal(6.0, result); // (1 + 2) + 3 = 6

        // Test mixed operators
        result = parser.Parse("10 + 5 - 3");
        Assert.Equal(12.0, result); // (10 + 5) - 3 = 12
    }

    [Fact]
    public void NestedLeftAssociativeParser_InterceptsMethodCall()
    {
        var parser = Grammars.NestedLeftAssociativeParser();

        // Test basic number
        var result = parser.Parse("5");
        Assert.Equal(5.0, result);

        // Test multiplication (higher precedence)
        result = parser.Parse("3 * 2");
        Assert.Equal(6.0, result);

        // Test division
        result = parser.Parse("10 / 2");
        Assert.Equal(5.0, result);

        // Test addition (lower precedence)
        result = parser.Parse("3 + 2");
        Assert.Equal(5.0, result);

        // Test precedence: multiplication before addition
        result = parser.Parse("2 + 3 * 4");
        Assert.Equal(14.0, result); // 2 + (3 * 4) = 2 + 12 = 14

        result = parser.Parse("2 * 3 + 4");
        Assert.Equal(10.0, result); // (2 * 3) + 4 = 6 + 4 = 10

        // Test complex expression
        result = parser.Parse("1 + 2 * 3 - 4 / 2");
        Assert.Equal(5.0, result); // 1 + (2*3) - (4/2) = 1 + 6 - 2 = 5

        // Test left-associativity within same precedence
        result = parser.Parse("20 / 4 / 2");
        Assert.Equal(2.5, result); // (20 / 4) / 2 = 5 / 2 = 2.5
    }

    [Fact]
    public void CalculatorParser_InterceptsMethodCall()
    {
        var parser = Grammars.CalculatorParser();

        // Test basic number
        var result = parser.Parse("5");
        Assert.NotNull(result);
        Assert.Equal(5m, ((Number)result).Value);

        // Test negation
        result = parser.Parse("-5");
        Assert.NotNull(result);
        Assert.IsType<NegateExpression>(result);
        Assert.Equal(5m, ((Number)((NegateExpression)result).Inner).Value);

        // Test double negation
        result = parser.Parse("--5");
        Assert.NotNull(result);
        Assert.IsType<NegateExpression>(result);

        // Test addition
        result = parser.Parse("3 + 2");
        Assert.NotNull(result);
        Assert.IsType<Addition>(result);

        // Test multiplication
        result = parser.Parse("3 * 2");
        Assert.NotNull(result);
        Assert.IsType<Multiplication>(result);

        // Test precedence: multiplication before addition
        result = parser.Parse("2 + 3 * 4");
        Assert.NotNull(result);
        Assert.IsType<Addition>(result);
        var add = (Addition)result;
        Assert.IsType<Number>(add.Left);
        Assert.IsType<Multiplication>(add.Right);

        // Test parentheses
        result = parser.Parse("(2 + 3) * 4");
        Assert.NotNull(result);
        Assert.IsType<Multiplication>(result);
        var mult = (Multiplication)result;
        Assert.IsType<Addition>(mult.Left);
        Assert.IsType<Number>(mult.Right);

        // Test complex expression with negation
        result = parser.Parse("-2 + 3");
        Assert.NotNull(result);
        Assert.IsType<Addition>(result);
        add = (Addition)result;
        Assert.IsType<NegateExpression>(add.Left);
        Assert.IsType<Number>(add.Right);
    }

    [Fact]
    public void CountingOneOf_OnlyMatchingParserInvoked()
    {
        CountingParser.Reset();
        var parser = Grammars.CountingOneOfParser();

        var result = parser.Parse("b");
        Assert.Equal('b', result);

        Assert.Equal(0, CountingParser.GetCount("a"));
        Assert.Equal(1, CountingParser.GetCount("b"));
    }

    [Fact]
    public void CountingOneOf_SkipsWhitespaceOnce()
    {
        CountingParser.Reset();
        var parser = Grammars.CountingOneOfParser();

        var result = parser.Parse("   b");
        Assert.Equal('b', result);

        Assert.Equal(0, CountingParser.GetCount("a"));
        Assert.Equal(1, CountingParser.GetCount("b"));
    }

    [Fact]
    public void GeneratedSwitch_UsesGeneratedTargetParserBody()
    {
        DualCountingCharParser.Reset();

        var parser = Grammars.Switch_UsesGeneratedTargetParser();

        Assert.Equal('x', parser.Parse("ax"));
        Assert.Equal(1, DualCountingCharParser.GetGeneratedCount("x"));
        Assert.Equal(0, DualCountingCharParser.GetRuntimeCount("x"));
        Assert.Equal(0, DualCountingCharParser.GetGeneratedCount("y"));

        DualCountingCharParser.Reset();

        Assert.Equal('y', parser.Parse("by"));
        Assert.Equal(1, DualCountingCharParser.GetGeneratedCount("y"));
        Assert.Equal(0, DualCountingCharParser.GetRuntimeCount("y"));
        Assert.Equal(0, DualCountingCharParser.GetGeneratedCount("x"));
    }

    [Fact]
    public void GeneratedSelect_UsesGeneratedTargetParserBody()
    {
        DualCountingCharParser.Reset();

        var parser = Grammars.Select_UsesGeneratedTargetParser();

        var xContext = new Grammars.SelectTestContext(new Scanner("x")) { PreferX = true };
        var xResult = new ParseResult<char>();
        Assert.True(parser.Parse(xContext, ref xResult));
        Assert.Equal('x', xResult.Value);
        Assert.Equal(1, DualCountingCharParser.GetGeneratedCount("x"));
        Assert.Equal(0, DualCountingCharParser.GetRuntimeCount("x"));

        DualCountingCharParser.Reset();

        var yContext = new Grammars.SelectTestContext(new Scanner("y")) { PreferX = false };
        var yResult = new ParseResult<char>();
        Assert.True(parser.Parse(yContext, ref yResult));
        Assert.Equal('y', yResult.Value);
        Assert.Equal(1, DualCountingCharParser.GetGeneratedCount("y"));
        Assert.Equal(0, DualCountingCharParser.GetRuntimeCount("y"));
    }

    [Fact]
    public void KeywordParser_Generates_Multiple_Factories_With_Arguments()
    {
        var lower = Grammars.FooLowerParser();
        Assert.Equal("foo", lower.Parse("foo"));
        Assert.Null(lower.Parse("FOO"));

        var upper = Grammars.FooUpperParser();
        Assert.Equal("FOO", upper.Parse("FOO"));
        Assert.Null(upper.Parse("foo"));
    }

    [Fact]
    public void GeneratedParser_TracksSpanCorrectly()
    {
        // Test that the generated parser correctly tracks start and end positions
        var parser = Grammars.TermsTextParser();
        var context = new ParseContext(new Scanner("hello world"));
        var result = new ParseResult<string>();

        var success = parser.Parse(context, ref result);

        Assert.True(success);
        Assert.Equal("hello", result.Value);
        Assert.Equal(0, result.Start);  // Should start at position 0
        Assert.Equal(5, result.End);    // Should end at position 5 (length of "hello")
    }

    [Fact]
    public void GeneratedParser_SpanMatchesRuntimeParser()
    {
        // Test that generated parser span tracking matches runtime parser behavior
        var input = "    hello world";
        
        // Test with generated parser
        var generatedParser = Grammars.TermsTextParser();
        var generatedContext = new ParseContext(new Scanner(input));
        var generatedResult = new ParseResult<string>();
        var generatedSuccess = generatedParser.Parse(generatedContext, ref generatedResult);

        // Test with runtime parser
        var runtimeParser = Terms.Text("hello");
        var runtimeContext = new ParseContext(new Scanner(input));
        var runtimeResult = new ParseResult<string>();
        var runtimeSuccess = runtimeParser.Parse(runtimeContext, ref runtimeResult);

        Assert.True(generatedSuccess);
        Assert.True(runtimeSuccess);
        Assert.Equal(runtimeResult.Value, generatedResult.Value);
        Assert.Equal(runtimeResult.Start, generatedResult.Start);
        Assert.Equal(runtimeResult.End, generatedResult.End);
    }

    [Fact]
    public void GeneratedParser_SpanMatchesInputLength()
    {
        // Test that span length matches parsed content
        var parser = Grammars.TermsIdentifierParser();
        var context = new ParseContext(new Scanner("identifier123"));
        var result = new ParseResult<TextSpan>();

        var success = parser.Parse(context, ref result);

        Assert.True(success);
        var span = result.Value;
        Assert.Equal("identifier123", span.ToString());
        Assert.Equal(0, result.Start);
        Assert.Equal(13, result.End);
        Assert.Equal(13, result.End - result.Start); // Span should be 13 characters
    }

    [Fact]
    public void SimpleValueParser_UsesTypeFromIncludedFile()
    {
        // Test that [IncludeFiles] allows using types from separate files
        var parser = ExternalTypeGrammars.SimpleValueParser();
        var result = parser.Parse("hello");

        Assert.NotNull(result);
        Assert.Equal("hello", result.Text);
    }

    [Fact]
    public void SimpleNumberParser_UsesTypeFromIncludedFile()
    {
        // Test that [IncludeFiles] works with decimal numbers
        var parser = ExternalTypeGrammars.SimpleNumberParser();
        var result = parser.Parse("123.45");

        Assert.NotNull(result);
        Assert.Equal(123.45m, result.Value);
    }

    [Fact]
    public void InheritedAttributesParser_UsesClassLevelAttributes()
    {
        // Test that class-level [IncludeFiles] and [IncludeUsings] work
        var parser = ClassLevelAttributeGrammars.InheritedAttributesParser();
        var result = parser.Parse("test");

        Assert.NotNull(result);
        Assert.Equal("test", result.Text);
    }

    [Fact]
    public void CombinedAttributesParser_CombinesClassAndMethodLevelAttributes()
    {
        // Test that method-level attributes combine with class-level attributes
        var parser = ClassLevelAttributeGrammars.CombinedAttributesParser();
        var result = parser.Parse("42.5");

        Assert.NotNull(result);
        Assert.Equal(42.5m, result.Value);
    }

    [Fact]
    public void AdditionalUsingsParser_UsesMultipleUsings()
    {
        // Test that additional usings from both class and method level are included
        var parser = ClassLevelAttributeGrammars.AdditionalUsingsParser();
        var result = parser.Parse("hello");

        Assert.NotNull(result);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void AnyOfDigitsParser_MatchesDigits()
    {
        // Test that AnyOf parser with digits works
        var parser = Grammars.AnyOfDigitsParser();
        var context = new ParseContext(new Scanner("12345abc"));
        var result = new ParseResult<TextSpan>();

        var success = parser.Parse(context, ref result);

        Assert.True(success);
        Assert.Equal("12345", result.Value.ToString());
    }

    [Fact]
    public void AnyOfDigitsParser_ReturnsFalseOnNoMatch()
    {
        var parser = Grammars.AnyOfDigitsParser();
        var context = new ParseContext(new Scanner("abc123"));
        var result = new ParseResult<TextSpan>();

        var success = parser.Parse(context, ref result);

        Assert.False(success);
    }

    [Fact]
    public void AnyOfLettersParser_RespectsMinAndMaxSize()
    {
        var parser = Grammars.AnyOfLettersParser();

        // Less than minSize (2) should fail
        var context1 = new ParseContext(new Scanner("a"));
        var result1 = new ParseResult<TextSpan>();
        Assert.False(parser.Parse(context1, ref result1));

        // Between min and max should work
        var context2 = new ParseContext(new Scanner("abc123"));
        var result2 = new ParseResult<TextSpan>();
        Assert.True(parser.Parse(context2, ref result2));
        Assert.Equal("abc", result2.Value.ToString());

        // Should be limited to maxSize (10)
        var context3 = new ParseContext(new Scanner("abcdefghijklmnop"));
        var result3 = new ParseResult<TextSpan>();
        Assert.True(parser.Parse(context3, ref result3));
        Assert.Equal("abcdefghij", result3.Value.ToString());
    }

    [Fact]
    public void NoneOfWhitespaceParser_MatchesNonWhitespace()
    {
        var parser = Grammars.NoneOfWhitespaceParser();

        // Match non-whitespace
        var context1 = new ParseContext(new Scanner("hello world"));
        var result1 = new ParseResult<TextSpan>();
        Assert.True(parser.Parse(context1, ref result1));
        Assert.Equal("hello", result1.Value.ToString());

        // Starts with whitespace should fail
        var context2 = new ParseContext(new Scanner(" hello"));
        var result2 = new ParseResult<TextSpan>();
        Assert.False(parser.Parse(context2, ref result2));
    }
}