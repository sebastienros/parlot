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
    public void Error_Is_Not_Nested_Too_Deeply()
    {
        Assert.False(FailureGrammars.ErrorNesting("[ 12 )", out _, out var failure));
        Assert.Equal((4, false), failure);
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
    // A pattern which needs more chars fails where it started, like the parser which reads them without moving the cursor
    [InlineData("abc:x", false, 4)]
    [InlineData("abc:xy", false, 4)]
    [InlineData("abc:xxy", false, 6)]
    public void Span_Code_Records_Where_It_Failed(string text, bool success, int offset)
    {
        Assert.Equal(success, FailureGrammars.Span(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.Equal(success, FailureGrammars.Span(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
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

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from;", 3)]
    [InlineData("  fromage,", 9)]
    [InlineData("  ,", 2)]
    public void Rejected_Value_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.Rejected(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.Rejected(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("  a  from;", "a from")]
    [InlineData("  a from;", "a from")]
    [InlineData("\n a\n from;", "a from")]
    [InlineData("  a  b;", "a b")]
    public void Rejected_Value_Of_Several_Tokens_Leaves_Its_White_Space_To_The_Next_Parser(string text, string expected)
    {
        // The white space before the value isn't the last one that was skipped: it is read again to know where the value
        // starts, and the cursor is moved back before it
        Assert.True(FailureGrammars.RejectedPair(text, out var value, out var failureOffset));
        Assert.Equal(expected, value);
        Assert.Equal(0, failureOffset);

        Assert.True(FailureGrammars.RejectedPair(new OneCharReader(text), out value, out (int Offset, int Line, int Column) failure));
        Assert.Equal(expected, value);
        Assert.Equal(default, failure);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("/* a */ from;", 8)]
    [InlineData("  /* a */\n  from;", 12)]
    [InlineData("/* a */ fromage,", 15)]
    public void Rejected_Value_Fails_After_The_Comments_Of_A_White_Space_Parser(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedAfterComments(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedAfterComments(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("  y")]
    [InlineData("  ")]
    [InlineData("y")]
    public void Rejected_Value_Fails_Where_A_Parser_Which_Skips_No_White_Space_Is_Invoked(string text)
    {
        // The white space was skipped from there by another parser, a lookahead which doesn't match: it is the value,
        // not what comes before it
        Assert.False(FailureGrammars.RejectedWhiteSpace(text, out _, out var failureOffset));
        Assert.Equal(0, failureOffset);

        Assert.False(FailureGrammars.RejectedWhiteSpace(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(0, failure.Offset);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from;", 3)]
    [InlineData("  fromage,", 9)]
    [InlineData("  ,", 2)]
    public void Value_Rejected_By_A_Conversion_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.ConversionRejected(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.ConversionRejected(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.ConversionRejected("  word ;", out var value, out failureOffset));
        Assert.Equal("word", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("fromage,", 7)]
    public void Value_Rejected_By_A_Tuple_Conversion_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.ConversionRejectedLiteral(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);
    }

    [Fact]
    public void Value_Rejected_By_A_Conversion_Doesnt_Stop_A_Longer_Attempt_Being_Reported()
    {
        Assert.False(FailureGrammars.ConversionSecond("  a  from", out _, out var failureOffset));
        Assert.Equal(5, failureOffset);

        Assert.False(FailureGrammars.ConversionSecond(new OneCharReader("  a  from"), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(5, failure.Offset);

        // A failure recorded further before the value is rejected is kept
        Assert.False(FailureGrammars.ConversionFurther("  from ( x", out _, out failureOffset));
        Assert.Equal(9, failureOffset);

        Assert.False(FailureGrammars.ConversionFurther(new OneCharReader("  from ( x"), out _, out failure));
        Assert.Equal(9, failure.Offset);
    }

    [Theory]
    [InlineData("  from;", 2)]
    [InlineData("/* a */ from;", 8)]
    [InlineData(" /* a */\n /* b */ from;", 18)]
    [InlineData(" /* a */ fromage,", 16)]
    [InlineData(" /* a */ /from;", 9)]
    public void Value_Rejected_By_A_Conversion_Fails_After_The_Comments_Before_It(string text, int offset)
    {
        Assert.False(FailureGrammars.ConversionRejectedAfterComments(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.ConversionRejectedAfterComments(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from;", 3)]
    [InlineData("  fromage,", 9)]
    public void Value_Rejected_By_A_Conversion_Fails_At_Its_Start_After_Another_Alternative(string text, int offset)
    {
        Assert.False(FailureGrammars.ConversionRejectedAlternative(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.ConversionRejectedAlternative(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Fact]
    public void Rejected_Value_Doesnt_Stop_A_Longer_Attempt_Being_Reported()
    {
        Assert.False(FailureGrammars.Second("  a  from", out _, out var failureOffset));
        Assert.Equal(5, failureOffset);

        // Without white space to skip, the start is where the parser was entered
        Assert.False(FailureGrammars.RejectedLiteral("from;", out _, out failureOffset));
        Assert.Equal(0, failureOffset);

        Assert.True(FailureGrammars.Rejected("  word ;", out var value, out failureOffset));
        Assert.Equal("word", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("abc((", 3)]
    [InlineData("abc  ((;", 5)]
    [InlineData("abc  (;", 5)] // Doesn't match, so the expected ';' fails after the white space
    [InlineData("abc ,", 4)]
    public void Not_Followed_By_Fails_At_What_Follows(string text, int offset)
    {
        Assert.False(FailureGrammars.NotFollowed(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.NotFollowed(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("abc((", 3)] // A lookahead that matched doesn't count: the 'x' expected next fails at 3, not after the "(("
    [InlineData("abc  ((y", 5)] // The 'x' expected next fails after its white space, at the lookahead's text
    [InlineData("abc  (", 5)] // The lookahead was expected, it fails after its white space
    [InlineData("abc", 3)]
    public void Followed_By_That_Matched_Doesnt_Count(string text, int offset)
    {
        Assert.False(FailureGrammars.Followed(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.Followed(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("((", 0)] // A lookahead that matched doesn't count: the 'x' expected next fails at 0, not after the "(("
    [InlineData("((y", 0)]
    [InlineData("  ((", 2)] // The 'x' expected next fails after its white space, at the lookahead's text, not after it
    [InlineData("  ((y", 2)]
    [InlineData("  (x", 2)] // The lookahead was expected, it fails after its white space
    [InlineData("", 0)]
    public void Peek_That_Matched_Doesnt_Count(string text, int offset)
    {
        Assert.False(FailureGrammars.Peeked(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.Peeked(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("> ab", 2)] // The lookahead skips the white space before the value and matches it: the value fails where it starts
    [InlineData(">ab", 1)]
    [InlineData("  >  abc", 5)]
    [InlineData("> a", 2)] // The lookahead was expected, it fails after its white space
    [InlineData(">", 1)]
    public void Value_Rejected_After_A_Peek_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.PeekedRejected(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.PeekedRejected(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("abc ;", 4)] // The lookahead was expected: where its parser stopped counts, unlike the text it matched
    [InlineData("  abc  ;", 7)]
    [InlineData("abc", 3)]
    [InlineData("  ;", 2)]
    [InlineData("abc : ?", 4)] // The lookahead matches up to 5, the '!' expected after the word fails after its white space
    [InlineData("  abc:", 5)] // and here where the word ends, not after the ':' of the lookahead
    public void Peek_That_Fails_Keeps_The_Failure_Of_Its_Parser(string text, int offset)
    {
        Assert.False(FailureGrammars.PeekedFurther(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.PeekedFurther(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("abc ?", 0)] // The first alternative of the lookahead reads up to 4 before the second one matches
    [InlineData("  abc  ?", 2)]
    [InlineData("abc ( ?", 0)]
    [InlineData("  abc", 2)]
    [InlineData("  ( ", 2)] // No alternative matches: the lookahead was expected, after its white space
    [InlineData("abc ? x", 0)]
    public void Peek_That_Matched_Forgets_The_Alternatives_Which_Failed(string text, int offset)
    {
        Assert.False(FailureGrammars.PeekedAlternatives(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.PeekedAlternatives(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData(4092)]
    [InlineData(4093)]
    [InlineData(4094)]
    [InlineData(4095)]
    [InlineData(4096)]
    public void Peek_Read_Across_Two_Buffers_Doesnt_Count(int padding)
    {
        // The buffer of the reader, of 4096 chars, ends in the white space the lookahead skips or in the text it matches:
        // the text is read again with more of it, and the cursor moves back to a start which is still buffered
        var text = new string(' ', padding - 1) + ">  ab";

        Assert.False(FailureGrammars.PeekedRejected(text, out _, out var failureOffset));
        Assert.Equal(padding + 2, failureOffset);

        Assert.False(FailureGrammars.PeekedRejected(new StringReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(padding + 2, failure.Offset);

        Assert.False(FailureGrammars.PeekedRejected(new OneCharReader(text), out _, out failure));
        Assert.Equal(padding + 2, failure.Offset);

        text = new string(' ', padding) + "abc ?";

        Assert.False(FailureGrammars.PeekedAlternatives(new StringReader(text), out _, out failure));
        Assert.Equal(padding, failure.Offset);

        text = new string(' ', padding) + "abc : ?";

        Assert.False(FailureGrammars.PeekedFurther(new StringReader(text), out _, out failure));
        Assert.Equal(padding + 4, failure.Offset);

        text = new string(' ', padding) + "abc ;";

        Assert.False(FailureGrammars.PeekedFurther(new StringReader(text), out _, out failure));
        Assert.Equal(padding + 4, failure.Offset);
    }

    [Theory]
    [InlineData("abc", 0)]
    [InlineData("  abc", 2)]
    [InlineData("  abd,", 5)]
    public void Not_Fails_At_What_Matched(string text, int offset)
    {
        Assert.False(FailureGrammars.Negative(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.Negative(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("a from;", 0)]
    [InlineData("  a from;", 2)]
    [InlineData("  a  from;", 2)]
    [InlineData("\n a\n from;", 2)]
    [InlineData("  a  b,", 6)]
    [InlineData("  a  ,", 5)]
    public void Rejected_Value_Of_Several_Tokens_Fails_At_Its_Start(string text, int offset)
    {
        // The cursor moves forward over the white space between the words, which isn't a failure there
        Assert.False(FailureGrammars.RejectedWords(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedWords(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.False(FailureGrammars.ConversionRejectedWords(text, out _, out failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.ConversionRejectedWords(new OneCharReader(text), out _, out failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedWords("  a  b ;", out var value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("a from;", 0)]
    [InlineData("  a from;", 2)]
    [InlineData("\n a\n from;", 2)]
    [InlineData("  a  fr,", 7)]
    public void Rejected_Value_Ending_With_The_Longest_Match_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedLongest(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedLongest(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedLongest("  a  fr ;", out var value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);

        // Without a selector, the cursor is moved back from each alternative which matched: not a failure where it ended
        Assert.False(FailureGrammars.RejectedLongestText(text, out _, out failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedLongestText(new OneCharReader(text), out _, out failure));
        Assert.Equal(offset, failure.Offset);

        Assert.False(FailureGrammars.RejectedLongestWord(text, out _, out failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedLongestWord(new OneCharReader(text), out _, out failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedLongestWord("  a  fr ;", out value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("a  x", 3, 1, 4)]
    [InlineData("a  b  x", 6, 1, 7)]
    [InlineData("a  b  c  x", 9, 1, 10)]
    [InlineData("a\n b\n x", 6, 3, 2)]
    [InlineData("a b c x", 6, 1, 7)]
    [InlineData("ax", 1, 1, 2)]
    public void Span_Code_Moved_Back_Over_White_Space_Fails_After_It(string text, int offset, int line, int column)
    {
        // An optional text isn't found after the white space the cursor skipped, and the span code reads on, or ends,
        // before that white space: the cursor is moved back from where the text was looked for, like at runtime
        Assert.False(FailureGrammars.SpanBacktracking(text, out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal((offset, line, column), failure);

        Assert.False(FailureGrammars.SpanBacktracking(new OneCharReader(text), out _, out var readerFailure));
        Assert.Equal((offset, line, column), readerFailure);

        Assert.True(FailureGrammars.SpanBacktracking("a  b  c  d", out var value, out failure));
        Assert.Equal("a", value);
        Assert.Equal(default, failure);
    }

    [Theory]
    [InlineData(">from;;!", 1)]
    [InlineData("  >from;;!", 3)]
    [InlineData("\n>from;;!", 2)]
    public void Rejected_Text_Before_A_Delimiter_Fails_At_Its_Start(string text, int offset)
    {
        // The delimiter is found and not consumed: the cursor is moved back from a delimiter which matched, not a failure
        // where it ended
        Assert.False(FailureGrammars.RejectedTextBefore(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBefore(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedTextBefore(">a word;;  !", out var value, out failureOffset));
        Assert.Equal("a word", value);
        Assert.Equal(0, failureOffset);

        // A delimiter which skips white space isn't searched for by its first char
        Assert.False(FailureGrammars.RejectedTextBeforeTerm(text, out _, out failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(new OneCharReader(text), out _, out failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedTextBeforeTerm(">a word  ;;  !", out value, out failureOffset));
        Assert.Equal("a word", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(">from ;;!", 1)]
    [InlineData("  >from  ;;!", 3)]
    [InlineData(">from\n;;\n!", 1)]
    // The delimiter is looked for after each white space of the text, which isn't a failure either
    [InlineData(">from here to there ;;!", 1)]
    [InlineData("  >from  here ; to\nthere;;!", 3)]
    public void Rejected_Text_Before_A_Delimiter_After_White_Space_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedTextBeforeTerm(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData(">from;;cd?")]
    [InlineData(">from;;  cd ?")]
    [InlineData(">from;;?")]
    [InlineData(">from;;")]
    public void Delimiter_Which_Isnt_Consumed_Is_A_Lookahead(string text)
    {
        // The first alternative of the delimiter reads further than the one which matches, which says nothing about the
        // text: the value fails at its start, like one followed by WhenFollowedBy
        Assert.False(FailureGrammars.RejectedTextBeforeChoice(text, out _, out var failureOffset));
        Assert.Equal(1, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBeforeChoice(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal((1, 1, 2), failure);

        Assert.True(FailureGrammars.RejectedTextBeforeChoice(">a word;;", out var value, out failureOffset));
        Assert.Equal("a word", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    // The value is rejected: the delimiter read the `;` of the text, where it wasn't found
    [InlineData(">from;;!", 1)]
    [InlineData(">from a; b ; c ;;!", 1)]
    [InlineData("\n >from ;\n;;!", 3)]
    // What follows the delimiter isn't found
    [InlineData(">a; b ;;?", 8)]
    [InlineData(">a; b ; ; ?", 10)]
    // The delimiter isn't found: the text ends with the input
    [InlineData(">a; b ;", 7)]
    public void Delimiter_Looked_For_In_A_Text_Isnt_A_Failure(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedTextBeforeConsumed(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBeforeConsumed(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedTextBeforeConsumed(">a; word ;; !", out var value, out failureOffset));
        Assert.Equal("a; word", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(4091)]
    [InlineData(4092)]
    [InlineData(4093)]
    [InlineData(8189)]
    public void Rejected_Text_Read_Again_With_More_Text_Fails_At_Its_Start(int length)
    {
        // The buffer of the reader, of 4096 chars, ends in or after the white space the delimiter skips: the delimiter isn't
        // found there, and the text is read again with more text. What was read up to then isn't a failure.
        var text = ">from" + new string('a', length - 5) + "  ;;!";

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(text, out _, out var failureOffset));
        Assert.Equal(1, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(new StringReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal((1, 1, 2), failure);

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(new OneCharReader(text), out _, out failure));
        Assert.Equal((1, 1, 2), failure);

        // The start of the text is still buffered when the failure is returned, on another line than the delimiter
        text = "\n\n >from" + new string('a', length - 9) + "\n\n;;!";

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(new StringReader(text), out _, out failure));
        Assert.Equal((4, 3, 3), failure);
    }

    [Theory]
    // The delimiter is where a text is expected
    [InlineData(">;;!", 1)]
    [InlineData("  >;;!", 3)]
    // What follows the delimiter isn't found
    [InlineData(">word;;?", 7)]
    [InlineData(">word;; ?", 8)]
    // The delimiter isn't found: the text ends with the input, where the delimiter is expected
    [InlineData(">word;", 6)]
    [InlineData(">word", 5)]
    public void Text_Before_A_Delimiter_Reports_The_Furthest_Failure(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedTextBefore(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBefore(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(text, out _, out failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedTextBeforeTerm(new OneCharReader(text), out _, out failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    // The delimiter is where a text is expected
    [InlineData(">;;!", 1)]
    [InlineData("  >;;!", 3)]
    [InlineData(">ab;;?", 5)]
    [InlineData(">ab;;  ?", 7)]
    // The delimiter isn't found, and the text ends with the input
    [InlineData(">ab;", 4)]
    public void Text_Before_A_Consumed_Delimiter_Reports_The_Furthest_Failure(string text, int offset)
    {
        Assert.False(FailureGrammars.TextBeforeConsumed(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.TextBeforeConsumed(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.TextBeforeConsumed(">ab;; !", out var value, out failureOffset));
        Assert.Equal("ab", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(">ab;;?", 3)]
    [InlineData("  >a;b;;", 6)]
    [InlineData(">;;?", 1)]
    [InlineData(">ab;", 4)]
    public void Parser_After_A_Text_Before_A_Delimiter_Fails_Where_It_Starts(string text, int offset)
    {
        // The text ends before the delimiter, where the next parser is expected: the delimiter was only looked for
        Assert.False(FailureGrammars.TextBeforeThenOther(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.TextBeforeThenOther(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.TextBeforeThenOther(">ab;;!", out var value, out failureOffset));
        Assert.Equal("ab", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(">ab;", 4)]
    [InlineData(">ab", 3)]
    [InlineData(">", 1)]
    [InlineData("  >a;b;c", 8)]
    public void Delimiter_Which_Isnt_Found_Fails_At_The_End(string text, int offset)
    {
        Assert.False(FailureGrammars.TextBeforeRequired(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.TextBeforeRequired(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.TextBeforeRequired(">a;b;;", out var value, out failureOffset));
        Assert.Equal("a;b", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    // The value is rejected
    [InlineData(">1 + 2;", 1)]
    [InlineData("  >  1  +  2;", 5)]
    [InlineData(">1 * 5 - 2;", 1)]
    [InlineData(">3;", 1)]
    // An operand, or what follows the expression, isn't found
    [InlineData(">1 + 3 ?", 7)]
    [InlineData(">1 +", 4)]
    [InlineData(">1  +  x", 7)]
    [InlineData(">1 * 2 + ", 9)]
    public void Rejected_Expression_Of_Valued_Operators_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedValuedOperators(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedValuedOperators(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedValuedOperators(">1 + 2 * 3 ;", out var value, out failureOffset));
        Assert.Equal(7, value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(";;cd?", 4)] // The first alternative stops at the '?', after the word: the second one matches, and 'x' isn't found at 2
    [InlineData(";;cd !", 6)] // The first alternative matches, 'x' isn't found after it
    [InlineData(";;  cd", 6)]
    [InlineData(";;?", 2)]
    [InlineData(";?", 0)]
    public void Span_Code_Alternative_Which_Fails_After_Reading_Is_Reported(string text, int offset)
    {
        // Span code moves a local position back to read the next alternative, where the other parsers move the cursor
        Assert.False(FailureGrammars.SpanChoice(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.SpanChoice(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.SpanChoice(";;x", out var value, out failureOffset));
        Assert.Equal(";;", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData(">;;cd?", 5)] // The rejected value keeps the failure of its first alternative
    [InlineData(">;;cd  ?", 7)]
    [InlineData(">;;?", 3)]
    [InlineData(">;;cd !", 1)] // No parser of the value failed after reading: it fails where it starts
    [InlineData(">;?", 1)]
    public void Span_Code_Alternative_Which_Fails_After_Reading_Is_Reported_In_A_Rejected_Value(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedSpanChoice(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedSpanChoice(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
    }

    [Theory]
    [InlineData("abx", 2)] // 'c' isn't found after the 'b' of the optional sequence
    [InlineData("abd", 2)]
    [InlineData("abcx", 3)]
    [InlineData("ax", 1)]
    public void Span_Code_Optional_Parser_Which_Fails_After_Reading_Is_Reported(string text, int offset)
    {
        Assert.False(FailureGrammars.SpanOptional(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.SpanOptional(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.SpanOptional("abcd", out var value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("abx", 2)] // 'c' isn't found after the 'b' of the first element
    [InlineData("abcbx", 4)] // nor after the 'b' of the second one
    [InlineData("abcbcx", 5)]
    [InlineData("ax", 1)]
    public void Span_Code_Loop_Element_Which_Fails_After_Reading_Is_Reported(string text, int offset)
    {
        Assert.False(FailureGrammars.SpanLoop(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.SpanLoop(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.SpanLoop("abcbcd", out var value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("afrom?", 5)] // "fr" is kept, and "from" isn't followed by '!'
    [InlineData("afr?", 3)]
    [InlineData("af", 1)]
    public void Span_Code_Longest_Alternative_Which_Fails_Further_Is_Reported(string text, int offset)
    {
        Assert.False(FailureGrammars.SpanLongest(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.SpanLongest(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.SpanLongest("afrom!x", out var value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("a:from;", 0)] // Span code which matched isn't a failure where it ended
    [InlineData("a:b,", 3)]
    [InlineData("a:;", 2)]
    [InlineData("a;", 1)]
    public void Rejected_Value_Of_Span_Code_Fails_At_Its_Start(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedSpan(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedSpan(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);

        Assert.True(FailureGrammars.RejectedSpan("a:b;", out var value, out failureOffset));
        Assert.Equal("a", value);
        Assert.Equal(0, failureOffset);
    }

    [Theory]
    [InlineData("a c", 2)] // 'b' isn't found after the white space, which the " c" of the value reads again
    [InlineData("  a c", 4)]
    [InlineData("a  b c", 0)] // Every parser of the value matched
    [InlineData("a b", 0)]
    public void Span_Code_Token_Which_Isnt_Found_After_White_Space_Is_Reported_In_A_Rejected_Value(string text, int offset)
    {
        Assert.False(FailureGrammars.RejectedSpanWhiteSpace(text, out _, out var failureOffset));
        Assert.Equal(offset, failureOffset);

        Assert.False(FailureGrammars.RejectedSpanWhiteSpace(new OneCharReader(text), out _, out (int Offset, int Line, int Column) failure));
        Assert.Equal(offset, failure.Offset);
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
