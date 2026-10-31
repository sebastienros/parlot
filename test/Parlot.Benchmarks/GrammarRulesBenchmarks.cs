using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method                | Mean      | Error     | StdDev   | Allocated |
// |---------------------- |----------:|----------:|---------:|----------:|
// | MixedWhiteSpace       | 120.82 ns | 61.595 ns | 3.376 ns |         - |
// | SameWhiteSpace        |  19.62 ns |  0.663 ns | 0.036 ns |         - |
// | UnseekableAlternative |  13.15 ns |  0.359 ns | 0.020 ns |         - |
// | SeekableAlternative   |  11.63 ns |  0.362 ns | 0.020 ns |         - |
// | SharedLeadingParser   |  31.00 ns |  1.948 ns | 0.107 ns |         - |
// | FactoredLeadingParser |  15.98 ns |  0.305 ns | 0.017 ns |         - |
// | LoopDetection         | 132.06 ns |  8.897 ns | 0.488 ns |         - |
// | LoopDetectionElided   |  87.63 ns |  5.196 ns | 0.285 ns |         - |

// Each performance rule of docs/grammar-rules.md, as the pattern it reports and the grammar it suggests. The numbers
// are in the documentation of the rules. The contexts are reused and the cursor moved back before each parse, such that
// the allocations of a context are not part of the result.

[MemoryDiagnoser, ShortRunJob]
public class GrammarRulesBenchmarks
{
    // PARLOT101: one alternative doesn't skip white space, so the choice has no lookup table
    private static readonly Parser<string> _mixedWhiteSpace = OneOf(
        Literals.Text("alpha"), Terms.Text("bravo"), Terms.Text("charlie"), Terms.Text("delta"),
        Terms.Text("echo"), Terms.Text("foxtrot"), Terms.Text("golf"), Terms.Text("hotel"));

    private static readonly Parser<string> _sameWhiteSpace = OneOf(
        Terms.Text("alpha"), Terms.Text("bravo"), Terms.Text("charlie"), Terms.Text("delta"),
        Terms.Text("echo"), Terms.Text("foxtrot"), Terms.Text("golf"), Terms.Text("hotel"));

    // PARLOT102: a predicate can't tell which chars it accepts, so its alternative is tried for every char
    private static readonly Parser<TextSpan> _unseekable = OneOf(
        Terms.Pattern(static c => c is >= 'a' and <= 'z'), Terms.Text("(").Then(static x => new TextSpan(x)), Terms.Text(")").Then(static x => new TextSpan(x)),
        Terms.Text("+").Then(static x => new TextSpan(x)), Terms.Text("-").Then(static x => new TextSpan(x)));

    private static readonly Parser<TextSpan> _seekable = OneOf(
        Terms.AnyOf("abcdefghijklmnopqrstuvwxyz"), Terms.Text("(").Then(static x => new TextSpan(x)), Terms.Text(")").Then(static x => new TextSpan(x)),
        Terms.Text("+").Then(static x => new TextSpan(x)), Terms.Text("-").Then(static x => new TextSpan(x)));

    // PARLOT103: both alternatives start with the identifier, which the second one parses again
    private static readonly Parser<TextSpan> _identifier = Terms.Identifier();

    private static readonly Parser<(TextSpan, char)> _sharedLeadingParser =
        _identifier.And(Terms.Char('+')).Or(_identifier.And(Terms.Char('-')));

    private static readonly Parser<(TextSpan, char)> _factored =
        _identifier.And(Terms.Char('+').Or(Terms.Char('-')));

    // PARLOT104: a parser type the loop analysis can't read keeps the loop detection of the Deferred that invokes it first
    private static readonly Parser<long> _loopDetection = Nested(static parser => new Opaque<long>(parser));

    private static readonly Parser<long> _loopDetectionElided = Nested(static parser => parser);

    private readonly ParseContext _mixedWhiteSpaceContext = new(new Scanner("hotel"));
    private readonly ParseContext _sameWhiteSpaceContext = new(new Scanner("hotel"));
    private readonly ParseContext _unseekableContext = new(new Scanner("-"));
    private readonly ParseContext _seekableContext = new(new Scanner("-"));
    private readonly ParseContext _sharedLeadingParserContext = new(new Scanner("identifier -"));
    private readonly ParseContext _factoredContext = new(new Scanner("identifier -"));
    private readonly ParseContext _loopDetectionContext = new(new Scanner("((((((((1))))))))"));
    private readonly ParseContext _loopDetectionElidedContext = new(new Scanner("((((((((1))))))))"));

    [GlobalSetup]
    public void Setup()
    {
        if (MixedWhiteSpace() != "hotel" || SameWhiteSpace() != "hotel" || UnseekableAlternative().ToString() != "-" || SeekableAlternative().ToString() != "-"
            || SharedLeadingParser().Item2 != '-' || FactoredLeadingParser().Item2 != '-' || LoopDetection() != 1 || LoopDetectionElided() != 1)
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }
    }

    [BenchmarkCategory("PARLOT101"), Benchmark]
    public string MixedWhiteSpace() => Parse(_mixedWhiteSpace, _mixedWhiteSpaceContext);

    [BenchmarkCategory("PARLOT101"), Benchmark]
    public string SameWhiteSpace() => Parse(_sameWhiteSpace, _sameWhiteSpaceContext);

    [BenchmarkCategory("PARLOT102"), Benchmark]
    public TextSpan UnseekableAlternative() => Parse(_unseekable, _unseekableContext);

    [BenchmarkCategory("PARLOT102"), Benchmark]
    public TextSpan SeekableAlternative() => Parse(_seekable, _seekableContext);

    [BenchmarkCategory("PARLOT103"), Benchmark]
    public (TextSpan, char) SharedLeadingParser() => Parse(_sharedLeadingParser, _sharedLeadingParserContext);

    [BenchmarkCategory("PARLOT103"), Benchmark]
    public (TextSpan, char) FactoredLeadingParser() => Parse(_factored, _factoredContext);

    [BenchmarkCategory("PARLOT104"), Benchmark]
    public long LoopDetection() => Parse(_loopDetection, _loopDetectionContext);

    [BenchmarkCategory("PARLOT104"), Benchmark]
    public long LoopDetectionElided() => Parse(_loopDetectionElided, _loopDetectionElidedContext);

    // Parenthesized integers, the parser of the Deferred being wrapped
    private static Parser<long> Nested(Func<Parser<long>, Parser<long>> wrap)
    {
        var expression = Deferred<long>();
        expression.Parser = wrap(Between(Terms.Char('('), expression, Terms.Char(')')).Or(Terms.Integer()));
        return expression;
    }

    private static T Parse<T>(Parser<T> parser, ParseContext context)
    {
        var result = new ParseResult<T>();

        context.Scanner.Cursor.ResetPosition(TextPosition.Start);

        if (!parser.Parse(context, ref result))
        {
            return default;
        }

        return result.Value;
    }

    // A parser type the loop analysis doesn't know, which invokes another parser
    private sealed class Opaque<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public Opaque(Parser<T> parser)
        {
            _parser = parser;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result) => _parser.Parse(context, ref result);
    }
}
