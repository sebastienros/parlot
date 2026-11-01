using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using System;

namespace Parlot.Benchmarks;

// Parses expressions whose white space is skipped by the parser of WithComments, which is invoked by each Terms parser.
// One text has only spaces, the other one comments too.

[MemoryDiagnoser, ShortRunJob]
public class CommentsBenchmarks
{
    private const string Spaces = "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private const string Comments = "1 - /* a */ ( 3 + 2.5 ) * 4 // b\n - 1 / 2 + 1 - ( 3 /* c */ + 2.5 ) * 4 - 1 / 2 // d\n + 1 - ( 3 + 2.5 ) * 4 - 1 / 2";

    private static readonly Parser<Tests.Calc.Expression> _parser = FluentParser.Expression.WithComments(static comments =>
    {
        comments.WithWhiteSpaceOrNewLine();
        comments.WithSingleLine("//");
        comments.WithMultiLine("/*", "*/");
    });

    [GlobalSetup]
    public void Setup()
    {
        var expected = FluentParser.Expression.Parse(Spaces)!.Evaluate();

        if (Expression_Spaces().Evaluate() != expected || Expression_Comments().Evaluate() != expected)
        {
            throw new InvalidOperationException("Incorrect expression parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public Tests.Calc.Expression Expression_Spaces() => _parser.Parse(new ParseContext(new Scanner(Spaces)))!;

    [Benchmark]
    public Tests.Calc.Expression Expression_Comments() => _parser.Parse(new ParseContext(new Scanner(Comments)))!;
}
