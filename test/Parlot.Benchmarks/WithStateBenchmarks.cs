using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

/// <summary>
/// Nested parentheses which clear a flag that an identifier reads, like the brackets of a JavaScript expression resetting
/// the 'in' restriction of a for head: without a scope (the flag is clear from the start), with a custom parser saving and restoring the flag, and with WithState.
/// </summary>
[MemoryDiagnoser]
public partial class WithStateBenchmarks
{
    private const int NoIn = 1;

    private Parser<int> _none = null!;
    private Parser<int> _custom = null!;
    private Parser<int> _withState = null!;
    private string _input = null!;
    private int _expected;

    [GlobalSetup]
    public void Setup()
    {
        _none = Create(static p => p);
        _custom = Create(static p => new CustomScope<int>(p));
        _withState = Create(static p => p.WithState<FlagsContext, int>(static c => c.Flags, static (c, f) => c.Flags = f, static f => f & ~NoIn));

        // Three levels of 4 parentheses around 64 leaves, each with one "in"
        var leaf = "(a, in, (b, c), d)";
        var middle = "(" + string.Join(", ", Enumerable.Repeat(leaf, 4)) + ")";
        var outer = "(" + string.Join(", ", Enumerable.Repeat(middle, 4)) + ")";
        _input = "(" + string.Join(", ", Enumerable.Repeat(outer, 4)) + ")";
        _expected = 4 * 4 * 4;

        if (None() != _expected || Custom() != _expected || WithState() != _expected || Generated() != _expected)
        {
            throw new InvalidOperationException("WithState benchmark returned an unexpected result.");
        }
    }

    [Benchmark(Baseline = true)]
    public int None() => Parse(_none, flags: 0);

    [Benchmark]
    public int Custom() => Parse(_custom, NoIn);

    [Benchmark]
    public int WithState() => Parse(_withState, NoIn);

    [Benchmark]
    public int Generated() => TryParseGenerated(_input, new GeneratedFlags { Flags = NoIn }, out var value) ? value : -1;

    // The baseline starts without the flag, which the other parsers clear in each parenthesis
    private int Parse(Parser<int> parser, int flags) => parser.TryParse(new FlagsContext(_input) { Flags = flags }, out var value, out _) ? value : -1;

    // The number of 'in' identifiers, which are only identifiers when the NoIn flag is clear
    private static Parser<int> Create(Func<Parser<int>, Parser<int>> scope)
    {
        var identifier = If<FlagsContext, int>(
            static c => (c.Flags & NoIn) == 0,
            Terms.Identifier().Then(static x => x.Span.SequenceEqual("in".AsSpan()) ? 1 : 0),
            Terms.Identifier().When(static (_, x) => !x.Span.SequenceEqual("in".AsSpan())).Then(static _ => 0));

        return Recursive<int>(self => OneOf(
            scope(Between(Terms.Char('('), Separated(Terms.Char(','), self), Terms.Char(')')).Then(static list => list.Sum())),
            identifier))
            .Eof();
    }

    public sealed class FlagsContext : ParseContext
    {
        public FlagsContext(string text) : base(new Scanner(text))
        {
        }

        public int Flags { get; set; }
    }

    public sealed class GeneratedFlags
    {
        public int Flags { get; set; }
    }

    public static partial bool TryParseGenerated(string text, GeneratedFlags flags, out int value);

    // The workaround of a grammar without WithState: a custom parser clearing the flag, without try/finally
    private sealed class CustomScope<T> : Parser<T>, ISeekable
    {
        private readonly Parser<T> _parser;

        public CustomScope(Parser<T> parser)
        {
            _parser = parser;
        }

        public bool CanSeek => _parser is ISeekable { CanSeek: true };

        public char[] ExpectedChars => ((ISeekable)_parser).ExpectedChars;

        public bool SkipWhitespace => ((ISeekable)_parser).SkipWhitespace;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var flagsContext = (FlagsContext)context;
            var saved = flagsContext.Flags;
            flagsContext.Flags = saved & ~NoIn;
            var success = _parser.Parse(context, ref result);
            flagsContext.Flags = saved;

            context.ExitParser(this);
            return success;
        }
    }
}
