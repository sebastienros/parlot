#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parlot;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// The furthest failure of span code which goes on after a parser failed in it: the generated <c>string</c> entry point,
/// which reads the text through a position local, reports what the generated <c>TextReader</c> one and the runtime
/// parsers do, which move the cursor back.
/// </summary>
public class SpanFailureParityTests
{
    private delegate bool StringEntry<T>(string text, out T value, out int failure);
    private delegate bool ReaderEntry<T>(TextReader reader, out T value, out (int Offset, int Line, int Column) failure);

    private sealed class Grammar
    {
        public Grammar(string alphabet, Func<string, (bool, int)> generatedString, Func<string, (bool, int)> generatedReader, Func<string, (bool, int)> runtimeString, Func<string, (bool, int)> runtimeReader)
        {
            Alphabet = alphabet;
            GeneratedString = generatedString;
            GeneratedReader = generatedReader;
            RuntimeString = runtimeString;
            RuntimeReader = runtimeReader;
        }

        public string Alphabet { get; }
        public Func<string, (bool Success, int Failure)> GeneratedString { get; }
        public Func<string, (bool Success, int Failure)> GeneratedReader { get; }
        public Func<string, (bool Success, int Failure)> RuntimeString { get; }
        public Func<string, (bool Success, int Failure)> RuntimeReader { get; }
    }

    private static Grammar Create<T>(string alphabet, Parser<T> parser, StringEntry<T> generatedString, ReaderEntry<T> generatedReader)
        => new(
            alphabet,
            text => generatedString(text, out _, out var failure) ? (true, 0) : (false, failure),
            text => generatedReader(new StringReader(text), out _, out var failure) ? (true, 0) : (false, failure.Offset),
            text =>
            {
                var context = new ParseContext(new Scanner(text));
                return parser.TryParse(context, out _, out _) ? (true, 0) : (false, context.Scanner.Cursor.FurthestOffset);
            },
            text =>
            {
                ParseContext context = null!;
                var options = new StreamParseOptions
                {
                    BufferSize = 1,
                    ContextFactory = (scanner, token) => context = new ParseContext(scanner, token),
                };

                return parser.TryParse(new StringReader(text), out _, out _, options) ? (true, 0) : (false, context.Scanner.Cursor.FurthestOffset);
            });

