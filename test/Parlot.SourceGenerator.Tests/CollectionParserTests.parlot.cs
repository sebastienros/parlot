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
