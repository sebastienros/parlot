using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Globalization;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Parses 2,000 identifiers of the lengths of JavaScript code with each identifier parser: the default ASCII one, the one
// with char predicates, and the one with code point predicates, without options and with the options of JavaScript. The
// last benchmark has an escape sequence or an astral char in one identifier of eight.
//
// The ASCII chars are tested with bit masks built from the predicates, which are only invoked for the other chars:
//
// | Method                          | Mean     | Error    | StdDev   | Ratio | Allocated |
// |-------------------------------- |---------:|---------:|---------:|------:|----------:|
// | Default                         | 40.39 us | 2.026 us | 5.973 us |  1.02 |  31.46 KB |
// | CharPredicates                  | 43.73 us | 1.239 us | 3.653 us |  1.11 |  31.46 KB |
// | CodePoints                      | 39.43 us | 0.966 us | 2.849 us |  1.00 |  31.46 KB |
// | JavaScript                      | 38.58 us | 1.171 us | 3.452 us |  0.98 |  31.46 KB |
// | JavaScriptEscapesAndAstralChars | 41.82 us | 1.233 us | 3.636 us |  1.06 |  31.46 KB |
[MemoryDiagnoser]
public class CodePointIdentifierBenchmarks
{
    private static readonly string[] _names = ["i", "jQuery", "elem", "length", "prototype", "documentElement", "_data", "fn", "type", "$", "handleObj", "nodeName"];

    private static readonly string _ascii = string.Join(" ", Enumerable.Range(0, 2000).Select(static i => _names[i % _names.Length]));

    private static readonly string _unicode = string.Join(" ", Enumerable.Range(0, 2000).Select(static i => (i % 8) switch
    {
        3 => "\\u0061" + _names[i % _names.Length],
        7 => _names[i % _names.Length] + "\U0001D49C",
        _ => _names[i % _names.Length],
    }));

    private static bool IsStart(int c) => c < 128
        ? c is '$' or '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z')
        : c < 0x10000 ? char.IsLetter((char)c) : CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.OtherLetter;

    private static bool IsPart(int c) => c is >= '0' and <= '9' || IsStart(c) || (c is >= 128 and < 0x10000 && char.IsDigit((char)c));

    private static readonly Parser<int> _default = ZeroOrMany(Terms.Identifier()).Then(static x => x.Count);

    private static readonly Parser<int> _charPredicates = ZeroOrMany(Terms.Identifier(static c => c >= 128 && char.IsLetter(c), static c => c >= 128 && char.IsLetterOrDigit(c))).Then(static x => x.Count);

    private static readonly Parser<int> _codePoints = ZeroOrMany(Terms.Identifier(IsStart, IsPart, IdentifierOptions.None)).Then(static x => x.Count);

    private static readonly Parser<int> _javaScript = ZeroOrMany(Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript)).Then(static x => x.Count);

    public CodePointIdentifierBenchmarks()
    {
        if (Default() != 2000) throw new InvalidOperationException(nameof(Default));
        if (CharPredicates() != 2000) throw new InvalidOperationException(nameof(CharPredicates));
        if (CodePoints() != 2000) throw new InvalidOperationException(nameof(CodePoints));
        if (JavaScript() != 2000) throw new InvalidOperationException(nameof(JavaScript));
        if (JavaScriptEscapesAndAstralChars() != 2000) throw new InvalidOperationException(nameof(JavaScriptEscapesAndAstralChars));
    }

    [Benchmark(Baseline = true)]
    public int Default() => _default.Parse(_ascii);

    [Benchmark]
    public int CharPredicates() => _charPredicates.Parse(_ascii);

    [Benchmark]
    public int CodePoints() => _codePoints.Parse(_ascii);

    [Benchmark]
    public int JavaScript() => _javaScript.Parse(_ascii);

    [Benchmark]
    public int JavaScriptEscapesAndAstralChars() => _javaScript.Parse(_unicode);
}
