using System;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public partial class TextParserBenchmarks
{
    private delegate bool TryParse(string input, out string value);

    private TryParse _generated;
    private TryParse _baseline;
    private string[] _inputs;

    [Params("Language", "Headers", "SharedPrefix")]
    public string Vocabulary { get; set; }

    [Params("Hit", "Miss", "Mixed")]
    public string Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var words = KeywordLookupBenchmarks.Words(Vocabulary);
        var parser = OneOf(words.Select(static word => Literals.Text(word)).ToArray());
        (_generated, _baseline) = Vocabulary switch
        {
            "Language" => ((TryParse)TryParseLanguage, (TryParse)TryParseLanguageBaseline),
            "Headers" => ((TryParse)TryParseHeaders, (TryParse)TryParseHeadersBaseline),
            "SharedPrefix" => ((TryParse)TryParseSharedPrefix, (TryParse)TryParseSharedPrefixBaseline),
            _ => throw new InvalidOperationException(),
        };
        _inputs = Enumerable.Range(0, 64).Select(index =>
        {
            var word = words[(index * 17 + index / 4) % words.Length];
            if (Scenario == "Hit" || Scenario == "Mixed" && index % 4 == 0)
            {
                return word + "tail";
            }

            var miss = (index % 4) switch
            {
                0 => "!" + word,
                1 => "!" + word[1..],
                2 => word[..^1] + "!",
                _ => word[..^1],
            };
            return words.Any(candidate => miss.StartsWith(candidate, StringComparison.Ordinal)) ? "!" + miss : miss;
        }).ToArray();
        foreach (var input in _inputs)
        {
            var success = parser.TryParse(input, out var expected);
            if (_generated(input, out var actual) != success || actual != expected
                || _baseline(input, out var baseline) != success || baseline != expected)
            {
                throw new InvalidOperationException($"Incorrect text parsing of '{input}'.");
            }
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 64)]
    public int GeneratedFirstCharacterLookup() => Count(_baseline);

    [Benchmark(OperationsPerInvoke = 64)]
    public int GeneratedPackedPrefixes() => Count(_generated);

    private int Count(TryParse parser)
    {
        var count = 0;
        foreach (var input in _inputs)
        {
            count += parser(input, out _) ? 1 : 0;
        }

        return count;
    }

    private static partial bool TryParseLanguage(string input, out string value);
    private static partial bool TryParseHeaders(string input, out string value);
    private static partial bool TryParseSharedPrefix(string input, out string value);
    private static partial bool TryParseLanguageBaseline(string input, out string value);
    private static partial bool TryParseHeadersBaseline(string input, out string value);
    private static partial bool TryParseSharedPrefixBaseline(string input, out string value);

    internal sealed class BaselineText : Parser<string>, ISeekable, ISourceable
    {
        private readonly TextLiteral _text;

        public BaselineText(string text) => _text = new TextLiteral(text, StringComparison.Ordinal);
        public bool CanSeek => _text.CanSeek;
        public char[] ExpectedChars => _text.ExpectedChars;
        public bool SkipWhitespace => false;
        public override bool Parse(ParseContext context, ref ParseResult<string> result) => _text.Parse(context, ref result);
        public SourceResult GenerateSource(SourceGenerationContext context) => _text.GenerateSource(context);
    }
}
