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

    [GenerateParser(nameof(TryParseBoundedZero))]
    private static Parser<int> BuildBoundedZero() =>
        ZeroOrMany(Literals.Char('x'), max: 2).And(Literals.Char('x'))
            .Then(static values => values.Item1.Count).Eof();

    [GenerateParser(nameof(TryParseBoundedOne))]
    private static Parser<int> BuildBoundedOne() =>
        OneOrMany(Literals.Char('x'), max: 2).And(Literals.Char('x'))
            .Then(static values => values.Item1.Count).Eof();

    [GenerateParser(nameof(TryParseBoundedSeparated))]
    private static Parser<int> BuildBoundedSeparated() =>
        Separated(Literals.Char(','), Literals.Char('x'), min: 2, max: 3)
            .AndSkip(Literals.Char(',')).And(Literals.Char('x'))
            .Then(static values => values.Item1.Count).Eof();

    [GenerateParser(nameof(TryParseOptionalSeparated))]
    private static Parser<IReadOnlyList<char>> BuildOptionalSeparated() =>
        Separated(Literals.Char(','), Literals.Char('x'), min: 0, max: 2).Eof();

    [GenerateParser(nameof(TryParseCapturedBoundedZero))]
    private static Parser<string> BuildCapturedBoundedZero() =>
        Capture(ZeroOrMany(Literals.Char('x'), max: 2))
            .And(Literals.Char('x')).Then(static values => values.Item1.ToString()).Eof();

    [GenerateParser(nameof(TryParseCapturedBoundedOne))]
    private static Parser<string> BuildCapturedBoundedOne() =>
        Capture(OneOrMany(Literals.Char('x'), max: 2))
            .And(Literals.Char('x')).Then(static values => values.Item1.ToString()).Eof();

    [GenerateParser(nameof(TryParseCapturedBoundedSeparated))]
    private static Parser<string> BuildCapturedBoundedSeparated() =>
        Capture(Separated(Literals.Char(','), Literals.Char('x'), min: 2, max: 3))
            .AndSkip(Literals.Char(',')).And(Literals.Char('x'))
            .Then(static values => values.Item1.ToString()).Eof();

    [GenerateParser(nameof(TryParseSeparatedMinimumFallback))]
    private static Parser<int> BuildSeparatedMinimumFallback() =>
        Separated(Literals.Char(','), Literals.Char('x'), min: 3)
            .Or(OneOrMany(Literals.Char('x')))
            .AndSkip(Literals.Char(','))
            .Then(static values => values.Count);

    [GenerateParser(nameof(TryParseOptionalSeparatedChoice))]
    private static Parser<char> BuildOptionalSeparatedChoice() =>
        OneOf(Separated(Literals.Char(','), Literals.Char('x'), min: 0).Then('x'), Literals.Char('y'));
}
