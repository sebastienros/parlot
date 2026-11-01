#nullable enable

using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Pooled strings and pooled matched texts. Each has a string and a TextReader entry point, compared with the runtime
// parser in PooledStringTests.
public static partial class PooledStringGrammars
{
    [GenerateParser(nameof(TryParseIdentifiers))]
    private static Parser<IReadOnlyList<string>> BuildIdentifiers() =>
        ZeroOrMany(Terms.Identifier().ToPooledString()).Eof();

    [GenerateParser(nameof(TryParseStrings))]
    private static Parser<IReadOnlyList<string>> BuildStrings() =>
        ZeroOrMany(Terms.String().ToPooledString()).Eof();

    // The pooled string of a sequence which only reads the text is generated as span code
    [GenerateParser(nameof(TryParseSpans))]
    private static Parser<IReadOnlyList<string>> BuildSpans() =>
        ZeroOrMany(Capture(Literals.Char('$').And(Literals.Identifier())).ToPooledString().AndSkip(Literals.Char(';'))).Eof();

    [GenerateParser(nameof(TryParseMatchedTexts))]
    private static Parser<IReadOnlyList<string>> BuildMatchedTexts() =>
        ZeroOrMany(OneOf(
            Terms.Text("select", caseInsensitive: true, returnMatchedText: true),
            Terms.Keyword("from", caseInsensitive: true, returnMatchedText: true))).Eof();
}
