using System.IO;
using System.Linq;
using System.Threading;
using Xunit;

namespace Parlot.Standalone.Tests;

public class FailureTests
{
    [Theory]
    // The furthest failure is where the longest attempt stopped, after the white space a token skipped
    [InlineData("SELECT a, b FRM t", 12, 1, 13)]
    [InlineData("SELECT a, FROM t", 15, 1, 16)]
    [InlineData("SELECT a b", 9, 1, 10)]
    [InlineData("SELECT", 6, 1, 7)]
    [InlineData("SELEC a", 0, 1, 1)]
    [InlineData("", 0, 1, 1)]
    // Eof() doesn't skip white space
    [InlineData("SELECT a\nFROM\n  t u", 17, 3, 4)]
    [InlineData("SELECT a\r\n, b\r\nFROM 1", 20, 3, 6)]
    public void String_Entry_Point_Returns_The_Furthest_Failure(string text, int offset, int line, int column)
    {
        Assert.False(FailureGrammars.Select(text, out var value, out var failure));
        Assert.Null(value);
        Assert.Equal((offset, line, column), failure);

        // The overload without the failure parses the same
        Assert.False(FailureGrammars.Select(text, out _));
        Assert.False(FailureGrammars.Select(new StringReader(text), out _, out var readerFailure));
        Assert.Equal(failure, readerFailure);
    }

    [Fact]
    public void Success_Returns_No_Failure()
    {
        Assert.True(FailureGrammars.Select("SELECT a, b FROM t", out var value, out var failure));
        Assert.Equal("t:2", value);
        Assert.Equal(default, failure);

        Assert.True(FailureGrammars.Select(new StringReader("select a from t"), out value, out failure));
        Assert.Equal("t:1", value);
        Assert.Equal(default, failure);
    }

    [Theory]
    [InlineData("1 + (2 + 3", 10, 1, 11)]
    [InlineData("1 + (2 + )", 9, 1, 10)]
    [InlineData("1 + (2 + 3))", 11, 1, 12)]
    [InlineData("((1)\n+\n(2 +\nx))", 12, 4, 1)]
    [InlineData("1 +", 3, 1, 4)]
    [InlineData("x", 0, 1, 1)]
    public void Recursive_Grammar_Returns_The_Furthest_Failure(string text, int offset, int line, int column)
    {
        Assert.False(FailureGrammars.Expression(text, CancellationToken.None, out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal((offset, line, column), failure);

        Assert.False(FailureGrammars.Expression(text, CancellationToken.None, out _, out int failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.Expression(new StringReader(text), CancellationToken.None, out _, out var readerFailure));
        Assert.Equal(failure, readerFailure);

        Assert.True(FailureGrammars.Expression("1 + (2 + 3)", CancellationToken.None, out var value, out failureOffset));
        Assert.Equal(6, value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4096)]
    [InlineData(10_000)]
    public void TextReader_Entry_Point_Reports_Failures_After_Refills(int padding)
    {
        // A long valid prefix makes the reader refill its buffer, and drop the text before the failure
        var lines = string.Concat(Enumerable.Repeat("1 +\n", padding));
        var text = lines + "(2 + 3 +\n  x)";
        var expected = (lines.Length + 11, padding + 2, 3);

        Assert.False(FailureGrammars.Expression(text, CancellationToken.None, out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(expected, failure);

        Assert.False(FailureGrammars.Expression(new OneCharReader(text), CancellationToken.None, out _, out var readerFailure));
        Assert.Equal(expected, readerFailure);

        Assert.True(FailureGrammars.Expression(new StringReader(lines + "1"), CancellationToken.None, out var value, out readerFailure));
        Assert.Equal(padding + 1, value);
        Assert.Equal(default, readerFailure);
    }

    [Fact]
    public void Cancellation_Throws()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        Assert.ThrowsAny<System.OperationCanceledException>(() => FailureGrammars.Expression("1", source.Token, out _, out int _));
    }

    [Theory]
    [InlineData("1,2", true, 0)]
    [InlineData("1, x", false, 3)]
    [InlineData("1 2", false, 2)]
    [InlineData("1,2,", false, 3)]
    public void Failure_Only_Overload_Generates_The_Parser(string text, bool success, int offset)
    {
        Assert.Equal(success, FailureGrammars.Pair(text, out var value, out var failureOffset));
        Assert.Equal(offset, failureOffset);
        if (success)
        {
            Assert.Equal((1L, 2L), value);
        }
    }

    [Fact]
    public void Error_Reports_Its_Position()
    {
        // ElseError reports where the parser it wraps was invoked, before the white space it skipped
        Assert.False(FailureGrammars.Error("[ 12 )", out _, out var failure));
        Assert.Equal((4, 1, 5), failure);

        // Without an error the furthest failure is reported
        Assert.False(FailureGrammars.Error("[ x", out _, out failure));
        Assert.Equal((2, 1, 3), failure);
    }

    [Theory]
    [InlineData("abc:xx", true, 0)]
    [InlineData("abd:xx", false, 0)]
    [InlineData("abc;xx", false, 3)]
    [InlineData("abc:x", false, 5)]
    [InlineData("abc:xxy", false, 6)]
    public void Span_Code_Records_Where_It_Failed(string text, bool success, int offset)
    {
        Assert.Equal(success, FailureGrammars.Span(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);
    }

    [Fact]
    public void Configured_Entry_Point_Returns_The_Failure()
    {
        Assert.True(FailureGrammars.Configured("4;", 3, out var value, out var failure));
        Assert.Equal(12, value);
        Assert.Equal(default, failure);

        Assert.False(FailureGrammars.Configured("4 ,", 3, out _, out failure));
        Assert.Equal((2, 1, 3), failure);
    }

    // Returns one char per read, so that a TextReader entry point refills its buffer as often as possible
    private sealed class OneCharReader : TextReader
    {
        private readonly string _text;
        private int _position;

        public OneCharReader(string text) => _text = text;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position == _text.Length || count == 0)
            {
                return 0;
            }

            buffer[index] = _text[_position++];
            return 1;
        }

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;
    }
}
