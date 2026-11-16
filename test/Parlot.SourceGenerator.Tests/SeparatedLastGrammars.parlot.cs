#nullable enable

using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated copies of the runtime grammars of SeparatedLastParityTests: lists which end with a last element that no
// separator follows (#588). Each value is followed by where the cursor is. Each has a string and a TextReader entry point
// returning the furthest failure.
public static partial class SeparatedLastGrammars
{
    private static readonly Parser<string> Item = Terms.Identifier().Then(static x => x.ToString());
    private static readonly Parser<string> Rest = Terms.Text("...").SkipAnd(Terms.Identifier()).Then(static x => "..." + x.ToString());

    private static Parser<string> Join(this Parser<IReadOnlyList<string>> parser) =>
        parser.Then(static list => string.Join(",", list));

    private static Parser<string> Located(this Parser<string> parser) =>
        parser.Then(static (context, value) => $"{value}|{context.Scanner.Cursor.Offset}");

    [GenerateParser(nameof(TryParsePlain))]
    public static Parser<string> BuildPlain() => Separated(Terms.Char(','), Item, Rest).Join().Located();

    [GenerateParser(nameof(TryParseMin))]
    public static Parser<string> BuildMin() => Separated(Terms.Char(','), Item, Rest, min: 2).Join().Located();

    [GenerateParser(nameof(TryParseTrailing))]
    public static Parser<string> BuildTrailing() => Separated(Terms.Char(','), Item, Rest, allowTrailingSeparator: true).Join().Located();

    [GenerateParser(nameof(TryParseEmptyEntries))]
    public static Parser<string> BuildEmptyEntries() => Separated(Terms.Char(','), Item, Rest, removeEmptyEntries: true).Join().Located();

    [GenerateParser(nameof(TryParseAllOptions))]
    public static Parser<string> BuildAllOptions() => Separated(Terms.Char(','), Item, Rest, min: 1, allowTrailingSeparator: true, removeEmptyEntries: true).Join().Located();

    [GenerateParser(nameof(TryParseCaptured))]
    public static Parser<string> BuildCaptured() => Capture(Separated(Terms.Char(','), Item, Rest, min: 1)).Then(static x => x.ToString()).Located();

    [GenerateParser(nameof(TryParseFollowed))]
    public static Parser<string> BuildFollowed() => Separated(Terms.Char(','), Item, Rest).AndSkip(Terms.Char(',')).AndSkip(Terms.Char(')')).Join().Located();

    [GenerateParser(nameof(TryParseChoice))]
    public static Parser<string> BuildChoice() => OneOf(Separated(Terms.Char(','), Item, Rest, min: 1).Join(), Terms.Integer().Then(static x => x.ToString())).Located();
}
