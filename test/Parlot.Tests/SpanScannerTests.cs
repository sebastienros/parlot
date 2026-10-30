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
        Assert.Throws<ArgumentNullException>(() => Literals.Scan(null!));
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
