using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures a choice whose alternatives are conversions of Terms parsers (Then, When, ThenTry), the shape of the tokens of
// most grammars: the choice skips the white space once, then invokes the conversion of the alternative.

[MemoryDiagnoser, ShortRunJob]
public class ConversionChoiceBenchmarks
{
    private readonly Parser<int> _token = OneOf(
        Terms.Text("let").Then(1),
        Terms.Integer().Then(static x => (int)x),
        Terms.String().Then(static s => s.Length),
        Terms.Identifier().When(static (_, x) => x.Length < 32).Then(static x => x.Length),
        Terms.Char(';').ThenTry(static c => (true, 0)));

    private Parser<int> _tokens = null!;

    private ParseContext _context = null!;

    [GlobalSetup]
    public void Setup()
    {
        _tokens = ZeroOrMany(_token).Then(static list => list.Count);
        var text = string.Concat(System.Linq.Enumerable.Repeat("let foo 'bar' 42 ; ", 100));
        _context = new ParseContext(new Scanner(text));

        if (Tokens() != 500)
        {
            throw new InvalidOperationException("Incorrect parsing.");
        }
    }

    [Benchmark]
    public int Tokens()
    {
        var result = new ParseResult<int>();
        _context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _tokens.Parse(_context, ref result);
        return result.Value;
    }
}