    private static readonly Dictionary<string, Grammar> Grammars = new()
    {
        ["Choice"] = Create(";cd!x? ", SpanFailureRuntimeGrammars.BuildChoice(), SpanFailureGrammars.TryParseChoice, SpanFailureGrammars.TryParseChoice),
        ["RejectedChoice"] = Create(";cd!x? ", SpanFailureRuntimeGrammars.BuildRejectedChoice(), SpanFailureGrammars.TryParseRejectedChoice, SpanFailureGrammars.TryParseRejectedChoice),
        ["TokenChoice"] = Create("abc!?x ", SpanFailureRuntimeGrammars.BuildTokenChoice(), SpanFailureGrammars.TryParseTokenChoice, SpanFailureGrammars.TryParseTokenChoice),
        ["Optional"] = Create("abcdx", SpanFailureRuntimeGrammars.BuildOptional(), SpanFailureGrammars.TryParseOptional, SpanFailureGrammars.TryParseOptional),
        ["ZeroOrOne"] = Create("abcdx", SpanFailureRuntimeGrammars.BuildZeroOrOne(), SpanFailureGrammars.TryParseZeroOrOne, SpanFailureGrammars.TryParseZeroOrOne),
        ["ZeroOrMany"] = Create("abcx", SpanFailureRuntimeGrammars.BuildZeroOrMany(), SpanFailureGrammars.TryParseZeroOrMany, SpanFailureGrammars.TryParseZeroOrMany),
        ["OneOrMany"] = Create("abcx ", SpanFailureRuntimeGrammars.BuildOneOrMany(), SpanFailureGrammars.TryParseOneOrMany, SpanFailureGrammars.TryParseOneOrMany),
        ["Longest"] = Create("afrom!x", SpanFailureRuntimeGrammars.BuildLongest(), SpanFailureGrammars.TryParseLongest, SpanFailureGrammars.TryParseLongest),
        ["LongestInChoice"] = Create("from!x", SpanFailureRuntimeGrammars.BuildLongestInChoice(), SpanFailureGrammars.TryParseLongestInChoice, SpanFailureGrammars.TryParseLongestInChoice),
        ["LongestTokens"] = Create("ab!? ", SpanFailureRuntimeGrammars.BuildLongestTokens(), SpanFailureGrammars.TryParseLongestTokens, SpanFailureGrammars.TryParseLongestTokens),
        ["EmptyStart"] = Create("abcdx", SpanFailureRuntimeGrammars.BuildEmptyStart(), SpanFailureGrammars.TryParseEmptyStart, SpanFailureGrammars.TryParseEmptyStart),
        ["Nested"] = Create("abcd!", SpanFailureRuntimeGrammars.BuildNested(), SpanFailureGrammars.TryParseNested, SpanFailureGrammars.TryParseNested),
        ["RejectedNested"] = Create("abcd! ", SpanFailureRuntimeGrammars.BuildRejectedNested(), SpanFailureGrammars.TryParseRejectedNested, SpanFailureGrammars.TryParseRejectedNested),
        ["Pattern"] = Create("abc:xy", SpanFailureRuntimeGrammars.BuildPattern(), SpanFailureGrammars.TryParsePattern, SpanFailureGrammars.TryParsePattern),
        ["PatternChoice"] = Create("abxy!", SpanFailureRuntimeGrammars.BuildPatternChoice(), SpanFailureGrammars.TryParsePatternChoice, SpanFailureGrammars.TryParsePatternChoice),
        ["RejectedStart"] = Create("abcdx", SpanFailureRuntimeGrammars.BuildRejectedStart(), SpanFailureGrammars.TryParseRejectedStart, SpanFailureGrammars.TryParseRejectedStart),
        ["RejectedEmptyStart"] = Create("abcdx", SpanFailureRuntimeGrammars.BuildRejectedEmptyStart(), SpanFailureGrammars.TryParseRejectedEmptyStart, SpanFailureGrammars.TryParseRejectedEmptyStart),
        ["RejectedWhiteSpace"] = Create("abc x", SpanFailureRuntimeGrammars.BuildRejectedWhiteSpace(), SpanFailureGrammars.TryParseRejectedWhiteSpace, SpanFailureGrammars.TryParseRejectedWhiteSpace),
        ["RejectedWhiteSpaceChoice"] = Create("abc x", SpanFailureRuntimeGrammars.BuildRejectedWhiteSpaceChoice(), SpanFailureGrammars.TryParseRejectedWhiteSpaceChoice, SpanFailureGrammars.TryParseRejectedWhiteSpaceChoice),
        ["RejectedWhiteSpaceLongest"] = Create("abc x", SpanFailureRuntimeGrammars.BuildRejectedWhiteSpaceLongest(), SpanFailureGrammars.TryParseRejectedWhiteSpaceLongest, SpanFailureGrammars.TryParseRejectedWhiteSpaceLongest),
        ["Number"] = Create("912!x ", SpanFailureRuntimeGrammars.BuildNumber(), SpanFailureGrammars.TryParseNumber, SpanFailureGrammars.TryParseNumber),
        ["RejectedList"] = Create("(a,)!? ", SpanFailureRuntimeGrammars.BuildRejectedList(), SpanFailureGrammars.TryParseRejectedList, SpanFailureGrammars.TryParseRejectedList),
    };

