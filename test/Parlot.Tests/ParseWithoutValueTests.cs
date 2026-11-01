#nullable enable

using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The parsers whose value isn't used, like those of <see cref="Capture{T}"/> or the skipped ones of <c>AndSkip</c>, are
/// invoked without their value when they can. These tests check that it matches what <c>Parse</c> matches, with the same
/// positions and the same parsers entered, and that the lists aren't allocated.
/// </summary>
public class ParseWithoutValueTests
{
    private static readonly Parser<char> A = Terms.Char('a');
    private static readonly Parser<char> B = Terms.Char('b');
    private static readonly Parser<char> C = Terms.Char('c');
    private static readonly Parser<IReadOnlyList<char>> As = ZeroOrMany(Terms.Char('a'));
    private static readonly Parser<IReadOnlyList<char>> Bs = OneOrMany(Terms.Char('b'));

    private static readonly string[] Inputs = ["", "a", "b", "c", "ab", "abc", "a b b c", " a a b c d", "bbc", "aabbcc", "cba"];

    [Fact]
    public void ShouldInvokeTheOverride()
    {
        // ZeroOrOne doesn't build its list
        var context = new ParseContext(new Scanner("a"));
        var result = new ParseResult<IReadOnlyList<char>>();

        Assert.True(ParseWithoutValue(ZeroOrOne(A))(context, ref result));
        Assert.Null(result.Value);
        Assert.Equal(0, result.Start);
        Assert.Equal(1, result.End);
    }

    [Fact]
    public void LoopsShouldMatchParse()
    {
        AssertSameAsParse(As);
        AssertSameAsParse(Bs);
        AssertSameAsParse(ZeroOrMany(Terms.Char('a'), 1));
        AssertSameAsParse(OneOrMany(Terms.Char('b'), 1));
        AssertSameAsParse(ZeroOrMany(Bs));
        AssertSameAsParse(OneOrMany(A.And(Bs)));
        AssertSameAsParse(ZeroOrOne(Bs));
        AssertSameAsParse(ZeroOrOne(A));
        AssertSameAsParse(Bs.Optional());
    }

    [Fact]
    public void SequencesShouldMatchParse()
    {
        AssertSameAsParse(A.And(Bs));
        AssertSameAsParse(As.And(Bs));
        AssertSameAsParse(A.And(Bs).And(C));
        AssertSameAsParse(A.And(B).And(Bs));
        AssertSameAsParse(As.And(B).And(C).And(Bs));
        AssertSameAsParse(As.And(B).And(C).And(Bs).And(A));
        AssertSameAsParse(As.And(B).And(C).And(Bs).And(A).And(Bs));
        AssertSameAsParse(As.And(B).And(C).And(Bs).And(A).And(Bs).And(C));
    }

    [Fact]
    public void SkippingSequencesShouldMatchParse()
    {
        AssertSameAsParse(A.AndSkip(Bs));
        AssertSameAsParse(As.AndSkip(B));
        AssertSameAsParse(A.And(B).AndSkip(Bs));
        AssertSameAsParse(A.And(B).And(C).AndSkip(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).AndSkip(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).And(B).AndSkip(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).And(B).And(C).AndSkip(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).And(B).And(C).And(A).AndSkip(Bs));

        AssertSameAsParse(As.SkipAnd(Bs));
        AssertSameAsParse(A.SkipAnd(Bs));
        AssertSameAsParse(A.And(Bs).SkipAnd(C));
        AssertSameAsParse(As.And(B).SkipAnd(Bs));
        AssertSameAsParse(As.And(B).And(C).SkipAnd(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).SkipAnd(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).And(B).SkipAnd(Bs));
        AssertSameAsParse(As.And(B).And(C).And(A).And(B).And(C).SkipAnd(Bs));
        AssertSameAsParse(A.AndSkip(Bs).SkipAnd(C));
        AssertSameAsParse(A.SkipAnd(Bs).AndSkip(C));
    }

    [Fact]
    public void ChoicesShouldMatchParse()
    {
        AssertSameAsParse(OneOf(Bs, As));
        AssertSameAsParse(OneOf(A.And(Bs).Then(static x => x.Item2), Bs, ZeroOrOne(C)));

        // An alternative that can't seek
        AssertSameAsParse(OneOf(Bs, Literals.Pattern(static c => c == 'c').Then<IReadOnlyList<char>>(static _ => [])));

        // All alternatives skip white space
        AssertSameAsParse(OneOf(Terms.Text("ab").And(Bs).Then(static x => x.Item2), Bs));
    }

