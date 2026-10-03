#nullable enable

using System;
using System.Collections.Generic;
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

    [Fact]
    public void CapturesSpanRefills()
    {
        var parser = Capture(ZeroOrMany(Terms.Identifier()));
        var text = string.Join(" ", Enumerable.Range(0, 1000).Select(static i => "id" + i));

        Assert.Equal(text, parser.Parse(new ChunkedReader(text, 1), new StreamParseOptions { BufferSize = 1 }).ToString());
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

    [Fact]
    public void CustomWhiteSpaceParsersAreTokens()
    {
        var parser = ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));
        var text = string.Join(" /* a comment */ ", Enumerable.Range(0, 100).Select(static i => "id" + i)) + " // end";

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = 2 });

        Assert.Equal(100, parser.Parse(new ChunkedReader(text, 1), options)!.Count);
        Assert.InRange(peak(), 1, 64);
    }

#if NET10_0_OR_GREATER
    [Theory]
    [InlineData("_bigJson")]
    [InlineData("_longJson")]
    [InlineData("_wideJson")]
    [InlineData("_deepJson")]
    public void BenchmarkDocumentsMatchTheWholeText(string field)
    {
        var benchmark = new Parlot.Benchmarks.JsonBench();
        benchmark.Setup();

        var text = (string)typeof(Parlot.Benchmarks.JsonBench).GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(benchmark)!;
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
        var (log, failed) = Parlot.Benchmarks.StreamingBenchmarks.CreateLog(10_000);

        var (options, peak) = Observe(new StreamParseOptions { BufferSize = bufferSize });

        Assert.Equal(failed, Parlot.Tests.AccessLog.LogParser.Log.Parse(new ChunkedReader(log, chunk), options));

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
