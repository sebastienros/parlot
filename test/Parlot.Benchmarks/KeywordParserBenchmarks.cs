using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public partial class KeywordParserBenchmarks
{
    private delegate bool TryParse(string input, out string value);

    private TryParse _generated;
    private TryParse _firstCharacterLookup;
    private Parser<string> _parser;
    private KeywordLookupBenchmarks.CharMatcher _tree;
    private FrozenDictionary<string, string>.AlternateLookup<ReadOnlySpan<char>> _frozen;
    private string[] _inputs;

    [Params("Small", "Medium", "Language", "SharedPrefix")]
    public string Vocabulary { get; set; }

    [Params("Hit", "Miss", "Mixed")]
    public string Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var words = KeywordLookupBenchmarks.Words(Vocabulary);
        _parser = OneOf(words.Select(static word => Literals.Keyword(word)).ToArray());
        _generated = Vocabulary switch
        {
            "Small" => TryParseSmall,
            "Medium" => TryParseMedium,
            "Language" => TryParseLanguage,
            "SharedPrefix" => TryParseSharedPrefix,
            _ => throw new InvalidOperationException(),
        };
        _firstCharacterLookup = Vocabulary switch
        {
            "Small" => TryParseSmallBaseline,
            "Medium" => TryParseMediumBaseline,
            "Language" => TryParseLanguageBaseline,
            "SharedPrefix" => TryParseSharedPrefixBaseline,
            _ => throw new InvalidOperationException(),
        };
        _tree = KeywordLookupBenchmarks.Compile(words, ignoreCase: false).Narrow;
        _frozen = words.ToFrozenDictionary(static word => word, StringComparer.Ordinal).GetAlternateLookup<ReadOnlySpan<char>>();
        _inputs = Enumerable.Range(0, 64).Select(index =>
        {
            var word = words[(index * 17 + index / 4) % words.Length];
            return Scenario == "Hit" || Scenario == "Mixed" && index % 4 == 0 ? word + "!"
                : (index % 4) switch
                {
                    0 => "!" + word,
                    1 => "z" + word[1..],
                    2 => word[..^1] + "Z!",
                    _ => word + "x!",
                };
        }).ToArray();
        foreach (var input in _inputs)
        {
            var success = _parser.TryParse(input, out var expected);
            if (Scenario == "Miss" && success || Scenario == "Hit" && !success
                || _generated(input, out var actual) != success || success && actual != expected
                || _firstCharacterLookup(input, out var baseline) != success || success && baseline != expected
                || (MatchTree(input) >= 0) != success || _frozen.TryGetValue(Token(input), out _) != success)
            {
                throw new InvalidOperationException($"Incorrect keyword parsing of '{input}'.");
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int RuntimeOneOf()
    {
        var count = 0;
        foreach (var input in _inputs)
        {
            count += _parser.TryParse(input, out _) ? 1 : 0;
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int GeneratedOneOf()
    {
        var count = 0;
        foreach (var input in _inputs)
        {
            count += _generated(input, out _) ? 1 : 0;
        }

        return count;
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 64)]
    public int GeneratedFirstCharacterLookup()
    {
        var count = 0;
        foreach (var input in _inputs)
        {
            count += _firstCharacterLookup(input, out _) ? 1 : 0;
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int ScanAndTree()
    {
        var count = 0;
        foreach (var input in _inputs)
        {
            count += MatchTree(input) >= 0 ? 1 : 0;
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int ScanAndFrozen()
    {
        var count = 0;
        foreach (var input in _inputs)
        {
            count += _frozen.TryGetValue(Token(input), out _) ? 1 : 0;
        }

        return count;
    }

    private int MatchTree(string input) => _tree(Token(input));

    private static ReadOnlySpan<char> Token(string input)
    {
        var length = 0;
        while (length < input.Length && (uint)((input[length] | 0x20) - 'a') <= 'z' - 'a')
        {
            length++;
        }

        return input.AsSpan(0, length);
    }

    private static partial bool TryParseSmall(string input, out string value);
    private static partial bool TryParseMedium(string input, out string value);
    private static partial bool TryParseLanguage(string input, out string value);
    private static partial bool TryParseSharedPrefix(string input, out string value);
    private static partial bool TryParseSmallBaseline(string input, out string value);
    private static partial bool TryParseMediumBaseline(string input, out string value);
    private static partial bool TryParseLanguageBaseline(string input, out string value);
    private static partial bool TryParseSharedPrefixBaseline(string input, out string value);

    // Hide only the concrete keyword type from the optimization, retaining the original
    // seek table and exactly the same emitted keyword body (no extra generated wrapper).
    internal sealed class BaselineKeyword : Parser<string>, ISeekable, ISourceable
    {
        private readonly KeywordLiteral _keyword;

        public BaselineKeyword(string word)
        {
            _keyword = new KeywordLiteral(word);
        }

        public bool CanSeek => _keyword.CanSeek;
        public char[] ExpectedChars => _keyword.ExpectedChars;
        public bool SkipWhitespace => false;
        public override bool Parse(ParseContext context, ref ParseResult<string> result) => _keyword.Parse(context, ref result);
        public SourceResult GenerateSource(SourceGenerationContext context) => _keyword.GenerateSource(context);
    }
}
