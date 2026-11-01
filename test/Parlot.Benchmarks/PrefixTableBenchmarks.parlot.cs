using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// The generated grammars of PrefixTableBenchmarks, built like PrefixTableGrammar
public partial class PrefixTableBenchmarks
{
    [GenerateParser(nameof(TryParseUnary))]
    private static Parser<long> BuildUnary() =>
        Terms.Integer().Unary(
            (Terms.Char('-'), static x => -x),
            (Terms.Char('~'), static x => ~x),
            (Terms.Char('!'), static x => x == 0 ? 1 : 0));

    [GenerateParser(nameof(TryParseTable))]
    private static Parser<long> BuildTable() =>
        Terms.Integer().Operators().Unary(
            (Terms.Char('-'), static x => -x),
            (Terms.Char('~'), static x => ~x),
            (Terms.Char('!'), static x => x == 0 ? 1 : 0));
}
