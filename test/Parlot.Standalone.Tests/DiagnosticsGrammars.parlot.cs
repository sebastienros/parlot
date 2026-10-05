using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class DiagnosticsGrammars
{
    [GenerateParser(nameof(Choice), Diagnostics = true)]
    public static Parser<char> BuildChoice() => Literals.Char('a').Named("a").SkipAnd(Literals.Char('b').Named("b")).Named("ab")
        .Or(Literals.Char('a').Named("a").SkipAnd(Literals.Char('c').Named("c")).Named("ac"));

    [GenerateParser(nameof(Optional), Diagnostics = true)]
    public static Parser<char> BuildOptional() => Literals.Char('x').Named("x").Optional().SkipAnd(Literals.Char('a'));

    [GenerateParser(nameof(Throws), Diagnostics = true)]
    public static Parser<char> BuildThrows() => Literals.Char('a').Then<char>(static _ => throw new System.InvalidOperationException("callback"));

    [GenerateParser(nameof(Configured), Diagnostics = true)]
    public static Parser<long> BuildConfigured(int factor) => Terms.Integer().Then(value => value * factor);

    [GenerateParser(nameof(Packed), Diagnostics = true)]
    public static Parser<string> BuildPacked() => OneOf(Literals.Text("one"), Literals.Text("two"), Literals.Text("three"), Literals.Text("four"),
        Literals.Text("five"), Literals.Text("six"), Literals.Text("seven"), Literals.Text("eight"));

    [GenerateParser(nameof(Silent))]
    public static Parser<char> BuildSilent() => Literals.Char('a');
}
