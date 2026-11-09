#nullable enable

using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A <see cref="ParseContext.WhiteSpaceParser"/> that tells the chars it can start with isn't invoked before the other chars.
/// </summary>
public class WhiteSpaceParserHintsTests
{
    // Tokens which don't start white space or a comment, separated by white space and comments or not
    private static Parser<IReadOnlyList<TextSpan>> Tokens() => ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf(".();", maxSize: 1)));

    private static Parser<TextSpan> Comments() => new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/").Build();

    // The same white space and comments, with a parser that can start with any char
    private static Parser<TextSpan> CommentsWithoutHints() => Capture(ZeroOrMany(OneOf(
        Literals.WhiteSpace(includeNewLines: true), Literals.Comments("//"), Literals.Comments("/*", "*/"), Literals.Pattern(static c => c == '\u0001'))));

    // Parses the text, with the offsets where the white space parser was entered
    private static (IReadOnlyList<TextSpan>? Value, List<int> Offsets) Parse(Parser<IReadOnlyList<TextSpan>> grammar, object whiteSpaceParser, string text, Parser<TextSpan>? contextWhiteSpaceParser = null)
    {
        var offsets = new List<int>();
        var context = new ParseContext(new Scanner(text))
        {
            WhiteSpaceParser = contextWhiteSpaceParser,
            OnEnterParser = (parser, c) =>
            {
                if (ReferenceEquals(parser, whiteSpaceParser))
                {
                    offsets.Add(c.Scanner.Cursor.Offset);
                }
            },
        };

        return (grammar.TryParse(context, out var value, out _) ? value : null, offsets);
    }

    [Theory]
    [InlineData("a.b(c);")]
    [InlineData("a .b( c ) ;")]
    [InlineData("a/*x*/.b// y\n(c)\n;")]
    [InlineData("  a")]
    [InlineData("")]
    public void IsOnlyInvokedBeforeItsChars(string text)
    {
        var comments = Comments();
        var (value, offsets) = Parse(Tokens().WithWhiteSpaceParser(comments), comments, text);

        Assert.NotNull(value);

        // Before white space and comments, and at the end of the text, which a stream needs it to look at
        Assert.All(offsets, offset => Assert.True(offset == text.Length || text[offset] is ' ' or '\n' or '/', $"Invoked at {offset}"));
        Assert.Contains(text.Length, offsets);

        if (!text.Any(static c => c is ' ' or '\n' or '/'))
        {
            Assert.Equal([text.Length], offsets);
        }
    }

    [Fact]
    public void SkipsTheSameTextAsWithoutHints()
    {
        const string text = "a/*x*/.b // y\n( c )\n;/* end */";

        var expected = Tokens().WithWhiteSpaceParser(CommentsWithoutHints()).Parse(text)!.Select(static t => t.ToString()).ToArray();

        Assert.Equal(["a", ".", "b", "(", "c", ")", ";"], expected);
        Assert.Equal(expected, Tokens().WithWhiteSpaceParser(Comments()).Parse(text)!.Select(static t => t.ToString()));
        Assert.Equal(expected, Tokens().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/")).Parse(text)!.Select(static t => t.ToString()));
    }

    [Fact]
    public void WithoutHintsIsInvokedBeforeEveryToken()
    {
        var whiteSpace = Capture(ZeroOrMany(Literals.Pattern(static c => c == ' ')));
        var (value, offsets) = Parse(Tokens().WithWhiteSpaceParser(whiteSpace), whiteSpace, "a.b");

        Assert.Equal(3, value!.Count);
        Assert.Equal([0, 1, 2, 3], offsets);
    }

    [Fact]
    public void HintsAreReadThroughOptionalParsers()
    {
        // A white space parser which can be empty has no hints, but it only reads where its parser does
        var whiteSpace = Capture(ZeroOrOne(Literals.Char(' ').Optional()));
        var (value, offsets) = Parse(Tokens().WithWhiteSpaceParser(whiteSpace), whiteSpace, "a. b");

        Assert.Equal(3, value!.Count);
        Assert.Equal([2, 4], offsets);
    }

    [Fact]
    public void IsInvokedBeforeAnyCharWhenAChoiceCanStartWithAnyChar()
    {
        var whiteSpace = Capture(OneOrMany(OneOf(Literals.AnyOf(" "), Literals.Pattern(static c => c == '#'))));
        var (value, offsets) = Parse(Tokens().WithWhiteSpaceParser(whiteSpace), whiteSpace, "a#.b");

        Assert.Equal(3, value!.Count);
        Assert.Equal([0, 1, 3, 4], offsets);
    }

    [Theory]
    [InlineData("a\u00A0.\u3000b", 3, new[] { 1, 3, 5 })]
    [InlineData("a\u00A0\u3000", 1, new[] { 1 })]
    [InlineData("a\u00A0\u00E9", 1, new[] { 1 })]
    [InlineData("a\u00E9", 1, new int[0])]
    public void ReadsNonAsciiChars(string text, int count, int[] expected)
    {
        // The white space of the default set, which is above ASCII too. An identifier doesn't start with \u00E9, but the
        // white space parser isn't invoked before it.
        var whiteSpace = Literals.WhiteSpace(includeNewLines: true);
        var (value, offsets) = Parse(Tokens().WithWhiteSpaceParser(whiteSpace), whiteSpace, text);

        Assert.Equal(count, value!.Count);
        Assert.Equal(expected, offsets);
    }

    // Skips '~' and any char above ASCII
    private static int SkipTildeAndNonAscii(ReadOnlySpan<char> span)
    {
        var i = 0;
        while (i < span.Length && (span[i] == '~' || span[i] >= 0x80))
        {
            i++;
        }

        return i;
    }

    [Theory]
    [InlineData("a~b")]
    [InlineData("a\u00E9b")]
    [InlineData("a\u3000~\uFFFFb")]
    [InlineData("a\uFFFFb")]
    public void IsInvokedBeforeAnyNonAsciiCharWithTheNonAsciiHint(string text)
    {
        // OneOf<T>.NonAsciiSeekableChar stands for any char above ASCII, not for U+FFFF only
        var whiteSpace = Literals.Scan(SkipTildeAndNonAscii, "~" + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar);
        var grammar = Terms.Text("a").And(Terms.Text("b")).Then(static x => (IReadOnlyList<TextSpan>)[new TextSpan(x.Item1), new TextSpan(x.Item2)]);
        var (value, offsets) = Parse(grammar.WithWhiteSpaceParser(whiteSpace), whiteSpace, text);

        Assert.NotNull(value);
        Assert.Equal([1], offsets);

        // It isn't invoked before an ASCII char it can't start with
        var (other, otherOffsets) = Parse(grammar.WithWhiteSpaceParser(whiteSpace), whiteSpace, "a#b");
        Assert.Null(other);
        Assert.Empty(otherOffsets);
    }

    [Fact]
    public void ContextWhiteSpaceParserUsesItsHints()
    {
        var comments = Comments();
        var (value, offsets) = Parse(Tokens(), comments, "a.b /* c */ (d)", contextWhiteSpaceParser: comments);

        Assert.Equal(6, value!.Count);
        Assert.Equal([3, 15], offsets);
    }

    [Fact]
    public void ContextWhiteSpaceParserCanBeChanged()
    {
        var context = new ParseContext(new Scanner("#a b")) { WhiteSpaceParser = Capture(OneOrMany(Literals.Char('#'))) };

        Assert.True(Terms.Identifier().TryParse(context, out var a, out _));
        Assert.Equal("a", a.ToString());

        // The hints of the previous parser don't apply to this one
        context.WhiteSpaceParser = Capture(OneOrMany(Literals.Char(' ')));

        Assert.True(Terms.Identifier().TryParse(context, out var b, out _));
        Assert.Equal("b", b.ToString());

        context.WhiteSpaceParser = null;
        context.Scanner.Cursor.ResetPosition(0);

        Assert.False(Terms.Identifier().TryParse(context, out _, out _));
    }

    [Fact]
    public void NestedWhiteSpaceParsersRestoreTheirHints()
    {
        // The inner parser skips '#', the outer one ' ': each token is skipped with the parser of its scope
        var inner = Terms.Char('(').SkipAnd(ZeroOrMany(Terms.Identifier()).WithWhiteSpaceParser(Capture(OneOrMany(Literals.Char('#'))))).AndSkip(Terms.Char(')'));
        var grammar = ZeroOrMany(OneOf(inner, Terms.Identifier().Then(static x => (IReadOnlyList<TextSpan>)new[] { x }))).WithWhiteSpaceParser(Capture(OneOrMany(Literals.Char(' '))));

        var value = grammar.Parse("a (#b#c) d");

        Assert.Equal(["a", "b,c", "d"], value!.Select(static x => string.Join(",", x.Select(static t => t.ToString()))));
        Assert.False(grammar.Eof().TryParse("a (b c)", out _));
        Assert.False(grammar.Eof().TryParse("a#(b)", out _));
    }

    [Theory]
    [InlineData("  /x")]
    [InlineData("/* a */ /")]
    [InlineData("// a\n.")]
    [InlineData(".")]
    public void RestoresTheCursorOnFailure(string text)
    {
        var parser = Terms.Char(';').WithWhiteSpaceParser(Comments());
        var context = new ParseContext(new Scanner(text));

        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a /")]
    [InlineData("a /* b")]
    [InlineData("a #")]
    [InlineData("a.b /")]
    [InlineData("a.b /* c */ #")]
    public void ReportsTheSameFailure(string text)
    {
        var withHints = Tokens().WithWhiteSpaceParser(Comments()).Eof().ElseError("Expected a token");
        var withoutHints = Tokens().WithWhiteSpaceParser(CommentsWithoutHints()).Eof().ElseError("Expected a token");

        Assert.False(withoutHints.TryParse(text, out _, out var expected));
        Assert.False(withHints.TryParse(text, out _, out var error));
        Assert.Equal(expected!.Position, error!.Position);
        Assert.Equal(expected.Message, error.Message);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(3, 4)]
    public void StreamsInvokeItOnlyBeforeItsChars(int chunk, int bufferSize)
    {
        const string text = "a.b /* c */ (d)// e\n;";
        var comments = Comments();
        var offsets = new List<int>();
        var options = new StreamParseOptions
        {
            BufferSize = bufferSize,
            ContextFactory = (scanner, ct) => new ParseContext(scanner, ct)
            {
                OnEnterParser = (parser, c) =>
                {
                    if (ReferenceEquals(parser, comments))
                    {
                        offsets.Add(c.Scanner.Cursor.Offset);
                    }
                },
            },
        };

        var value = Tokens().WithWhiteSpaceParser(comments).Parse(new ChunkedReader(text, chunk), options);

        Assert.Equal(["a", ".", "b", "(", "d", ")", ";"], value!.Select(static t => t.ToString()));

        // A token can be parsed again after a refill, the white space parser too, but never before another char
        Assert.All(offsets, offset => Assert.True(offset == text.Length || text[offset] is ' ' or '\n' or '/', $"Invoked at {offset}"));
        Assert.Contains(3, offsets);
        Assert.Contains(15, offsets);
    }

    // Returns the text in chunks of a fixed size
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

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;
    }
}
