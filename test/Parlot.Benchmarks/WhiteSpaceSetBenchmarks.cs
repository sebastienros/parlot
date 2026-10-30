using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using System;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                           | Mean      | Error     | Ratio | Allocated |
// |--------------------------------- |----------:|----------:|------:|----------:|
// | Json_Default                     | 30.405 us | 2.3881 us |  1.00 |   65.8 KB |
// | Json_AsciiSet                    | 30.295 us | 0.6694 us |  1.00 |   65.8 KB |
// | Json_AsciiWhiteSpaceParser       | 35.501 us | 0.2033 us |  1.17 |   65.8 KB |
// | Json_Generated                   | 25.029 us | 2.2293 us |  0.82 |  68.66 KB |
// | Json_GeneratedAsciiSet           | 24.918 us | 0.9886 us |  0.82 |  68.66 KB |
// | Expression_Default               |  1.127 us | 0.0061 us |  1.00 |   1.41 KB |
// | Expression_AsciiSet              |  1.098 us | 0.0717 us |  0.97 |   1.41 KB |
// | Expression_AsciiWhiteSpaceParser |  1.436 us | 0.0167 us |  1.27 |   1.41 KB |

// Parses the same texts with the default white space set, with the ASCII set, and with a WhiteSpaceParser that skips the
// same ASCII chars, the only way to change the set before WhiteSpaceSet. The texts only contain ASCII white space.

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class WhiteSpaceSetBenchmarks
{
    private const string Expression = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private static readonly Parser<TextSpan> _asciiWhiteSpaceParser = Literals.AnyOf(Character.AsciiWhiteSpaceOrNewLines);

    private readonly string _json = BuildJson();

    [GlobalSetup]
    public void Setup()
    {
        var expected = Json_Default().ToString();

        if (Json_AsciiSet().ToString() != expected || Json_AsciiWhiteSpaceParser().ToString() != expected
            || Json_Generated().ToString() != expected || Json_GeneratedAsciiSet().ToString() != expected)
        {
            throw new InvalidOperationException("Incorrect JSON parsing.");
        }

        var value = Expression_Default().Evaluate();

        if (Expression_AsciiSet().Evaluate() != value || Expression_AsciiWhiteSpaceParser().Evaluate() != value)
        {
            throw new InvalidOperationException("Incorrect expression parsing.");
        }
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Json")]
    public IJson Json_Default() => JsonParser.Json.Parse(new ParseContext(new Scanner(_json)))!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_AsciiSet() => JsonParser.Json.Parse(new ParseContext(new Scanner(_json)) { WhiteSpaceSet = WhiteSpaceSet.Ascii })!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_AsciiWhiteSpaceParser() => JsonParser.Json.Parse(new ParseContext(new Scanner(_json)) { WhiteSpaceParser = _asciiWhiteSpaceParser })!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_Generated()
    {
        _ = GeneratedParsers.TryParseJson(_json, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_GeneratedAsciiSet()
    {
        _ = GeneratedParsers.TryParseJsonAscii(_json, out var result);
        return result;
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_Default() => FluentParser.Expression.Parse(new ParseContext(new Scanner(Expression)))!;

    [Benchmark, BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_AsciiSet() => FluentParser.Expression.Parse(new ParseContext(new Scanner(Expression)) { WhiteSpaceSet = WhiteSpaceSet.Ascii })!;

    [Benchmark, BenchmarkCategory("Expression")]
    public Tests.Calc.Expression Expression_AsciiWhiteSpaceParser() => FluentParser.Expression.Parse(new ParseContext(new Scanner(Expression)) { WhiteSpaceParser = _asciiWhiteSpaceParser })!;

    // An indented document: 64 objects of 4 members, one of them an array of 4 strings
    private static string BuildJson()
    {
        var builder = new StringBuilder("[\n");

        for (var i = 0; i < 64; i++)
        {
            builder.Append("  {\n");
            builder.Append("    \"id\": \"").Append(i).Append("\",\n");
            builder.Append("    \"name\": \"item").Append(i).Append("\",\n");
            builder.Append("    \"tags\": [ \"a\", \"b\", \"c\", \"d\" ],\n");
            builder.Append("    \"kind\": \"value\"\n");
            builder.Append(i < 63 ? "  },\n" : "  }\n");
        }

        return builder.Append(']').ToString();
    }
}
