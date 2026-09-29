using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class CollectionGrammars
{
    [GenerateParser(nameof(TryParseZeroStrings))]
    private static Parser<IReadOnlyList<string>> BuildZeroStrings() =>
        ZeroOrMany(Literals.Text("x")).Eof();

    [GenerateParser(nameof(TryParseOneStrings))]
    private static Parser<IReadOnlyList<string>> BuildOneStrings() =>
        OneOrMany(Literals.Text("x")).Eof();

    [GenerateParser(nameof(TryParseSeparatedStrings))]
    private static Parser<IReadOnlyList<string>> BuildSeparatedStrings() =>
        Separated(Literals.Char(','), Literals.Text("x")).Eof();

    [GenerateParser(nameof(TryParseSeparatedInterior))]
    private static Parser<IReadOnlyList<string>> BuildSeparatedInterior() =>
        Separated(Literals.Char(','), Literals.Text("x"), removeEmptyEntries: true).Eof();

    [GenerateParser(nameof(TryParseSeparatedLeading))]
    private static Parser<IReadOnlyList<string>> BuildSeparatedLeading() =>
        Separated(Literals.Char(','), Literals.Text("x"), allowLeadingSeparator: true).Eof();

    [GenerateParser(nameof(TryParseSeparatedTrailing))]
    private static Parser<IReadOnlyList<string>> BuildSeparatedTrailing() =>
        Separated(Literals.Char(','), Literals.Text("x"), allowTrailingSeparator: true).Eof();

    [GenerateParser(nameof(TryParseSeparatedAllOptions))]
    private static Parser<IReadOnlyList<string>> BuildSeparatedAllOptions() =>
        Separated(Literals.Char(','), Literals.Text("x"),
            removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true).Eof();

    [GenerateParser(nameof(TryParseSeparatedDiscarded))]
    private static Parser<char> BuildSeparatedDiscarded() =>
        Separated(Literals.Char(','), Literals.Text("x"),
            removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true)
            .SkipAnd(Literals.Char('?')).Eof();

    [GenerateParser(nameof(TryParseSeparatedUnmatchedRun))]
    private static Parser<int> BuildSeparatedUnmatchedRun() =>
        Separated(Literals.Char(','), Literals.Text("x"), removeEmptyEntries: true)
            .AndSkip(Literals.Text(",,?"))
            .Then(static values => values.Count).Eof();

    [GenerateParser(nameof(TryParseSeparatedLeadingFallback))]
    private static Parser<int> BuildSeparatedLeadingFallback() =>
        Separated(Literals.Char(','), Literals.Text("x"), allowLeadingSeparator: true)
            .Then(static values => values.Count)
            .Or(Literals.Char(',').Then(static _ => -1)).Eof();

    [GenerateParser(nameof(TryParseSeparatedOptionalOptions))]
    private static Parser<IReadOnlyList<char>> BuildSeparatedOptionalOptions() =>
        Separated(ZeroOrOne(Literals.Char(',')), ZeroOrOne(Literals.Char('a')),
            removeEmptyEntries: true, allowLeadingSeparator: true).Eof();

    [GenerateParser(nameof(TryParseTuples))]
    private static Parser<IReadOnlyList<(int Number, string Text)>> BuildTuples() =>
        ZeroOrMany(Literals.Text("x").Then(static text => (1, text))).Eof();

    [GenerateParser(nameof(TryParseSeparatedThenQuestion))]
    private static Parser<int> BuildSeparatedThenQuestion() =>
        Separated(Literals.Char(','), Literals.Text("x"))
            .AndSkip(Literals.Text(",?"))
            .Then(static values => values.Count)
            .Eof();

    [GenerateParser(nameof(TryParseCapturedZero))]
    private static Parser<string> BuildCapturedZero() =>
        Capture(ZeroOrMany(Literals.Text("x"))).Then(static span => span.ToString()).Eof();
}
