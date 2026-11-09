using System;
using System.IO;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class SpanScannerTests
{
    public static TheoryData<string> LiquidInputs() =>
    [
        "", "a", "abc", "_", "1", "123", "123a", "12 a", "a1", "a-", "a-}", "a-%", "a--}", "a-b}", "1-}", "1-a", "1-",
        "-a", "a_b-c d", "abc-", "  x", "a\n", "a}", "a%}", "é", "aé", "a-}}", "{{ a }}",
    ];

    [Theory]
    [MemberData(nameof(LiquidInputs))]
    public void ScanMatchesHandWrittenIdentifier(string input)
    {
        AssertSameResult(new HandWrittenLiquidIdentifier(), Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars), input);
    }

    [Theory]
    [MemberData(nameof(LiquidInputs))]
    public void ScanMatchesHandWrittenIdentifierInSequences(string input)
    {
        // Followed by end tags, in a choice, the seek hints and the dash exclusion take part
        var handWritten = ZeroOrMany(SkipWhiteSpace(new HandWrittenLiquidIdentifier()).Or(Terms.Text("-}}").Then(static _ => new TextSpan("end"))).Or(Terms.Text("}}").Then(static _ => new TextSpan("end"))));
        var scanned = ZeroOrMany(Terms.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars).Or(Terms.Text("-}}").Then(static _ => new TextSpan("end"))).Or(Terms.Text("}}").Then(static _ => new TextSpan("end"))));

        var expected = handWritten.TryParse(input + " a-}} b}}", out var expectedValue);
        Assert.Equal(expected, scanned.TryParse(input + " a-}} b}}", out var value));
        Assert.Equal(string.Join("|", expectedValue), string.Join("|", value));
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("a-}", "a")]
    [InlineData("a-%}", "a")]
    [InlineData("a--}", "a-")]
    [InlineData("a-", "a-")]
    [InlineData("a-b c", "a-b")]
    [InlineData("12a", "12a")]
    [InlineData("12-", "12-")]
    public void ScanReturnsLiquidIdentifier(string input, string expected)
    {
        Assert.Equal(expected, Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars).Parse(input).ToString());
    }

    [Theory]
    [InlineData("123", "123")]
    [InlineData("2foo", "2foo")]
    [InlineData("1-}", "1")]
    [InlineData("a-b-%}", "a-b")]
    public void ScanReturnsLiquidVariableSignature(string input, string expected)
    {
        Assert.Equal(expected, Literals.Scan(LiquidScanners.VariableSignature, LiquidScanners.StartChars).Parse(input).ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("123 ")]
    [InlineData("1-}")]
    [InlineData("-a")]
    [InlineData(" a")]
    public void ScanFailsWithoutMovingTheCursor(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<TextSpan>();

        Assert.False(Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars).Parse(context, ref result));
        Assert.Equal(TextPosition.Start, context.Scanner.Cursor.Position);
    }

    [Fact]
    public void TermsScanSkipsWhiteSpace()
    {
        var parser = Terms.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars);

        Assert.Equal("abc", parser.Parse("  \n abc").ToString());

        var context = new ParseContext(new Scanner("  123 "));
        var result = new ParseResult<TextSpan>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(TextPosition.Start, context.Scanner.Cursor.Position);
    }

    [Fact]
    public void ScanDeclaresStartChars()
    {
        var seekable = (ISeekable)Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars);

        Assert.True(seekable.CanSeek);
        Assert.False(seekable.SkipWhitespace);
        Assert.Equal(LiquidScanners.StartChars.ToCharArray(), seekable.ExpectedChars);

        var term = (ISeekable)Terms.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars);
        Assert.True(term.CanSeek);
        Assert.True(term.SkipWhitespace);

        Assert.False(((ISeekable)Literals.Scan(LiquidScanners.Identifier)).CanSeek);
    }

    [Fact]
    public void ScanTracksLinesAndColumns()
    {
        // Matches up to the first space, new lines included
        var parser = Literals.Scan(static span => span.IndexOf(' ') is var i and >= 0 ? i : span.Length)
            .Then(static (context, span) => (span.ToString(), context.Scanner.Cursor.Position));

        var (text, position) = parser.Parse("ab\ncd\ne f");
        Assert.Equal("ab\ncd\ne", text);
        Assert.Equal(new TextPosition(7, 3, 2), position);

        (text, position) = parser.Parse("abc d");
        Assert.Equal("abc", text);
        Assert.Equal(new TextPosition(3, 1, 4), position);
    }

    public static TheoryData<string> MultiLineTokens() =>
    [
        "ab\ncd\ne f", "ab\ncd", "\nab c", "ab\n c", "ab\n\n\n c", "a\r\nb\rc\n d", "\n", "\n\n",
        new string('a', 70) + "\n" + new string('b', 70) + " x",
        new string('a', 30) + "\n" + new string('b', 40) + "\n\n" + new string('c', 50) + "\n" + new string('d', 60) + " x",
        new string('\n', 100) + "a",
    ];

    [Theory]
    [MemberData(nameof(MultiLineTokens))]
    public void ScanTracksNewLinesLikeAdvancingCharByChar(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<TextSpan>();

        Assert.True(Literals.Scan(UpToSpace).Parse(context, ref result));

        var expected = new Cursor(input);

        for (var i = 0; i < result.Value.Length; i++)
        {
            expected.Advance();
        }

        Assert.Equal(expected.Position, context.Scanner.Cursor.Position);
        Assert.Equal(expected.Eof, context.Scanner.Cursor.Eof);
        Assert.Equal(expected.Current, context.Scanner.Cursor.Current);
    }

    // Matches up to the first space, new lines included
    private static int UpToSpace(ReadOnlySpan<char> span)
    {
        var index = span.IndexOf(' ');

        if (index >= 0)
        {
            return index;
        }

        return span.Length;
    }

    [Fact]
    public void ScanResultHasThePositions()
    {
        var context = new ParseContext(new Scanner("ab cd"));
        context.Scanner.Cursor.AdvanceNoNewLines(3);
        var result = new ParseResult<TextSpan>();

        Assert.True(Literals.Scan(LiquidScanners.Identifier).Parse(context, ref result));
        Assert.Equal(3, result.Start);
        Assert.Equal(5, result.End);
        Assert.Equal("cd", result.Value.ToString());
    }

    [Fact]
    public void ScanRejectsTokensLongerThanTheText()
    {
        var parser = Literals.Scan(static span => span.Length + 1);

        Assert.Throws<InvalidOperationException>(() => parser.Parse("abc"));
    }

    [Fact]
    public void ScanCanCaptureState()
    {
        // At runtime, unlike in generated parsers, a scanner can be any delegate
        var terminator = ';';
        var parser = Literals.Scan(span => span.IndexOf(terminator) is var i and >= 0 ? i : 0);

        Assert.Equal("abc", parser.Parse("abc;").ToString());
        Assert.False(parser.TryParse("abc", out _));
    }

    [Fact]
    public void ScanRequiresAScanner()
    {
        Assert.Throws<ArgumentNullException>(() => Literals.Scan((SpanScanner)null!));
        Assert.Throws<ArgumentNullException>(() => Literals.Scan((TextScanner)null!));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123a")]
    [InlineData("abc-")]
    [InlineData("abc-}")]
    [InlineData("1-a")]
    public void ScanMarksTheEndOfNonFinalText(string input)
    {
        // A prefix of a non-final buffer is inconclusive when more text could change the token
        for (var length = 1; length < input.Length; length++)
        {
            var context = new ParseContext(new Scanner(input.Substring(0, length), isFinal: false));
            var cursor = context.Scanner.Cursor;
            var result = new ParseResult<TextSpan>();

            var success = Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars).Parse(context, ref result);
            var expected = Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars).TryParse(input, out var value);

            if (!cursor.HitEnd)
            {
                Assert.Equal(expected, success);
                Assert.Equal(expected ? value.ToString() : "", success ? result.Value.ToString() : "");
            }
        }
    }

    [Theory]
    [MemberData(nameof(LiquidInputs))]
    public void ScanReadsOneCharAtATime(string input)
    {
        var text = input + " a-}} b}}";

        var expected = _liquidTokens.TryParse(text, out var expectedValue);
        var options = new StreamParseOptions { BufferSize = 1 };

        Assert.Equal(expected, _liquidTokens.TryParse(new OneCharReader(text), out var value, options));
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("é")]
    [InlineData("a%}")]
    [InlineData("-a")]
    public void ScanReadsOneCharAtATimeComparesFailures(string input)
    {
        // Some inputs of ScanReadsOneCharAtATime aren't only tokens, so failures are compared too
        Assert.False(_liquidTokens.TryParse(input + " a-}} b}}", out _));
    }

    [Theory]
    [InlineData("-->", "-->")]
    [InlineData("--> a comment\nx", "--> a comment")]
    [InlineData("-->\r\nx", "-->")]
    public void TextScanMatchesAtTheStartOfTheInput(string input, string expected)
    {
        Assert.Equal(expected, Literals.Scan(LineStartScanners.HtmlCloseComment, "-").Parse(input).ToString());
    }

    [Theory]
    [InlineData("x\n--> c", 2, "--> c")]
    [InlineData("x\r\n-->", 3, "-->")]
    [InlineData("x\r-->", 2, "-->")]
    public void TextScanMatchesAtTheStartOfALine(string input, int offset, string expected)
    {
        var context = new ParseContext(new Scanner(input));
        context.Scanner.Cursor.Advance(offset);
        var result = new ParseResult<TextSpan>();

        Assert.True(Literals.Scan(LineStartScanners.HtmlCloseComment, "-").Parse(context, ref result));
        Assert.Equal(expected, result.Value.ToString());
        Assert.Equal(offset, result.Start);
        Assert.Equal(offset + expected.Length, result.End);
        Assert.Equal(offset + expected.Length, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("x-->y", 1)]
    [InlineData("x -->", 2)]
    [InlineData("x\n-->", 0)]
    [InlineData("x\n->", 2)]
    [InlineData("--", 0)]
    [InlineData("", 0)]
    public void TextScanFailsWithoutMovingTheCursor(string input, int offset)
    {
        var context = new ParseContext(new Scanner(input));
        context.Scanner.Cursor.Advance(offset);
        var position = context.Scanner.Cursor.Position;
        var result = new ParseResult<TextSpan>();

        Assert.False(Literals.Scan(LineStartScanners.HtmlCloseComment, "-").Parse(context, ref result));
        Assert.Equal(position, context.Scanner.Cursor.Position);
    }

    [Fact]
    public void TextScanGetsTheWholeTextAndThePosition()
    {
        var parser = Literals.Scan(static (text, position) => text.ToString() == "ab cd" && position == 3 ? 2 : 0);

        var context = new ParseContext(new Scanner("ab cd"));
        var result = new ParseResult<TextSpan>();
        Assert.False(parser.Parse(context, ref result));

        context.Scanner.Cursor.Advance(3);
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("cd", result.Value.ToString());
        Assert.True(context.Scanner.Cursor.Eof);
    }

    [Fact]
    public void TermsTextScanSkipsWhiteSpaceBeforeTheToken()
    {
        // The white space is part of the text before the token
        var parser = Terms.Scan(LineStartScanners.HtmlCloseComment, "-");

        Assert.Equal("-->", parser.Parse(" \n-->").ToString());
        Assert.False(parser.TryParse(" -->", out _));

        Assert.True(((ISeekable)parser).CanSeek);
        Assert.True(((ISeekable)parser).SkipWhitespace);
        Assert.Equal(['-'], ((ISeekable)parser).ExpectedChars);
    }

    [Fact]
    public void TextScanMarksTheEndOfNonFinalText()
    {
        var context = new ParseContext(new Scanner("--", isFinal: false));
        var result = new ParseResult<TextSpan>();

        Assert.False(Literals.Scan(LineStartScanners.HtmlCloseComment, "-").Parse(context, ref result));
        Assert.True(context.Scanner.Cursor.HitEnd);
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void TextScanRejectsTokensLongerThanTheText()
    {
        var parser = Literals.Scan(static (text, position) => text.Length - position + 1);

        Assert.Throws<InvalidOperationException>(() => parser.Parse("abc"));
    }

    public static TheoryData<string> HtmlCommentInputs() =>
    [
        "", "-->", "--> c", "-->\n", "x-->y", "x --> y", "x\n-->y", "x\n--> y\nz", "x\r\n--> y", "x\r-->",
        "--> a\n--> b\nx-->y", "x\n \n-->", "x\n\n-->", "a--\n-->", "a--\n>", "--", "x\n--", "-->-->",
        "abc def\n-->\nghi-->jkl",
    ];

    [Theory]
    [MemberData(nameof(HtmlCommentInputs))]
    public void TextScanReadsTheTextBeforeTheTokenOneCharAtATime(string input)
    {
        // The char before the token is kept in the stream buffer, even when the buffer is refilled one char at a time
        var expected = _htmlTokens.TryParse(input, out var expectedValue);
        var options = new StreamParseOptions { BufferSize = 1 };

        Assert.Equal(expected, _htmlTokens.TryParse(new OneCharReader(input), out var value, options));
        Assert.Equal(expectedValue, value);
    }

    [Theory]
    [InlineData("x\n-->y", "x|comment")]
    [InlineData("x-->y", "x|--|>|y")]
    [InlineData("-->y\nx", "comment|x")]
    [InlineData("x --> y", "x|--|>|y")]
    public void TextScanTellsTheStartOfALine(string input, string expected)
    {
        Assert.Equal(expected, _htmlTokens.Parse(input));
        Assert.Equal(expected, _htmlTokens.Parse(new OneCharReader(input), new StreamParseOptions { BufferSize = 1 }));
    }

    [Fact]
    public async System.Threading.Tasks.Task TextScanSeesTheStartOfEachValueOfParseMany()
    {
        // Each value is parsed as its own input: the second "-->" follows a ';' in the stream, but starts its value
        var list = new System.Collections.Generic.List<string>();

        await foreach (var value in Literals.Scan(LineStartScanners.HtmlCloseComment, "-").Then(static x => x.ToString()).ParseManyAsync(new StringReader("-->;-->"), ';', cancellationToken: TestContext.Current.CancellationToken))
        {
            list.Add(value);
        }

        Assert.Equal(["-->", "-->"], list);
    }

    // The JavaScript tokens around an HTML-like close comment
    private static readonly Parser<string> _htmlTokens =
        ZeroOrMany(Terms.Scan(LineStartScanners.HtmlCloseComment, "-").Then(static _ => "comment")
            .Or(Terms.Text("--"))
            .Or(Terms.Text(">"))
            .Or(Terms.Identifier().Then(static x => x.ToString())))
            .Then(static list => string.Join("|", list))
            .Eof();

    // Eof makes the parse fail when the tokens don't cover the text
    private static readonly Parser<string> _liquidTokens =
        ZeroOrMany(Terms.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars).Or(Terms.Text("-}}").Then(static _ => new TextSpan("end"))).Or(Terms.Text("}}").Then(static _ => new TextSpan("end"))))
            .Then(static list => string.Join("|", list))
            .Eof();

    private static void AssertSameResult(Parser<TextSpan> expectedParser, Parser<TextSpan> parser, string input)
    {
        var expectedContext = new ParseContext(new Scanner(input));
        var expectedResult = new ParseResult<TextSpan>();
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<TextSpan>();

        Assert.Equal(expectedParser.Parse(expectedContext, ref expectedResult), parser.Parse(context, ref result));
        Assert.Equal(expectedResult.Value.ToString(), result.Value.ToString());
        Assert.Equal(expectedResult.Start, result.Start);
        Assert.Equal(expectedResult.End, result.End);
        Assert.Equal(expectedContext.Scanner.Cursor.Position, context.Scanner.Cursor.Position);
    }

    private sealed class OneCharReader : TextReader
    {
        private readonly string _text;
        private int _position;

        public OneCharReader(string text)
        {
            _text = text;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position == _text.Length || count == 0)
            {
                return 0;
            }

            buffer[index] = _text[_position++];
            return 1;
        }

#if NET8_0_OR_GREATER
        public override int Read(Span<char> buffer)
        {
            if (_position == _text.Length || buffer.IsEmpty)
            {
                return 0;
            }

            buffer[0] = _text[_position++];
            return 1;
        }
#endif
    }
}
