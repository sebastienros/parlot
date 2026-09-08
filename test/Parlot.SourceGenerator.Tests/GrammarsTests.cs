using System;
using System.Linq;
using System.Reflection;
using Parlot.Tests.Calc;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class GrammarsTests
{
    [Fact]
    public void Generated_Public_Surface_Is_Only_Direct_TryParse_Methods()
    {
        var methods = typeof(Grammars).GetMethods(
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => method.Name.StartsWith("TryParse", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(methods);
        Assert.All(methods, static method =>
        {
            Assert.Equal(typeof(bool), method.ReturnType);
            Assert.Equal(typeof(string), method.GetParameters()[0].ParameterType);
            Assert.True(method.GetParameters()[^1].IsOut);
        });
        Assert.DoesNotContain(
            typeof(Grammars).GetMethods(BindingFlags.NonPublic | BindingFlags.Static),
            static method => method.Name.StartsWith("Build", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("one", 1.0)]
    [InlineData("two + three", 5.0)]
    [InlineData("one + two + three", 6.0)]
    public void Expression_Composes_Choice_Sequence_And_Repetition(string input, double expected)
    {
        Assert.True(Grammars.TryParseExpression(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("10 - 4 - 2", 4.0)]
    [InlineData("10 + 5 - 3", 12.0)]
    public void Left_Associative_Operators_Evaluate_In_Order(string input, double expected)
    {
        Assert.True(Grammars.TryParseLeftAssociative(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("2 + 3 * 4", 14.0)]
    [InlineData("2 * 3 + 4", 10.0)]
    [InlineData("1 + 2 * 3 - 4 / 2", 5.0)]
    [InlineData("20 / 4 / 2", 2.5)]
    public void Nested_Left_Associative_Operators_Preserve_Precedence(string input, double expected)
    {
        Assert.True(Grammars.TryParseNestedLeftAssociative(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Left_Associative_Rolls_Back_An_Operator_With_No_Right_Operand()
    {
        Assert.True(Grammars.TryParseLeftAssociativeThenPlus("1+", out var value));
        Assert.Equal(1m, value);
    }

    [Fact]
    public void Unary_Rolls_Back_An_Operator_With_No_Operand()
    {
        Assert.True(Grammars.TryParseUnaryFallback("-", out var value));
        Assert.Equal(42m, value);
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("-5", -5)]
    [InlineData("--5", 5)]
    [InlineData("2 * 3", 6)]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    public void Recursive_Calculator_Produces_User_Owned_Ast(string input, decimal expected)
    {
        Assert.True(Grammars.TryParseCalculator(input, out var expression));
        Assert.Equal(expected, expression.Evaluate());
    }

    [Fact]
    public void Recursive_Calculator_Preserves_Ast_Shape()
    {
        Assert.True(Grammars.TryParseCalculator("2 + 3 * 4", out var expression));
        var addition = Assert.IsType<Addition>(expression);
        Assert.IsType<Number>(addition.Left);
        Assert.IsType<Multiplication>(addition.Right);

        Assert.True(Grammars.TryParseCalculator("(2 + 3) * 4", out expression));
        var multiplication = Assert.IsType<Multiplication>(expression);
        Assert.IsType<Addition>(multiplication.Left);
        Assert.IsType<Number>(multiplication.Right);
    }

    [Fact]
    public void Seekable_OneOf_Invokes_Only_The_Matching_Custom_Emitter()
    {
        GeneratedParserCounters.Reset();

        Assert.True(Grammars.TryParseCountingOneOf("  b", out var value));
        Assert.Equal('b', value);
        Assert.Equal(0, GeneratedParserCounters.GetCount("a"));
        Assert.Equal(1, GeneratedParserCounters.GetCount("b"));
    }

    [Fact]
    public void Switch_Uses_The_Generated_Target_Parser_Body()
    {
        GeneratedParserCounters.Reset();

        Assert.True(Grammars.TryParseCustomSwitch("ax", out var x));
        Assert.Equal('x', x);
        Assert.Equal(1, GeneratedParserCounters.GetCount("x"));
        Assert.Equal(0, GeneratedParserCounters.GetCount("y"));

        GeneratedParserCounters.Reset();
        Assert.True(Grammars.TryParseCustomSwitch("by", out var y));
        Assert.Equal('y', y);
        Assert.Equal(1, GeneratedParserCounters.GetCount("y"));
        Assert.Equal(0, GeneratedParserCounters.GetCount("x"));
    }

    [Fact]
    public void Select_Uses_Per_Call_Configuration_And_Generated_Target_Bodies()
    {
        GeneratedParserCounters.Reset();

        Assert.True(Grammars.TryParseCustomSelect("x", preferX: true, out var x));
        Assert.Equal('x', x);
        Assert.Equal(1, GeneratedParserCounters.GetCount("x"));

        GeneratedParserCounters.Reset();
        Assert.True(Grammars.TryParseCustomSelect("y", preferX: false, out var y));
        Assert.Equal('y', y);
        Assert.Equal(1, GeneratedParserCounters.GetCount("y"));
    }

    [Fact]
    public void Repetition_Stops_When_An_Optional_Inner_Parser_Makes_No_Progress()
    {
        Assert.True(Grammars.TryParseZeroOrManyOptional("aaa", out var zeroOrMany));
        Assert.Equal(3, zeroOrMany.Count);
        Assert.True(Grammars.TryParseOneOrManyOptional("aaa", out var oneOrMany));
        Assert.Equal(3, oneOrMany.Count);
        Assert.False(Grammars.TryParseOneOrManyOptional("", out _));
        Assert.True(Grammars.TryParseSeparatedOptional("aaa", out var separated));
        Assert.Equal(3, separated.Count);
        Assert.False(Grammars.TryParseSeparatedOptional("", out _));
    }

    [Fact]
    public void Generic_Grammar_Helper_Builds_A_User_Owned_Result()
    {
        Assert.True(Grammars.TryParseGenericProperty("1 == LoNg 3", out var node));
        Assert.Equal(3L, Assert.IsType<BasicNode>(node).Value);
    }

    [Fact]
    public void Application_Models_Are_Constructed_By_Generated_Callbacks()
    {
        Assert.True(ExternalTypeGrammars.TryParseSimpleValue("hello", out var value));
        Assert.Equal(new SimpleValue("hello"), value);
        Assert.True(ExternalTypeGrammars.TryParseSimpleNumber("123.45", out var number));
        Assert.Equal(new SimpleNumber(123.45m), number);
    }

    [Fact]
    public void Class_And_Method_Level_Attributes_Apply_To_Standalone_Factories()
    {
        Assert.True(ClassLevelAttributeGrammars.TryParseValue("test", out var value));
        Assert.Equal(new SimpleValue("test"), value);
        Assert.True(ClassLevelAttributeGrammars.TryParseText("hello", out var text));
        Assert.Equal("hello", text);
    }
}