    [Theory]
    // The first alternative stops at the '?', after the word: the second one matches, and 'x' isn't found after it
    [InlineData("Choice", ";;cd?", 4)]
    [InlineData("Choice", ";;cd !", 6)]
    [InlineData("Choice", ";;  cd", 6)]
    [InlineData("Choice", ";;?", 2)]
    [InlineData("Choice", ";?", 0)]
    [InlineData("RejectedChoice", ";;cd?", 4)]
    [InlineData("RejectedChoice", ";;?", 2)]
    [InlineData("TokenChoice", "a b c ? x", 6)]
    [InlineData("TokenChoice", "a b ? x", 4)]
    [InlineData("TokenChoice", "a ? y", 4)]
    [InlineData("Optional", "abx", 2)]
    [InlineData("Optional", "abd", 2)]
    [InlineData("Optional", "ax", 1)]
    [InlineData("ZeroOrOne", "abx", 2)]
    [InlineData("ZeroOrMany", "abcbx", 4)]
    [InlineData("ZeroOrMany", "abcbcd", 5)]
    [InlineData("OneOrMany", "a b c b x", 8)]
    [InlineData("OneOrMany", "a b x", 4)]
    [InlineData("Longest", "afrom?", 5)]
    [InlineData("Longest", "afr?", 3)]
    [InlineData("LongestInChoice", "from?", 4)]
    [InlineData("LongestInChoice", "fr?", 2)]
    // The alternatives which matched skipped white space, which isn't a failure where it ended
    [InlineData("LongestTokens", "a b", 0)]
    [InlineData("LongestTokens", "a  b!", 0)]
    [InlineData("LongestTokens", "a b ?", 4)]
    [InlineData("LongestTokens", "a ?", 2)]
    [InlineData("LongestTokens", "a", 1)]
    // Nothing was read by the alternative which failed, so the rejected value fails where it started
    [InlineData("EmptyStart", "ad", 0)]
    [InlineData("EmptyStart", "abd", 2)]
    [InlineData("Nested", "abc?", 3)]
    [InlineData("Nested", "abab?", 4)]
    [InlineData("RejectedNested", "a b c ? a", 6)]
    // A pattern which needs more chars fails where it started, like the parser which doesn't move the cursor
    [InlineData("Pattern", "abc:x", 4)]
    [InlineData("Pattern", "abc:xy", 4)]
    [InlineData("Pattern", "abc:xxy", 6)]
    [InlineData("PatternChoice", "abx", 2)]
    [InlineData("PatternChoice", "axx?", 3)]
    // Span code which fails where it started moves nothing back
    [InlineData("RejectedStart", "a", 0)]
    [InlineData("RejectedStart", "abc", 3)]
    [InlineData("RejectedEmptyStart", "a", 0)]
    [InlineData("RejectedEmptyStart", "ac", 2)]
    // The token isn't found after the white space, which the next text reads again
    [InlineData("RejectedWhiteSpace", "a c", 2)]
    [InlineData("RejectedWhiteSpace", "a  b c", 0)]
    [InlineData("RejectedWhiteSpaceChoice", "a x", 2)]
    [InlineData("RejectedWhiteSpaceChoice", "a  c x", 0)]
    [InlineData("RejectedWhiteSpaceLongest", "a x", 2)]
    [InlineData("RejectedWhiteSpaceLongest", "a  bc x", 0)]
    // 999 isn't a byte: the number fails after its digits, and the "9" which matches isn't followed by the end
    [InlineData("Number", "999", 3)]
    [InlineData("Number", " 999", 4)]
    [InlineData("Number", "12?", 2)]
    [InlineData("Number", "9 999", 1)]
    [InlineData("RejectedList", "(a, b,)", 0)]
    [InlineData("RejectedList", "(a, b)", 5)]
    [InlineData("RejectedList", "(a,)!", 4)]
    public void Reports_Where_A_Parser_Failed_After_Reading(string name, string text, int offset)
    {
        var grammar = Grammars[name];

        var expected = (false, offset);

        Assert.Equal(
            $"runtime {expected}, runtime reader {expected}, generated reader {expected}, generated string {expected}",
            $"runtime {grammar.RuntimeString(text)}, runtime reader {grammar.RuntimeReader(text)}, generated reader {grammar.GeneratedReader(text)}, generated string {grammar.GeneratedString(text)}");
    }

