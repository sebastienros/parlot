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

    // The same word, rejected by a conversion
    private static readonly Parser<string> ConversionRejected =
        Terms.Identifier().ThenTry(static (ParseContext _, int _, int _, TextSpan value, out string result) =>
        {
            result = value.ToString();
            return result != "from";
        }).AndSkip(Terms.Char(';')).Eof();

    private static readonly Parser<string> ConversionRejectedLiteral =
        Literals.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString())).AndSkip(Terms.Char(';')).Eof();

    // A value of two words that a conversion rejects: the white space before it isn't the last one which was skipped
    private static readonly Parser<string> ConversionRejectedWords =
        Terms.Identifier().And(Terms.Identifier()).ThenTry(static value => (value.Item2.ToString() != "from", value.Item1.ToString()))
            .AndSkip(Terms.Char(';')).Eof();

    // The word after comments, which a parser skips instead of the white space set
    private static readonly Parser<string> ConversionRejectedAfterComments =
        ConversionRejected.WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    // The word after a text which isn't there: its white space was skipped before the word is read
    private static readonly Parser<string> ConversionRejectedAlternative =
        Terms.Text("select").Or(Terms.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString())))
            .AndSkip(Terms.Char(';')).Eof();

    private static readonly Parser<string> NotFollowed =
        Terms.Identifier().WhenNotFollowedBy(Terms.Text("((")).Then(static value => value.ToString()).AndSkip(Terms.Char(';')).Eof();

    private static readonly Parser<string> Followed =
        Terms.Identifier().WhenFollowedBy(Terms.Text("((")).Then(static value => value.ToString()).AndSkip(Terms.Char('x')).Eof();

    private static readonly Parser<string> Negative =
        Not(Terms.Text("abc")).SkipAnd(Terms.Identifier()).Then(static value => value.ToString()).AndSkip(Terms.Char(';')).Eof();

    // The furthest failure of the string and of the TextReader parsers, which match the generated ones (standalone FailureTests)
    private static void AssertFurthest<T>(Parser<T> parser, string text, int offset, int bufferSize = 1)
    {
        var context = Context(text);
        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(offset, context.Scanner.Cursor.FurthestOffset);

        ParseContext readerContext = null!;
        var options = new StreamParseOptions
        {
            BufferSize = bufferSize,
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
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from;", 3)]
    [InlineData("  fromage,", 9)]
    [InlineData("  ,", 2)]
    public void Value_Rejected_By_A_Conversion_Fails_At_Its_Start(string text, int offset)
    {
        AssertFurthest(ConversionRejected, text, offset);

        var context = Context("  word ;");
        Assert.True(ConversionRejected.TryParse(context, out var value, out _));
        Assert.Equal("word", value);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("fromage,", 7)]
    public void Value_Rejected_By_A_Tuple_Conversion_Fails_At_Its_Start(string text, int offset) => AssertFurthest(ConversionRejectedLiteral, text, offset);

    [Fact]
    public void Value_Rejected_By_A_Conversion_Doesnt_Stop_A_Longer_Attempt_Being_Reported()
    {
        AssertFurthest(Terms.Identifier().SkipAnd(Terms.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString()))).Eof(), "  a  from", 5);

        // A failure recorded further before the value is rejected is kept
        var further = Terms.Text("from").And(Terms.Char('(')).And(Terms.Char(')')).Then(static _ => "call")
            .Or(Terms.Identifier().ThenTry(static value => (value.ToString() != "from", value.ToString())));
        AssertFurthest(further.Eof(), "  from ( x", 9);
    }

    [Theory]
    [InlineData("a from;", 0)]
    [InlineData("  a from;", 2)]
    [InlineData("  a  from;", 2)]
    [InlineData("\n a\n from;", 2)]
    [InlineData("  a  b,", 6)]
    [InlineData("  a  ,", 5)]
    public void Value_Of_Several_Tokens_Rejected_By_A_Conversion_Fails_At_Its_Start(string text, int offset) => AssertFurthest(ConversionRejectedWords, text, offset);

    [Theory]
    [InlineData("  from;", 2)]
    [InlineData("/* a */ from;", 8)]
    [InlineData(" /* a */\n /* b */ from;", 18)]
    [InlineData(" /* a */ fromage,", 16)]
    [InlineData(" /* a */ /from;", 9)]
    public void Value_Rejected_By_A_Conversion_Fails_After_The_Comments_Before_It(string text, int offset) => AssertFurthest(ConversionRejectedAfterComments, text, offset);

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from;", 3)]
    [InlineData("  fromage,", 9)]
    public void Value_Rejected_By_A_Conversion_Fails_At_Its_Start_After_Another_Alternative(string text, int offset) => AssertFurthest(ConversionRejectedAlternative, text, offset);

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

        // A conversion rejects these values at the same place
        AssertFurthest(words.ThenTry(static _ => (false, 0)).Eof(), text, offset);
        AssertFurthest(selected.ThenTry(static _ => (false, 0)).Eof(), text, offset);
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

        // Rejected by a conversion
        AssertFurthest(Terms.Identifier().And(Longest(Terms.Text("fr"), Terms.Text("from"))).ThenTry(static _ => (false, 0)).Eof(), text, offset);
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
    [InlineData(">ab;;", 1)]
    [InlineData("  >ab;;", 3)]
    [InlineData(">a;b;;", 1)]
    [InlineData(">ab ;;", 1)]
    [InlineData(">a b ;;", 1)]
    [InlineData("  >two words  and ; more ;;", 3)]
    public void Rejected_Text_Before_A_Delimiter_Fails_At_Its_Start(string text, int offset)
    {
        // The delimiter is found and not consumed: the cursor is moved back from a delimiter which matched, not a failure
        // where it ended
        var literal = Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;")).And(Literals.Char(';')).When(static (_, _) => false)).Eof();
        AssertFurthest(literal, text, offset);

        // A delimiter which skips white space isn't searched for by its first chars
        var term = Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;")).And(Terms.Char(';')).When(static (_, _) => false)).Eof();
        AssertFurthest(term, text, offset);

        // The text alone, and the text followed by its delimiter
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;")).When(static (_, _) => false)).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;")).AndSkip(Terms.Text(";;")).When(static (_, _) => false)).Eof(), text, offset);

        // Rejected by a conversion
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;")).And(Literals.Char(';')).ThenTry(static _ => (false, 0))).Eof(), text, offset);

        // A delimiter which is consumed is part of the value
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), consumeDelimiter: true).When(static (_, _) => false)).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;"), consumeDelimiter: true).When(static (_, _) => false)).Eof(), text, offset);

        // A delimiter which reads some text before it fails, where it is looked for in the text
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Char(';').And(Literals.Char(';'))).When(static (_, _) => false)).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Char(';').And(Terms.Char(';')), consumeDelimiter: true).When(static (_, _) => false)).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">ab", 1)]
    [InlineData(">a b", 1)]
    [InlineData("  >two words  and ; more", 3)]
    public void Rejected_Text_Up_To_The_End_Fails_At_Its_Start(string text, int offset)
    {
        // The delimiter isn't found: where it was looked for in the text isn't a failure either
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;")).When(static (_, _) => false)).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Char(';').And(Terms.Char(';')), consumeDelimiter: true).When(static (_, _) => false)).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">12 foo bar: x", 7)]
    [InlineData(">foo  bar baz :", 9)]
    public void Delimiter_Looked_For_Past_The_End_Of_The_Text_Isnt_A_Failure(string text, int offset)
    {
        // `foo` isn't followed by a colon: the delimiter is looked for from there up to `bar`, past where the text ends
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Identifier().And(Terms.Char(':')))).AndSkip(Literals.Char('!')).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">ab;;?", 3)]
    [InlineData(">a;b;;?", 4)]
    [InlineData(">a;b;;", 4)]
    [InlineData(">;;?", 1)]
    public void Parser_After_A_Text_Before_A_Delimiter_Fails_Where_It_Starts(string text, int offset)
    {
        // The text ends before the delimiter, where the next parser is expected: the delimiter was only looked for
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), canBeEmpty: true)).AndSkip(Literals.Char('!')).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;"), canBeEmpty: true)).AndSkip(Literals.Char('!')).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">;;", 1)]
    [InlineData("  >;;!", 3)]
    [InlineData("  > ;;!", 3)]
    public void Empty_Text_Before_A_Delimiter_Fails_At_The_Delimiter(string text, int offset)
    {
        // The delimiter matched where a text is expected, like what Not finds: nothing was read further
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;"))).AndSkip(Terms.Text(";;")).AndSkip(Terms.Char('!')).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;"), consumeDelimiter: true)).AndSkip(Terms.Char('!')).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">ab;", 4)]
    [InlineData(">ab", 3)]
    [InlineData(">", 1)]
    [InlineData(">a;b;c", 6)]
    public void Delimiter_Which_Isnt_Found_Fails_At_The_End(string text, int offset)
    {
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), failOnEof: true)).AndSkip(Literals.Text(";;")).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Terms.Text(";;"), failOnEof: true)).AndSkip(Terms.Text(";;")).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), failOnEof: true, consumeDelimiter: true)).Eof(), text, offset);

        // The whole text is read when the end isn't a failure, and the delimiter is expected there
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), canBeEmpty: true)).AndSkip(Literals.Text(";;")).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">ab;;cd?", 7)]
    [InlineData(">ab;;  cd ?", 10)]
    [InlineData(">ab;;?", 5)]
    public void Parser_Failing_After_The_Delimiter_Is_Still_The_Furthest_Failure(string text, int offset)
    {
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"))).AndSkip(Literals.Text(";;")).AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Eof(), text, offset);
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(Literals.Text(";;"), consumeDelimiter: true)).AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">ab;;cd?", 7)]
    [InlineData(">ab;;  cd ?", 10)]
    [InlineData(">ab;;?", 5)]
    public void Alternative_Of_A_Delimiter_Counts_When_The_Delimiter_Is_Consumed(string text, int offset)
    {
        // The first alternative reads further than the one which matches
        var delimiter = Literals.Text(";;").AndSkip(Terms.Identifier()).AndSkip(Terms.Char('!')).Or(Literals.Text(";;"));

        // A delimiter which is consumed is part of the value, like the alternatives of a choice in it
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(delimiter, consumeDelimiter: true).When(static (_, _) => false)).Eof(), text, offset);

        // One which isn't consumed is a lookahead, like the one of WhenFollowedBy
        AssertFurthest(Terms.Char('>').SkipAnd(AnyCharBefore(delimiter).When(static (_, _) => false)).Eof(), text, 1);
        AssertFurthest(Terms.Char('>').SkipAnd(Literals.Text("ab").WhenFollowedBy(delimiter).When(static (_, _) => false)).Eof(), text, 1);
    }

    [Theory]
    [InlineData(">/* a comment */", 1)]
    [InlineData("  >/* a comment */", 3)]
    [InlineData(">'a\\'b' ", 1)]
    [InlineData(">  'a\\'b' ", 3)]
    public void Token_Read_Again_With_More_Text_Forgets_What_It_Recorded(string text, int offset)
    {
        // A token which ends with the buffer fails there and moves the cursor back, then is read again with more text:
        // the end of the buffer isn't a failure
        var comment = Terms.Char('>').SkipAnd(Terms.Comments("/*", "*/").When(static (_, _) => false)).Eof();
        var quoted = Terms.Char('>').SkipAnd(Terms.String().When(static (_, _) => false)).Eof();
        var parser = text.Contains('/') ? comment.Then(static _ => 0) : quoted.Then(static _ => 0);

        for (var bufferSize = 1; bufferSize <= 8; bufferSize++)
        {
            AssertFurthest(parser, text, offset, bufferSize);
        }
    }

    [Theory]
    [InlineData(">1 + 2", 1)]
    [InlineData(">1  +  2", 1)]
    [InlineData("  >  1 + 2 * 3", 5)]
    [InlineData(">1 * 2 + 3", 1)]
    [InlineData(">1", 1)]
    public void Rejected_Expression_Of_Valued_Operators_Fails_At_Its_Start(string text, int offset)
    {
        // The white space before an operator whose value is passed to the factory is skipped to look for it, and the
        // cursor is moved back for the level which parses the operator: not a failure
        var valued = Terms.Integer().Operators()
            .LeftAssociative([Terms.Text("*"), Terms.Text("/")], static (a, b, op) => op == "*" ? a * b : a / b)
            .LeftAssociative([Terms.Text("+"), Terms.Text("-")], static (a, b, op) => op == "+" ? a + b : a - b);

        AssertFurthest(Terms.Char('>').SkipAnd(valued.When(static (_, _) => false)).Eof(), text, offset);

        // The same table, whose factories don't receive the operators
        var plain = Terms.Integer().Operators()
            .LeftAssociative((Terms.Text("*"), static (a, b) => a * b), (Terms.Text("/"), static (a, b) => a / b))
            .LeftAssociative((Terms.Text("+"), static (a, b) => a + b), (Terms.Text("-"), static (a, b) => a - b));

        AssertFurthest(Terms.Char('>').SkipAnd(plain.When(static (_, _) => false)).Eof(), text, offset);
    }

    [Theory]
    [InlineData(">1 + 2 ?", 7)]
    [InlineData(">1 +", 4)]
    [InlineData(">1  +  x", 7)]
    [InlineData(">1 * 2 + ", 9)]
    public void Expression_Of_Valued_Operators_Reports_What_Follows_It(string text, int offset)
    {
        var valued = Terms.Integer().Operators()
            .LeftAssociative([Terms.Text("*"), Terms.Text("/")], static (a, b, op) => op == "*" ? a * b : a / b)
            .LeftAssociative([Terms.Text("+"), Terms.Text("-")], static (a, b, op) => op == "+" ? a + b : a - b);

        AssertFurthest(Terms.Char('>').SkipAnd(valued).Eof(), text, offset);

        var plain = Terms.Integer().Operators()
            .LeftAssociative((Terms.Text("*"), static (a, b) => a * b), (Terms.Text("/"), static (a, b) => a / b))
            .LeftAssociative((Terms.Text("+"), static (a, b) => a + b), (Terms.Text("-"), static (a, b) => a - b));

        AssertFurthest(Terms.Char('>').SkipAnd(plain).Eof(), text, offset);
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