    [Fact]
    public void WrappersShouldMatchParse()
    {
        AssertSameAsParse(Between(A, Bs, C));
        AssertSameAsParse(Between(As, Bs, ZeroOrMany(C)));
        AssertSameAsParse(Between(A, B, Bs));
        AssertSameAsParse(Bs.Else([]));
        AssertSameAsParse(Bs.Else(static _ => []));
        AssertSameAsParse(Not(Bs));
#pragma warning disable CS0618 // Discard is obsolete
        AssertSameAsParse(Bs.Discard<int>(1));
#pragma warning restore CS0618
        AssertSameAsParse(Bs.Then(1));
        AssertSameAsParse(Bs.Then(static x => x.Count));
        AssertSameAsParse(A.WhenFollowedBy(Bs));
        AssertSameAsParse(A.WhenNotFollowedBy(Bs));
        AssertSameAsParse(SkipWhiteSpace(OneOrMany(Literals.Char('b'))));
        AssertSameAsParse(Bs.WithWhiteSpaceSet(WhiteSpaceSet.Ascii));
        AssertSameAsParse(A.And(Bs).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char(' ')))));
    }

    [Fact]
    public void SeparatedShouldMatchParse()
    {
        AssertSameAsParse(Separated(B, As));
        AssertSameAsParse(Separated(Bs, A));
        AssertSameAsParse(Separated(B, OneOrMany(Terms.Char('a')), min: 2, max: 3), ["a b a", "a b a b a b a", "a", "a b", "a b b a"]);
        AssertSameAsParse(Separated(B, As, min: 0, max: 0), ["", "b", "a b a b"]);

        // The options parse with the values
        AssertSameAsParse(Separated(B, OneOrMany(Terms.Char('a')), removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true),
            ["b a b b a b", "a b", "b", ""]);
    }

    [Fact]
    public void DeferredShouldMatchParse()
    {
        var deferred = Deferred<IReadOnlyList<char>>();
        deferred.Parser = Bs;
        AssertSameAsParse(deferred);
        AssertSameAsParse(A.AndSkip(deferred));

        // Nested lists in parentheses
        var nested = Recursive<IReadOnlyList<char>>(static self => OneOrMany(Between(Terms.Char('('), ZeroOrMany(self).Then<char>(static _ => 'x'), Terms.Char(')'))));
        AssertSameAsParse(nested, ["()", "(())()", "(()", "((()())())", ""]);

        // The parser can be replaced
        deferred.Parser = A.Then<IReadOnlyList<char>>(static _ => []);
        AssertSameAsParse(deferred);
    }

    [Fact]
    public void AdapterShouldMatchParse()
    {
        // The + operator adapts the parsers it appends
        AssertSameAsParse(A.And(B) + Bs);
        AssertSameAsParse(Capture(A.And(B) + Bs));
    }

    [Fact]
    public void SkippedParsersShouldKeepTheirPositions()
    {
        // The skipped parser sets the end of AndSkip and the start of SkipAnd
        AssertSamePositions(A.AndSkip(Bs), A.AndSkip(Bs.Then(static x => x)));
        AssertSamePositions(A.And(C).AndSkip(Bs), A.And(C).AndSkip(Bs.Then(static x => x)));
        AssertSamePositions(Bs.SkipAnd(A), Bs.Then(static x => x).SkipAnd(A));
        AssertSamePositions(Between(Bs, A, Bs), Between(Bs.Then(static x => x), A, Bs.Then(static x => x)));
        AssertSamePositions(Bs.Then(1), Bs.Then(static _ => 1));
#pragma warning disable CS0618 // Discard is obsolete
        AssertSamePositions(Bs.Discard<int>(1), Bs.Then(static _ => 1));
#pragma warning restore CS0618
        AssertSamePositions(Separated(Bs, A), Separated(Bs.Then(static x => x), A));
        AssertSamePositions(Separated(Bs, A, removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true),
            Separated(Bs.Then(static x => x), A, removeEmptyEntries: true, allowLeadingSeparator: true, allowTrailingSeparator: true));
    }

    [Fact]
    public void CaptureOfSequenceShouldNotBuildItsLists()
    {
        var dotted = Capture(Terms.Identifier().And(ZeroOrMany(Literals.Char('.').SkipAnd(Literals.Identifier()))));

        Assert.Equal("a.b.c", dotted.Parse("a.b.c d").ToString());
        Assert.Equal("a", dotted.Parse("a").ToString());
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("a.b.c.d.e.f")]
    [InlineData("a")]
    public void CaptureOfSequenceShouldNotAllocate(string text)
    {
        var dotted = Capture(Literals.Identifier().And(ZeroOrMany(Literals.Char('.').SkipAnd(Literals.Identifier()))));
        Assert.Equal(0, CaptureTests.MeasureAllocations(dotted, text));

        var separated = Capture(Separated(Literals.Char('.'), Literals.Identifier()));
        Assert.Equal(0, CaptureTests.MeasureAllocations(separated, text));

        // The lists of the alternatives
        var choice = Capture(OneOf(OneOrMany(Literals.Char('?').Then(static _ => default(TextSpan))), Separated(Literals.Char('.'), Literals.Identifier())));
        Assert.Equal(0, CaptureTests.MeasureAllocations(choice, text));
    }

    [Theory]
    [InlineData("x;;;;;;")]
    [InlineData("x;")]
    public void SkippedListShouldNotAllocate(string text)
    {
        Assert.Equal(0, CaptureTests.MeasureAllocations(Literals.Char('x').AndSkip(ZeroOrMany(Literals.Char(';'))), text));
        Assert.Equal(0, CaptureTests.MeasureAllocations(ZeroOrMany(Literals.Char('x')).SkipAnd(ZeroOrMany(Literals.Char(';'))).Then(0), text));
        Assert.Equal(0, CaptureTests.MeasureAllocations(Between(Literals.Char('x'), OneOrMany(Literals.Char(';')).Then(1), Literals.Char(';').Optional()), text));
#pragma warning disable CS0618 // Discard is obsolete
        Assert.Equal(0, CaptureTests.MeasureAllocations(OneOrMany(Literals.Char('x').Or(Literals.Char(';'))).Discard<int>(1), text));
#pragma warning restore CS0618
    }
