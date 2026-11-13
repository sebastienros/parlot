#nullable enable

using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The parsers which may not invoke their parser, like <c>Optional</c> and the loops, don't invoke it when the current char
/// (after the white space it skips) can't start it, as a choice doesn't invoke an alternative. These tests check that they
/// match what they match when the parser declares no hints, with the same positions and furthest failures, and that the
/// parser isn't entered when it can't start.
/// </summary>
public class SeekGuardTests
{
    // A sequence which is usually not there, like the conditional tail of an expression: '?' a ':' b
    private static Parser<(TextSpan, TextSpan)> Tail() =>
        Terms.Char('?').SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(':')).And(Terms.Identifier());

    // The same tail starting with a parser which declares it can start with a '?' or with any non-ASCII char
    private static Parser<(TextSpan, TextSpan)> NonAsciiTail() =>
        Terms.Scan(static span => span.Length > 0 && (span[0] == '?' || span[0] >= 0x80) ? 1 : 0, "?" + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(':')).And(Terms.Identifier());

    // The same parser without hints: a sequence which starts with a parser that doesn't read can start with any char
    private static Parser<T> WithoutHints<T>(Parser<T> parser) => Always().SkipAnd(parser);

    private static readonly string[] Inputs =
    [
        "", " ", "a", "a;", "a ;", "a?b:c", "a ? b : c;", "a ?b", "a ? b :", "a ? 1 : c", "a\n\t? b\n: c ;", "a ?? b",
        "a ? b : c ? d : e", "a ? b : c x", "a  ", "?b:c", " ? b : c", "a?b:c?d:e?f:g;", "a , b", "a, b ,c", "a,", "a ,; b", "a,|b,|c",

        // Non-ASCII chars where the guarded parser is tried: the first and the last ones, a surrogate pair, a lone surrogate,
        // the char of NonAsciiSeekableChar itself, the last ASCII char, and white space which isn't ASCII
        "a é b : c", "aéb:c", "a é b : c é d : e;", "a é", "a é ", "a é b", "a \u0080 b : c", "a\u0080b:c", "a \uFFFF b : c", "a\uFFFFb:c",
        "a \u007F b : c", "a\u007Fb:c", "a \uD83D\uDE00 b : c", "a \uD83D b : c", "a \uDE00b:c", "a\u00A0é b : c", "a \u3000 ? b : c", "a\u00A0",
        "é b : c", "é", "a é b : c ? d : e é f : g", "a é b , c", "aéb", "a é| b é c", "a é é b", "a \u0080", "a\uFFFF",
    ];

    public static IEnumerable<object[]> Grammars()
    {
        foreach (var name in GrammarNames)
        {
            yield return [name, false];
            yield return [name, true];
        }
    }

    private static readonly string[] GrammarNames =
        ["optional", "zeroorone", "zeroormany", "oneormany", "separated", "separated-options", "capture", "deferred", "zeroormany-max"];

    // The grammar, and the same grammar where the guarded parser declares no hints
    // With nonAscii, the guarded parser declares it can start with any non-ASCII char
    private static (Parser<string> Guarded, Parser<string> Reference) Create(string name, bool nonAscii) => name switch
    {
        "optional" => Pair(nonAscii, static tail => Terms.Identifier().And(tail.Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"?{t.Item2.Value.Item1}:{t.Item2.Value.Item2}" : ""))),
        "zeroorone" => Pair(nonAscii, static tail => Terms.Identifier().And(ZeroOrOne(tail))
            .Then(static t => t.Item1.ToString() + string.Concat(t.Item2.Select(static x => $"?{x.Item1}:{x.Item2}")))),
        "zeroormany" => Pair(nonAscii, static tail => Terms.Identifier().And(ZeroOrMany(tail))
            .Then(static t => t.Item1.ToString() + string.Concat(t.Item2.Select(static x => $"?{x.Item1}:{x.Item2}")))),
        "zeroormany-max" => Pair(nonAscii, static tail => Terms.Identifier().And(ZeroOrMany(tail, 2))
            .Then(static t => t.Item1.ToString() + string.Concat(t.Item2.Select(static x => $"?{x.Item1}:{x.Item2}")))),
        "oneormany" => Pair(nonAscii, static tail => Terms.Identifier().And(OneOrMany(tail).Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? string.Concat(t.Item2.Value.Select(static x => $"?{x.Item1}:{x.Item2}")) : ""))),
        "separated" => nonAscii
            ? (Separated(SequenceWithNonAsciiSeparator(), Terms.Identifier()).Then(Join), Separated(WithoutHints(SequenceWithNonAsciiSeparator()), Terms.Identifier()).Then(Join))
            : (Separated(Separator(), Terms.Identifier()).Then(Join), Separated(WithoutHints(Separator()), Terms.Identifier()).Then(Join)),
        "separated-options" => nonAscii
            ? (Separated(SequenceWithNonAsciiSeparator(), Terms.Identifier(), allowTrailingSeparator: true).Then(Join),
                Separated(WithoutHints(SequenceWithNonAsciiSeparator()), Terms.Identifier(), allowTrailingSeparator: true).Then(Join))
            : (Separated(Separator(), Terms.Identifier(), allowTrailingSeparator: true).Then(Join),
                Separated(WithoutHints(Separator()), Terms.Identifier(), allowTrailingSeparator: true).Then(Join)),

        // The white space before the parser is captured, the guard moves the cursor back before invoking it
        "capture" => Pair(nonAscii, static tail => Terms.Identifier().And(Capture(tail).Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"[{t.Item2.Value}]" : ""))),

        // The hints are those of the parser the deferred parser is set to after the optional is built
        "deferred" => Pair(nonAscii, static tail =>
        {
            var deferred = Deferred<(TextSpan, TextSpan)>();
            var parser = Terms.Identifier().And(deferred.Optional())
                .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"?{t.Item2.Value.Item1}:{t.Item2.Value.Item2}" : ""));
            deferred.Parser = tail;
            return parser;
        }),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    private static Parser<(char, Option<char>)> Separator() => Terms.Char(',').And(Literals.Char('|').Optional());

    // A separator which declares it can start with a ',' or with any non-ASCII char
    private static Parser<(TextSpan, Option<char>)> SequenceWithNonAsciiSeparator() =>
        Terms.Scan(static span => span.Length > 0 && (span[0] == ',' || span[0] >= 0x80) ? 1 : 0, "," + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .And(Literals.Char('|').Optional());

    private static string Join(IReadOnlyList<TextSpan> items) => string.Join(",", items);

    private static (Parser<string>, Parser<string>) Pair(bool nonAscii, Func<Parser<(TextSpan, TextSpan)>, Parser<string>> create) => nonAscii
        ? (create(NonAsciiTail()), create(WithoutHints(NonAsciiTail())))
        : (create(Tail()), create(WithoutHints(Tail())));

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHints(string name, bool nonAscii)
    {
        var (guarded, reference) = Create(name, nonAscii);

        foreach (var text in Inputs)
        {
            var expected = Run(reference, text);
            var actual = Run(guarded, text);

            Assert.True(expected == actual, $"{name} ({nonAscii}) on '{text}': {actual} instead of {expected}");

            // Followed by a parser which fails, so that the furthest failure is the one a parse would report
            var expectedFurthest = Furthest(reference.AndSkip(Terms.Char('#')), text);
            var actualFurthest = Furthest(guarded.AndSkip(Terms.Char('#')), text);

            Assert.True(expectedFurthest == actualFurthest, $"{name} ({nonAscii}) on '{text}': furthest failure {actualFurthest} instead of {expectedFurthest}");
        }
    }

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHintsWithoutValue(string name, bool nonAscii)
    {
        var (guarded, reference) = Create(name, nonAscii);
        var guardedCapture = Capture(guarded);
        var referenceCapture = Capture(reference);

        foreach (var text in Inputs)
        {
            Assert.Equal(Run(referenceCapture, text), Run(guardedCapture, text));
        }
    }

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHintsOnStreams(string name, bool nonAscii)
    {
        var (guarded, reference) = Create(name, nonAscii);

        foreach (var text in Inputs)
        {
            foreach (var bufferSize in new[] { 1, 2, 5 })
            {
                var options = new StreamParseOptions { BufferSize = bufferSize };
                var expected = reference.TryParse(new StreamingTests.ChunkedReader(text, () => 1, yield: false), out var expectedValue, options);
                var actual = guarded.TryParse(new StreamingTests.ChunkedReader(text, () => 1, yield: false), out var actualValue, options);

                Assert.True(expected == actual, $"{name} ({nonAscii}) on '{text}' with buffer size {bufferSize}");
                Assert.Equal(expectedValue, actualValue);
            }
        }
    }

    private static int Furthest<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();

        Assert.False(parser.Parse(context, ref result));
        return context.Scanner.Cursor.FurthestOffset;
    }

    private static string Run<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();
        var success = parser.Parse(context, ref result);

        return success
            ? $"OK {result.Value} ({result.Start}, {result.End}) at {context.Scanner.Cursor.Offset}"
            : $"FAIL at {context.Scanner.Cursor.Offset}";
    }

    [Theory]
    [InlineData("a")]
    [InlineData("a ;")]
    [InlineData("a  \n ")]
    public void OptionalShouldNotInvokeAParserWhichCantStart(string text)
    {
        var tail = Tail();

        AssertNotEntered(Terms.Identifier().And(tail.Optional()), tail, text);
        AssertNotEntered(Terms.Identifier().And(ZeroOrOne(tail)), tail, text);
        AssertNotEntered(Terms.Identifier().And(ZeroOrMany(tail)), tail, text);
        AssertNotEntered(Terms.Identifier().And(Capture(tail).Optional()), tail, text);
    }

    [Fact]
    public void LoopsShouldNotInvokeAParserWhichCantStart()
    {
        var tail = Tail();

        // The loop stops before the ';' without entering the parser again
        var context = Trace(Terms.Identifier().And(ZeroOrMany(tail)), "a ? b : c ? d : e ;", tail, out var entered);
        Assert.Equal(2, entered);
        Assert.Equal(17, context.Scanner.Cursor.Offset);

        context = Trace(Terms.Identifier().And(OneOrMany(tail)), "a ? b : c ;", tail, out entered);
        Assert.Equal(1, entered);
        Assert.Equal(9, context.Scanner.Cursor.Offset);

        var separator = Separator();
        context = Trace(Separated(separator, Terms.Identifier()), "a, b ;", separator, out entered);
        Assert.Equal(1, entered);
        Assert.Equal(4, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldInvokeAParserWhichCanStart()
    {
        var tail = Tail();

        // The parser fails after reading: the cursor is moved back before the white space it skipped
        var context = Trace(Terms.Identifier().And(tail.Optional()), "a ? b ;", tail, out var entered);
        Assert.Equal(1, entered);
        Assert.Equal(1, context.Scanner.Cursor.Offset);

        context = Trace(Terms.Identifier().And(ZeroOrMany(tail)), "a ? b : c ? d ;", tail, out entered);
        Assert.Equal(2, entered);
        Assert.Equal(9, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldInvokeAParserWithoutHints()
    {
        // A parser which can succeed without reading declares no hints, and the char can't tell it doesn't match
        var empty = Literals.Pattern(static c => c == 'x', minSize: 0);
        Trace(Terms.Identifier().And(empty.Optional()), "a ;", empty, out var entered);
        Assert.Equal(1, entered);

        // A choice which can start with any char
        var choice = OneOf(Terms.Char('?'), WithoutHints(Terms.Char('!')));
        Trace(Terms.Identifier().And(choice.Optional()), "a ;", choice, out entered);
        Assert.Equal(1, entered);
    }

    [Fact]
    public void ShouldNotGuardAChoice()
    {
        // A choice selects its alternatives from the char already, the guard would only skip the white space a second time
        var oneOf = OneOf(Terms.Char('a'), Terms.Char('b'));
        Trace(ZeroOrMany(oneOf), "a b ;", oneOf, out var entered);
        Assert.Equal(3, entered);

        var longest = Longest(Terms.Char('a'), Terms.Char('b'));
        Trace(ZeroOrMany(longest), "a b ;", longest, out entered);
        Assert.Equal(3, entered);
    }

    [Fact]
    public void ShouldUseTheWhiteSpaceOfTheContext()
    {
        // The white space parser skips comments, so the '?' after one can start the tail
        var tail = Tail();
        var parser = Terms.Identifier().And(tail.Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"?{t.Item2.Value.Item1}:{t.Item2.Value.Item2}" : ""))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

        Assert.Equal("a?b:c", parser.Parse("a /* x */ ? b : c"));
        Assert.Equal("a", parser.Parse("a /* x */ ;"));

        // A white space set the char isn't part of
        var ascii = Terms.Identifier().And(tail.Optional())
            .Then(static t => t.Item2.HasValue)
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

        Assert.True(ascii.Parse("a ? b : c"));
        Assert.False(ascii.Parse("a ? b : c"));
    }

    // A parser which can start with an 'a' or with any non-ASCII char, declared with NonAsciiSeekableChar. It reads one char.
    private static Parser<string> NonAsciiChar() =>
        Terms.Scan(static span => span.Length > 0 ? 1 : 0, "a" + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .Then(static x => x.ToString());

    // A separator which can start with a ',' or with any non-ASCII char
    private static Parser<string> NonAsciiSeparator() =>
        Terms.Scan(static span => span.Length > 0 ? 1 : 0, "," + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .Then(static x => x.ToString());

    [Theory]
    [InlineData("aé")]
    [InlineData("a é")]
    [InlineData("a\n\té")]
    public void LoopsShouldMatchANonAsciiCharDeclaredAsAnyNonAscii(string text)
    {
        var word = NonAsciiChar();

        Assert.Equal(["a", "é"], ZeroOrMany(word).Parse(text));
        Assert.Equal(["a", "é"], OneOrMany(word).Parse(text));
    }

    [Theory]
    [InlineData("é")]
    [InlineData(" é")]
    [InlineData("\n\té")]
    public void OptionalAndZeroOrOneShouldMatchANonAsciiCharDeclaredAsAnyNonAscii(string text)
    {
        var word = NonAsciiChar();

        var optional = word.Optional().Parse(text);
        Assert.True(optional.HasValue);
        Assert.Equal("é", optional.Value);

        Assert.Equal(["é"], ZeroOrOne(word).Parse(text));
    }

    [Theory]
    [InlineData("aéa")]
    [InlineData("a é a")]
    public void SeparatedShouldMatchANonAsciiSeparatorDeclaredAsAnyNonAscii(string text)
    {
        Assert.Equal(["a", "a"], Separated(NonAsciiSeparator(), Terms.Char('a').Then(static _ => "a")).Parse(text));
    }

    [Fact]
    public void GuardsShouldStillSkipAnAsciiCharOutsideTheHints()
    {
        var word = NonAsciiChar();
        var separator = NonAsciiSeparator();

        AssertNotEntered(Terms.Identifier().And(word.Optional()), word, "a #");
        AssertNotEntered(Terms.Identifier().And(ZeroOrOne(word)), word, "a #");
        AssertNotEntered(Terms.Identifier().And(ZeroOrMany(word)), word, "a #");
        AssertNotEntered(Terms.Identifier().And(OneOrMany(word).Optional()), word, "a #");
        AssertNotEntered(Separated(separator, Terms.Char('a')), separator, "a #");
    }

    [Fact]
    public void GuardsShouldEnterAParserWhichCanStartWithAnyNonAsciiChar()
    {
        var word = NonAsciiChar();
        var separator = NonAsciiSeparator();

        var context = Trace(Terms.Identifier().And(word.Optional()), "a é", word, out var entered);
        Assert.True(entered > 0);
        Assert.Equal(3, context.Scanner.Cursor.Offset);

        context = Trace(Terms.Identifier().And(ZeroOrOne(word)), "a é", word, out entered);
        Assert.True(entered > 0);
        Assert.Equal(3, context.Scanner.Cursor.Offset);

        context = Trace(Terms.Identifier().And(ZeroOrMany(word)), "a é", word, out entered);
        Assert.True(entered > 0);
        Assert.Equal(3, context.Scanner.Cursor.Offset);

        context = Trace(Terms.Identifier().And(OneOrMany(word).Optional()), "a é", word, out entered);
        Assert.True(entered > 0);
        Assert.Equal(3, context.Scanner.Cursor.Offset);

        context = Trace(Separated(separator, Terms.Char('a')), "a é a", separator, out entered);
        Assert.True(entered > 0);
        Assert.Equal(5, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData('\u0080')]
    [InlineData('\u00E9')]
    [InlineData('\uD83D')]
    [InlineData('\uDE00')]
    [InlineData('\uFFFE')]
    [InlineData('\uFFFF')]
    public void GuardsShouldEnterAParserBeforeEveryNonAsciiChar(char c)
    {
        var word = NonAsciiChar();
        var separator = NonAsciiSeparator();

        foreach (var text in new[] { $"a{c}", $"a {c}", $"a\n\t{c}" })
        {
            Assert.Equal(["a", c.ToString()], ZeroOrMany(word).Parse(text));
            Assert.Equal(["a", c.ToString()], OneOrMany(word).Parse(text));
            Assert.Equal(c.ToString(), Terms.Char('a').SkipAnd(word.Optional()).Parse(text).Value);
            Assert.Equal([c.ToString()], Terms.Char('a').SkipAnd(ZeroOrOne(word)).Parse(text));
            Assert.Equal(["a", "a"], Separated(separator, Terms.Char('a').Then(static _ => "a")).Parse(text + "a"));
        }
    }

    [Theory]
    [InlineData("a \u007F")]
    [InlineData("a #")]
    [InlineData("a \u0001")]
    [InlineData("a ")]
    [InlineData("a \n")]
    public void GuardsOfAnyNonAsciiCharShouldSkipAnAsciiCharAndTheEndOfTheText(string text)
    {
        // The last ASCII char isn't one of the non-ASCII chars, and neither is the NUL the cursor reads at the end of the text
        var word = NonAsciiChar();
        var separator = NonAsciiSeparator();

        AssertNotEntered(Terms.Identifier().And(word.Optional()), word, text);
        AssertNotEntered(Terms.Identifier().And(ZeroOrOne(word)), word, text);
        AssertNotEntered(Terms.Identifier().And(ZeroOrMany(word)), word, text);
        AssertNotEntered(Terms.Identifier().And(OneOrMany(word).Optional()), word, text);
        AssertNotEntered(Separated(separator, Terms.Char('a')), separator, text);
    }

    [Fact]
    public void GuardsShouldOnlyEnterAParserBeforeTheNonAsciiCharsItLists()
    {
        // Without NonAsciiSeekableChar, the non-ASCII chars are the ones the parser lists
        var word = Terms.Char('é').Then(static x => x.ToString());

        AssertNotEntered(Terms.Identifier().And(word.Optional()), word, "a ü");
        AssertNotEntered(Terms.Identifier().And(ZeroOrMany(word)), word, "a \uFFFF");
        AssertNotEntered(Terms.Identifier().And(ZeroOrMany(word)), word, "a \u0080");

        var context = Trace(Terms.Identifier().And(ZeroOrMany(word)), "a é é ü", word, out var entered);
        Assert.Equal(2, entered);
        Assert.Equal(5, context.Scanner.Cursor.Offset);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void LoopsShouldMatchIdentifiersStartingWithANonAsciiLetter()
    {
        // More than SeekableHints.MaxNonAsciiChars start chars: the identifier declares NonAsciiSeekableChar itself
        var letters = string.Concat(Enumerable.Range(0, char.MaxValue + 1).Select(static c => (char)c).Where(char.IsLetter));
        var identifier = Terms.Identifier(letters, letters).Then(static x => x.ToString());

        Assert.Contains(Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar, Assert.IsAssignableFrom<Parlot.Rewriting.ISeekable>(identifier).ExpectedChars);

        Assert.Equal(["a", "é", "日本語", "ૹ", "b"], ZeroOrMany(identifier).Parse("a é 日本語 ૹ b"));
        Assert.Equal(["日本語", "a"], OneOrMany(identifier).Parse("日本語 a 1"));
        Assert.Equal("é", identifier.Optional().Parse(" é").Value);
        Assert.Equal(["x", "é", "ü"], Separated(Terms.Char(','), identifier).Parse("x, é ,ü"));

        // A non-ASCII char which isn't a letter is tried, and rejected by the identifier
        Assert.Equal(["a"], ZeroOrMany(identifier).Parse("a ± b"));
        Assert.False(identifier.Optional().Parse("±").HasValue);
    }
#endif

    private static void AssertNotEntered<T>(Parser<T> parser, object guarded, string text)
    {
        var context = Trace(parser, text, guarded, out var entered);

        Assert.Equal(0, entered);

        // The white space after the identifier isn't consumed
        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    private static ParseContext Trace<T>(Parser<T> parser, string text, object guarded, out int entered)
    {
        var count = 0;
        var context = new ParseContext(new Scanner(text))
        {
            OnEnterParser = (parser, _) =>
            {
                if (parser == guarded)
                {
                    count++;
                }
            },
        };

        var result = new ParseResult<T>();
        Assert.True(parser.Parse(context, ref result));

        entered = count;
        return context;
    }
}
