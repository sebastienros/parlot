#nullable enable

using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars setting a state with WithState for the duration of a parser, held by a configuration parameter. Compared with
// the runtime parsers in StateTests.
[IncludeUsings("Parlot.SourceGenerator.Tests")]
public static partial class StateGrammars
{
    // "yield" is a keyword in a generator body, "*{...}", and an identifier elsewhere
    [GenerateParser(nameof(TryParseGenerator))]
    private static Parser<string> BuildGenerator(StateOptions options) =>
        OneOf(
            Between(
                Terms.Text("*{"),
                Separated(
                    Terms.Char(','),
                    If(
                        () => options.Generator,
                        OneOf(Terms.Text("yield").Then(static _ => "keyword"), Terms.Identifier().Then(static x => "identifier:" + x.ToString())),
                        Terms.Identifier().Then(static x => "identifier:" + x.ToString())))
                .WithState(() => options.Generator, value => options.Generator = value, static _ => true),
                Terms.Char('}')),
            Separated(
                Terms.Char(','),
                If(
                    () => options.Generator,
                    Terms.Text("yield").Then(static _ => "keyword"),
                    Terms.Identifier().Then(static x => "identifier:" + x.ToString()))))
        .Then(static list => string.Join("|", list))
        .Eof();

    // The depth of the parentheses around 'x', and an error for a missing ')'
    [GenerateParser(nameof(TryParseDepth))]
    private static Parser<int> BuildDepth(StateOptions options) =>
        Recursive<int>(self => OneOf(
            Terms.Char('(').SkipAnd(self).AndSkip(Terms.Char(')').ElseError("Expected ')'"))
                .WithState(() => options.Depth, value => options.Depth = value, static depth => depth + 1),
            Terms.Char('x').Then(_ => options.Depth)))
        .Eof();

    // The state of a parser whose value isn't used, read from the context
    [GenerateParser(nameof(TryParseDiscarded))]
    private static Parser<char> BuildDiscarded() =>
        Terms.Char('a')
            .AndSkip(Terms.Text("b")
                .When(static (_, _) => StaticState.Value == 1)
                .WithState<ParseContext, int>(static _ => StaticState.Value, static (_, value) => StaticState.Value = value, static x => x + 1))
            .Eof();
}
