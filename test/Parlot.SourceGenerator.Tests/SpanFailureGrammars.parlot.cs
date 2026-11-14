#nullable enable

using System.Collections.Generic;
using System.IO;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars generated as span code, where a parser fails after reading some text and what encloses it goes on from before
// that text: the furthest failure is then the one of the runtime parsers (SpanFailureParityTests)
public static partial class SpanFailureGrammars
{
    // An alternative which fails after reading, before one which matches
    [GenerateParser(nameof(TryParseChoice))]
    public static Parser<string> BuildChoice() =>
        Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;")).AndSkip(Literals.Char('x')).Eof();

    // The same choice in a value which a condition rejects
    [GenerateParser(nameof(TryParseRejectedChoice))]
    public static Parser<string> BuildRejectedChoice() =>
        Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;"))
            .When(static (_, _) => false).Eof();

    // Three alternatives, the last one failing the choice, with tokens
    [GenerateParser(nameof(TryParseTokenChoice))]
    public static Parser<string> BuildTokenChoice() =>
        OneOf(
            Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Char('!')),
            Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Text("c")).AndSkip(Terms.Char('!')),
            Terms.Text("a").AndSkip(Terms.Char('?')))
        .AndSkip(Terms.Char('x')).Eof();

    // An optional sequence which fails after its first text, without white space
    [GenerateParser(nameof(TryParseOptional))]
    public static Parser<string> BuildOptional() =>
        Literals.Text("a").AndSkip(Literals.Text("b").AndSkip(Literals.Text("c")).Optional()).AndSkip(Literals.Text("d").Optional()).Eof();

    [GenerateParser(nameof(TryParseZeroOrOne))]
    public static Parser<string> BuildZeroOrOne() =>
        Literals.Text("a").AndSkip(ZeroOrOne(Literals.Text("b").AndSkip(Literals.Text("c")))).AndSkip(Literals.Text("d").Optional()).Eof();

    // Loops whose element fails after reading
    [GenerateParser(nameof(TryParseZeroOrMany))]
    public static Parser<string> BuildZeroOrMany() =>
        Literals.Text("a").AndSkip(ZeroOrMany(Literals.Text("b").AndSkip(Literals.Text("c")))).AndSkip(Literals.Char('x').Optional()).Eof();

    [GenerateParser(nameof(TryParseOneOrMany))]
    public static Parser<string> BuildOneOrMany() =>
        Literals.Text("a").AndSkip(OneOrMany(Terms.Text("b").AndSkip(Terms.Text("c")))).AndSkip(Literals.Char('x').Optional()).Eof();

    // The longest match, with an alternative which fails further than the one which is kept
    [GenerateParser(nameof(TryParseLongest))]
    public static Parser<string> BuildLongest() =>
        Literals.Text("a").AndSkip(Longest(Literals.Text("fr"), Literals.Text("from").AndSkip(Literals.Char('!')))).AndSkip(Literals.Char('x').Optional()).Eof();

    // A longest match whose alternatives all fail, in a choice whose next alternative matches
    [GenerateParser(nameof(TryParseLongestInChoice))]
    public static Parser<string> BuildLongestInChoice() =>
        OneOf(
            Longest(Literals.Text("fr").AndSkip(Literals.Char('!')), Literals.Text("from").AndSkip(Literals.Char('!'))),
            Literals.Text("f"))
        .AndSkip(Literals.Char('x').Optional()).Eof();

    // Alternatives of the longest match which skip white space and match, before one which skips that white space again,
    // in a value which a condition rejects: a match isn't a failure where its white space ended
    [GenerateParser(nameof(TryParseLongestTokens))]
    public static Parser<string> BuildLongestTokens() =>
        Longest(Terms.Text("a").AndSkip(Terms.Text("b")), Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Char('!').Optional()))
            .When(static (_, _) => false).Eof();

    // An alternative which fails where it started, after an optional text which isn't there: nothing was read
    [GenerateParser(nameof(TryParseEmptyStart))]
    public static Parser<string> BuildEmptyStart() =>
        Literals.Text("a").SkipAnd(OneOf(Literals.Text("b").Optional().SkipAnd(Literals.Text("c")), Literals.Text("d")))
            .When(static (_, _) => false).Eof();

    // Nested: a loop of choices of sequences, captured
    [GenerateParser(nameof(TryParseNested))]
    public static Parser<string> BuildNested() =>
        Capture(OneOrMany(OneOf(
            Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('c').Optional()).And(Literals.Char('!')).Then(static _ => 'x'),
            Literals.Char('a').And(OneOf(Literals.Char('b').And(Literals.Char('d')).Then(static _ => 'y'), Literals.Char('b'))).Then(static _ => 'z'),
            Literals.Char('c')))).Then(static value => value.ToString()).AndSkip(Literals.Char('!').Optional()).Eof();

    // The same loop of choices, with tokens, in a value which a condition rejects
    [GenerateParser(nameof(TryParseRejectedNested))]
    public static Parser<IReadOnlyList<string>> BuildRejectedNested() =>
        ZeroOrMany(OneOf(
            Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Text("c").Optional()).AndSkip(Terms.Char('!')),
            Terms.Text("a").AndSkip(Terms.Text("b").AndSkip(Terms.Text("d")).Optional()),
            Terms.Text("c")))
        .When(static (_, value) => value.Count < 2).Eof();

    // A pattern which needs more chars than it found, after a text, and in an alternative
    [GenerateParser(nameof(TryParsePattern))]
    public static Parser<string> BuildPattern() =>
        Literals.Text("abc").SkipAnd(Literals.Char(':')).SkipAnd(Literals.Pattern(static c => c == 'x', minSize: 2))
            .Then(static value => value.ToString()).Eof();

    [GenerateParser(nameof(TryParsePatternChoice))]
    public static Parser<string> BuildPatternChoice() =>
        OneOf(
            Literals.Text("ab").SkipAnd(Literals.Pattern(static c => c == 'x', minSize: 2)).Then(static value => value.ToString()),
            Literals.Pattern(static c => c == 'a', minSize: 2).Then(static value => value.ToString()),
            Literals.Text("a").SkipAnd(Literals.AnyOf("xy", minSize: 2)).Then(static value => value.ToString()),
            Literals.Text("a"))
        .AndSkip(Literals.Char('!').Optional()).Eof();

    // Span code invoked by other code, which fails where it started in a value which a condition rejects
    [GenerateParser(nameof(TryParseRejectedStart))]
    public static Parser<string> BuildRejectedStart()
    {
        var inner = Deferred<string>();
        inner.Parser = Literals.Text("b").AndSkip(Literals.Text("c")).AndSkip(Literals.Text("d"));
        return Literals.Text("a").AndSkip(inner.Optional()).When(static (_, _) => false).Eof();
    }

    // The same, after an optional text which isn't there
    [GenerateParser(nameof(TryParseRejectedEmptyStart))]
    public static Parser<string> BuildRejectedEmptyStart()
    {
        var inner = Deferred<string>();
        inner.Parser = Literals.Text("b").Optional().SkipAnd(Literals.Text("c")).AndSkip(Literals.Text("d"));
        return Literals.Text("a").AndSkip(inner.Optional()).When(static (_, _) => false).Eof();
    }

    // A token which isn't found after the white space it skipped, in a value which goes on after that white space
    [GenerateParser(nameof(TryParseRejectedWhiteSpace))]
    public static Parser<string> BuildRejectedWhiteSpace() =>
        Terms.Text("a").AndSkip(Terms.Text("b").Optional()).AndSkip(Literals.Text(" c").Optional()).When(static (_, _) => false).Eof();

    // A number which its type can't represent fails after its digits, which the parser moves the cursor back from
    [GenerateParser(nameof(TryParseNumber))]
    public static Parser<string> BuildNumber() =>
        OneOf(
            Literals.Number<byte>().AndSkip(Literals.Char('!')).Then(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Literals.Text("9"),
            Terms.Number<byte>().Then(static value => "n"))
        .AndSkip(Literals.Char('x').Optional()).Eof();

    // Delimiters, a captured loop of tokens and chars which need a minimum count, in a value which a condition rejects
    [GenerateParser(nameof(TryParseRejectedList))]
    public static Parser<string> BuildRejectedList() =>
        Between(Terms.Char('('), Capture(ZeroOrMany(Terms.Identifier().AndSkip(Terms.Char(',')))), Terms.Char(')'))
            .And(Literals.AnyOf("!?", minSize: 2).Optional())
            .When(static (_, value) => value.Item1.Length < 3).Then(static value => value.Item1.ToString()).Eof();
}
