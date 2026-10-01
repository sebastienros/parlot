using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class CollectionBenchmarks
{
    [GenerateParser(nameof(TryParseZero))]
    private static Parser<IReadOnlyList<(int, string)>> BuildZero() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser(nameof(TryParseOne))]
    private static Parser<IReadOnlyList<(int, string)>> BuildOne() =>
        OneOrMany(Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser(nameof(TryParseSeparated))]
    private static Parser<IReadOnlyList<(int, string)>> BuildSeparated() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)));

    [GenerateParser(nameof(TryParseBoundedZero))]
    private static Parser<IReadOnlyList<(int, string)>> BuildBoundedZero() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text)), max: 2);

    [GenerateParser(nameof(TryParseBoundedOne))]
    private static Parser<IReadOnlyList<(int, string)>> BuildBoundedOne() =>
        OneOrMany(Literals.Text("x").Then(static text => (1, text)), max: 2);

    [GenerateParser(nameof(TryParseBoundedSeparated))]
    private static Parser<IReadOnlyList<(int, string)>> BuildBoundedSeparated() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)), min: 1, max: 2);

    [GenerateParser(nameof(TryParseSeparatedOptions))]
    private static Parser<IReadOnlyList<(int, string)>> BuildSeparatedOptions() =>
        Separated(Literals.Char(','), Literals.Text("x").Then(static text => (1, text)),
            removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true);
}
