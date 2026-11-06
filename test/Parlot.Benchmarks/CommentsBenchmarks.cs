using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using System;
using System.Text;

namespace Parlot.Benchmarks;

// Parses expressions and JSON whose white space is skipped by the parser of WithComments. One text has only spaces, the other
// one comments too. The parser is only invoked before the chars it can start with, white space and '/', see
// CustomWhiteSpace: Json_Default and Json_WithComments differ by the invocations before white space.

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class CommentsBenchmarks
{
    private const string Spaces = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private const string Comments = "1 - /* a */ ( 3 + 2.5 ) * 4 // b\n - 1 / 2 + 1 - ( 3 /* c */ + 2.5 ) * 4 - 1 / 2 // d\n + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private static readonly Parser<Tests.Calc.Expression> _parser = FluentParser.Expression.WithComments(static comments =>
    {
        comments.WithWhiteSpaceOrNewLine();
        comments.WithSingleLine("//");
        comments.WithMultiLine("/*", "*/");
    });

    private static readonly Parser<IJson> _jsonParser = JsonParser.Json.WithComments(static comments =>
    {
        comments.WithWhiteSpaceOrNewLine();
        comments.WithSingleLine("//");
        comments.WithMultiLine("/*", "*/");
    });

    private readonly string _json = BuildJson(comments: false);

    private readonly string _jsonComments = BuildJson(comments: true);

    [GlobalSetup]
    public void Setup()
    {
        var expected = FluentParser.Expression.Parse(Spaces)!.Evaluate();

        if (Expression_Spaces().Evaluate() != expected || Expression_Comments().Evaluate() != expected)
        {
            throw new InvalidOperationException("Incorrect expression parsing.");
        }

        var json = Json_Default().ToString();

        if (Json_WithComments().ToString() != json || Json_Comments().ToString() != json)
        {
            throw new InvalidOperationException("Incorrect JSON parsing.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_Spaces() => _parser.Parse(new ParseContext(new Scanner(Spaces)))!;

    [Benchmark, BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_Comments() => _parser.Parse(new ParseContext(new Scanner(Comments)))!;

    // The JSON without comments, with the default white space set
    [Benchmark(Baseline = true), BenchmarkCategory("Json")]
    public IJson Json_Default() => JsonParser.Json.Parse(new ParseContext(new Scanner(_json)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_WithComments() => _jsonParser.Parse(new ParseContext(new Scanner(_json)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_Comments() => _jsonParser.Parse(new ParseContext(new Scanner(_jsonComments)))!;

    private static string BuildJson(bool comments)
    {
        var builder = new StringBuilder("[\n");

        for (var i = 0; i < 64; i++)
        {
            builder.Append(comments ? "  { // item\n" : "  {\n");
            builder.Append("    \"id\": \"").Append(i).Append("\",\n");
            builder.Append("    \"name\": \"item").Append(i).Append(comments ? "\", /* name */\n" : "\",\n");
            builder.Append("    \"tags\": [ \"a\", \"b\", \"c\", \"d\" ],\n");
            builder.Append("    \"kind\": \"value\"\n");
            builder.Append(i < 63 ? "  },\n" : "  }\n");
        }

        return builder.Append(']').ToString();
    }
}