    [Theory]
    [InlineData("Choice")]
    [InlineData("RejectedChoice")]
    [InlineData("TokenChoice")]
    [InlineData("Optional")]
    [InlineData("ZeroOrOne")]
    [InlineData("ZeroOrMany")]
    [InlineData("OneOrMany")]
    [InlineData("Longest")]
    [InlineData("LongestInChoice")]
    [InlineData("LongestTokens")]
    [InlineData("EmptyStart")]
    [InlineData("Nested")]
    [InlineData("RejectedNested")]
    [InlineData("Pattern")]
    [InlineData("PatternChoice")]
    [InlineData("RejectedStart")]
    [InlineData("RejectedEmptyStart")]
    [InlineData("RejectedWhiteSpace")]
    [InlineData("RejectedWhiteSpaceChoice")]
    [InlineData("RejectedWhiteSpaceLongest")]
    [InlineData("Number")]
    [InlineData("RejectedList")]
    public void Generated_And_Runtime_Parsers_Report_The_Same_Failure(string name)
    {
        var grammar = Grammars[name];
        var random = new Random(name.Length * 7919);
        var builder = new StringBuilder();
        var differences = new List<string>();

        for (var i = 0; i < 4000; i++)
        {
            builder.Clear();

            for (var length = random.Next(0, 10); length > 0; length--)
            {
                builder.Append(grammar.Alphabet[random.Next(grammar.Alphabet.Length)]);
            }

            var text = builder.ToString();
            var expected = grammar.RuntimeString(text);
            var runtimeReader = grammar.RuntimeReader(text);
            var generatedReader = grammar.GeneratedReader(text);
            var generatedString = grammar.GeneratedString(text);

            if (runtimeReader != expected || generatedReader != expected || generatedString != expected)
            {
                differences.Add($"'{text}': runtime {expected}, runtime reader {runtimeReader}, generated reader {generatedReader}, generated string {generatedString}");
            }
        }

        Assert.True(differences.Count == 0, $"{differences.Count} differences, of which:{Environment.NewLine}{string.Join(Environment.NewLine, differences.GetRange(0, Math.Min(differences.Count, 12)))}");
    }
}

