#if NET10_0_OR_GREATER
using Parlot.Benchmarks;
using Parlot.Benchmarks.FarkleParsers;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using System;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Parlot.Tests;

public class BenchmarksTests
{
    const decimal _expected1 = (decimal)3.5;
    const decimal _expected2 = (decimal)-64.5;

    [Theory]
    [InlineData(8, "Valid")]
    [InlineData(128, "Valid")]
    [InlineData(8, "UnknownType")]
    [InlineData(128, "UnknownType")]
    [InlineData(8, "InvalidExpression")]
    [InlineData(128, "InvalidExpression")]
    public void KeywordSourceGrammar(int declarationCount, string scenario)
    {
        var benchmark = new KeywordSourceBenchmarks { DeclarationCount = declarationCount, Scenario = scenario };
        benchmark.Setup();
        if (scenario == "Valid")
        {
            var before = benchmark.Before();
            var after = benchmark.After();
            Assert.Equal(declarationCount, before.Count);
            Assert.Equal(before, after);
        }
        else
        {
            Assert.Null(benchmark.Before());
            Assert.Null(benchmark.After());
        }
    }

    [Fact]
    public void KeywordSourceGrammarPreservesBoundariesAndExpressions()
    {
        const string input = " public static int integer = (10 + 2) * 3 - 1;\r\nbyte internalValue = 4 + 5 * 6; \r\n";
        Assert.True(KeywordSourceBenchmarks.TryParseBefore(input, out var before));
        Assert.True(KeywordSourceBenchmarks.TryParseAfter(input, out var after));
        Assert.Equal(
            [new KeywordSourceDeclaration("int", "integer", 2, 35), new KeywordSourceDeclaration("byte", "internalValue", 0, 34)],
            before);
        Assert.Equal(before, after);
        foreach (var invalid in new[] { "", "integer x = 1;", "INT x = 1;", "int x = (1 + );", "int x = 1; trailing" })
        {
            Assert.False(KeywordSourceBenchmarks.TryParseBefore(invalid, out _));
            Assert.False(KeywordSourceBenchmarks.TryParseAfter(invalid, out _));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationParsing(bool canBeCanceled)
    {
        var benchmarks = new CancellationBenchmarks { CanBeCanceled = canBeCanceled };
        benchmarks.Setup();
        try
        {
            Assert.Equal(128, benchmarks.Fluent());
            Assert.Equal(128, benchmarks.Generated());
            Assert.Equal(128, benchmarks.GeneratedWithoutToken());
        }
        finally
        {
            benchmarks.Cleanup();
        }
    }

    [Theory]
    [InlineData("%hello%", 5)]
    [InlineData("%hello\\nworld%", 11)]
    public void GeneratedStrings(string input, int decodedLength)
    {
        var benchmarks = new GeneratedStringBenchmarks { Input = input };
        benchmarks.Setup();

        Assert.Equal(input.Length, benchmarks.Captured());
        Assert.Equal(decodedLength, benchmarks.Decoded());
    }

    [Theory]
    [InlineData("a", true)]
    [InlineData("\u00e9", true)]
    [InlineData("\u4e2d", false)]
    public void CharacterMapLookup(string input, bool success)
    {
        var benchmarks = new CharMapBenchmarks { Input = input };
        benchmarks.Setup();

        Assert.Equal(success, benchmarks.Lookup());
        Assert.Equal(success, benchmarks.Lookup());
    }

    [Fact]
    public void CursorMatchHello()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CursorMatchHello();
        Assert.NotNull(result);
    }

    [Fact]
    public void CursorMatchGoodbye()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CursorMatchGoodbye();
        Assert.NotNull(result);
    }

