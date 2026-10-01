using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class TextParserBenchmarks
{
    [GenerateParser(nameof(TryParseLanguage))]
    private static Parser<string> BuildLanguage() => Build("Language", baseline: false);

    [GenerateParser(nameof(TryParseHeaders))]
    private static Parser<string> BuildHeaders() => Build("Headers", baseline: false);

    [GenerateParser(nameof(TryParseSharedPrefix))]
    private static Parser<string> BuildSharedPrefix() => Build("SharedPrefix", baseline: false);

    [GenerateParser(nameof(TryParseLanguageBaseline))]
    private static Parser<string> BuildLanguageBaseline() => Build("Language", baseline: true);

    [GenerateParser(nameof(TryParseHeadersBaseline))]
    private static Parser<string> BuildHeadersBaseline() => Build("Headers", baseline: true);

    [GenerateParser(nameof(TryParseSharedPrefixBaseline))]
    private static Parser<string> BuildSharedPrefixBaseline() => Build("SharedPrefix", baseline: true);

    private static Parser<string> Build(string vocabulary, bool baseline) =>
        OneOf(KeywordLookupBenchmarks.Words(vocabulary).Select(word => baseline
            ? (Parser<string>)new BaselineText(word) : Literals.Text(word)).ToArray());
}
