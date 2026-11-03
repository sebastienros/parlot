using System;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class KeywordRangesBenchmarks
{
    private static readonly (char First, char Last)[] WordPart =
        [('a', 'z'), ('A', 'Z'), ('0', '9'), ('_', '_'), ('$', '$'), ('\u0080', '￾')];

    [GenerateParser(nameof(TryParseRanges))]
    private static Parser<string> BuildRanges() => OneOf(
        Terms.Keyword("from", WordPart, caseInsensitive: true), Terms.Keyword("select", WordPart, caseInsensitive: true),
        Terms.Keyword("where", WordPart, caseInsensitive: true), Terms.Keyword("in", WordPart, caseInsensitive: true),
        Terms.Keyword("inner", WordPart, caseInsensitive: true), Terms.Keyword("join", WordPart, caseInsensitive: true),
        Terms.Keyword("or", WordPart, caseInsensitive: true), Terms.Keyword("and", WordPart, caseInsensitive: true),
        Terms.Keyword("group", WordPart, caseInsensitive: true), Terms.Keyword("by", WordPart, caseInsensitive: true),
        Terms.Keyword("having", WordPart, caseInsensitive: true), Terms.Keyword("left", WordPart, caseInsensitive: true),
        Terms.Keyword("order", WordPart, caseInsensitive: true));

    [GenerateParser(nameof(TryParseScans))]
    private static Parser<int> BuildScans() => OneOf(
        Terms.Scan(static x => ScanKeyword(x, "from"), "fF"), Terms.Scan(static x => ScanKeyword(x, "select"), "sS"),
        Terms.Scan(static x => ScanKeyword(x, "where"), "wW"), Terms.Scan(static x => ScanKeyword(x, "in"), "iI"),
        Terms.Scan(static x => ScanKeyword(x, "inner"), "iI"), Terms.Scan(static x => ScanKeyword(x, "join"), "jJ"),
        Terms.Scan(static x => ScanKeyword(x, "or"), "oO"), Terms.Scan(static x => ScanKeyword(x, "and"), "aA"),
        Terms.Scan(static x => ScanKeyword(x, "group"), "gG"), Terms.Scan(static x => ScanKeyword(x, "by"), "bB"),
        Terms.Scan(static x => ScanKeyword(x, "having"), "hH"), Terms.Scan(static x => ScanKeyword(x, "left"), "lL"),
        Terms.Scan(static x => ScanKeyword(x, "order"), "oO")).Then(static span => span.Length);
}
