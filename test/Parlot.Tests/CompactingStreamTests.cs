#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Parlot.Fluent;
using Parlot.Tests.Json;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// Tests the parsing of readers through a compacting buffer. <see cref="StreamingTests"/> compares its results with the parsing of strings.
/// </summary>
public class CompactingStreamTests
{
    [Fact]
    public void RootArraysKeepTheBufferBounded()
    {
        const int count = 100_000;

        var text = new StringBuilder("[");

        for (var i = 0; i < count; i++)
        {
            text.Append(i == 0 ? "" : ",").Append("{\"id\":\"").Append(i).Append("\",\"name\":\"").Append('n', i % 32).Append("\"}");
        }

        text.Append(']');

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 64 });
        var result = (JsonArray)JsonParser.Json.Parse(new ChunkedReader(text.ToString(), 7), options)!;

        Assert.Equal(count, result.Elements.Count);
        Assert.Equal("{\"id\":\"99999\",\"name\":\"" + new string('n', 99_999 % 32) + "\"}", result.Elements[count - 1].ToString());

        // The largest element has 61 chars
        Assert.InRange(peak(), 1, 256);
    }

    [Fact]
    public void RepetitionsKeepTheBufferBounded()
    {
        var parser = ZeroOrMany(Terms.Identifier()).Eof();
        var text = string.Join(" ", Enumerable.Range(0, 10_000).Select(static i => "id" + i));

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 16 });
        var result = parser.Parse(new ChunkedReader(text, 3), options)!;

        Assert.Equal(10_000, result.Count);
        Assert.Equal("id9999", result[9_999].ToString());
        Assert.InRange(peak(), 1, 64);
    }

    [Fact]
    public void NewLinesAreFoundAcrossRefills()
    {
        // The white space is searched as a token: a new line at its end is found once its start was refilled away
        var parser = NewLineTests.CreateStatements();
        var spaces = new string(' ', 100);
        var text = string.Join("", Enumerable.Range(0, 1_000).Select(i => "a" + i + (i % 2 == 0 ? spaces + "\n" : " ;")));

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 16 });
        var result = parser.Parse(new ChunkedReader(text, 3), options)!;

        Assert.Equal(string.Join("|", Enumerable.Range(0, 1_000).Select(static i => "a" + i)), result);

        // The white space is kept while it's searched, not the statements before it
        Assert.InRange(peak(), 1, 256);

        // Without the new line the statements need a semicolon
        Assert.Null(parser.Parse(new ChunkedReader("a" + spaces + "b", 3), options));
        Assert.Equal("return|a", parser.Parse(new ChunkedReader("return" + spaces + "\na", 3), options));
        Assert.Equal("return a", parser.Parse(new ChunkedReader("return" + spaces + "a", 3), options));
    }

    [Fact]
    public void GuardedRepetitionsKeepTheBufferBounded()
    {
        // The loop skips the white space to test whether a statement can start after it, then moves back before it: the
        // white space stays pinned until the statement or the end is read, and is released after
        var statement = Terms.Text("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(';'));
        var parser = ZeroOrMany(statement).AndSkip(Terms.Char('}')).Eof();
        var padding = new string(' ', 40);
        var text = string.Join(padding, Enumerable.Range(0, 10_000).Select(static i => "let id" + i + ";")) + padding + "}";

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 16 });
        var result = parser.Parse(new ChunkedReader(text, 3), options)!;

        Assert.Equal(10_000, result.Count);
        Assert.Equal("id9999", result[9_999].ToString());
        Assert.InRange(peak(), 1, 128);
    }

    [Fact]
    public void AlternativesKeepTheTextTheyCanReadAgain()
    {
        // Both alternatives start with 'a', the first one is read again from the start by the second one
        var parser = OneOf(
            ZeroOrMany(Terms.Char('a')).AndSkip(Terms.Char(';')).Then(static x => x.Count),
            ZeroOrMany(Terms.Char('a')).AndSkip(Terms.Char('.')).Then(static x => -x.Count));

        var text = new string('a', 1000);

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(-1000, parser.Parse(new ChunkedReader(text + ".", 1), options));
        Assert.True(peak() > 1000, $"Peak {peak()}");

        // The same text when the alternatives have distinct first chars
        var exclusive = OneOf(
            OneOrMany(Terms.Char('a')).AndSkip(Terms.Char(';')).Then(static x => x.Count),
            Terms.Char('b').SkipAnd(ZeroOrMany(Terms.Char('a'))).Then(static x => -x.Count));

        (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(1000, exclusive.Parse(new ChunkedReader(text + ";", 1), options));
        Assert.InRange(peak(), 1, 16);
    }

    [Fact]
    public void LongestAlternativesKeepTheTextTheyCanReadAgain()
    {
        // Both alternatives are read from the start, and the cursor moves back to the end of the first one, the longest
        var parser = Longest(
            OneOrMany(Terms.Char('a')).AndSkip(Terms.Char(';')).Then(static x => x.Count),
            Terms.Char('a').Then(static _ => -1));

        var text = new string('a', 1000);

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(1000, parser.Parse(new ChunkedReader(text + ";", 1), options));
        Assert.True(peak() > 1000, $"Peak {peak()}");

        (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(-1, parser.Parse(new ChunkedReader(text + ".", 1), options));

        // The same text when the alternatives have distinct first chars: a single alternative is invoked
        var exclusive = Longest(
            OneOrMany(Terms.Char('a')).AndSkip(Terms.Char(';')).Then(static x => x.Count),
            Terms.Char('b').SkipAnd(ZeroOrMany(Terms.Char('a'))).Then(static x => -x.Count));

        (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(1000, exclusive.Parse(new ChunkedReader(text + ";", 1), options));
        Assert.InRange(peak(), 1, 16);
    }

    [Fact]
    public void DeferredAlternativesSetLaterPinNothing()
    {
        // The deferred declares its first char once the choice first parses, so a single alternative
        // remains for each char and the text can be released as it is read
        var deferred = Deferred<int>();
        var parser = OneOf(
            deferred,
            Terms.Char('b').SkipAnd(ZeroOrMany(Terms.Char('a'))).Then(static x => -x.Count));

        deferred.Parser = OneOrMany(Terms.Char('a')).AndSkip(Terms.Char(';')).Then(static x => x.Count);

        var text = new string('a', 1000);

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(1000, parser.Parse(new ChunkedReader(text + ";", 1), options));
        Assert.InRange(peak(), 1, 16);

        (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(-1000, parser.Parse(new ChunkedReader("b" + text, 1), options));
        Assert.InRange(peak(), 1, 16);
    }

    [Fact]
    public void CommitReleasesTheText()
    {
        var item = Terms.Char('a').Commit();
        var parser = OneOf(
            ZeroOrMany(item).AndSkip(Terms.Char(';')).Then(static x => x.Count),
            ZeroOrMany(item).AndSkip(Terms.Char('.')).Then(static x => -x.Count));

        var text = new string('a', 1000);

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });

        Assert.Equal(1000, parser.Parse(new ChunkedReader(text + ";", 1), options));
        Assert.InRange(peak(), 1, 16);

        // Strings aren't compacted
        Assert.Equal(-1000, parser.Parse(text + "."));

        // The second alternative would read the committed text again
        var exception = Assert.Throws<ParseException>(() => parser.Parse(new ChunkedReader(text + ".", 1), new StreamParseOptions { BufferSize = 4 }));
        Assert.Contains("Commit()", exception.Message);
        Assert.Equal(0, exception.Position.Offset);

        // Even when the text is still buffered
        exception = Assert.Throws<ParseException>(() => parser.Parse(new ChunkedReader("aa.x", int.MaxValue), new StreamParseOptions { BufferSize = 3 }));
        Assert.Contains("Commit()", exception.Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(64)]
    [InlineData(4096)]
    public void PositionsAreTheOnesOfTheWholeTextAcrossRefills(int bufferSize)
    {
        // The lines of the text dropped by each refill are counted before it's discarded
        var position = static (ParseContext c) => (c.Scanner.Cursor.Position.Offset, c.Scanner.Cursor.Position.Line, c.Scanner.Cursor.Position.Column);
        var assignment = Terms.Identifier().AndSkip(Terms.Char('=')).And(Terms.Integer()).AndSkip(Terms.Char(';')).Then((c, _) => position(c));
        var parser = ZeroOrMany(assignment).AndSkip(Terms.Text("end").ElseError("Expected end"));

        var text = new StringBuilder();

        for (var i = 0; i < 2_000; i++)
        {
            text.Append(i % 3 == 0 ? "\r\n" : "\n").Append(' ', i % 5).Append('a').Append(i).Append(i % 7 == 0 ? "\n=\n" : " = ").Append(i).Append(';');
        }

        var valid = text + "\n end";
        var expected = parser.Parse(valid)!;

        Assert.Equal(2_000, expected.Count);
        Assert.Equal(expected, parser.Parse(new ChunkedReader(valid, 5), new StreamParseOptions { BufferSize = bufferSize }));

        // The error is reported where the text which isn't an assignment starts, after the white space it was skipped from
        var invalid = text + "\n\n  a = 1 ; \n x";
        var stringError = Assert.Throws<ParseException>(() => parser.Parse(invalid));
        var streamError = Assert.Throws<ParseException>(() => parser.Parse(new ChunkedReader(invalid, 5), new StreamParseOptions { BufferSize = bufferSize }));

        Assert.Equal("Expected end", streamError.Message);
        Assert.Equal(stringError.Position, streamError.Position);
        Assert.Equal(invalid.Length - 4, streamError.Position.Offset);
    }

    [Fact]
    public void CapturesSpanRefills()
    {
        var parser = Capture(ZeroOrMany(Terms.Identifier()));
        var text = string.Join(" ", Enumerable.Range(0, 1000).Select(static i => "id" + i));

        Assert.Equal(text, parser.Parse(new ChunkedReader(text, 1), new StreamParseOptions { BufferSize = 1 }).ToString());
    }

    // Parsers which read from where they are invoked, though what they read first skips white space: a capture holds
    // that white space, a lookahead leaves the cursor before it. Each is under a condition or a conversion, which reads
    // where the value starts, after the white space, to report a value it rejects.
    private static readonly System.Collections.Generic.Dictionary<string, Parser<string>> ReadFromTheirStart = new()
    {
        ["when-capture"] = Capture(Terms.Integer()).When(static (_, value) => value.Length < 3).Then(static value => value.ToString()),
        ["thentry-capture"] = Capture(Terms.Integer()).ThenTry(static value => (value.Length < 3, value.ToString())),
        ["thentry-capture-offsets"] = Capture(Terms.Integer()).ThenTry(static (ParseContext _, int start, int end, TextSpan value, out string result) =>
        {
            result = $"{value}@{start}-{end}";
            return value.Length < 3;
        }),
        ["when-capture-sequence"] = Capture(Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer())).When(static (_, value) => value.Length < 8).Then(static value => value.ToString()),
        ["when-peek"] = Peek(Terms.Char('a')).When(static (_, _) => true).SkipAnd(Literals.Text("abc")),
        ["thentry-peek"] = Peek(Terms.Char('a')).ThenTry(static value => (true, value)).SkipAnd(Literals.Text("abc")),
        ["when-peek-rejected"] = Peek(Terms.Identifier()).When(static (_, value) => value.Length < 3).SkipAnd(Terms.Identifier()).Then(static value => value.ToString()),
        ["thentry-peek-rejected"] = Peek(Terms.Identifier()).ThenTry(static value => (value.Length < 3, value)).SkipAnd(Terms.Identifier()).Then(static value => value.ToString()),
        ["when-not"] = Not(Terms.Char('x')).SkipAnd(Capture(Terms.Identifier())).When(static (_, value) => value.Length < 4).Then(static value => value.ToString()),
        ["when-token"] = Terms.Integer().When(static (_, value) => value < 100).Then(static value => value.ToString()),
        ["thentry-token"] = Terms.Integer().ThenTry(static value => (value < 100, value.ToString())),
        ["when-tokens"] = Terms.Identifier().And(Terms.Integer()).When(static (_, value) => value.Item2 < 100).Then(static value => $"{value.Item1}{value.Item2}"),
    };

    public static TheoryData<string, string> ReadFromTheirStartCases()
    {
        var data = new TheoryData<string, string>();

        void Add(string grammar, params string[] texts)
        {
            foreach (var text in texts)
            {
                data.Add(grammar, text);
            }
        }

        string[] numbers = ["9", " 9", "  9", " 12", "  12", "123", "  123", "\n\t 9 x", " x", "  ", ""];
        Add("when-capture", numbers);
        Add("thentry-capture", numbers);
        Add("thentry-capture-offsets", numbers);
        Add("when-capture-sequence", "a=1", " a=1", "  a = 1", " a = 12", "  ab = 12", " a =", "  a = x");
        string[] texts = ["abc", " abc", "  abc", " ab", " x", "  ", ""];
        Add("when-peek", texts);
        Add("thentry-peek", texts);
        string[] words = ["ab", " ab", "  ab x", " abc", "  abcd ", " 1", "  "];
        Add("when-peek-rejected", words);
        Add("thentry-peek-rejected", words);
        Add("when-not", "ab", " ab", "  ab", "   a", " x", "  abcd", "  ");
        Add("when-token", "9", "  9", "  123", " x", "  ");
        Add("thentry-token", "9", "  9", "  123", " x", "  ");
        Add("when-tokens", "a 9", "  a  9", "  a  123", " a x", "  ");

        return data;
    }

    [Theory]
    [MemberData(nameof(ReadFromTheirStartCases))]
    public void ConditionsAndConversionsReadTheirParserFromWhereItIsInvoked(string grammar, string text)
    {
        var parser = ReadFromTheirStart[grammar];

        // The buffer holds the text one char at a time, or a few. After 4096 spaces or about, the white space before the
        // value, or the value, is read across two buffers of the default size.
        foreach (var (bufferSize, padding) in new[] { (1, 0), (2, 0), (3, 0), (1, 4094), (4096, 4094), (4096, 4095), (4096, 4096), (4096, 4097) })
        {
            var padded = new string(' ', padding) + text;

            var context = new ParseContext(new Scanner(padded));
            var expected = parser.TryParse(context, out var expectedValue, out _);

            // A failed parse leaves the cursor where it was
            Assert.True(expected || context.Scanner.Cursor.Offset == 0);

            ParseContext? readerContext = null;
            var options = new StreamParseOptions { BufferSize = bufferSize, ContextFactory = (scanner, ct) => readerContext = new ParseContext(scanner, ct) };
            var actual = parser.TryParse(new ChunkedReader(padded, 1), out var actualValue, out _, options);
            var description = $"'{text}' after {padding} spaces with a buffer of {bufferSize}";

            Assert.True(expected == actual, $"{description}: {(actual ? $"matched '{actualValue}'" : "failed")}, the string parser {(expected ? $"matched '{expectedValue}'" : "failed")}");
            Assert.True(expectedValue == actualValue, $"{description}: '{actualValue}' instead of '{expectedValue}'");
            Assert.True(context.Scanner.Cursor.Offset == readerContext!.Scanner.Cursor.Offset, $"{description}: the cursor is at {readerContext.Scanner.Cursor.Offset} instead of {context.Scanner.Cursor.Offset}");
            Assert.True(context.Scanner.Cursor.FurthestOffset == readerContext.Scanner.Cursor.FurthestOffset, $"{description}: the furthest failure is {readerContext.Scanner.Cursor.FurthestOffset} instead of {context.Scanner.Cursor.FurthestOffset}");
        }
    }

    [Theory]
    [InlineData(" 9", " 9")]
    [InlineData("  9", null)]
    [InlineData("12", "12")]
    [InlineData(" 12", null)]
    public void CapturesUnderAConditionHoldTheWhiteSpaceTheirParserSkips(string text, string? expected)
    {
        var when = Capture(Terms.Integer()).When(static (_, value) => value.Length < 3).Then(static value => value.ToString());
        var thenTry = Capture(Terms.Integer()).ThenTry(static value => (value.Length < 3, value.ToString()));

        foreach (var parser in new[] { when, thenTry })
        {
            Assert.Equal(expected, parser.Parse(text));
            Assert.Equal(expected, parser.Parse(new ChunkedReader(text, 1), new StreamParseOptions { BufferSize = 1 }));
            Assert.Equal(expected, parser.Parse(new ChunkedReader(text + " x", 1), new StreamParseOptions { BufferSize = 1 }));
        }
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData(" abc", null)]
    public void LookaheadsUnderAConditionLeaveTheCursorBeforeTheWhiteSpaceTheirParserSkips(string text, string? expected)
    {
        var when = Peek(Terms.Char('a')).When(static (_, _) => true).SkipAnd(Literals.Text("abc"));
        var thenTry = Peek(Terms.Char('a')).ThenTry(static value => (true, value)).SkipAnd(Literals.Text("abc"));

        foreach (var parser in new[] { when, thenTry })
        {
            Assert.Equal(expected, parser.Parse(text));
            Assert.Equal(expected, parser.Parse(new ChunkedReader(text, 1), new StreamParseOptions { BufferSize = 1 }));
        }
    }

    [Fact]
    public void ConditionsOnTokensDontKeepTheWhiteSpaceBeforeThem()
    {
        // A token skips the white space before it itself, before anything else, and so does a sequence or a choice of
        // tokens: a condition or a conversion skips it first, to know where a value it rejects starts, and doesn't keep it
        var spaces = new string(' ', 10_000);

        var when = Terms.Char('[').SkipAnd(Terms.Identifier().When(static (_, value) => value.Length < 5)).AndSkip(Terms.Char(']')).Then(static value => value.ToString());
        var thenTry = Terms.Char('[').SkipAnd(Terms.Identifier().ThenTry(static value => (value.Length < 5, value.ToString()))).AndSkip(Terms.Char(']'));
        var sequence = Terms.Char('[').SkipAnd(Terms.Identifier().AndSkip(Terms.Char('!')).When(static (_, value) => value.Length < 5)).AndSkip(Terms.Char(']')).Then(static value => value.ToString());
        var choice = Terms.Char('[').SkipAnd(OneOf(Terms.Identifier(), Terms.Pattern(char.IsDigit)).ThenTry(static value => (value.Length < 5, value.ToString()))).AndSkip(Terms.Char(']'));

        foreach (var parser in new[] { when, thenTry, sequence, choice })
        {
            var mark = parser == sequence ? "!" : "";

            var (options, peak) = Observe(new StreamParseOptions { BufferSize = 16 });
            Assert.Equal("id1", parser.Parse(new ChunkedReader("[" + spaces + "id1" + mark + spaces + "]", 3), options));
            Assert.InRange(peak(), 1, 64);

            // A rejected one fails where it starts
            ParseContext? context = null;
            options = new StreamParseOptions { BufferSize = 16, ContextFactory = (scanner, ct) => context = new ParseContext(scanner, ct) };
            Assert.False(parser.TryParse(new ChunkedReader("[" + spaces + "longer" + mark + "]", 3), out _, out _, options));
            Assert.Equal(1 + spaces.Length, context!.Scanner.Cursor.FurthestOffset);
        }
    }

    [Fact]
    public void WhiteSpaceBeforeAParserReadFromItsStartIsKeptWhileItIsSkipped()
    {
        // A capture holds the white space its parser skips: it is read before the capture, to know where a value which
        // is rejected starts, and stays buffered for the capture to read it again
        var spaces = new string(' ', 1000);
        var when = Capture(Terms.Integer()).When(static (_, value) => value.Length < 1010).Then(static value => value.ToString()).AndSkip(Terms.Char(';'));
        var thenTry = Capture(Terms.Integer()).ThenTry(static value => (value.Length < 1010, value.ToString())).AndSkip(Terms.Char(';'));

        foreach (var parser in new[] { when, thenTry })
        {
            var (options, peak) = Observe(new StreamParseOptions { BufferSize = 4 });
            Assert.Equal(spaces + "12", parser.Parse(new ChunkedReader(spaces + "12 ;", 1), options));
            Assert.True(peak() > 1000, $"Peak {peak()}");

            ParseContext? context = null;
            options = new StreamParseOptions { BufferSize = 4, ContextFactory = (scanner, ct) => context = new ParseContext(scanner, ct) };
            Assert.False(parser.TryParse(new ChunkedReader(spaces + "12345678901 ;", 1), out _, out _, options));
            Assert.Equal(0, context!.Scanner.Cursor.Offset);
            Assert.Equal(1000, context.Scanner.Cursor.FurthestOffset);
        }

        // A lookahead reads it again too, and it is released once the lookahead is done
        var peeked = Peek(Terms.Identifier()).When(static (_, value) => value.Length < 5).SkipAnd(Terms.Identifier()).AndSkip(ZeroOrMany(Terms.Char(';'))).Then(static value => value.ToString());
        var (peekedOptions, peekedPeak) = Observe(new StreamParseOptions { BufferSize = 4 });
        Assert.Equal("ab", peeked.Parse(new ChunkedReader(spaces + "ab " + new string(';', 5000), 1), peekedOptions));
        Assert.InRange(peekedPeak(), 1000, 1100);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 3)]
    [InlineData(7, 5)]
    public void StringEscapesSplitByRefillsAreDecoded(int chunk, int bufferSize)
    {
        // Every split of the escape sequences lands at the end of a window with one of the chunk sizes
        var parser = ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript).ToPooledString()).Eof();
        var text = string.Concat(Enumerable.Repeat(" 'a\\\r\nb\\\rc\\\nd\\101\\7\\u{1F600}\\x41\\$'", 20));
        var expected = string.Concat(Enumerable.Repeat("abcdA\u0007\U0001F600A$", 20));

        var result = parser.Parse(new ChunkedReader(text, chunk), new StreamParseOptions { BufferSize = bufferSize })!;

        Assert.Equal(20, result.Count);
        Assert.Equal(expected, string.Concat(result));
    }

    [Fact]
    public void TextSpansRemainValid()
    {
        var parser = ZeroOrMany(Terms.Identifier());
        var text = string.Join(" ", Enumerable.Range(0, 1000).Select(static i => "id" + i));

        var result = parser.Parse(new ChunkedReader(text, 1), new StreamParseOptions { BufferSize = 1 })!;

        Assert.Equal(1000, result.Count);
        Assert.All(result, static (span, i) => Assert.Equal("id" + i, span.ToString()));
    }

    [Theory]
    [InlineData("foreach", "  forea")]
    [InlineData("foreach", "  forex")]
    [InlineData("foreach", "")]
    [InlineData("foreach", "    ")]
    public void FailedParsesRestoreTheCursor(string keyword, string text)
    {
        var parser = Terms.Text(keyword).And(Terms.Char(';'));
        ParseContext? context = null;
        var options = new StreamParseOptions { BufferSize = 1, ContextFactory = (scanner, ct) => context = new ParseContext(scanner, ct) };

        Assert.False(parser.TryParse(new ChunkedReader(text, 1), out _, options));
        Assert.Equal(0, context!.Scanner.Cursor.Offset);
    }

    [Fact]
    public void BufferedCharactersAreLimited()
    {
        var options = new StreamParseOptions { BufferSize = 4, MaxBufferedCharacters = 16 };

        Assert.Equal("[\"abcdefghij\"]", JsonParser.Json.Parse(new ChunkedReader("[\"abcdefghij\"]", 1), options)!.ToString());

        // The size of a document isn't limited, only the size of what is buffered
        var array = "[" + string.Join(",", Enumerable.Repeat("\"abcdefghij\"", 100)) + "]";
        Assert.Equal(array, JsonParser.Json.Parse(new ChunkedReader(array, 1), options)!.ToString());

        var exception = Assert.Throws<ParseException>(() => JsonParser.Json.Parse(new ChunkedReader("[\"abcdefghijklmnopqrstuvwxyz\"]", 1), options));
        Assert.Contains("16", exception.Message);

        Assert.False(JsonParser.Json.TryParse(new ChunkedReader("[\"abcdefghijklmnopqrstuvwxyz\"]", 1), out _, out var error, options));
        Assert.Equal(exception.Message, error!.Message);
    }

    [Fact]
    public void ParsesSupportCancellation()
    {
        using var cts = new CancellationTokenSource();
        var text = "[" + string.Join(",", Enumerable.Repeat("\"a\"", 100)) + "]";
        var reader = new ChunkedReader(text, 1) { OnRead = position => { if (position == 50) cts.Cancel(); } };

        Assert.ThrowsAny<OperationCanceledException>(() => JsonParser.Json.Parse(reader, new StreamParseOptions { BufferSize = 1 }, cts.Token));

        reader = new ChunkedReader(text, 1);
        Assert.False(JsonParser.Json.TryParse(reader, out _, out var error, new StreamParseOptions { BufferSize = 1 }, cts.Token));
        Assert.NotNull(error);
    }

    [Fact]
    public void WholeTextsInTheFirstBufferAreParsedAsStrings()
    {
        ParseContext? context = null;
        var options = new StreamParseOptions { ContextFactory = (scanner, ct) => context = new ParseContext(scanner, ct) };

        Assert.Equal("[\"a\"]", JsonParser.Json.Parse(new ChunkedReader("[\"a\"]", 1), options)!.ToString());
        Assert.False(context!.IsCompacting);
        Assert.Equal("[\"a\"]", context.Scanner.Cursor.Buffer);
    }

    [Fact]
    public void ParsersReadingTheEndOfTheBufferMustBeTokens()
    {
        var text = "abc";
        var options = new StreamParseOptions { BufferSize = 1 };

        // Reads the cursor without handling the end of the buffer
        var exception = Assert.Throws<InvalidOperationException>(() => ZeroOrMany(new CharParser(token: false)).Parse(new ChunkedReader(text, 1), options));
        Assert.Contains("ParseToken", exception.Message);

        Assert.Equal("abc", string.Concat(ZeroOrMany(new CharParser(token: true)).Parse(new ChunkedReader(text, 1), options)!));
        Assert.Equal("abc", string.Concat(ZeroOrMany(new CharParser(token: true)).Parse(text)!));
    }

    [Fact]
    public void ContextFactoriesMustUseTheScanner()
    {
        var options = new StreamParseOptions { ContextFactory = static (_, ct) => new ParseContext(new Scanner("a"), ct) };

        Assert.Throws<InvalidOperationException>(() => Terms.Char('a').Parse(new ChunkedReader("a", 1), options));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CustomWhiteSpaceParsersAreTokens(bool hints)
    {
        // With its hints, the white space parser isn't invoked before the identifiers. Without them, as when it can start with
        // any char, it is.
        var parser = hints
            ? ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"))
            : ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/").WithParser(Literals.Pattern(static c => c == '\u0001')));
        var text = string.Join(" /* a comment */ ", Enumerable.Range(0, 100).Select(static i => "id" + i)) + " // end";

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 2 });

        Assert.Equal(100, parser.Parse(new ChunkedReader(text, 1), options)!.Count);
        Assert.InRange(peak(), 1, 64);
    }

    [Theory]
    [InlineData("var a; let b var c;\n  var d")]
    [InlineData("let a; const b; let c;")]
    [InlineData("  var")]
    [InlineData("let a;\nlet ;")]
    public void CollectedDiagnosticsMatchTheWholeText(string text)
    {
        // Choices discard the diagnostics of their alternatives, optional parsers absorb the errors of the missing tokens
        var keyword = OneOf(
            Terms.Text("const").Error("'const' is not supported"),
            Terms.Text("var").Warning("'var' is deprecated"),
            Terms.Text("let"));
        var statement = keyword
            .And(Terms.Identifier().ElseError("Expected a name"))
            .AndSkip(Terms.Char(';').ElseError("Expected ';'").Optional());
        var parser = ZeroOrMany(statement);

        var expectedContext = new ParseContext(new Scanner(text)) { CollectDiagnostics = true };
        var expected = parser.TryParse(expectedContext, out var expectedValue, out _);
        var expectedDiagnostics = expectedContext.Diagnostics.Select(Describe).ToArray();

        Assert.NotEmpty(expectedDiagnostics);

        foreach (var (chunk, bufferSize) in new[] { (1, 1), (3, 4), (int.MaxValue, 4096) })
        {
            ParseContext? context = null;
            var options = new StreamParseOptions
            {
                BufferSize = bufferSize,
                ContextFactory = (scanner, ct) => context = new ParseContext(scanner, ct) { CollectDiagnostics = true },
            };

            Assert.Equal(expected, parser.TryParse(new ChunkedReader(text, chunk), out var value, out _, options));
            Assert.Equal(expectedValue?.Count, value?.Count);
            Assert.Equal(expectedDiagnostics, context!.Diagnostics.Select(Describe).ToArray());
        }

        static string Describe(ParseDiagnostic diagnostic)
            => $"{diagnostic.Severity} {diagnostic.Position.Offset} {diagnostic.Position.Line}:{diagnostic.Position.Column} {diagnostic.Message}";
    }

