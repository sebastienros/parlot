#nullable enable

using System.Linq;
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

    // HQL's words: letters, digits, '_', '$' and every char above ASCII
    private static readonly string WordPart =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_$"
        + new string(Enumerable.Range(0x80, 0xFFFE - 0x80 + 1).Select(static i => (char)i).ToArray());

    [GenerateParser(nameof(TryParseRanges))]
    private static Parser<string> BuildRanges() =>
        Terms.Keyword("from", WordPart, caseInsensitive: true);

    [GenerateParser(nameof(TryParseRangesMatched))]
    private static Parser<string> BuildRangesMatched() =>
        Literals.Keyword("from", WordPart, caseInsensitive: true, returnMatchedText: true);

    // The same ranges on all, so this is a keyword lookup of the identifier chars
    [GenerateParser(nameof(TryParseRangesChoice))]
    private static Parser<string> BuildRangesChoice() =>
        OneOf(
            Terms.Keyword("from", WordPart, caseInsensitive: true), Terms.Keyword("select", WordPart, caseInsensitive: true),
            Terms.Keyword("where", WordPart, caseInsensitive: true), Terms.Keyword("in", WordPart, caseInsensitive: true),
            Terms.Keyword("inner", WordPart, caseInsensitive: true), Terms.Keyword("join", WordPart, caseInsensitive: true),
            Terms.Keyword("or", WordPart, caseInsensitive: true), Terms.Keyword("order", WordPart, caseInsensitive: true));
}