public static partial class SpanFailureGrammars
{
    public static partial bool TryParseChoice(string text, out string value, out int failure);
    public static partial bool TryParseChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedChoice(string text, out string value, out int failure);
    public static partial bool TryParseRejectedChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTokenChoice(string text, out string value, out int failure);
    public static partial bool TryParseTokenChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseOptional(string text, out string value, out int failure);
    public static partial bool TryParseOptional(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseZeroOrOne(string text, out string value, out int failure);
    public static partial bool TryParseZeroOrOne(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseZeroOrMany(string text, out string value, out int failure);
    public static partial bool TryParseZeroOrMany(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseOneOrMany(string text, out string value, out int failure);
    public static partial bool TryParseOneOrMany(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLongest(string text, out string value, out int failure);
    public static partial bool TryParseLongest(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLongestInChoice(string text, out string value, out int failure);
    public static partial bool TryParseLongestInChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLongestTokens(string text, out string value, out int failure);
    public static partial bool TryParseLongestTokens(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseEmptyStart(string text, out string value, out int failure);
    public static partial bool TryParseEmptyStart(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseNested(string text, out string value, out int failure);
    public static partial bool TryParseNested(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedNested(string text, out IReadOnlyList<string> value, out int failure);
    public static partial bool TryParseRejectedNested(TextReader reader, out IReadOnlyList<string> value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePattern(string text, out string value, out int failure);
    public static partial bool TryParsePattern(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePatternChoice(string text, out string value, out int failure);
    public static partial bool TryParsePatternChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedStart(string text, out string value, out int failure);
    public static partial bool TryParseRejectedStart(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedEmptyStart(string text, out string value, out int failure);
    public static partial bool TryParseRejectedEmptyStart(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedWhiteSpace(string text, out string value, out int failure);
    public static partial bool TryParseRejectedWhiteSpace(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedWhiteSpaceChoice(string text, out string value, out int failure);
    public static partial bool TryParseRejectedWhiteSpaceChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedWhiteSpaceLongest(string text, out string value, out int failure);
    public static partial bool TryParseRejectedWhiteSpaceLongest(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseNumber(string text, out string value, out int failure);
    public static partial bool TryParseNumber(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseRejectedList(string text, out string value, out int failure);
    public static partial bool TryParseRejectedList(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
}

// The runtime parsers of SpanFailureGrammars, whose file is only compiled by the generator
internal static class SpanFailureRuntimeGrammars
{
    // An alternative which fails after reading, before one which matches
    public static Parser<string> BuildChoice() =>
        Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;")).AndSkip(Literals.Char('x')).Eof();

    // The same choice in a value which a condition rejects
    public static Parser<string> BuildRejectedChoice() =>
        Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;"))
            .When(static (_, _) => false).Eof();

    // Three alternatives, the last one failing the choice, with tokens
    public static Parser<string> BuildTokenChoice() =>
        OneOf(
            Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Char('!')),
            Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Text("c")).AndSkip(Terms.Char('!')),
            Terms.Text("a").AndSkip(Terms.Char('?')))
        .AndSkip(Terms.Char('x')).Eof();

    // An optional sequence which fails after its first text, without white space
    public static Parser<string> BuildOptional() =>
        Literals.Text("a").AndSkip(Literals.Text("b").AndSkip(Literals.Text("c")).Optional()).AndSkip(Literals.Text("d").Optional()).Eof();

    public static Parser<string> BuildZeroOrOne() =>
        Literals.Text("a").AndSkip(ZeroOrOne(Literals.Text("b").AndSkip(Literals.Text("c")))).AndSkip(Literals.Text("d").Optional()).Eof();

    // Loops whose element fails after reading
    public static Parser<string> BuildZeroOrMany() =>
        Literals.Text("a").AndSkip(ZeroOrMany(Literals.Text("b").AndSkip(Literals.Text("c")))).AndSkip(Literals.Char('x').Optional()).Eof();

    public static Parser<string> BuildOneOrMany() =>
        Literals.Text("a").AndSkip(OneOrMany(Terms.Text("b").AndSkip(Terms.Text("c")))).AndSkip(Literals.Char('x').Optional()).Eof();

    // The longest match, with an alternative which fails further than the one which is kept
    public static Parser<string> BuildLongest() =>
        Literals.Text("a").AndSkip(Longest(Literals.Text("fr"), Literals.Text("from").AndSkip(Literals.Char('!')))).AndSkip(Literals.Char('x').Optional()).Eof();

    // A longest match whose alternatives all fail, in a choice whose next alternative matches
    public static Parser<string> BuildLongestInChoice() =>
        OneOf(
            Longest(Literals.Text("fr").AndSkip(Literals.Char('!')), Literals.Text("from").AndSkip(Literals.Char('!'))),
            Literals.Text("f"))
        .AndSkip(Literals.Char('x').Optional()).Eof();

    // Alternatives of the longest match which skip white space and match, before one which skips that white space again,
    // in a value which a condition rejects: a match isn't a failure where its white space ended
    public static Parser<string> BuildLongestTokens() =>
        Longest(Terms.Text("a").AndSkip(Terms.Text("b")), Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Char('!').Optional()))
            .When(static (_, _) => false).Eof();

    // An alternative which fails where it started, after an optional text which isn't there: nothing was read
    public static Parser<string> BuildEmptyStart() =>
        Literals.Text("a").SkipAnd(OneOf(Literals.Text("b").Optional().SkipAnd(Literals.Text("c")), Literals.Text("d")))
            .When(static (_, _) => false).Eof();

    // Nested: a loop of choices of sequences, captured
    public static Parser<string> BuildNested() =>
        Capture(OneOrMany(OneOf(
            Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('c').Optional()).And(Literals.Char('!')).Then(static _ => 'x'),
            Literals.Char('a').And(OneOf(Literals.Char('b').And(Literals.Char('d')).Then(static _ => 'y'), Literals.Char('b'))).Then(static _ => 'z'),
            Literals.Char('c')))).Then(static value => value.ToString()).AndSkip(Literals.Char('!').Optional()).Eof();

    // The same loop of choices, with tokens, in a value which a condition rejects
    public static Parser<IReadOnlyList<string>> BuildRejectedNested() =>
        ZeroOrMany(OneOf(
            Terms.Text("a").AndSkip(Terms.Text("b")).AndSkip(Terms.Text("c").Optional()).AndSkip(Terms.Char('!')),
            Terms.Text("a").AndSkip(Terms.Text("b").AndSkip(Terms.Text("d")).Optional()),
            Terms.Text("c")))
        .When(static (_, value) => value.Count < 2).Eof();

    // A pattern which needs more chars than it found, after a text, and in an alternative
    public static Parser<string> BuildPattern() =>
        Literals.Text("abc").SkipAnd(Literals.Char(':')).SkipAnd(Literals.Pattern(static c => c == 'x', minSize: 2))
            .Then(static value => value.ToString()).Eof();

    public static Parser<string> BuildPatternChoice() =>
        OneOf(
            Literals.Text("ab").SkipAnd(Literals.Pattern(static c => c == 'x', minSize: 2)).Then(static value => value.ToString()),
            Literals.Pattern(static c => c == 'a', minSize: 2).Then(static value => value.ToString()),
            Literals.Text("a").SkipAnd(Literals.AnyOf("xy", minSize: 2)).Then(static value => value.ToString()),
            Literals.Text("a"))
        .AndSkip(Literals.Char('!').Optional()).Eof();

    // Span code invoked by other code, which fails where it started in a value which a condition rejects
    public static Parser<string> BuildRejectedStart()
    {
        var inner = Deferred<string>();
        inner.Parser = Literals.Text("b").AndSkip(Literals.Text("c")).AndSkip(Literals.Text("d"));
        return Literals.Text("a").AndSkip(inner.Optional()).When(static (_, _) => false).Eof();
    }

    // The same, after an optional text which isn't there
    public static Parser<string> BuildRejectedEmptyStart()
    {
        var inner = Deferred<string>();
        inner.Parser = Literals.Text("b").Optional().SkipAnd(Literals.Text("c")).AndSkip(Literals.Text("d"));
        return Literals.Text("a").AndSkip(inner.Optional()).When(static (_, _) => false).Eof();
    }

    // A token which isn't found after the white space it skipped, in a value which goes on after that white space
    public static Parser<string> BuildRejectedWhiteSpace() =>
        Terms.Text("a").AndSkip(Terms.Text("b").Optional()).AndSkip(Literals.Text(" c").Optional()).When(static (_, _) => false).Eof();

    // The same with a choice and with the longest match, which skip the white space once for their alternatives
    public static Parser<string> BuildRejectedWhiteSpaceChoice() =>
        Terms.Text("a").AndSkip(OneOf(Terms.Text("b"), Terms.Text("c")).Optional()).AndSkip(Literals.Text(" x").Optional())
            .When(static (_, _) => false).Eof();

    public static Parser<string> BuildRejectedWhiteSpaceLongest() =>
        Terms.Text("a").AndSkip(Longest(Terms.Text("b"), Terms.Text("bc")).Optional()).AndSkip(Literals.Text(" x").Optional())
            .When(static (_, _) => false).Eof();

    // A number which its type can't represent fails after its digits, which the parser moves the cursor back from
    public static Parser<string> BuildNumber() =>
        OneOf(
            Literals.Number<byte>().AndSkip(Literals.Char('!')).Then(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            Literals.Text("9"),
            Terms.Number<byte>().Then(static value => "n"))
        .AndSkip(Literals.Char('x').Optional()).Eof();

    // Delimiters, a captured loop of tokens and chars which need a minimum count, in a value which a condition rejects
    public static Parser<string> BuildRejectedList() =>
        Between(Terms.Char('('), Capture(ZeroOrMany(Terms.Identifier().AndSkip(Terms.Char(',')))), Terms.Char(')'))
            .And(Literals.AnyOf("!?", minSize: 2).Optional())
            .When(static (_, value) => value.Item1.Length < 3).Then(static value => value.Item1.ToString()).Eof();
}