#if NET10_0_OR_GREATER
    [Theory]
    [InlineData("_bigJson")]
    [InlineData("_longJson")]
    [InlineData("_wideJson")]
    [InlineData("_deepJson")]
    public void BenchmarkDocumentsMatchTheWholeText(string field)
    {
        var benchmark = new Benchmarks.JsonBench();
        benchmark.Setup();

        var text = (string)typeof(Benchmarks.JsonBench).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(benchmark)!;
        var expected = JsonParser.Json.Parse(text)!.ToString();

        foreach (var (chunk, bufferSize) in new[] { (1, 1), (3, 16), (int.MaxValue, 64), (7, 4096) })
        {
            Assert.Equal(expected, JsonParser.Json.Parse(new ChunkedReader(text, chunk), new StreamParseOptions { BufferSize = bufferSize })!.ToString());
        }
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 16)]
    [InlineData(7, 64)]
    [InlineData(int.MaxValue, 4096)]
    public void BenchmarkLogsKeepTheBufferBounded(int chunk, int bufferSize)
    {
        var (log, failed) = Benchmarks.StreamingBenchmarks.CreateLog(10_000);

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = bufferSize });

        Assert.Equal(failed, AccessLog.LogParser.Log.Parse(new ChunkedReader(log, chunk), options));

        // The longest record has 121 chars
        Assert.InRange(peak(), 1, Math.Max(bufferSize, 128) * 2);
    }