#endif

    private delegate bool ParseHandler<T>(ParseContext context, ref ParseResult<T> result);

    private static ParseHandler<T> ParseWithoutValue<T>(Parser<T> parser)
    {
        // The delegate dispatches to the override, as a call does
        var method = typeof(Parser<T>).GetMethod("ParseWithoutValue", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (ParseHandler<T>)Delegate.CreateDelegate(typeof(ParseHandler<T>), parser, method);
    }

    private static void AssertSameAsParse<T>(Parser<T> parser, string[]? inputs = null)
    {
        var withoutValue = ParseWithoutValue(parser);

        foreach (var text in inputs ?? Inputs)
        {
            var expected = Run<T>(text, parser.Parse);
            var actual = Run<T>(text, withoutValue);

            Assert.True(expected.Success == actual.Success, $"{parser} on '{text}'");
            Assert.True(expected.Offset == actual.Offset, $"{parser} on '{text}': offset {actual.Offset} instead of {expected.Offset}");
            Assert.True(expected.Trace.Count == actual.Trace.Count, $"{parser} on '{text}': {actual.Trace.Count} parsers entered instead of {expected.Trace.Count}");

            for (var i = 0; i < expected.Trace.Count; i++)
            {
                Assert.True(expected.Trace[i] == actual.Trace[i], $"{parser} on '{text}': the parser entered at {i} is different");
            }

            if (expected.Success)
            {
                Assert.True(expected.Start == actual.Start && expected.End == actual.End,
                    $"{parser} on '{text}': ({actual.Start}, {actual.End}) instead of ({expected.Start}, {expected.End})");
            }
        }
    }

    private static void AssertSamePositions<T, U>(Parser<T> parser, Parser<U> reference)
    {
        foreach (var text in Inputs)
        {
            var expected = Run<U>(text, reference.Parse);
            var actual = Run<T>(text, parser.Parse);

            Assert.True(expected.Success == actual.Success, $"{parser} on '{text}'");
            Assert.True(expected.Offset == actual.Offset, $"{parser} on '{text}': offset {actual.Offset} instead of {expected.Offset}");

            if (expected.Success)
            {
                Assert.True(expected.Start == actual.Start && expected.End == actual.End,
                    $"{parser} on '{text}': ({actual.Start}, {actual.End}) instead of ({expected.Start}, {expected.End})");
            }
        }
    }

    private static (bool Success, int Start, int End, int Offset, List<(bool Enter, object Parser, int Offset)> Trace) Run<T>(string text, ParseHandler<T> parse)
    {
        var trace = new List<(bool, object, int)>();
        var context = new ParseContext(new Scanner(text))
        {
            OnEnterParser = (parser, context) => trace.Add((true, parser, context.Scanner.Cursor.Offset)),
            OnExitParser = (parser, context) => trace.Add((false, parser, context.Scanner.Cursor.Offset)),
        };

        var result = new ParseResult<T>();
        var success = parse(context, ref result);

        return (success, result.Start, result.End, context.Scanner.Cursor.Offset, trace);
    }
}
