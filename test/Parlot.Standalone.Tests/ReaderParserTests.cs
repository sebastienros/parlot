using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace Parlot.Standalone.Tests;

public class ReaderParserTests
{
    // The generated reader entry points read 4096 characters at a time.
    private const int BufferSize = 4096;

    private delegate bool StringParser<T>(string text, out T value);
    private delegate bool ReaderParser<T>(TextReader reader, out T value);

    [Fact]
    public void Tokens_Split_At_Every_Refill_Position_Match_String_Parsing()
    {
        CheckPadded<int>(Grammar.TryParseNumber, Grammar.TryParseNumber, ' ', " 12345 ", "12x", "-42", "");
        CheckPadded<char>(Grammar.TryParseAlternative, Grammar.TryParseAlternative, ' ', "ab", "  ac", "ad");
        CheckPadded<string>(Grammar.TryParseString, Grammar.TryParseString, ' ', "'hello \\u0041 world'", "\"open");
        CheckPadded<int>(Grammar.TryParseOptional, Grammar.TryParseOptional, ' ', "123", "", "x");
        CheckPadded<string>(Grammar.TryParseCustomWhitespace, Grammar.TryParseCustomWhitespace, '_', "hello__world", "hello world", "hello___worl");
        CheckPadded<string>(Grammar.TryParseKeyword, Grammar.TryParseKeyword, ' ', "interface", "int9", "internal", "catch", "ifx", "yields");
        CheckPadded<string>(Grammar.TryParseKeywordFallback, Grammar.TryParseKeywordFallback, ' ', "while!", "whilex!", " \r\nunknown!");
        CheckPadded<string>(Grammar.TryParseTextChoice, Grammar.TryParseTextChoice, ' ', "if12", "if1x", "Content-Encoding-x", "Content-Typ", "line\r\nend", "line\r\n", "\ud800\udc00");
        CheckPadded<string>(Grammar.TryParseTextChoiceFallback, Grammar.TryParseTextChoiceFallback, ' ', "Content!", "Content-Type!", "Content-Typo!");
        CheckPadded<(int, int, int)>(Grammar.TryParseTextChoicePosition, Grammar.TryParseTextChoicePosition, '\n', " \r\nline\r\nend!", "\r\nif!");
        CheckPadded<char>(Grammar.TryParseError, Grammar.TryParseError, 'y', "x");

        var options = new GrammarOptions { Prefix = ">", Formal = true };
        CheckPadded<string>(
            (string text, out string value) => Grammar.TryParseConfiguredWhitespace(text, options, out value),
            (TextReader reader, out string value) => Grammar.TryParseConfiguredWhitespace(reader, options, out value),
            '_', "hello_world", "hello-world");
    }

    [Fact]
    public void Values_Larger_Than_The_Buffer_Match_String_Parsing()
    {
        var numbers = string.Join(", ", Enumerable.Range(0, 5_000));
        Check<IReadOnlyList<int>>(Grammar.TryParseNumbers, Grammar.TryParseNumbers, numbers);
        Check<IReadOnlyList<int>>(Grammar.TryParseNumbers, Grammar.TryParseNumbers, numbers + ",");
        Check<IReadOnlyList<int>>(Grammar.TryParseNumbers, Grammar.TryParseNumbers, numbers + ", x");

        Check<string>(Grammar.TryParseString, Grammar.TryParseString, "'" + new string('a', 3 * BufferSize) + "'");
        Check<string>(Grammar.TryParseString, Grammar.TryParseString, "'" + new string('a', 3 * BufferSize));
        Check<int>(Grammar.TryParseNumber, Grammar.TryParseNumber, new string('1', 9) + new string(' ', 3 * BufferSize));

        foreach (var depth in new[] { 1000, 2047, 2048, 2500 })
        {
            Check<int>(Grammar.TryParseRecursive, Grammar.TryParseRecursive, new string('(', depth) + "x" + new string(')', depth));
            Check<int>(Grammar.TryParseRecursive, Grammar.TryParseRecursive, new string('(', depth) + "x" + new string(')', depth - 1));
        }

        var capture = "!" + new string('_', 2 * BufferSize);
        Check<string>(Grammar.TryParseKeywordCapture, Grammar.TryParseKeywordCapture, capture + "class1");
        Check<string>(Grammar.TryParseTextChoiceCapture, Grammar.TryParseTextChoiceCapture, "!" + new string(' ', 2 * BufferSize) + "Content-Encoding!");
        Check<string>(Grammar.TryParseTextChoiceCapture, Grammar.TryParseTextChoiceCapture, "!" + new string(' ', 2 * BufferSize) + "Content-Encodin!");
    }

    [Fact]
    public void Reader_Results_Do_Not_Depend_On_Read_Sizes()
    {
        var numbers = string.Join(",", Enumerable.Range(0, 3_000));
        var expected = Enumerable.Range(0, 3_000).ToArray();
        foreach (var chunk in new[] { 1, 7, 4095, 4097 })
        {
            Assert.True(Grammar.TryParseNumbers(new ChunkedReader(numbers, chunk), out var value));
            Assert.Equal(expected, value);
        }
    }

    [Fact]
    public void Reader_Entry_Points_Throw_On_Cancellation()
    {
        using var source = new CancellationTokenSource();
        var input = new string(' ', 2 * BufferSize) + "42";
        Assert.True(Grammar.TryParseCancelableNumber(new StringReader(input), source.Token, out var value));
        Assert.Equal(42, value);

        source.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => Grammar.TryParseCancelableNumber(new StringReader(input), source.Token, out _));

        using var sequenceSource = new CancellationTokenSource();
        var options = new CancellationOptions { Source = sequenceSource };
        Assert.ThrowsAny<OperationCanceledException>(() =>
            Grammar.TryParseCancelableSequence(new StringReader(new string('x', 3 * BufferSize)), options, sequenceSource.Token, out _));
    }

    [Fact]
    public void Reader_Entry_Points_Reject_Null()
    {
        Assert.Throws<ArgumentNullException>(() => Grammar.TryParseNumber((TextReader)null, out _));
    }

    private static void CheckPadded<T>(StringParser<T> parseString, ReaderParser<T> parseReader, char padding, params string[] inputs)
    {
        foreach (var input in inputs)
        {
            for (var split = 0; split <= input.Length + 1; split++)
            {
                Check(parseString, parseReader, new string(padding, BufferSize - split) + input);
                Check(parseString, parseReader, new string(padding, 2 * BufferSize - split) + input);
            }
        }
    }

    private static void Check<T>(StringParser<T> parseString, ReaderParser<T> parseReader, string input)
    {
        var expected = parseString(input, out var expectedValue);
        foreach (var reader in new TextReader[] { new StringReader(input), new ChunkedReader(input, 1), new ChunkedReader(input, 13) })
        {
            Assert.Equal(expected, parseReader(reader, out var value));
            Assert.Equal(expectedValue, value);
        }
    }

    // A reader returning at most a fixed number of characters per read
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
