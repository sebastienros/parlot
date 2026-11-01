using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using System;
using System.Text;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                                     | Mean      | Error     | Ratio | Allocated |
// |------------------------------------------- |----------:|----------:|------:|----------:|
// | Json_Default                               |  31.83 us |  0.774 us |  1.00 |  64.07 KB |
// | Json_AsciiSet                              |  31.98 us |  0.783 us |  1.01 |  64.07 KB |
// | Json_AsciiWhiteSpaceParser                 |  43.48 us |  2.961 us |  1.37 |  64.07 KB |
// | Json_SpaceTabCrLfSet                       |  32.35 us |  4.308 us |  1.02 |  64.07 KB |
// | Json_SpaceTabCrLfWhiteSpaceParser          |  40.30 us |  1.918 us |  1.27 |  64.07 KB |
// | Json_Generated                             |  31.90 us |  7.326 us |  1.00 |  64.02 KB |
// | Json_GeneratedAsciiSet                     |  29.61 us |  5.361 us |  0.93 |  64.02 KB |
// | Json_GeneratedSpaceTabCrLfSet              |  29.02 us |  3.374 us |  0.91 |  64.02 KB |
// | Json_GeneratedSpaceTabCrLfWhiteSpaceParser |  39.71 us |  1.769 us |  1.25 |  64.02 KB |
// | Expression_Default                         |  1.127 us | 0.0061 us |  1.00 |   1.41 KB |
// | Expression_AsciiSet                        |  1.098 us | 0.0717 us |  0.97 |   1.41 KB |
// | Expression_AsciiWhiteSpaceParser           |  1.436 us | 0.0167 us |  1.27 |   1.41 KB |

// Parses the same texts with the default white space set, with the ASCII set, and with a WhiteSpaceParser that skips the
// same ASCII chars, the only way to change the set before WhiteSpaceSet. The JSON texts are also parsed with the SpaceTabCrLf set
// and with a WhiteSpaceParser skipping its chars. The texts only contain JSON white space.

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class WhiteSpaceSetBenchmarks
{
    private const string Expression = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private static readonly Parser<TextSpan> _asciiWhiteSpaceParser = Literals.AnyOf(Character.AsciiWhiteSpaceOrNewLines);
    private static readonly Parser<TextSpan> _spaceTabCrLfWhiteSpaceParser = Literals.AnyOf(Character.SpaceTabCrLfWhiteSpaceOrNewLines);

    private readonly string _json = BuildJson();

    [GlobalSetup]
    public void Setup()
    {
        var expected = Json_Default().ToString();

        if (Json_AsciiSet().ToString() != expected || Json_AsciiWhiteSpaceParser().ToString() != expected
            || Json_SpaceTabCrLfSet().ToString() != expected || Json_SpaceTabCrLfWhiteSpaceParser().ToString() != expected
            || Json_Generated().ToString() != expected || Json_GeneratedAsciiSet().ToString() != expected
            || Json_GeneratedSpaceTabCrLfSet().ToString() != expected || Json_GeneratedSpaceTabCrLfWhiteSpaceParser().ToString() != expected)
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
    public IJson Json_SpaceTabCrLfSet() => JsonParser.Json.Parse(new ParseContext(new Scanner(_json)) { WhiteSpaceSet = WhiteSpaceSet.SpaceTabCrLf })!;

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_SpaceTabCrLfWhiteSpaceParser() => JsonParser.Json.Parse(new ParseContext(new Scanner(_json)) { WhiteSpaceParser = _spaceTabCrLfWhiteSpaceParser })!;

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

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_GeneratedSpaceTabCrLfSet()
    {
        _ = GeneratedParsers.TryParseJsonSpaceTabCrLf(_json, out var result);
        return result;
    }

    [Benchmark, BenchmarkCategory("Json")]
    public IJson Json_GeneratedSpaceTabCrLfWhiteSpaceParser()
    {
        _ = GeneratedParsers.TryParseJsonWhiteSpaceParser(_json, out var result);
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
