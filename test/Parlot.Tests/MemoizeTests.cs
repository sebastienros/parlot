#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class MemoizeTests
{
    // The outcome of a parse: the value, the result's range and the cursor's position, line and column
    private static string Parse<T>(Parser<T> parser, string text, ParseContext? context = null)
    {
        context ??= new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();
        var success = parser.Parse(context, ref result);
        var cursor = context.Scanner.Cursor.Position;

        return success
            ? $"OK {result.Value} [{result.Start}-{result.End}] at {cursor.Offset}{cursor}"
            : $"FAIL at {cursor.Offset}{cursor}";
    }

    private static Parser<string> Assignment(Parser<string> prefix) => OneOf(
        prefix.And(Terms.Integer()).Then(static x => $"{x.Item1}=int:{x.Item2}"),
        prefix.And(Terms.String()).Then(static x => $"{x.Item1}=string:{x.Item2}"),
        prefix.And(Terms.Identifier()).Then(static x => $"{x.Item1}=id:{x.Item2}"),
        Terms.Text("x"));

    private static Parser<string> Prefix() => Terms.Identifier().AndSkip(Terms.Char('=')).Then(static x => x.ToString());

    [Theory]
    [InlineData("a=1")]
    [InlineData("a = 'b'")]
    [InlineData("\n a\r\n=\n  b ")]
    [InlineData("a = ")]
    [InlineData("a b")]
    [InlineData("x")]
    [InlineData("")]
    public void ReturnsTheResultsOfTheParser(string text)
    {
        Assert.Equal(Parse(Assignment(Prefix()), text), Parse(Assignment(Prefix().Memoize()), text));
    }

    [Fact]
    public void DocumentationSample()
    {
        var target = Separated(Terms.Char('.'), Terms.Identifier()).Then(static x => x.Count).Memoize();

        var assignment = target.AndSkip(Terms.Char('=')).And(Terms.Integer()).Then(static x => $"assign {x.Item1}");
        var call = target.AndSkip(Terms.Char('(')).AndSkip(Terms.Char(')')).Then(static x => $"call {x}");
        var statement = OneOf(assignment, call, target.Then(static x => $"expression {x}")).AndSkip(Terms.Char(';'));

        Assert.Equal("expression 3", statement.Parse("a.b.c;"));
        Assert.Equal("call 2", statement.Parse("a.b();"));
        Assert.Equal("assign 1", statement.Parse("a = 1;"));
    }

    [Fact]
    public void ParsesAPrefixOnce()
    {
        var prefix = new CountingParser<string>(Prefix());
        var parser = Assignment(prefix.Memoize());

        Assert.Equal("OK a=id:b [0-6] at 6(2:5)", Parse(parser, "a\n = b "));
        Assert.Equal(1, prefix.Count);

        // Without memoization, each alternative parses it
        prefix.Count = 0;
        Assert.Equal("OK a=id:b [0-6] at 6(2:5)", Parse(Assignment(prefix), "a\n = b "));
        Assert.Equal(3, prefix.Count);
    }

    [Fact]
    public void MemoizesFailures()
    {
        var prefix = new CountingParser<string>(Prefix());
        var parser = Assignment(prefix.Memoize());

        Assert.Equal("FAIL at 0(1:1)", Parse(parser, "a b"));
        Assert.Equal(1, prefix.Count);

        Assert.Equal("OK x [0-1] at 1(1:2)", Parse(parser, "x"));
        Assert.Equal(2, prefix.Count);
    }

    [Fact]
    public void RestoresTheCursorWhenItFails()
    {
        var parser = Literals.Text("ab").Memoize();
        var context = new ParseContext(new Scanner("ac"));

        // Parsed, then memoized
        Assert.Equal("FAIL at 0(1:1)", Parse(parser, "ac", context));
        Assert.Equal("FAIL at 0(1:1)", Parse(parser, "ac", context));
    }

    [Fact]
    public void RestoresTheEndPosition()
    {
        var prefix = new CountingParser<string>(Terms.Text("a\r\nb\nc"));
        var memoized = prefix.Memoize();
        var parser = memoized.AndSkip(Terms.Char('!')).Or(memoized.AndSkip(Terms.Char('?'))).Or(memoized);

        Assert.Equal("OK a\r\nb\nc [2-8] at 8(4:2)", Parse(parser, " \na\r\nb\nc "));
        Assert.Equal(1, prefix.Count);
    }

    [Fact]
    public void DoesNotShareResultsBetweenContexts()
    {
        var prefix = new CountingParser<string>(Prefix());
        var parser = Assignment(prefix.Memoize());

        Assert.Equal("OK a=id:b [0-5] at 5(1:6)", Parse(parser, "a = b"));
        Assert.Equal("OK a=id:b [0-5] at 5(1:6)", Parse(parser, "a = b"));
        Assert.Equal(2, prefix.Count);

        // The same text in another context
        Assert.Equal("OK c=int:1 [0-3] at 3(1:4)", Parse(parser, "c=1"));
        Assert.Equal(3, prefix.Count);
    }

    [Fact]
    public void DistinguishesTheOffsets()
    {
        var item = new CountingParser<string>(Terms.Identifier().Then(static x => x.ToString()));
        var memoized = item.Memoize();

        // Each item is parsed by both alternatives, at offsets which share an entry of the table
        var text = string.Join(" ", Enumerable.Range(0, 40).Select(static i => new string('a', 1 + i % 3)));
        var parser = ZeroOrMany(memoized.AndSkip(Terms.Char(',')).Or(memoized)).Then(static x => string.Join(" ", x));

        Assert.Equal($"OK {text} [0-{text.Length}] at {text.Length}(1:{text.Length + 1})", Parse(parser, text));

        // The last attempt at the end of the text fails twice too
        Assert.Equal(41, item.Count);
    }

    [Fact]
    public void ReturnsTheSameValue()
    {
        var parser = Terms.Identifier().Then(static x => new List<string> { x.ToString() }).Memoize();
        var both = parser.AndSkip(Terms.Char('!')).Or(parser).And(Always()).Then(static x => x.Item1);
        var context = new ParseContext(new Scanner("a"));
        var first = new ParseResult<List<string>>();
        var second = new ParseResult<List<string>>();

        Assert.True(parser.Parse(context, ref first));
        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        Assert.True(both.Parse(context, ref second));
        Assert.Same(first.Value, second.Value);
    }

    [Fact]
    public void DependsOnTheWhiteSpaceParser()
    {
        // The same parser at the same offset, with and without comments
        var identifier = new CountingParser<TextSpan>(Terms.Identifier());
        var memoized = identifier.Memoize();
        var parser = memoized.AndSkip(Terms.Char('!')).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"))
            .Or(memoized)
            .Then(static x => x.ToString());

        Assert.Equal("FAIL at 0(1:1)", Parse(parser, "/* a */ b"));
        Assert.Equal(2, identifier.Count);

        Assert.Equal("OK b [8-10] at 10(1:11)", Parse(parser, "/* a */ b!"));
    }

    [Fact]
    public void DependsOnTheWhiteSpaceSet()
    {
        var identifier = new CountingParser<TextSpan>(Terms.Identifier());
        var memoized = identifier.Memoize();
        var parser = memoized.AndSkip(Terms.Char('!')).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)
            .Or(memoized)
            .Then(static x => x.ToString());

        // A no-break space is only white space in the default set
        Assert.Equal("OK a [1-2] at 2(1:3)", Parse(parser, " a"));
        Assert.Equal(2, identifier.Count);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("a!")]
    [InlineData("a!!")]
    [InlineData("a?")]
    [InlineData("a!?")]
    [InlineData("b")]
    public void DoesNotMemoizeAStoppedRecursion(string text)
    {
        static Parser<string> Create(bool memoize)
        {
            // The parser invokes itself at its position through d: the loop detection fails the inner invocation,
            // a result which is only valid while d is active
            var d = Deferred<string>();
            var parser = OneOf(d.AndSkip(Literals.Char('!')).Then(static x => x + "!"), Literals.Char('a').Then(static _ => "a"));

            if (memoize)
            {
                parser = parser.Memoize();
            }

            d.Parser = parser;

            return OneOf(d.AndSkip(Literals.Char('?')).Then(static x => x + "?"), parser);
        }

        Assert.Equal(Parse(Create(memoize: false), text), Parse(Create(memoize: true), text));
    }

    [Fact]
    public void DoesNotReuseAResultWhenARecursionWouldBeStopped()
    {
        var d = Deferred<string>();
        d.Parser = Literals.Char('a').Then(static _ => "a");
        var counting = new CountingParser<string>(d);
        var parser = counting.Memoize();
        var context = new ParseContext(new Scanner("a"));

        Assert.Equal("OK a [0-1] at 1(1:2)", Parse(parser, "a", context));
        context.Scanner.Cursor.ResetPosition(TextPosition.Start);

        // d is active at the position, as when it's parsed again at its position, so it would stop the recursion
        Assert.True(context.PushParserAtPosition(d));
        Assert.Equal("FAIL at 0(1:1)", Parse(parser, "a", context));
        Assert.Equal(2, counting.Count);
        context.PopParserAtPosition(d, 0);

        // The recorded result is reused again
        Assert.Equal("OK a [0-1] at 1(1:2)", Parse(parser, "a", context));
        Assert.Equal(2, counting.Count);
    }

    [Fact]
    public void DoesNotReuseAResultWhichDependsOnTheActiveParsers()
    {
        var d = Deferred<string>();
        d.Parser = Literals.Char('a').Then(static _ => "a");
        var parser = new ActiveProbe(d).Memoize();
        var context = new ParseContext(new Scanner("a"));

        Assert.Equal("OK inactive [0-0] at 0(1:1)", Parse(parser, "a", context));

        Assert.True(context.PushParserAtPosition(d));
        Assert.Equal("OK active [0-0] at 0(1:1)", Parse(parser, "a", context));
        context.PopParserAtPosition(d, 0);

        Assert.Equal("OK inactive [0-0] at 0(1:1)", Parse(parser, "a", context));
    }

    [Fact]
    public void ReusesResultsWithoutLoopDetectionWhenOtherParsersAreActive()
    {
        var counting = new CountingParser<string>(Literals.Char('a').Then(static _ => "a"));
        var parser = counting.Memoize();
        var d = Deferred<string>();
        d.Parser = Literals.Char('b').Then(static _ => "b");
        var context = new ParseContext(new Scanner("a"));

        Assert.Equal("OK a [0-1] at 1(1:2)", Parse(parser, "a", context));
        context.Scanner.Cursor.ResetPosition(TextPosition.Start);

        // The parse didn't reach a parser with loop detection, d can't change it
        Assert.True(context.PushParserAtPosition(d));
        Assert.Equal("OK a [0-1] at 1(1:2)", Parse(parser, "a", context));
        Assert.Equal(1, counting.Count);
    }

    [Fact]
    public void MemoizesARecursivePrefix()
    {
        var value = Deferred<int>();
        var group = new CountingParser<int>(Between(Terms.Char('('), value, Terms.Char(')')));
        var memoized = group.Memoize();
        value.Parser = OneOf(
            memoized.AndSkip(Terms.Char('!')).Then(static x => x * 2),
            memoized.AndSkip(Terms.Char('?')).Then(static x => x * 3),
            memoized,
            Terms.Integer().Then(static x => (int)x));

        // value is active at the start of each group, which only reaches it after the parenthesis
        Assert.Equal(6, value.Parse("((((1))!))?"));

        // Once at each parenthesis, and at the integer, instead of 3 times at each level
        Assert.Equal(5, group.Count);
    }

    [Fact]
    public void ReusesResultsWhenTheRecursionStartsAfterThePosition()
    {
        var d = Deferred<string>();
        d.Parser = Literals.Char('a').Then(static _ => "a");
        var counting = new CountingParser<string>(Literals.Char('(').SkipAnd(d).AndSkip(Literals.Char(')')));
        var parser = counting.Memoize();
        var context = new ParseContext(new Scanner("(a)"));

        Assert.Equal("OK a [0-3] at 3(1:4)", Parse(parser, "(a)", context));
        context.Scanner.Cursor.ResetPosition(TextPosition.Start);

        // Like a group in an expression: d is active at the position, but the parse only reached it after the parenthesis
        Assert.True(context.PushParserAtPosition(d));
        Assert.Equal("OK a [0-3] at 3(1:4)", Parse(parser, "(a)", context));
        Assert.Equal(1, counting.Count);
    }

    [Fact]
    public void ForwardsTheSeekableHints()
    {
        var memoized = (ISeekable)Terms.Text("ab").Memoize();

        Assert.True(memoized.CanSeek);
        Assert.Equal(new[] { 'a' }, memoized.ExpectedChars);
        Assert.True(memoized.SkipWhitespace);

        // Read when they are asked for, like the ones of a deferred parser
        var deferred = Deferred<string>();
        var late = (ISeekable)deferred.Memoize();
        var choice = OneOf(deferred.Memoize(), Literals.Text("c"));

        Assert.False(late.CanSeek);
        deferred.Parser = Literals.Text("b");
        Assert.True(late.CanSeek);
        Assert.Equal(new[] { 'b' }, late.ExpectedChars);
        Assert.False(late.SkipWhitespace);

        Assert.Equal("b", choice.Parse("b"));
        Assert.Equal("c", choice.Parse("c"));
        Assert.Null(choice.Parse("d"));
    }

    [Fact]
    public void CallsTheParserHooks()
    {
        var inner = Literals.Text("a");
        var parser = inner.Memoize();
        var choice = parser.AndSkip(Literals.Char('!')).Or(parser);
        var entered = new List<object>();
        var context = new ParseContext(new Scanner("a")) { OnEnterParser = (p, _) => entered.Add(p) };

        Assert.Equal("OK a [0-1] at 1(1:2)", Parse(choice, "a", context));

        // The inner parser is only entered once
        Assert.Equal(2, entered.Count(x => ReferenceEquals(x, parser)));
        Assert.Equal(1, entered.Count(x => ReferenceEquals(x, inner)));
    }

    [Fact]
    public void MovesBackToCommittedTextLikeTheParser()
    {
        static Parser<string> Create(Parser<string> prefix) => OneOf(prefix.AndSkip(Terms.Char(';')), prefix.AndSkip(Terms.Char('.')));

        var parser = Create(Terms.Identifier().Then(static x => x.ToString()).Commit());
        var memoized = Create(Terms.Identifier().Then(static x => x.ToString()).Commit().Memoize());

        Assert.Equal("abc", memoized.Parse("abc."));
        Assert.Equal("abc", memoized.Parse(new StringReader("abc;")));

        // The second alternative moves back to the committed text, which is discarded
        var expected = Assert.Throws<ParseException>(() => parser.Parse(new ChunkedReader("abc.", 1), new StreamParseOptions { BufferSize = 1 }));
        var actual = Assert.Throws<ParseException>(() => memoized.Parse(new ChunkedReader("abc.", 1), new StreamParseOptions { BufferSize = 1 }));
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.Position.Offset, actual.Position.Offset);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(4096)]
    public void ReusesResultsInCompactingStreams(int bufferSize)
    {
        var prefix = new CountingParser<string>(Prefix());
        var parser = ZeroOrMany(Assignment(prefix.Memoize()).AndSkip(Terms.Char(';')));
        var text = string.Concat(Enumerable.Range(0, 20).Select(static i => $"a{i} = {(i % 3 == 0 ? "1" : i % 3 == 1 ? "'s'" : "b")};"));

        var expected = parser.Parse(text)!;
        var count = prefix.Count;
        prefix.Count = 0;

        var actual = parser.Parse(new ChunkedReader(text, 1), new StreamParseOptions { BufferSize = bufferSize })!;

        Assert.Equal(expected, actual);

        // Once for each item, and at the end of the text
        Assert.Equal(21, count);

        // A token which reads the end of the buffer is parsed again with more text, the memoized parser isn't
        Assert.Equal(count, prefix.Count);
    }

    [Fact]
    public async Task ParsesSuccessiveValues()
    {
        var parser = Assignment(Prefix().Memoize());
        var lines = new[] { "a=1", "b = 'c'", "d = e", "x" };
        var values = new List<string>();

        await foreach (var value in parser.ParseManyAsync(new ChunkedReader(string.Join("\n", lines), 1), new StreamParseOptions { BufferSize = 1 }))
        {
            values.Add(value);
        }

        Assert.Equal(lines.Select(line => parser.Parse(line)), values);
    }

    // Tells whether a parser is active at the position, like a custom recursive parser does before it marks itself as active
    private sealed class ActiveProbe : Parser<string>
    {
        private readonly object _parser;

        public ActiveProbe(object parser)
        {
            _parser = parser;
        }

        public override bool Parse(ParseContext context, ref ParseResult<string> result)
        {
            var offset = context.Scanner.Cursor.Offset;
            result.Set(offset, offset, context.IsParserActiveAtPosition(_parser) ? "active" : "inactive");
            return true;
        }
    }

    private sealed class CountingParser<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public CountingParser(Parser<T> parser)
        {
            _parser = parser;
        }

        public int Count { get; set; }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            Count++;
            return _parser.Parse(context, ref result);
        }
    }

    private sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;
        private int _position;

        public ChunkedReader(string text, int chunk)
        {
            _text = text;
            _chunk = chunk;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(Math.Min(count, _chunk), _text.Length - _position);
            _text.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }
    }
}
