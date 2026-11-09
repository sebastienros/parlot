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

    // The same parser without hints: a sequence which starts with a parser that doesn't read can start with any char
    private static Parser<T> WithoutHints<T>(Parser<T> parser) => Always().SkipAnd(parser);

    private static readonly string[] Inputs =
    [
        "", " ", "a", "a;", "a ;", "a?b:c", "a ? b : c;", "a ?b", "a ? b :", "a ? 1 : c", "a\n\t? b\n: c ;", "a ?? b",
        "a ? b : c ? d : e", "a ? b : c x", "a  ", "?b:c", " ? b : c", "a?b:c?d:e?f:g;", "a , b", "a, b ,c", "a,", "a ,; b", "a,|b,|c",
    ];

    public static IEnumerable<object[]> Grammars()
    {
        foreach (var name in GrammarNames)
        {
            yield return [name];
        }
    }

    private static readonly string[] GrammarNames =
        ["optional", "zeroorone", "zeroormany", "oneormany", "separated", "separated-options", "capture", "deferred", "zeroormany-max"];

    // The grammar, and the same grammar where the guarded parser declares no hints
    private static (Parser<string> Guarded, Parser<string> Reference) Create(string name) => name switch
    {
        "optional" => Pair(static tail => Terms.Identifier().And(tail.Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"?{t.Item2.Value.Item1}:{t.Item2.Value.Item2}" : ""))),
        "zeroorone" => Pair(static tail => Terms.Identifier().And(ZeroOrOne(tail))
            .Then(static t => t.Item1.ToString() + string.Concat(t.Item2.Select(static x => $"?{x.Item1}:{x.Item2}")))),
        "zeroormany" => Pair(static tail => Terms.Identifier().And(ZeroOrMany(tail))
            .Then(static t => t.Item1.ToString() + string.Concat(t.Item2.Select(static x => $"?{x.Item1}:{x.Item2}")))),
        "zeroormany-max" => Pair(static tail => Terms.Identifier().And(ZeroOrMany(tail, 2))
            .Then(static t => t.Item1.ToString() + string.Concat(t.Item2.Select(static x => $"?{x.Item1}:{x.Item2}")))),
        "oneormany" => Pair(static tail => Terms.Identifier().And(OneOrMany(tail).Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? string.Concat(t.Item2.Value.Select(static x => $"?{x.Item1}:{x.Item2}")) : ""))),
        "separated" => (Separated(Separator(), Terms.Identifier()).Then(Join), Separated(WithoutHints(Separator()), Terms.Identifier()).Then(Join)),
        "separated-options" => (
            Separated(Separator(), Terms.Identifier(), allowTrailingSeparator: true).Then(Join),
            Separated(WithoutHints(Separator()), Terms.Identifier(), allowTrailingSeparator: true).Then(Join)),

        // The white space before the parser is captured, the guard moves the cursor back before invoking it
        "capture" => Pair(static tail => Terms.Identifier().And(Capture(tail).Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"[{t.Item2.Value}]" : ""))),

        // The hints are those of the parser the deferred parser is set to after the optional is built
        "deferred" => Pair(static tail =>
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

    private static string Join(IReadOnlyList<TextSpan> items) => string.Join(",", items);

    private static (Parser<string>, Parser<string>) Pair(Func<Parser<(TextSpan, TextSpan)>, Parser<string>> create) =>
        (create(Tail()), create(WithoutHints(Tail())));

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHints(string name)
    {
        var (guarded, reference) = Create(name);

        foreach (var text in Inputs)
        {
            var expected = Run(reference, text);
            var actual = Run(guarded, text);

            Assert.True(expected == actual, $"{name} on '{text}': {actual} instead of {expected}");

            // Followed by a parser which fails, so that the furthest failure is the one a parse would report
            var expectedFurthest = Furthest(reference.AndSkip(Terms.Char('#')), text);
            var actualFurthest = Furthest(guarded.AndSkip(Terms.Char('#')), text);

            Assert.True(expectedFurthest == actualFurthest, $"{name} on '{text}': furthest failure {actualFurthest} instead of {expectedFurthest}");
        }
    }

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHintsWithoutValue(string name)
    {
        var (guarded, reference) = Create(name);
        var guardedCapture = Capture(guarded);
        var referenceCapture = Capture(reference);

        foreach (var text in Inputs)
        {
            Assert.Equal(Run(referenceCapture, text), Run(guardedCapture, text));
        }
    }

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHintsOnStreams(string name)
    {
        var (guarded, reference) = Create(name);

        foreach (var text in Inputs)
        {
            foreach (var bufferSize in new[] { 1, 2, 5 })
            {
                var options = new StreamParseOptions { BufferSize = bufferSize };
                var expected = reference.TryParse(new StreamingTests.ChunkedReader(text, () => 1, yield: false), out var expectedValue, options);
                var actual = guarded.TryParse(new StreamingTests.ChunkedReader(text, () => 1, yield: false), out var actualValue, options);

                Assert.True(expected == actual, $"{name} on '{text}' with buffer size {bufferSize}");
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

    private static void AssertNotEntered<T>(Parser<T> parser, object guarded, string text)
    {
        var context = Trace(parser, text, guarded, out var entered);

        Assert.Equal(0, entered);

        // The white space after the identifier isn't consumed
        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    // A word starting with 'a', 'b' or any char above ASCII, declared with OneOf<T>.NonAsciiSeekableChar. Then() invokes the
    // scanner, so the guards are on: they don't apply to a parser which tests the char itself.
    private static Parser<string> NonAsciiWord() =>
        Terms.Scan(ScanWord, "ab" + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar).Then(static x => x.ToString());

    private static int ScanWord(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty || !(span[0] is 'a' or 'b' || span[0] >= 0x80))
        {
            return 0;
        }

        var end = span.IndexOfAny(' ', ',');
        return end < 0 ? span.Length : end;
    }

    [Theory]
    [InlineData("a é", "a,é")]
    [InlineData("é", "é")]
    [InlineData(" \uFFFF", "\uFFFF")]
    [InlineData("a ç b", "a,ç,b")]
    [InlineData("a 日本 x", "a,日本")]
    [InlineData("a x", "a")]
    [InlineData("x", "")]
    public void LoopsTryAnyNonAsciiCharWithTheNonAsciiHint(string input, string expected)
    {
        Assert.Equal(expected, string.Join(",", ZeroOrMany(NonAsciiWord()).Parse(input)!));

        if (expected.Length > 0)
        {
            Assert.Equal(expected, string.Join(",", OneOrMany(NonAsciiWord()).Parse(input)!));
        }
        else
        {
            Assert.Null(OneOrMany(NonAsciiWord()).Parse(input));
        }
    }

    [Theory]
    [InlineData("é", "é")]
    [InlineData("  ü", "ü")]
    [InlineData("a", "a")]
    [InlineData("x", "")]
    public void OptionalsTryAnyNonAsciiCharWithTheNonAsciiHint(string input, string expected)
    {
        var optional = NonAsciiWord().Optional().Parse(input);
        Assert.Equal(expected, optional.HasValue ? optional.Value : "");

        Assert.Equal(expected, string.Concat(ZeroOrOne(NonAsciiWord()).Parse(input)!));
    }

    [Theory]
    [InlineData("a·b", "a,b")]
    [InlineData("a · b·c", "a,b,c")]
    [InlineData("a, b", "a,b")]
    [InlineData("a;b", "a")]
    public void SeparatorsTryAnyNonAsciiCharWithTheNonAsciiHint(string input, string expected)
    {
        // A ',' or any char above ASCII separates the identifiers
        var separator = Terms.Scan(static span => span.Length > 0 && (span[0] == ',' || span[0] >= 0x80) ? 1 : 0, "," + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .Then(static x => x.ToString());

        Assert.Equal(expected, string.Join(",", Separated(separator, Terms.Identifier()).Parse(input)!));
    }

    [Fact]
    public void GuardStillSkipsAnAsciiCharOutsideTheNonAsciiHint()
    {
        var word = NonAsciiWord();
        var context = Trace(Terms.Identifier().And(word.Optional()), "x y", word, out var entered);

        Assert.Equal(0, entered);
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
