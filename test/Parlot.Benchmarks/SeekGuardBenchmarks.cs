using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures optional parsers and loops whose parser is a sequence that usually doesn't match, like the tails of the
// expressions of a JavaScript grammar:
// - Optional: 200 statements 'let a = b;' whose expression has an optional conditional tail '? x : y', present once in 8,
//   built like in the issue: '?' not followed by '?', '.', then two operands around ':'.
// - Workaround: the same grammar with the tail wrapped in a single-alternative choice, OneOf(tail).Optional(), which
//   dispatched on its first chars before the optional parsers did.
// - Loop: 200 blocks '{ let a = b; }', each a loop of statements that ends at the '}'.
// - SeparatedList: 200 lists 'f(a, b, c)' whose separator is ',' not followed by ','.
// - TailMiss, TailHit: the optional tail alone, before ' ;' where it can't start and before ' ? b : c' where it matches.
// - CharMiss, CharHit: an optional ',' before ' ;' and before ' ,': a single literal, which the guard doesn't save much.
// The parsers are invoked through Parse with a reused context, such that the allocations of a parse are not part of the result.

[MemoryDiagnoser, ShortRunJob]
public class SeekGuardBenchmarks
{
    private const int Count = 200;

    private Parser<int> _optional;
    private Parser<int> _workaround;
    private Parser<int> _zeroOrMany;
    private Parser<int> _separated;
    private ParseContext _statementsContext;
    private ParseContext _blocksContext;
    private ParseContext _callsContext;

    private Parser<Option<(string, string)>> _tail;
    private Parser<Option<char>> _char;
    private ParseContext _tailMissContext;
    private ParseContext _tailHitContext;
    private ParseContext _charMissContext;
    private ParseContext _charHitContext;

    [GlobalSetup]
    public void Setup()
    {
        _optional = CreateStatements(static tail => tail.Optional());
        _workaround = CreateStatements(static tail => OneOf(tail).Optional());

        var statement = Terms.Text("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char('=')).And(Terms.Identifier()).AndSkip(Terms.Char(';'));
        var block = Terms.Char('{').SkipAnd(ZeroOrMany(statement)).AndSkip(Terms.Char('}'));
        _zeroOrMany = ZeroOrMany(block).Then(static blocks => blocks.Sum(static b => b.Count));

        var separator = Terms.Char(',').WhenNotFollowedBy(Literals.Char(','));
        var call = Terms.Identifier().AndSkip(Terms.Char('(')).And(Separated(separator, Terms.Identifier())).AndSkip(Terms.Char(')'));
        _separated = ZeroOrMany(call).Then(static calls => calls.Sum(static c => c.Item2.Count));

        _statementsContext = new ParseContext(new Scanner(string.Concat(Enumerable.Range(0, Count).Select(static i => i % 8 == 0 ? "let a = b ? c : d;\n" : "let a = b;\n"))));
        _blocksContext = new ParseContext(new Scanner(string.Concat(Enumerable.Range(0, Count).Select(static _ => "{ let a = b; }\n"))));
        _callsContext = new ParseContext(new Scanner(string.Concat(Enumerable.Range(0, Count).Select(static _ => "f(a, b, c)\n"))));

        _tail = CreateTail(Terms.Identifier().Then(static x => x.ToString())).Optional();
        _char = Terms.Char(',').Optional();
        _tailMissContext = new ParseContext(new Scanner(" ;"));
        _tailHitContext = new ParseContext(new Scanner(" ? b : c"));
        _charMissContext = new ParseContext(new Scanner(" ;"));
        _charHitContext = new ParseContext(new Scanner(" ,"));

        if (TailMiss() || !TailHit() || CharMiss() || !CharHit())
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }

        if (Optional() != Count + Count / 8 || Workaround() != Count + Count / 8 || Loop() != Count || SeparatedList() != 3 * Count)
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }
    }

    private static Parser<int> CreateStatements(Func<Parser<(string, string)>, Parser<Option<(string, string)>>> optional)
    {
        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString());

        var tail = CreateTail(expression);

        expression.Parser = primary.And(optional(tail)).Then(static t => t.Item2.HasValue ? t.Item1 + "?" : t.Item1);

        var statement = Terms.Text("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char('=')).SkipAnd(expression).AndSkip(Terms.Char(';'));

        // The number of statements, and one more for each tail
        return ZeroOrMany(statement).Then(static statements =>
        {
            var count = 0;

            foreach (var s in statements)
            {
                count += s.EndsWith('?') ? 2 : 1;
            }

            return count;
        });
    }

    // Optional -> Sequence -> SequenceAndSkip -> SequenceSkipAnd -> WhenNotFollowedBy -> SkipWhiteSpace -> TextLiteral
    private static Parser<(string, string)> CreateTail(Parser<string> expression) =>
        Terms.Text("?").WhenNotFollowedBy(Literals.Char('?').Or(Literals.Char('.'))).SkipAnd(expression).AndSkip(Terms.Char(':')).And(expression);

    [Benchmark]
    public bool TailMiss() => Parse(_tail, _tailMissContext).HasValue;

    [Benchmark]
    public bool TailHit() => Parse(_tail, _tailHitContext).HasValue;

    [Benchmark]
    public bool CharMiss() => Parse(_char, _charMissContext).HasValue;

    [Benchmark]
    public bool CharHit() => Parse(_char, _charHitContext).HasValue;

    [Benchmark]
    public int Optional() => Parse(_optional, _statementsContext);

    [Benchmark]
    public int Workaround() => Parse(_workaround, _statementsContext);

    [Benchmark]
    public int Loop() => Parse(_zeroOrMany, _blocksContext);

    [Benchmark]
    public int SeparatedList() => Parse(_separated, _callsContext);

    private static T Parse<T>(Parser<T> parser, ParseContext context)
    {
        var result = new ParseResult<T>();

        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        parser.Parse(context, ref result);

        return result.Value;
    }
}
