using Parlot.Tests.Calc;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class GrammarsTests
{
    [Fact]
    public void ParserWithNoName_GeneratesProperty()
    {
        var parser = Grammars.ParserWithNoName_Parser;

        var result = parser.Parse("hello");
        Assert.Equal("hello", result);

        result = parser.Parse("world");
        Assert.Null(result);
    }

    [Fact]
    public void HelloParser_GeneratesProperty()
    {
        var parser = Grammars.Hello;

        var result = parser.Parse("hello");
        Assert.Equal("hello", result);

        result = parser.Parse("world");
        Assert.Null(result);
    }

    [Fact]
    public void ExpressionParser_GeneratesProperty()
    {
        var parser = Grammars.ParseExpression;

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
    public void LeftAssociativeParser_GeneratesProperty()
    {
        var parser = Grammars.ParseLeftAssociative;

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
    public void NestedLeftAssociativeParser_GeneratesProperty()
    {
        var parser = Grammars.ParseNestedLeftAssociative;

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
    public void CalculatorParser_GeneratesProperty()
    {
        var parser = Grammars.ParseCalculator;

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
        var parser = Grammars.ParseCountingOneOf;

        var result = parser.Parse("b");
        Assert.Equal('b', result);

        Assert.Equal(0, CountingParser.GetCount("a"));
        Assert.Equal(1, CountingParser.GetCount("b"));
    }

    [Fact]
    public void CountingOneOf_SkipsWhitespaceOnce()
    {
        CountingParser.Reset();
        var parser = Grammars.ParseCountingOneOf;

        var result = parser.Parse("   b");
        Assert.Equal('b', result);

        Assert.Equal(0, CountingParser.GetCount("a"));
        Assert.Equal(1, CountingParser.GetCount("b"));
    }

    [Fact]
    public void KeywordParser_Generates_Multiple_Factories_With_Arguments()
    {
        var lower = Grammars.ParseFooLower;
        Assert.Equal("foo", lower.Parse("foo"));
        Assert.Null(lower.Parse("FOO"));

        var upper = Grammars.ParseFooUpper;
        Assert.Equal("FOO", upper.Parse("FOO"));
        Assert.Null(upper.Parse("foo"));
    }
}