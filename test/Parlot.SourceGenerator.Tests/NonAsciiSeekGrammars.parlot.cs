#nullable enable

using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Choices with an alternative that can start with many non-ASCII chars, declared as any non-ASCII char. Compared with the
// runtime parsers in NonAsciiSeekTests.
public static partial class NonAsciiSeekGrammars
{
    // ASCII letters, and the Greek and Cyrillic ones
    private static readonly string Letters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"
        + new string(Enumerable.Range(0x370, 0x500 - 0x370).Select(static i => (char)i).Where(char.IsLetter).ToArray());

    private static Parser<string> Tokens() =>
        OneOf(
            Terms.Text("if"),
            Terms.Char('(').Then(static _ => "("),
            Terms.Text("ω!").Then(static _ => "omega!"),
            Terms.AnyOf(Letters).Then(static x => x.ToString()));

    private static Parser<string> WithOther() =>
        OneOf(
            Literals.Text("if"),
            Literals.Pattern(static c => c == '#' || c == 'ж', maxSize: 1).Then(static x => "other:" + x.ToString()),
            Literals.AnyOf(Letters).Then(static x => x.ToString()),
            Literals.Text("€"));

    [GenerateParser(nameof(TryParseTokens))]
    private static Parser<System.Collections.Generic.IReadOnlyList<string>> BuildTokens() => ZeroOrMany(Tokens()).Eof();

    [GenerateParser(nameof(TryParseWithOther))]
    private static Parser<string> BuildWithOther() => WithOther();
}