    [Fact]
    public void CursorMatchNone()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.CursorMatchNone();
        Assert.Null(result);
    }

    [Fact]
    public void Lookup()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.Lookup();
        Assert.Equal('d', result);
    }

    [Fact]
    public void SkipWhiteSpace_1()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.SkipWhiteSpace_1();
        Assert.Equal('a', result);
    }

    [Fact]
    public void SkipWhiteSpace_10()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.SkipWhiteSpace_10();
        Assert.Equal('a', result);
    }

    [Fact]
    public void DecodeStringWithoutEscapes()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.DecodeStringWithoutEscapes();
        Assert.Equal("This is a new line \n \t and a tab and some \xa0", result);
    }

    [Fact]
    public void DecodeStringWithEscapes()
    {
        var benchmarks = new ParlotBenchmarks();
        benchmarks.Setup();
        var result = benchmarks.DecodeStringWithEscapes();
        Assert.Equal("This is a new line \n \t and a tab and some \xa0", result);
    }

    [Fact]
    public void ExpressionRawSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotRawSmall();
        Assert.NotNull(result);
        Assert.Equal(_expected1, result.Evaluate());
    }

    [Fact]
    public void ExpressionFluentSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotFluentSmall();
        Assert.NotNull(result);
        Assert.Equal(_expected1, result.Evaluate());
    }

    [Fact]
    public void ExpressionGeneratedSmall()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotGeneratedSmall();
        Assert.NotNull(result);
        Assert.Equal(_expected1, result.Evaluate());
    }

    [Fact]
    public void ExpressionRawBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotRawBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionFluentBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotFluentBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionGeneratedBig()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotGeneratedBig();
        Assert.NotNull(result);
        Assert.Equal(_expected2, result.Evaluate());
    }

    [Fact]
    public void ExpressionFluentUnary()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotFluentUnary();
        Assert.NotNull(result);
        Assert.Equal(26m, result.Evaluate());
    }

    [Fact]
    public void ExpressionGeneratedUnary()
    {
        var benchmarks = new ExprBench();
        var result = benchmarks.ParlotGeneratedUnary();
        Assert.NotNull(result);
        Assert.Equal(26m, result.Evaluate());
    }

    [Fact]
    public void ExpressionFarkle()
    {
        var benchmarks = new ExprBench();
        benchmarks.Setup();

        Assert.Equal(_expected1, benchmarks.FarkleSmall().Evaluate());
        Assert.Equal(_expected2, benchmarks.FarkleBig().Evaluate());
        Assert.Equal(26m, benchmarks.FarkleUnary().Evaluate());
        AssertExpressionEqual(benchmarks.ParlotFluentSmall(), benchmarks.FarkleSmall());
        AssertExpressionEqual(benchmarks.ParlotFluentBig(), benchmarks.FarkleBig());
        AssertExpressionEqual(benchmarks.ParlotFluentUnary(), benchmarks.FarkleUnary());
    }

    [Theory]
    [InlineData("1 + 2 * 3", 7)]
    [InlineData("(1 + 2) * 3", 9)]
    [InlineData("10 - 3 - 2", 5)]
    [InlineData("8 / 2 / 2", 2)]
    [InlineData("---2", -2)]
    [InlineData(" \t2.5 * (4 - 1)\r\n", 7.5)]
    [InlineData("-2.5e-2 + .5", 0.475)]
    [InlineData("1.", 1)]
    [InlineData("1-2*3", -5)]
    [InlineData("8/-2+--6", 2)]
    [InlineData("-(1+2)*-3", 9)]
    [InlineData("12.5e+1 / 5", 25)]
    public void FarkleExpressionGrammar(string input, double expected)
    {
        var result = FarkleExpressionParser.Parse(input);

        Assert.Equal((decimal)expected, result.Evaluate());
        AssertExpressionEqual(FluentParser.Expression.Parse(input), result);
    }

    [Fact]
    public void FarkleExpressionPreservesDecimalPrecision()
    {
        var result = Assert.IsType<Number>(FarkleExpressionParser.Parse("0.1234567890123456789012345678"));

        Assert.Equal(0.1234567890123456789012345678m, result.Value);
    }

    private static void AssertExpressionEqual(Expression expected, Expression actual)
    {
        Assert.Equal(expected.GetType(), actual.GetType());

        switch (expected)
        {
            case Number number:
                Assert.Equal(number.Value, Assert.IsType<Number>(actual).Value);
                break;
            case BinaryExpression binary:
                var actualBinary = Assert.IsAssignableFrom<BinaryExpression>(actual);
                AssertExpressionEqual(binary.Left, actualBinary.Left);
                AssertExpressionEqual(binary.Right, actualBinary.Right);
                break;
            case UnaryExpression unary:
                AssertExpressionEqual(unary.Inner, Assert.IsAssignableFrom<UnaryExpression>(actual).Inner);
                break;
            default:
                Assert.Fail($"Unexpected expression type: {expected.GetType()}");
                break;
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 +")]
    [InlineData("(1 + 2")]
    [InlineData("1 2")]
    [InlineData("1 + 2 trailing")]
    public void FarkleExpressionRejectsInvalidInput(string input)
    {
        Assert.Throws<InvalidOperationException>(() => FarkleExpressionParser.Parse(input));
    }

    [Fact]
    public void JsonFarkle()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();

        Assert.Equal(benchmarks.BigJson_Parlot().ToString(), benchmarks.BigJson_Farkle().ToString());
        Assert.Equal(benchmarks.LongJson_Parlot().ToString(), benchmarks.LongJson_Farkle().ToString());
        Assert.Equal(benchmarks.DeepJson_Parlot().ToString(), benchmarks.DeepJson_Farkle().ToString());
        Assert.Equal(benchmarks.WideJson_Parlot().ToString(), benchmarks.WideJson_Farkle().ToString());
    }

    [Fact]
    public void FarkleJsonGrammar()
    {
        var result = Assert.IsType<JsonObject>(FarkleJsonParser.Parse(
            " \r\n{ \"items\": [\"hello\", {}, [], {\"nested\": \"value\"}], \"empty\": \"\" }\t"));
        var array = Assert.IsType<JsonArray>(result.Members["items"]);

        Assert.Equal(4, array.Elements.Count);
        Assert.Equal("hello", Assert.IsType<JsonString>(array.Elements[0]).Value);
        Assert.Empty(Assert.IsType<JsonObject>(array.Elements[1]).Members);
        Assert.Empty(Assert.IsType<JsonArray>(array.Elements[2]).Elements);
        Assert.Equal("value", Assert.IsType<JsonString>(
            Assert.IsType<JsonObject>(array.Elements[3]).Members["nested"]).Value);
        Assert.Equal("", Assert.IsType<JsonString>(result.Members["empty"]).Value);
    }

    [Theory]
    [InlineData("{}", 0)]
    [InlineData("{\"key\":\"value\"}", 1)]
    public void FarkleJsonBuildsIndependentObjects(string input, int count)
    {
        var first = Assert.IsType<JsonObject>(FarkleJsonParser.Parse(input));
        var second = Assert.IsType<JsonObject>(FarkleJsonParser.Parse(input));
        first.Members.Add("new", new JsonString("item"));

        Assert.Equal(count, second.Members.Count);
        Assert.False(second.Members.ContainsKey("new"));
    }

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("\"hello\\nworld\"", "hello\nworld")]
    [InlineData("\"quote: \\\" slash: \\\\\"", "quote: \" slash: \\")]
    [InlineData("\"\\u0041\"", "A")]
    public void FarkleJsonStrings(string input, string expected)
    {
        Assert.Equal(expected, Assert.IsType<JsonString>(FarkleJsonParser.Parse(input)).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[\"value\",]")]
    [InlineData("{\"key\" \"value\"}")]
    [InlineData("{\"key\":}")]
    [InlineData("\"unterminated")]
    [InlineData("[] trailing")]
    [InlineData("[,]")]
    [InlineData("[,\"value\"]")]
    [InlineData("{\"key\":\"value\",}")]
    public void FarkleJsonRejectsInvalidInput(string input)
    {
        Assert.Throws<InvalidOperationException>(() => FarkleJsonParser.Parse(input));
    }

    [Fact]
    public void BigJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.BigJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void BigJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.BigJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void DeepJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.DeepJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void DeepJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.DeepJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void LongJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.LongJson_Parlot();
        Assert.NotNull(result);
    }

    [Fact]
    public void LongJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.LongJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void WideJson()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        benchmarks.WideJson_Parlot();
    }

    [Fact]
    public void WideJsonGenerated()
    {
        var benchmarks = new JsonBench();
        benchmarks.Setup();
        var result = benchmarks.WideJson_ParlotGenerated();
        Assert.NotNull(result);
    }

    [Fact]
    public void ParlotEmail()
    {
        var benchmarks = new RegexBenchmarks();
        var result = benchmarks.ParlotEmail();
        Assert.Equal(RegexBenchmarks.Email, result);
    }

    [Fact]
    public void ParlotEmailGenerated()
    {
        var benchmarks = new RegexBenchmarks();
        var result = benchmarks.ParlotEmailGenerated();
        Assert.Equal(RegexBenchmarks.Email, result);
    }

    [Fact]
    public void RegexLibraryComparisons()
    {
        var benchmarks = new RegexBenchmarks();
        benchmarks.Setup();

        Assert.Equal(RegexBenchmarks.Email, benchmarks.RegexEmail());
        Assert.Equal(RegexBenchmarks.Email, benchmarks.RegexEmailCompiled());
        Assert.Equal(RegexBenchmarks.Email, benchmarks.RegexEmailGenerated());
        Assert.Equal(RegexBenchmarks.Email, benchmarks.ParlotEmail());
        Assert.Equal(RegexBenchmarks.Email, benchmarks.ParlotEmailGenerated());
        Assert.Equal(RegexBenchmarks.Email, benchmarks.PidginEmail());
        Assert.Equal(RegexBenchmarks.Email, benchmarks.FarkleEmail());
    }

    [Theory]
    [InlineData("a@b.c")]
    [InlineData("user.name+tag@sub-domain.example.com")]
    [InlineData("User9+tag-1@Domain-2.Example9")]
    [InlineData("\u00e9@\u4e2d.\u03b1")]
    [InlineData("\u0661@\u0662.\u0663")]
    public void EmailRecognizersMatchParlot(string input)
    {
        Assert.Equal(input, RegexBenchmarks.EmailRegex.Match(input).Value);
        Assert.Equal(input, EmailParser.Parser.Parse(input).ToString());
        Assert.True(EmailParser.TryParseGenerated(input, out var generated));
        Assert.Equal(input, generated);
        Assert.Same(input, PidginEmailParser.Parse(input));
        Assert.Same(input, FarkleEmailParser.Parse(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("user.example.com")]
    [InlineData("user@")]
    [InlineData("@domain.com")]
    [InlineData("user@domain")]
    [InlineData("user@.com")]
    [InlineData("user@domain.")]
    [InlineData("user name@domain.com")]
    [InlineData(" user@domain.com")]
    [InlineData("user@domain.com ")]
    public void EmailRecognizersRejectInvalidInput(string input)
    {
        Assert.Throws<Pidgin.ParseException<char>>(() => PidginEmailParser.Parse(input));
        Assert.Throws<InvalidOperationException>(() => FarkleEmailParser.Parse(input));
    }

    [Fact]
    public void SimpleGeneratedParsers()
    {
        var benchmarks = new SimpleParsersBenchmarks();

        Assert.Equal("hello", benchmarks.Text_Generated());
        Assert.Equal(123.456m, benchmarks.Decimal_Generated());
        Assert.Equal(123L, benchmarks.Integer_Generated());
        Assert.Equal("cherry", benchmarks.OneOf_Generated());
        Assert.Equal("cherry", benchmarks.LiteralOneOf_Generated());
        Assert.Equal(("price", 99.99m), benchmarks.And_Generated());
        Assert.Equal(new[] { 1m, 2m, 3m, 4m, 5m }, benchmarks.ZeroOrMany_Generated());
        Assert.Equal(42.5m, benchmarks.SkipWhiteSpace_Generated());
    }

    [Fact]
    public void GeneratedCollections()
    {
        foreach (var combinator in new[] { "ZeroOrMany", "OneOrMany", "Separated", "SeparatedOptions" })
        {
            foreach (var count in new[] { 0, 1, 4, 5, 32 })
            {
                var benchmarks = new CollectionBenchmarks { Count = count, Combinator = combinator };
                benchmarks.Setup();
                Assert.Equal(count > 0 || combinator == "ZeroOrMany", benchmarks.Generated());
            }
        }
    }

    [Fact]
    public void GeneratedConfiguration()
    {
        var benchmarks = new IfSelectBenchmarks { Condition = true, Match = true };
        benchmarks.Setup();

        Assert.True(benchmarks.IfGenerated());
        Assert.True(benchmarks.SelectGenerated());
        Assert.True(benchmarks.IfElseGenerated());
        Assert.True(benchmarks.SelectElseGenerated());
    }

    [Fact]
    public void GeneratedSql()
    {
        var benchmarks = new SqlParserBenchmarks
        {
            Sql = "select a where a not like '%foo%'"
        };

        Assert.True(benchmarks.Generated());
    }

    [Fact]
    public void ParlotLookupFluent()
    {
        var benchmarks = new SwitchExpressionBenchmarks() { Length = 2 };
        benchmarks.Setup();
        var result = benchmarks.LookupMatchFluent();
    }

}
#endif
