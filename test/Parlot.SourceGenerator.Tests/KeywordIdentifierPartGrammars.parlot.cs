#nullable enable

using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Keywords with custom identifier chars. Each has a string and a TextReader entry point, compared with the runtime
// parser in KeywordIdentifierPartTests.
public static partial class KeywordIdentifierPartGrammars
{
    private const string LiquidIdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

    [GenerateParser(nameof(TryParseLiquid))]
    private static Parser<string> BuildLiquid() =>
        Terms.Keyword("in", LiquidIdentifierPart);

    [GenerateParser(nameof(TryParseNonAscii))]
    private static Parser<string> BuildNonAscii() =>
        Literals.Keyword("in", "é-", caseInsensitive: true, returnMatchedText: true);

    // A choice of 8 keywords, large enough for the keyword lookup, which doesn't support custom identifier chars
    [GenerateParser(nameof(TryParseChoice))]
    private static Parser<string> BuildChoice() =>
        OneOf(
            Terms.Keyword("if"), Terms.Keyword("else"), Terms.Keyword("elsif"), Terms.Keyword("for"),
            Terms.Keyword("in", LiquidIdentifierPart), Terms.Keyword("and"), Terms.Keyword("or"), Terms.Keyword("with"));
}
