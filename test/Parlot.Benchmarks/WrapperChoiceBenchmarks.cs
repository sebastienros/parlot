using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Apple M4 Pro, .NET 10, two rounds each, before and after the wrappers told the choice their chars:
// | Method    | Before   | After    | Allocated |
// |---------- |---------:|---------:|----------:|
// | Operators | 65.88 ns | 12.27 ns |         - |
// | Operators | 66.58 ns | 12.06 ns |         - |
// | Sequences | 49.02 ns | 11.26 ns |         - |
// | Sequences | 49.43 ns | 11.15 ns |         - |

// Measures a choice of 8 alternatives that wrap the parser they start with, the last one matching after a space:
// - Operators: Unary, LeftAssociative and RightAssociative parsers,
// - Sequences: sequences built before the Deferred parser they start with is set, like the statements of a grammar which
//   reference its expression.
// The wrappers tell the choice the chars they start with, so it builds a lookup table, skips the white space once and only
// tries the last alternative. The contexts are reused and the cursor moved back before each parse, such that the allocations
// of a parse are not part of the result.

[MemoryDiagnoser, ShortRunJob]
public class WrapperChoiceBenchmarks
{
    private const int Count = 8;

    private Parser<string> _operators;
    private Parser<string> _sequences;
    private ParseContext _operatorsContext;
    private ParseContext _sequencesContext;

    [GlobalSetup]
    public void Setup()
    {
        var operators = new Parser<string>[Count];
        var sequences = new Parser<string>[Count];
        var expressions = new Deferred<string>[Count];

        for (var i = 0; i < Count; i++)
        {
            var operand = Terms.Text(((char)('a' + i)).ToString());

            operators[i] = (i % 3) switch
            {
                0 => operand.Unary((Terms.Char('-'), static x => x)),
                1 => operand.LeftAssociative((Terms.Char('+'), static (x, y) => x + y)),
                _ => operand.RightAssociative((Terms.Char('^'), static (x, y) => x + y)),
            };

            expressions[i] = Deferred<string>();
            sequences[i] = expressions[i].AndSkip(Terms.Char(';'));
        }

        _operators = OneOf(operators);
        _sequences = OneOf(sequences);

        for (var i = 0; i < Count; i++)
        {
            expressions[i].Parser = Terms.Text(((char)('a' + i)).ToString());
        }

        var last = ((char)('a' + Count - 1)).ToString();

        _operatorsContext = new ParseContext(new Scanner(" " + last));
        _sequencesContext = new ParseContext(new Scanner(" " + last + ";"));

        if (Operators() != last || Sequences() != last)
        {
            throw new InvalidOperationException("Incorrect OneOf parsing.");
        }
    }

    [Benchmark]
    public string Operators()
    {
        var result = new ParseResult<string>();

        _operatorsContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _operators.Parse(_operatorsContext, ref result);

        return result.Value;
    }

    [Benchmark]
    public string Sequences()
    {
        var result = new ParseResult<string>();

        _sequencesContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _sequences.Parse(_sequencesContext, ref result);

        return result.Value;
    }
}
