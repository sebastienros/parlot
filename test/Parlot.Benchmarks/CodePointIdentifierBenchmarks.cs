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
//
// The Tokens rows read 2,000 JavaScript tokens with a choice which holds the identifier before the numbers and the
// punctuation. The identifier declares the ASCII chars of its start predicate, the backslash and any non-ASCII char, so the
// choice doesn't try it for the other tokens, and invokes the parser of Lookup() directly (fastest of alternating slices):
//
// | Method                 | Before   | After    |
// |----------------------- |---------:|---------:|
// | TokensJavaScript       | 34.6 us  | 29.7 us  |
// | TokensJavaScriptLookup | 32.2 us  | 29.6 us  |
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

    // The tokens of JavaScript code: keywords, identifiers, numbers and punctuation, read by a choice which holds the
    // identifier before the numbers and the punctuation, like the primary expressions of JavaScript
    private static readonly string[] _tokens = ["var", "elem", "=", "jQuery", ".", "fn", "(", "type", ",", "0", ")", ";", "if", "(", "!", "length", ")", "return", "handleObj", "[", "1", "]", "{", "}", "function", "$"];

    private static readonly string _code = string.Join(" ", Enumerable.Range(0, 2000).Select(static i => _tokens[i % _tokens.Length]));

    private static Parser<TextSpan> Text(string text) => Terms.Text(text).Then(static x => new TextSpan(x));

    private static Parser<int> Tokens(Parser<TextSpan> identifier) => ZeroOrMany(OneOf(
        Text("var"), Text("if"), Text("return"), Text("function"),
        identifier,
        Terms.Integer().Then(static x => default(TextSpan)),
        Text("="), Text("."), Text(","), Text("("), Text(")"), Text(";"), Text("!"), Text("["), Text("]"), Text("{"), Text("}")))
        .Then(static x => x.Count);

    // The identifier declares its hints
    private static readonly Parser<int> _tokensJavaScript = Tokens(Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript));

    // The hints are given with Lookup(), whose parser the choice invokes directly
    private static readonly Parser<int> _tokensJavaScriptLookup = Tokens(Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript)
        .Lookup(true, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ$_\\" + Fluent.OneOf<TextSpan>.NonAsciiSeekableChar));

    public CodePointIdentifierBenchmarks()
    {
        if (TokensJavaScript() != 2000) throw new InvalidOperationException(nameof(TokensJavaScript));
        if (TokensJavaScriptLookup() != 2000) throw new InvalidOperationException(nameof(TokensJavaScriptLookup));
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

    [Benchmark]
    public int TokensJavaScript() => _tokensJavaScript.Parse(_code);

    [Benchmark]
    public int TokensJavaScriptLookup() => _tokensJavaScriptLookup.Parse(_code);
}
