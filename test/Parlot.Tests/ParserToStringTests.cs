using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class ParserToStringTests
{
    // The grammar of issue #590: each level refers to the next one twice, which doubled the text with each level
    private static Deferred<long> Levels(int levels)
    {
        var expression = Deferred<long>();
        var primary = OneOf(Literals.Integer(), Between(Literals.Char('('), expression, Literals.Char(')')));

        Parser<long> level = primary;

        for (var i = 0; i < levels; i++)
        {
            var operand = Deferred<long>();
            operand.Parser = level;
            level = operand.And(ZeroOrMany(Literals.Char('+').SkipAnd(operand))).Then(static x => x.Item1 + x.Item2.Sum());
        }

        expression.Parser = level;

        return expression;
    }

    [Fact]
    public void SharedParsersArePrintedOnce()
    {
        var lengths = new[] { 10, 11, 12, 13 }.Select(static levels => Levels(levels).ToString().Length).ToArray();

        // Each level adds the same text
        var step = lengths[1] - lengths[0];
        Assert.InRange(step, 1, 100);
        Assert.Equal(step, lengths[2] - lengths[1]);
        Assert.Equal(step, lengths[3] - lengths[2]);
    }

    [Fact]
    public void SharedDeferredIsWrittenAsReference()
    {
        // The expression is #1, where Between reaches it again, and the operands #2 and #3, which are invoked twice
        Assert.Equal(
            "#1:(#2:(#3:(NumberLiteral | Between(Char('('),#1,Char(')')) on [( + - 0 1 2 3 4 5 6 7 8 9] (Deferred))"
            + " & Char('+') & #3 (skip)* (Then) (Deferred))"
            + " & Char('+') & #2 (skip)* (Then) (Deferred))",
            Levels(2).ToString());
    }

    [Fact]
    public void LargeGrammarIsTruncated()
    {
        var text = Levels(1_000).ToString();

        Assert.Equal(4096, text.Length);
        Assert.EndsWith("...", text);

        // A wrapper doesn't write past the length either
        text = SkipWhiteSpace(Levels(1_000)).ToString();

        Assert.Equal(4096, text.Length);
        Assert.EndsWith("...", text);
    }

    [Fact]
    public void DeepGrammarDoesNotOverflowTheStack()
    {
        var parser = Literals.Char('a').Then(static x => x);

        for (var i = 0; i < 200_000; i++)
        {
            parser = parser.Then(static x => x);
        }

#if NET8_0_OR_GREATER
        var allocated = GC.GetAllocatedBytesForCurrentThread();
#endif
        var text = parser.ToString();

        // The 500 parsers above the cut are written around it
        Assert.Equal("..." + string.Concat(Enumerable.Repeat(" (Then)", 500)), text);

#if NET8_0_OR_GREATER
        // Written in one builder, the parsers below the cut aren't formatted
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - allocated, 0, 1_000_000);
#endif
    }

    [Fact]
    public void SharedTermsAreWrittenEachTime()
    {
        var comma = Terms.Char(',');

        Assert.Equal("Char(',') (Skip WS) & Char(',') (Skip WS)", comma.And(comma).ToString());
    }

    [Fact]
    public void CycleTerminates()
    {
        var expression = Deferred<char>();
        expression.Parser = Literals.Char('a').Or(Between(Literals.Char('('), expression, Literals.Char(')')));

        Assert.Equal("#1:(Char('a') | Between(Char('('),#1,Char(')')) on [( a] (Deferred))", expression.ToString());

        // From the choice, the Deferred is printed once, and its parser, the choice, is a reference
        Assert.Equal("#1:(Char('a') | Between(Char('('),#1 (Deferred),Char(')')) on [( a])", expression.Parser.ToString());
    }

    [Fact]
    public void SharedParserIsWrittenAsReference()
    {
        var pair = Literals.Char('a').And(Literals.Char('b'));
        var parser = pair.And(pair);

        Assert.Equal("#1:(Char('a') & Char('b')) & #1 ", parser.ToString());

        pair.Named("pair");

        Assert.Equal("pair:(Char('a') & Char('b')) & pair ", parser.ToString());
    }

    [Fact]
    public void ReferencesAreNumberedPerCall()
    {
        var a = ZeroOrMany(Literals.Char('a'));
        var b = ZeroOrMany(Literals.Char('b'));
        var parser = a.And(b).And(a).And(b);

        const string expected = "#1:(Char('a')*) & #2:(Char('b')*) & #1  & #2 ";
        Assert.Equal(expected, parser.ToString());
        Assert.Equal(expected, parser.ToString());

        // A parser printed by a call isn't a reference in the next one
        Assert.Equal("Char('a')*", a.ToString());
    }

    [Fact]
    public void UnsharedParsersKeepTheirText()
    {
        var parser = Literals.Char('a').And(ZeroOrMany(Literals.Text("b"))).Then(static x => x.Item1);

        Assert.Equal("Char('a') & Text(\"b\")* (Then)", parser.ToString());
    }

    [Fact]
    public void FailedToStringDoesNotLeakIntoTheNextOne()
    {
        var parser = ZeroOrMany(new ThrowingParser());

        Assert.Throws<InvalidOperationException>(() => parser.ToString());

        // Through an adapter of an IParser<T>, whose text isn't written with the others
        IParser<char> throwing = new ThrowingIParser();
        Assert.Throws<InvalidOperationException>(() => (Literals.Char('a') | throwing).ToString());

        var shared = ZeroOrMany(Literals.Char('a'));
        Assert.Equal("Char('a')*", shared.ToString());
        Assert.Equal("#1:(Char('a')*) & #1", shared.And(shared).ToString());
    }

    [Fact]
    public void CycleThroughCustomParserTerminates()
    {
        var deferred = Deferred<char>();
        deferred.Parser = new WrapParser(deferred);

        // The custom parser writes the text of the Deferred apart, which can't refer to it
        Assert.Equal("Wrap(...) (Deferred)", deferred.ToString());

        deferred.Named("d");
        Assert.Equal("d (Deferred)", deferred.ToString());
    }

    [Fact]
    public void CycleThroughCustomParserTerminatesAtTheFirstTrackedParser()
    {
        // The custom parser invokes a wrapper, which isn't tracked, then a parser reached for the first time
        var deferred = Deferred<char>();
        deferred.Parser = Literals.Char('a').And(new WrapParser(SkipWhiteSpace(deferred))).Then(static x => x.Item1);

        Assert.Equal("Char('a') & Wrap(... (Skip WS)) (Then) (Deferred)", deferred.ToString());
    }

    [Fact]
    public void TextOfTheMaxLengthIsCutWithEllipsis()
    {
        // Text("...") fills the 4,096 chars, the rest of the text is cut
        var text = Literals.Text(new string('b', 4088)).And(Literals.Char('z')).ToString();

        Assert.Equal(4096, text.Length);
        Assert.EndsWith("...", text);
    }

    [Fact]
    public void CutDoesNotSplitSurrogatePairs()
    {
        var x = ZeroOrMany(Literals.Char('a'));

        for (var count = 2_025; count < 2_040; count++)
        {
            var text = x.And(Literals.Text(string.Concat(Enumerable.Repeat("\U0001F600", count)))).And(x).ToString();

            Assert.False(char.IsHighSurrogate(text[text.Length - 4]), $"{count}");
        }
    }

    [Fact]
    public void CutDoesNotSplitLabels()
    {
        var x = ZeroOrMany(Literals.Char('a'));

        // Around the length where the labels push the reference at the end past the cut
        for (var length = 4_060; length < 4_071; length++)
        {
            var text = x.And(Literals.Text(new string('b', length))).And(x).ToString();

            Assert.InRange(text.Length, 0, 4096);
            Assert.StartsWith("#1:(Char('a')*) & ", text);
            // Either the whole reference or none of it
            Assert.True(text.EndsWith("& #1 ", StringComparison.Ordinal) || text.EndsWith("...", StringComparison.Ordinal), text);
            Assert.DoesNotContain("#...", text);
        }
    }

    [Fact]
    public void CustomParserIsWrittenAsItsToString()
    {
        var shared = ZeroOrMany(Literals.Char('a'));
        var custom = new PairParser(shared, shared);

        // The parsers a custom parser writes are written in full in its text, which is written where it is reached
        Assert.Equal("Char('a')* / Char('a')*", custom.ToString());
        Assert.Equal("Char('a')* / Char('a')* & Char('a')* / Char('a')* & #1:(Char('a')*)  & #1 ", custom.And(custom).And(shared).And(shared).ToString());
    }

    [Fact]
    public void ToStringCalledByAParserIsWrittenInFull()
    {
        var shared = ZeroOrMany(Literals.Char('a'));

        // The ToString() which SpyParser calls isn't part of the text which invokes it
        Assert.Equal("Char('a')* & Spy(Char('a')*)", shared.And(new SpyParser(shared)).ToString());
    }

    [Fact]
    public void SameNamesAreToldApart()
    {
        var a = ZeroOrMany(Literals.Char('a')).Named("x");
        var b = ZeroOrMany(Literals.Char('b')).Named("x");
        var c = ZeroOrMany(Literals.Char('c')).Named("");
        var d = ZeroOrMany(Literals.Char('d')).Named("#2");

        // The name of b and d is taken, c has none
        Assert.Equal(
            "x:(Char('a')*) & x#1:(Char('b')*) & #2:(Char('c')*)  & #2#3:(Char('d')*)  & x  & x#1  & #2  & #2#3",
            a.And(b).And(c).And(d).And(a).And(b).And(c).And(d).ToString());
    }

    [Fact]
    public void DeepGrammarIsCutAndContinues()
    {
        var parser = Literals.Char('a').Then(static x => x);

        for (var i = 0; i < 200_000; i++)
        {
            parser = parser.Then(static x => x);
        }

        var shared = ZeroOrMany(Literals.Char('b'));

        // The parsers after the cut are written, and reference the ones before
        Assert.EndsWith(" (Then) & #1:(Char('b')*) & #1 ", parser.And(shared).And(shared).ToString());
    }

    [Fact]
    public void ReferencesAfterTheCutHaveLabels()
    {
        // A reference near the end of the text, which the labels push past it
        var x = ZeroOrMany(Literals.Char('a')).Named("x");
        var y = ZeroOrMany(Literals.Char('b'));

        var parser = x.AndSkip(y);

        for (var i = 0; i < 339; i++)
        {
            parser = parser.AndSkip(x);
        }

        var text = parser.AndSkip(y).ToString();

        Assert.Equal(text.Contains("#1"), text.Contains("#1:("));
    }

    private sealed class SpyParser : Parser<char>
    {
        private readonly object _inner;

        public SpyParser(object inner)
        {
            _inner = inner;
        }

        public override bool Parse(ParseContext context, ref ParseResult<char> result) => false;

        public override string ToString() => "Spy(" + _inner.ToString() + ")";
    }

    private sealed class ThrowingIParser : IParser<char>
    {
        public bool Parse(ParseContext context, out int start, out int end, out object value)
        {
            start = end = 0;
            value = null!;
            return false;
        }

        public override string ToString() => throw new InvalidOperationException();
    }

    private sealed class WrapParser : Parser<char>
    {
        private readonly Parser<char> _inner;

        public WrapParser(Parser<char> inner)
        {
            _inner = inner;
        }

        public override bool Parse(ParseContext context, ref ParseResult<char> result) => false;

        public override string ToString() => $"Wrap({_inner})";
    }

    private sealed class ThrowingParser : Parser<char>
    {
        public override bool Parse(ParseContext context, ref ParseResult<char> result) => false;

        public override string ToString() => throw new InvalidOperationException();
    }

    private sealed class PairParser : Parser<char>
    {
        private readonly Parser<IReadOnlyList<char>> _first;
        private readonly Parser<IReadOnlyList<char>> _second;

        public PairParser(Parser<IReadOnlyList<char>> first, Parser<IReadOnlyList<char>> second)
        {
            _first = first;
            _second = second;
        }

        public override bool Parse(ParseContext context, ref ParseResult<char> result) => false;

        public override string ToString() => $"{_first} / {_second}";
    }

}
