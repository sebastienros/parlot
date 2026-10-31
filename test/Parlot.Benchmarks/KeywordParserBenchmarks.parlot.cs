using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class KeywordParserBenchmarks
{
    [GenerateParser(nameof(TryParseSmall))]
    private static Parser<string> BuildSmall() =>
        OneOf(KeywordLookupBenchmarks.Words("Small").Select(static word => Literals.Keyword(word)).ToArray());

    [GenerateParser(nameof(TryParseMedium))]
    private static Parser<string> BuildMedium() =>
        OneOf(KeywordLookupBenchmarks.Words("Medium").Select(static word => Literals.Keyword(word)).ToArray());

    [GenerateParser(nameof(TryParseLanguage))]
    private static Parser<string> BuildLanguage() =>
        OneOf(KeywordLookupBenchmarks.Words("Language").Select(static word => Literals.Keyword(word)).ToArray());

    [GenerateParser(nameof(TryParseSharedPrefix))]
    private static Parser<string> BuildSharedPrefix() =>
        OneOf(KeywordLookupBenchmarks.Words("SharedPrefix").Select(static word => Literals.Keyword(word)).ToArray());

    [GenerateParser(nameof(TryParseSmallBaseline))]
    private static Parser<string> BuildSmallBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("Small").Select(static word => (Parser<string>)new BaselineKeyword(word)).ToArray());

    [GenerateParser(nameof(TryParseMediumBaseline))]
    private static Parser<string> BuildMediumBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("Medium").Select(static word => (Parser<string>)new BaselineKeyword(word)).ToArray());

    [GenerateParser(nameof(TryParseLanguageBaseline))]
    private static Parser<string> BuildLanguageBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("Language").Select(static word => (Parser<string>)new BaselineKeyword(word)).ToArray());

    [GenerateParser(nameof(TryParseSharedPrefixBaseline))]
    private static Parser<string> BuildSharedPrefixBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("SharedPrefix").Select(static word => (Parser<string>)new BaselineKeyword(word)).ToArray());

    [GenerateParser(nameof(TryParseSmallWrapped))]
    private static Parser<string> BuildSmallWrapped() =>
        OneOf(KeywordLookupBenchmarks.Words("Small").Select(static word => Literals.Keyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseMediumWrapped))]
    private static Parser<string> BuildMediumWrapped() =>
        OneOf(KeywordLookupBenchmarks.Words("Medium").Select(static word => Literals.Keyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseLanguageWrapped))]
    private static Parser<string> BuildLanguageWrapped() =>
        OneOf(KeywordLookupBenchmarks.Words("Language").Select(static word => Literals.Keyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseSharedPrefixWrapped))]
    private static Parser<string> BuildSharedPrefixWrapped() =>
        OneOf(KeywordLookupBenchmarks.Words("SharedPrefix").Select(static word => Literals.Keyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseSmallWrappedBaseline))]
    private static Parser<string> BuildSmallWrappedBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("Small").Select(static word => new BaselineKeyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseMediumWrappedBaseline))]
    private static Parser<string> BuildMediumWrappedBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("Medium").Select(static word => new BaselineKeyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseLanguageWrappedBaseline))]
    private static Parser<string> BuildLanguageWrappedBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("Language").Select(static word => new BaselineKeyword(word).Then(word)).ToArray());

    [GenerateParser(nameof(TryParseSharedPrefixWrappedBaseline))]
    private static Parser<string> BuildSharedPrefixWrappedBaseline() =>
        OneOf(KeywordLookupBenchmarks.Words("SharedPrefix").Select(static word => new BaselineKeyword(word).Then(word)).ToArray());
}
