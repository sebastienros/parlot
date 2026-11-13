using Parlot.Fluent;
using System.IO;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The furthest failure of runtime parsers, <see cref="Cursor.FurthestOffset"/> and <see cref="Cursor.FurthestPosition"/>.
/// The source-generated entry points returning it are covered by the standalone FailureTests, with the same grammars.
/// </summary>
public class FurthestFailureTests
{
    private static readonly Parser<string> Select =
        Terms.Keyword("SELECT", caseInsensitive: true)
            .SkipAnd(Separated(Terms.Char(','), Terms.Identifier()))
            .AndSkip(Terms.Keyword("FROM", caseInsensitive: true))
            .And(Terms.Identifier())
            .Then(static value => value.Item2.ToString() + ":" + value.Item1.Count)
            .Eof();

    private static readonly Parser<long> Expression = BuildExpression();

    private static Parser<long> BuildExpression()
    {
        var expression = Deferred<long>();
        var primary = Terms.Integer().Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        expression.Parser = primary.And(ZeroOrMany(Terms.Char('+').SkipAnd(primary)))
            .Then(static value =>
            {
                var sum = value.Item1;
                foreach (var item in value.Item2)
                {
                    sum += item;
                }
                return sum;
            });
        return expression.Eof();
    }

    // A program: statements, then the end of the text or an error
    private static readonly Parser<System.Collections.Generic.IReadOnlyList<string>> Program =
        ZeroOrMany(
            Terms.Keyword("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char('=')).AndSkip(Terms.Integer()).AndSkip(Terms.Char(';'))
                .Or(Terms.Identifier().AndSkip(Terms.Text("()")).AndSkip(Terms.Char(';')))
                .Then(static value => value.ToString()))
            .AndSkip(Eof().ElseError("Expected a statement"));

    private static ParseContext Context(string text) => new(new Scanner(text));

    [Theory]
    [InlineData("SELECT a, b FRM t", 12, 1, 13)]
    [InlineData("SELECT a, FROM t", 15, 1, 16)]
    [InlineData("SELECT a b", 9, 1, 10)]
    [InlineData("SELECT", 6, 1, 7)]
    [InlineData("SELEC a", 0, 1, 1)]
    [InlineData("", 0, 1, 1)]
    [InlineData("SELECT a\nFROM\n  t u", 17, 3, 4)]
    [InlineData("SELECT a\r\n, b\r\nFROM 1", 20, 3, 6)]
    public void Failed_Parse_Reports_The_Furthest_Failure(string text, int offset, int line, int column)
    {
        var context = Context(text);

        Assert.False(Select.TryParse(context, out _, out var error));
        Assert.Null(error);

        var cursor = context.Scanner.Cursor;

        // The parser moved the cursor back to where it started
        Assert.Equal(0, cursor.Offset);
        Assert.Equal(offset, cursor.FurthestOffset);
        Assert.Equal(new TextPosition(offset, line, column), cursor.FurthestPosition);
    }

    [Theory]
    [InlineData("1 + (2 + 3", 10, 1, 11)]
    [InlineData("1 + (2 + )", 9, 1, 10)]
    [InlineData("1 + (2 + 3))", 11, 1, 12)]
    [InlineData("((1)\n+\n(2 +\nx))", 12, 4, 1)]
    [InlineData("1 +", 3, 1, 4)]
    [InlineData("x", 0, 1, 1)]
    public void Nested_Grammar_Reports_The_Furthest_Alternative(string text, int offset, int line, int column)
    {
        var context = Context(text);

        Assert.Equal(0, Expression.Parse(context));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(new TextPosition(offset, line, column), context.Scanner.Cursor.FurthestPosition);

        // The same through the overloads which create the context, and TryMatch
        context = Context(text);
        Assert.False(Expression.TryParse(context, out _, out _));
        Assert.Equal(offset, context.Scanner.Cursor.FurthestOffset);

        context = Context(text);
        Assert.False(Expression.TryMatch(context, out _, out _));
        Assert.Equal(offset, context.Scanner.Cursor.FurthestOffset);
    }

    [Fact]
    public void Error_At_The_Start_Of_A_Statement_Keeps_The_Furthest_Failure()
    {
        var text = "let a = 1;\nfoo();\nlet b = ;";
        var context = Context(text);

        Assert.False(Program.TryParse(context, out _, out var error));

        // The error is where the statements ended, before the white space; the furthest failure is where the last one went wrong
        Assert.NotNull(error);
        Assert.Equal(new TextPosition(17, 2, 7), error.Position);
        Assert.Equal(new TextPosition(26, 3, 9), context.Scanner.Cursor.FurthestPosition);
    }

    [Fact]
    public void Furthest_Failure_Is_Reset_Between_Parses_On_A_Reused_Context()
    {
        var context = Context("1 + (2 + 3");

        Assert.False(Expression.TryParse(context, out _, out _));
        Assert.Equal(10, context.Scanner.Cursor.FurthestOffset);

        // A parser failing earlier on the same context reports its own failure, not the one of the previous parse
        Assert.False(Terms.Char('(').TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.FurthestOffset);

        Assert.Equal(0, Expression.Parse(context));
        Assert.Equal(10, context.Scanner.Cursor.FurthestOffset);

        Assert.False(Terms.Text("1 -").TryMatch(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.FurthestOffset);
    }

    [Fact]
    public void Successive_Parses_Report_From_Where_They_Start()
    {
        // Parsing a statement at a time on the same context
        var statement = Terms.Integer().AndSkip(Terms.Char(';'));
        var context = Context("1; 2; 3 4");
        var cursor = context.Scanner.Cursor;

        Assert.True(statement.TryParse(context, out var value, out _));
        Assert.Equal(1, value);
        Assert.True(statement.TryParse(context, out value, out _));
        Assert.Equal(2, value);
        Assert.Equal(5, cursor.Offset);

        // The previous parses read up to 5, the failure is at the '4', the start is restored
        Assert.False(statement.TryParse(context, out _, out _));
        Assert.Equal(5, cursor.Offset);
        Assert.Equal(new TextPosition(8, 1, 9), cursor.FurthestPosition);
    }

    [Fact]
    public void ResetFurthestOffset_Forgets_The_Recorded_Failures()
    {
        var context = Context("1 + (2 + 3");
        var cursor = context.Scanner.Cursor;
        var result = new ParseResult<long>();

        Assert.False(Expression.Parse(context, ref result));
        Assert.Equal(10, cursor.FurthestOffset);

        // The low level Parse doesn't reset it: it is invoked by the parsers which compose others
        var charResult = new ParseResult<char>();
        Assert.False(Terms.Char('(').Parse(context, ref charResult));
        Assert.Equal(10, cursor.FurthestOffset);

        cursor.ResetFurthestOffset();
        Assert.Equal(0, cursor.FurthestOffset);
        Assert.Equal(new TextPosition(0, 1, 1), cursor.FurthestPosition);

        // A successful parse reports at least where it ended
        Assert.True(Terms.Integer().Parse(context, ref result));
        Assert.Equal(1, cursor.FurthestOffset);
    }

    [Fact]
    public void Successful_Parse_Reports_Where_It_Ended_Or_Further()
    {
        var context = Context("SELECT a, b FROM t");
        Assert.Equal("t:2", Select.Parse(context));
        Assert.Equal(18, context.Scanner.Cursor.FurthestOffset);
    }

    private static readonly Parser<string> Rejected =
        Terms.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from").AndSkip(Terms.Char(';')).Eof();

    private static readonly Parser<string> NotFollowed =
        Terms.Identifier().WhenNotFollowedBy(Terms.Text("((")).Then(static value => value.ToString()).AndSkip(Terms.Char(';')).Eof();

    private static readonly Parser<string> Followed =
        Terms.Identifier().WhenFollowedBy(Terms.Text("((")).Then(static value => value.ToString()).AndSkip(Terms.Char('x')).Eof();

    private static readonly Parser<string> Negative =
        Not(Terms.Text("abc")).SkipAnd(Terms.Identifier()).Then(static value => value.ToString()).AndSkip(Terms.Char(';')).Eof();

    // The furthest failure of the string and of the TextReader parsers, which match the generated ones (standalone FailureTests)
    private static void AssertFurthest<T>(Parser<T> parser, string text, int offset)
    {
        var context = Context(text);
        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(offset, context.Scanner.Cursor.FurthestOffset);

        ParseContext readerContext = null!;
        var options = new StreamParseOptions
        {
            BufferSize = 1,
            ContextFactory = (scanner, token) => readerContext = new ParseContext(scanner, token),
        };

        Assert.False(parser.TryParse(new OneCharReader(text), out _, out _, options));
        Assert.NotNull(readerContext);
        Assert.Equal(offset, readerContext.Scanner.Cursor.FurthestOffset);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from;", 3)]
    [InlineData("  fromage,", 9)]
    [InlineData("  ,", 2)]
    public void Rejected_Value_Fails_At_Its_Start(string text, int offset) => AssertFurthest(Rejected, text, offset);

    [Fact]
    public void Rejected_Value_Doesnt_Stop_A_Longer_Attempt_Being_Reported()
    {
        AssertFurthest(Terms.Identifier().SkipAnd(Terms.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from")).Eof(), "  a  from", 5);
        AssertFurthest(Literals.Identifier().Then(static value => value.ToString()).When(static (_, value) => value != "from").AndSkip(Terms.Char(';')).Eof(), "from;", 0);

        var context = Context("  word ;");
        Assert.True(Rejected.TryParse(context, out var value, out _));
        Assert.Equal("word", value);
    }

    [Theory]
    [InlineData("abc((", 3)]
    [InlineData("abc  ((;", 5)]
    [InlineData("abc  (;", 5)]
    [InlineData("abc ,", 4)]
    public void Not_Followed_By_Fails_At_What_Follows(string text, int offset) => AssertFurthest(NotFollowed, text, offset);

    [Theory]
    [InlineData("abc((", 3)]
    [InlineData("abc  ((y", 5)]
    [InlineData("abc  (", 5)]
    [InlineData("abc", 3)]
    public void Followed_By_That_Matched_Doesnt_Count(string text, int offset) => AssertFurthest(Followed, text, offset);

    [Theory]
    [InlineData("abc", 0)]
    [InlineData("  abc", 2)]
    [InlineData("  abd,", 5)]
    public void Not_Fails_At_What_Matched(string text, int offset) => AssertFurthest(Negative, text, offset);

    [Theory]
    [InlineData("a from", 0)]
    [InlineData("  a from", 2)]
    [InlineData("  a  from", 2)]
    [InlineData("\n a\n from", 2)]
    public void Moving_Forward_In_A_Rejected_Value_Isnt_A_Failure(string text, int offset)
    {
        // The lookahead skips the white space after the first word, and the second word then moves the cursor over it,
        // forward, without reading it again
        var words = Terms.Identifier().WhenFollowedBy(Terms.Identifier()).And(Terms.Identifier());
        AssertFurthest(words.When(static (_, _) => false).Eof(), text, offset);

        // The cursor is moved forward to the end of the match that a selector keeps
        var selected = Terms.Identifier().And(Longest(static matches => matches.IndexOfLongest(), Terms.Text("fr"), Terms.Text("from")));
        AssertFurthest(selected.When(static (_, _) => false).Eof(), text, offset);
    }

    [Theory]
    [InlineData("a from", 0)]
    [InlineData("  a from", 2)]
    [InlineData("\n a\n from", 2)]
    public void Rejected_Value_Ending_With_The_Longest_Match_Fails_At_Its_Start(string text, int offset)
    {
        // The cursor is moved back from each alternative which matched, to read the next one: not a failure where it ended
        var word = Terms.Identifier().Then(static value => value.ToString());

        AssertFurthest(Terms.Identifier().And(Longest(Terms.Text("fr"), Terms.Text("from"))).When(static (_, _) => false).Eof(), text, offset);
        AssertFurthest(Terms.Identifier().And(Longest(Terms.Text("from"), Terms.Text("fr"))).When(static (_, _) => false).Eof(), text, offset);
        AssertFurthest(Terms.Identifier().And(Longest(Terms.Text("fr"), word)).When(static (_, _) => false).Eof(), text, offset);
        AssertFurthest(Terms.Identifier().And(Longest(Terms.Text("fr"), Terms.Text("fro"), word)).When(static (_, _) => false).Eof(), text, offset);

        // The alternatives are read without their value
        AssertFurthest(Terms.Identifier().AndSkip(Longest(Terms.Text("fr"), Terms.Text("from"))).When(static (_, _) => false).Eof(), text, offset);
        AssertFurthest(Terms.Identifier().AndSkip(Longest(Terms.Text("fr"), word)).When(static (_, _) => false).Eof(), text, offset);
    }

    [Theory]
    [InlineData("a from?", 6)]
    [InlineData("  a from?", 8)]
    public void Rejected_Value_Reports_An_Alternative_Of_Longest_Which_Failed_Further(string text, int offset)
    {
        // "from" isn't followed by '!': that alternative fails after the match that is kept, and stays the furthest failure
        var longest = Longest(Terms.Text("fr"), Terms.Text("from").AndSkip(Literals.Char('!')));

        AssertFurthest(Terms.Identifier().And(longest).When(static (_, _) => false).Eof(), text, offset);
        AssertFurthest(Terms.Identifier().AndSkip(longest).When(static (_, _) => false).Eof(), text, offset);
        AssertFurthest(Terms.Identifier().And(Longest(static matches => matches.IndexOfLongest(), Terms.Text("fr"), Terms.Text("from").AndSkip(Literals.Char('!')))).When(static (_, _) => false).Eof(), text, offset);
    }

    [Theory]
    [InlineData("a  x", 3)]
    [InlineData("a  b  x", 6)]
    [InlineData("a  b  c  x", 9)]
    [InlineData("a\n b\n x", 6)]
    [InlineData("a b c x", 6)]
    [InlineData("ax", 1)]
    public void Optional_Text_Fails_After_The_White_Space_Before_It(string text, int offset)
    {
        // What follows is read from before that white space: generated span code moves the cursor back there
        var parser = Terms.Text("a").AndSkip(Terms.Text("b").AndSkip(Terms.Text("c")).Optional()).AndSkip(Terms.Text("d").Optional()).Eof();
        AssertFurthest(parser, text, offset);
    }

    [Theory]
    [InlineData("1 + (2 + 3", 10)]
    [InlineData("((1)\n+\n(2 +\nx))", 12)]
    public void TextReader_Context_Reports_The_Furthest_Failure(string text, int offset) => AssertFurthest(Expression, text, offset);

    // Returns one char per read, so that a TextReader parser refills its buffer as often as possible
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
