using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using System;
using System.Text;

using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Parses texts whose white space is skipped by the parser of WithComments, which is invoked by each Terms parser. One text
// of each grammar has only white space, the other one comments too.
//
// Composed: the white space parser WithComments built before CommentsParser, Capture(ZeroOrMany(OneOf(...))) of the
// builder's parsers. WithComments: the CommentsParser it builds now.

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class CommentsBenchmarks
{
    private const string Spaces = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private const string Comments = "1 - /* a */ ( 3 + 2.5 ) * 4 // b\n - 1 / 2 + 1 - ( 3 /* c */ + 2.5 ) * 4 - 1 / 2 // d\n + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private static readonly Action<CommentsBuilder> _comments = static comments =>
    {
        comments.WithWhiteSpaceOrNewLine();
        comments.WithSingleLine("//");
        comments.WithMultiLine("/*", "*/");
    };

    private static Parser<TextSpan> Composed() => Capture(ZeroOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Literals.Comments("//"), Literals.Comments("/*", "*/"))));

    private static readonly Parser<Tests.Calc.Expression> _parser = FluentParser.Expression.WithComments(_comments);

    private static readonly Parser<Tests.Calc.Expression> _composedParser = FluentParser.Expression.WithWhiteSpaceParser(Composed());

    private static readonly Parser<IJson> _jsonParser = JsonParser.Json.WithComments(_comments);

    private static readonly Parser<IJson> _composedJsonParser = JsonParser.Json.WithWhiteSpaceParser(Composed());

    private readonly string _json = BuildJson(comments: false);

    private readonly string _jsonComments = BuildJson(comments: true);

    [GlobalSetup]
    public void Setup()
    {
        var expected = FluentParser.Expression.Parse(Spaces)!.Evaluate();

        if (Expression_Spaces().Evaluate() != expected || Expression_Comments().Evaluate() != expected
            || Expression_ComposedSpaces().Evaluate() != expected || Expression_ComposedComments().Evaluate() != expected)
        {
            throw new InvalidOperationException("Incorrect expression parsing.");
        }

        var json = JsonParser.Json.Parse(_json)!.ToString();

        if (Json_Spaces().ToString() != json || Json_Comments().ToString() != json
            || Json_ComposedSpaces().ToString() != json || Json_ComposedComments().ToString() != json
            || Json_GeneratedComments().ToString() != json || Json_GeneratedComposedComments().ToString() != json)
        {
            throw new InvalidOperationException("Incorrect JSON parsing.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_ComposedSpaces() => _composedParser.Parse(new ParseContext(new Scanner(Spaces)))!;

    [Benchmark, BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_Spaces() => _parser.Parse(new ParseContext(new Scanner(Spaces)))!;

    [Benchmark, BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_ComposedComments() => _composedParser.Parse(new ParseContext(new Scanner(Comments)))!;

    [Benchmark, BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_Comments() => _parser.Parse(new ParseContext(new Scanner(Comments)))!;

    [Benchmark(Baseline = true), BenchmarkCategory("Json")]
    public IJson Json_ComposedSpaces() => _composedJsonParser.Parse(new ParseContext(new Scanner(_json)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_Spaces() => _jsonParser.Parse(new ParseContext(new Scanner(_json)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_ComposedComments() => _composedJsonParser.Parse(new ParseContext(new Scanner(_jsonComments)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_Comments() => _jsonParser.Parse(new ParseContext(new Scanner(_jsonComments)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_GeneratedComposedComments()
    {
        _ = GeneratedParsers.TryParseJsonComposedComments(_jsonComments, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_GeneratedComments()
    {
        _ = GeneratedParsers.TryParseJsonComments(_jsonComments, out var result);
        return result;
    }

    // An indented document: 64 objects of 4 members, one of them an array of 4 strings, with a comment on two of its lines
    // and between two of its tokens
    private static string BuildJson(bool comments)
    {
        var builder = new StringBuilder("[\n");

        for (var i = 0; i < 64; i++)
        {
            builder.Append("  {").Append(comments ? " // item\n" : "\n");
            builder.Append("    \"id\": ").Append(comments ? "/* key */ " : "").Append('"').Append(i).Append("\",\n");
            builder.Append("    \"name\": \"item").Append(i).Append("\",\n");
            builder.Append("    \"tags\": [ \"a\", \"b\", \"c\", \"d\" ],").Append(comments ? " // tags\n" : "\n");
            builder.Append("    \"kind\": \"value\"\n");
            builder.Append(i < 63 ? "  },\n" : "  }\n");
        }

        return builder.Append(']').ToString();
    }
}
