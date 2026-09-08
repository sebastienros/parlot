using Parlot.Fluent;
using Parlot.SourceGenerator;

namespace Parlot.SourceGenerator.Tests;

public static partial class ExternalTypeGrammars
{
    [GenerateParser(nameof(TryParseSimpleValue))]
    [IncludeUsings("Parlot.SourceGenerator.Tests")]
    private static Parser<SimpleValue> BuildSimpleValue() =>
        Parsers.Terms.Identifier().Then(static value => new SimpleValue(value.ToString()));

    [GenerateParser(nameof(TryParseSimpleNumber))]
    [IncludeUsings("Parlot.SourceGenerator.Tests")]
    private static Parser<SimpleNumber> BuildSimpleNumber() =>
        Parsers.Terms.Decimal().Then(static value => new SimpleNumber(value));
}
