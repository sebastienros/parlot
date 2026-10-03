using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class DiscardGrammars
{
    private static Parser<string> MatchedText(string text) =>
        Literals.Text(text, caseInsensitive: true, returnMatchedText: true);

    [GenerateParser(nameof(TryParseSkipFirst))]
    private static Parser<string> BuildSkipFirst()
    {
        var text = MatchedText("keyword");
        return text.SkipAnd(text).AndSkip(Literals.Char('!')).Eof();
    }

    [GenerateParser(nameof(TryParseRetainFirst))]
    private static Parser<string> BuildRetainFirst()
    {
        var text = MatchedText("keyword");
        return text.AndSkip(text).AndSkip(Literals.Char('!')).Eof();
    }

    [GenerateParser(nameof(TryParseBetween))]
    private static Parser<string> BuildBetween() =>
        Between(MatchedText("keyword"), MatchedText("value"), MatchedText("end")).Eof();

    [GenerateParser(nameof(TryParseFallback))]
    private static Parser<string> BuildFallback() =>
        Between(MatchedText("keyword"), MatchedText("value"), MatchedText("end"))
            .Or(Literals.Pattern(static _ => true).Then(static span => span.ToString())).Eof();

    [GenerateParser(nameof(TryParseSeparated))]
    private static Parser<int> BuildSeparated() =>
        Separated(MatchedText("keyword"), MatchedText("value")).Then(static values => values.Count).Eof();

    [GenerateParser(nameof(TryParseSeparatedOptions))]
    private static Parser<int> BuildSeparatedOptions() =>
        Separated(MatchedText("keyword"), MatchedText("value"),
            removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true)
            .Then(static values => values.Count).Eof();

    [GenerateParser(nameof(TryParseLookahead))]
    private static Parser<string> BuildLookahead() =>
        MatchedText("value").WhenFollowedBy(MatchedText("keyword")).AndSkip(MatchedText("keyword")).Eof();

    [GenerateParser(nameof(TryParseNegativeLookahead))]
    private static Parser<string> BuildNegativeLookahead() =>
        MatchedText("value").WhenNotFollowedBy(MatchedText("keyword")).Eof();

    [GenerateParser(nameof(TryParseNot))]
    private static Parser<string> BuildNot() => Not(MatchedText("keyword")).SkipAnd(MatchedText("value")).Eof();

    [GenerateParser(nameof(TryParseTextBefore))]
    private static Parser<string> BuildTextBefore() =>
        AnyCharBefore(MatchedText("keyword"), consumeDelimiter: true).Then(static span => span.ToString()).Eof();

    [GenerateParser(nameof(TryParseCapture))]
    private static Parser<string> BuildCapture() =>
        Capture(MatchedText("keyword")).Then(static span => span.ToString()).Eof();

    [GenerateParser(nameof(TryParsePartialTuple))]
    private static Parser<(string, string)> BuildPartialTuple() =>
        MatchedText("value").And(MatchedText("keyword")).SkipAnd(MatchedText("value")).Eof();

    [GenerateParser(nameof(TryParseDeferred))]
    private static Parser<string> BuildDeferred()
    {
        var text = Deferred<string>();
        text.Parser = MatchedText("keyword").Or(Literals.Char('(').SkipAnd(text).AndSkip(Literals.Char(')')));
        return text.SkipAnd(text).Eof();
    }

    [GenerateParser(nameof(TryParseCallback))]
    private static Parser<string> BuildCallback(StringCallbackState state) =>
        MatchedText("keyword").Then(value => state.Record(value)).SkipAnd(MatchedText("value")).Eof();

    [GenerateParser(nameof(TryParseSkippedNumber))]
    private static Parser<string> BuildSkippedNumber() =>
        Literals.Number<byte>(NumberOptions.Integer).SkipAnd(Literals.Text("!")).Eof();
}