#endif

    /// <summary>
    /// Records the largest buffer seen by the parsers.
    /// </summary>
    private static (StreamParseOptions Options, Func<int> Peak) Observe(StreamParseOptions options)
    {
        var peak = 0;

        options.ContextFactory = (scanner, ct) => new ParseContext(scanner, ct)
        {
            OnEnterParser = (_, context) => peak = Math.Max(peak, context.Scanner.Cursor.Buffer.Length),
        };

        return (options, () => peak);
    }

    /// <summary>
    /// Reads a non-whitespace char.
    /// </summary>
    private sealed class CharParser : Parser<char>
    {
        private readonly bool _token;

        public CharParser(bool token)
        {
            _token = token;
        }

        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            if (_token && context.IsCompacting)
            {
                return context.ParseToken(this, ref result);
            }

            context.EnterParser(this);
            context.SkipWhiteSpace();

            var cursor = context.Scanner.Cursor;
            var start = cursor.Offset;

            if (!cursor.Eof)
            {
                var c = cursor.Current;
                cursor.Advance();
                result.Set(start, cursor.Offset, c);
                context.ExitParser(this);
                return true;
            }

            context.ExitParser(this);
            return false;
        }
    }

    private sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;

        public ChunkedReader(string text, int chunk)
        {
            _text = text;
            _chunk = chunk;
        }

        public int Position { get; private set; }

        public Action<int>? OnRead { get; set; }

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(Math.Min(count, _chunk), _text.Length - Position);
            _text.CopyTo(Position, buffer, index, length);
            Position += length;
            OnRead?.Invoke(Position);
            return length;
        }
    }
}
