using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using System.Linq;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// Measures a choice of operators, longest first, each returning its index as a binary expression grammar would.
// The '<' and '>' operators share their first chars, so the choice dispatches them on their prefixes.

[MemoryDiagnoser, ShortRunJob]
public class OperatorChoiceBenchmarks
{
    private static readonly string[] Operators = ["<<=", ">>=", "<=", ">=", "<>", "==", "!=", "&&", "||", "<<", ">>", "+=", "-=",
        "*=", "/=", "<", ">", "=", "!", "+", "-", "*", "/", "%", "&", "|", "^"];

    private Parser<int> _parser;
    private string[] _inputs;

    [GlobalSetup]
    public void Setup()
    {
        _parser = OneOf(Operators.Select(static (op, index) => Terms.Text(op).Then(index)).ToArray());

        // The comparison and shift operators are the ones whose first chars select several alternatives
        string[] compared = ["<<=", ">>=", "<=", ">=", "<>", "<<", ">>", "<", ">"];
        _inputs = Enumerable.Range(0, 64).Select(index => (index % 2 == 0 ? compared[index / 2 % compared.Length] : Operators[index % Operators.Length]) + " 1").ToArray();

        for (var i = 0; i < _inputs.Length; i++)
        {
            if (!_parser.TryParse(_inputs[i], out var index) || !_inputs[i].StartsWith(Operators[index] + " ", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Incorrect operator parsing of '{_inputs[i]}'.");
            }
        }
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int RuntimeOneOf()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _parser.TryParse(input, out var index) ? index : 0;
        }

        return sum;
    }
}
