#nullable enable

using System.Linq;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars selecting the ASCII white space set, and one with the default set. Each has a string and a TextReader entry
// point, compared with the runtime parser in WhiteSpaceSetTests.
public static partial class WhiteSpaceSetGrammars
{
    // The default set, the Unicode White_Space chars
    [GenerateParser(nameof(TryParseDefaultSeparated))]
    private static Parser<string> BuildDefaultSeparated() =>
        Separated(Terms.Char(','), Terms.Integer()).Then(static list => string.Join(",", list));

    [GenerateParser(nameof(TryParseSeparated))]
    private static Parser<string> BuildSeparated() =>
        Separated(Terms.Char(','), Terms.Integer()).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)
            .Then(static list => string.Join(",", list));

    // OneOf skips the white space once for the alternatives
    [GenerateParser(nameof(TryParseChoice))]
    private static Parser<string> BuildChoice() =>
        ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text(" "))).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)
            .Then(static list => string.Join("|", list));

    [GenerateParser(nameof(TryParseWhiteSpace))]
    private static Parser<string> BuildWhiteSpace() =>
        Literals.WhiteSpace(includeNewLines: true).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseNonWhiteSpace))]
    private static Parser<string> BuildNonWhiteSpace() =>
        Literals.NonWhiteSpace(includeNewLines: false).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static span => span.ToString());

    [GenerateParser(nameof(TryParseComments))]
    private static Parser<string> BuildComments() =>
        ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii)
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

    // The default set selected back inside the ASCII one
    [GenerateParser(nameof(TryParseNested))]
    private static Parser<long> BuildNested() =>
        Terms.Char('[').SkipAnd(Terms.Integer().WithWhiteSpaceSet(WhiteSpaceSet.Default)).AndSkip(Terms.Char(']'))
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

    // A callback with offsets skips the white space itself
    [GenerateParser(nameof(TryParseOffsets))]
    private static Parser<(int, int)> BuildOffsets() =>
        Terms.Identifier().Then(static (context, start, end, value) => (start, end)).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
}
