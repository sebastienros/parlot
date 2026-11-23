#nullable enable

using Parlot.Fluent;
using System;
using System.IO;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The reason a parser which rejects what it read records with the failure, <see cref="Cursor.FurthestReason"/> and
/// <see cref="ParseError.Reason"/>. The source-generated entry points returning it are covered by the standalone
/// FailureTests, and their parity with the runtime parsers by RejectedValueParityTests.
/// </summary>
public class FailureReasonTests
{
    public enum Reason
    {
        ReservedWord,
        IdentifierAfterNumber,
        InvalidDigit,
    }

    [Flags]
    public enum Flags
    {
        A = 1,
        B = 2,
    }

    private static readonly Parser<string> Word =
        Terms.Identifier().Then(static value => value.ToString());

    // A word which isn't reserved, then a ';'
    private static readonly Parser<string> Statement =
        Word.When(static (_, value) => value != "from", "'from' is reserved").AndSkip(Terms.Char(';')).Eof();

    // A number which isn't followed by an identifier
    private static readonly Parser<long> Number =
        Terms.Integer().WhenNotFollowedBy(Literals.Identifier(), Reason.IdentifierAfterNumber).Eof();

    // An octal number, whose conversion tells which digit is wrong
    private static readonly Parser<long> Octal =
        Terms.Pattern(static c => c >= '0' && c <= '9').ThenTry(static (ParseContext context, int start, int end, TextSpan value, out long result) =>
        {
            result = 0;
            var span = value.Span;

            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] > '7')
                {
                    context.Scanner.Cursor.RecordFailure(start + i, Reason.InvalidDigit);
                    return false;
                }

                result = result * 8 + span[i] - '0';
            }

            return true;
        }).Eof();

    private static ParseContext Context(string text) => new(new Scanner(text));

    // The error of the string and of the TextReader parsers
    private static void AssertReason<T>(Parser<T> parser, string text, object? reason, int offset)
    {
        var context = Context(text);
        Assert.False(parser.TryParse(context, out _, out var error));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(offset, context.Scanner.Cursor.FurthestOffset);
        Assert.Equal(reason, context.Scanner.Cursor.FurthestReason);
        AssertError(error, reason, offset);

        Assert.False(parser.TryParse(new OneCharReader(text), out _, out var readerError, new StreamParseOptions { BufferSize = 1 }));
        AssertError(readerError, reason, offset);

        Assert.False(parser.TryMatch(Context(text), out _, out var matchError));
        AssertError(matchError, reason, offset);
    }

    private static void AssertError(ParseError? error, object? reason, int offset)
    {
        if (reason is null)
        {
            Assert.Null(error);
            return;
        }

        Assert.NotNull(error);
        Assert.Equal(reason, error.Reason);
        Assert.Equal(reason as string ?? reason.ToString(), error.Message);
        Assert.Equal(offset, error.Position.Offset);
        Assert.False(error.IsNestedTooDeeply);
    }

    [Theory]
    [InlineData("from;", 0)]
    [InlineData("  from;", 2)]
    [InlineData("\n  from ;", 3)]
    public void Rejected_Value_Reports_Its_Reason(string text, int offset) => AssertReason(Statement, text, "'from' is reserved", offset);

    [Fact]
    public void Reason_Is_Reported_With_The_Position()
    {
        Assert.False(Statement.TryParse("\n  from;", out _, out var error));
        Assert.Equal(new TextPosition(3, 2, 3), error!.Position);
    }

    [Fact]
    public void Accepted_Value_Leaves_No_Error()
    {
        var context = Context(" word;");
        Assert.True(Statement.TryParse(context, out var value, out var error));
        Assert.Equal("word", value);
        Assert.Null(error);
        Assert.Null(context.Scanner.Cursor.FurthestReason);
    }

    [Theory]
    [InlineData("word", 4)]
    [InlineData("word ,", 5)]
    [InlineData("from", 0)]
    public void Further_Failure_Without_A_Reason_Hides_It(string text, int offset)
    {
        // "from" is rejected with a reason at 0, the ';' expected at 4 is further, but "from" alone fails at its start
        AssertReason(Statement, text, text == "from" ? "'from' is reserved" : null, offset);
    }

    [Fact]
    public void Further_Failure_Of_An_Alternative_Hides_It()
    {
        // The rejected word fails at 0, the alternative reads "from" and fails at 5, where it expects a '!'
        var parser = Statement.Or(Terms.Text("from").SkipAnd(Terms.Char('!')).Then(static _ => "!")).Eof();

        AssertReason(parser, "from ?", null, 5);
        Assert.Equal("!", parser.Parse("from !"));
    }

    [Fact]
    public void Alternative_Which_Matches_After_The_Rejection_Leaves_No_Error()
    {
        var parser = Word.When(static (_, value) => value != "from", "'from' is reserved")
            .Or(Terms.Text("from").Then(static _ => "keyword"))
            .AndSkip(Terms.Char(';')).Eof();

        var context = Context("from;");
        Assert.True(parser.TryParse(context, out var value, out var error));
        Assert.Equal("keyword", value);
        Assert.Null(error);
    }

    [Fact]
    public void First_Reason_At_An_Offset_Is_Kept()
    {
        var parser = OneOf(
            Word.When(static (_, value) => value != "from", "first"),
            Word.When(static (_, value) => value != "from", "second")).Eof();

        AssertReason(parser, " from", "first", 1);
    }

    [Fact]
    public void Further_Reason_Replaces_A_Reason()
    {
        var parser = OneOf(
            Word.When(static (_, value) => value != "a", "first"),
            Word.AndSkip(Terms.Identifier().When(static (_, value) => value.ToString() != "b", "second"))).Eof();

        AssertReason(parser, "a b", "second", 2);
    }

    [Theory]
    [InlineData("3in", 1)]
    [InlineData(" 12x", 3)]
    public void Not_Followed_By_Reports_Its_Reason(string text, int offset) => AssertReason(Number, text, Reason.IdentifierAfterNumber, offset);

    [Fact]
    public void Not_Reports_Its_Reason()
    {
        var parser = Not(Terms.Keyword("let"), "'let' can't start an expression").SkipAnd(Word).Eof();

        AssertReason(parser, "  let", "'let' can't start an expression", 2);
        Assert.Equal("lets", parser.Parse("lets"));
    }

    [Theory]
    [InlineData("128", 2)]
    [InlineData(" 7709", 4)]
    public void Conversion_Records_Its_Own_Reason(string text, int offset) => AssertReason(Octal, text, Reason.InvalidDigit, offset);

    [Fact]
    public void Conversion_Reports_Its_Reason()
    {
        var conversion = Word.ThenTry(static (ParseContext _, int _, int _, string value, out int result) =>
        {
            result = value.Length;
            return value != "from";
        }, Reason.ReservedWord).Eof();

        var tuple = Word.ThenTry(static value => (value != "from", value.Length), Reason.ReservedWord).Eof();

        AssertReason(conversion, "  from", Reason.ReservedWord, 2);
        AssertReason(tuple, "  from", Reason.ReservedWord, 2);
        Assert.Equal(4, tuple.Parse("word"));
    }

    [Fact]
    public void Message_Is_The_Text_Of_A_Reason_Which_Isnt_A_String()
    {
        var parser = Word.When(static (_, _) => false, Flags.A | Flags.B);

        Assert.False(parser.TryParse("x", out _, out var error));
        Assert.Equal(Flags.A | Flags.B, error!.Reason);
        Assert.Equal("A, B", error.Message);
    }

    [Fact]
    public void Lookahead_Forgets_The_Reason_Its_Parser_Recorded()
    {
        // The second word is rejected at 2 inside Not, which then matches: a failure at 2 after it has no reason
        var rejected = Word.AndSkip(Terms.Identifier().When(static (_, value) => value.ToString() == "x", "inner"));
        var parser = Not(rejected).SkipAnd(Word.AndSkip(Terms.Char('!'))).Eof();

        AssertReason(parser, "y z", null, 2);

        var followed = Peek(rejected.Or(Word)).SkipAnd(Word.AndSkip(Terms.Char('!'))).Eof();

        AssertReason(followed, "y z", null, 2);

        var notFollowed = Word.WhenNotFollowedBy(Terms.Char(',').SkipAnd(rejected)).AndSkip(Terms.Char('!')).Eof();

        AssertReason(notFollowed, "a ,y z", null, 2);
    }

    [Fact]
    public void Lookahead_Forgets_Reason_Recorded_At_The_Same_Offset()
    {
        // A reason recorded inside Not at offset 0 (same as recordedFailure) must not leak
        var atZero = Not(Word.When(static (_, _) => false, "inner")).SkipAnd(Terms.Char('!')).Eof();
        AssertReason(atZero, "foo", null, 0);

        // Same at a non-zero offset when no reason was recorded there before the lookahead
        var atNonZero = Terms.Char('>').SkipAnd(Not(Word.When(static (_, _) => false, "inner"))).SkipAnd(Terms.Char('!')).Eof();
        AssertReason(atNonZero, ">foo", null, 1);

        // Peek succeeding at offset 0 after an inner branch recorded a reason at offset 0
        var peekAtZero = Peek(Word.When(static (_, _) => false, "inner").Or(Word)).SkipAnd(Terms.Char('!')).Eof();
        AssertReason(peekAtZero, "foo", null, 0);
    }

    [Fact]
    public void Lookahead_Preserves_Existing_Reason_At_The_Same_Offset()
    {
        // Branch 1 records "first" at offset 0; Branch 2 runs a lookahead at offset 0 which must not erase "first"
        var parser = OneOf(
            Word.When(static (_, _) => false, "first"),
            Not(Word.When(static (_, _) => false, "inner")).SkipAnd(Terms.Char('!')).Then(static _ => "!")).Eof();

        AssertReason(parser, "foo", "first", 0);

        var atNonZero = Terms.Char('>').SkipAnd(OneOf(
            Word.When(static (_, _) => false, "first"),
            Not(Word.When(static (_, _) => false, "inner")).SkipAnd(Terms.Char('!')).Then(static _ => "!"))).Eof();

        AssertReason(atNonZero, ">foo", "first", 1);
    }

    [Fact]
    public void RecordFailure_Behind_FurthestOffset_Does_Not_Overwrite_Or_Leak_Reason()
    {
        var cursor = new Cursor("abcdef", TextPosition.Start);
        cursor.RecordFailure(4, null);
        cursor.RecordFailure(2, "behind");

        Assert.Equal(4, cursor.FurthestOffset);
        Assert.Null(cursor.FurthestReason);

        // Recording a reason at the actual furthest offset still works afterward
        cursor.RecordFailure(4, "at-furthest");
        Assert.Equal("at-furthest", cursor.FurthestReason);
    }

    [Fact]
    public void Lookahead_Keeps_The_Reason_Of_Its_Parser_When_It_Fails()
    {
        // The parser of Peek is expected where it fails, its reason too
        var parser = Peek(Word.When(static (_, value) => value == "x", "inner")).SkipAnd(Word).Eof();

        AssertReason(parser, " y", "inner", 1);
    }

    [Fact]
    public void Reused_Context_Forgets_The_Reason()
    {
        var context = Context("from;");
        Assert.False(Statement.TryParse(context, out _, out var error));
        Assert.NotNull(error);

        context.Scanner.Cursor.ResetPosition(0);
        context.Scanner.Cursor.ResetFurthestOffset();
        Assert.Null(context.Scanner.Cursor.FurthestReason);

        Assert.False(Terms.Integer().TryParse(context, out _, out error));
        Assert.Null(error);
    }

    [Fact]
    public void Error_Is_Reported_Before_A_Reason()
    {
        var parser = Statement.Or(Terms.Text("from").SkipAnd(Terms.Char('!').ElseError("Expected '!'")).Then(static _ => ""));

        Assert.False(parser.TryParse("from;", out _, out var error));
        Assert.Equal("Expected '!'", error!.Message);
        Assert.Null(error.Reason);
    }

    [Fact]
    public void Collected_Diagnostics_Leave_The_Reason_On_The_Cursor()
    {
        var parser = Statement.Or(Terms.Char('(').ElseError("Expected '('").Then(static _ => "("));
        var context = new ParseContext(new Scanner("from")) { CollectDiagnostics = true };

        // The error is only a fatal one, the grammar's errors are diagnostics
        Assert.False(parser.TryParse(context, out _, out var error));
        Assert.Null(error);
        Assert.Single(context.Diagnostics);
        Assert.Equal("'from' is reserved", context.Scanner.Cursor.FurthestReason);
    }

    [Fact]
    public void Reason_Is_Required()
    {
        Assert.Throws<ArgumentNullException>(() => Word.When(static (_, _) => true, null!));
        Assert.Throws<ArgumentNullException>(() => Word.ThenTry(static value => (true, value), null!));
        Assert.Throws<ArgumentNullException>(() => Not(Word, null!));
        Assert.Throws<ArgumentNullException>(() => Word.WhenNotFollowedBy(Word, null!));
    }

    [Fact]
    public void Failure_Recorded_Without_A_Reason_Records_The_Offset()
    {
        var cursor = new Cursor("abc", TextPosition.Start);
        cursor.RecordFailure(2, null);

        Assert.Equal(2, cursor.FurthestOffset);
        Assert.Null(cursor.FurthestReason);

        cursor.RecordFailure(1, "near");
        Assert.Null(cursor.FurthestReason);

        cursor.RecordFailure(2, "here");
        Assert.Equal("here", cursor.FurthestReason);
    }

    [Fact]
    public void Token_Read_Again_Restores_The_Reason_Recorded_Before_It()
    {
        // The rejected text records a reason at 0. The token records one further where the buffer of a TextReader ends,
        // which it then forgets when it is read again with more text: the reason recorded before it is reported.
        var parser = OneOf(Literals.Text("aa").When(static (_, _) => false, "rejected"), new ReasonToken()).Eof();

        // The token is longer than the buffer, which holds what was read so far
        var text = new string('a', 10_000);

        AssertReason(parser, text + "?", "rejected", 0);
        Assert.Equal(text + "x", parser.Parse(text + "x"));
        Assert.Equal(text + "x", parser.Parse(new OneCharReader(text + "x"), new StreamParseOptions { BufferSize = 1 }));
    }

    // Reads 'a's then an 'x', without moving the cursor until it matched, and records a reason where the text ends
    private sealed class ReasonToken : Parser<string>
    {
        public override bool Parse(ParseContext context, ref ParseResult<string> result)
        {
            if (context.IsCompacting)
            {
                return context.ParseToken(this, ref result);
            }

            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;
            var start = cursor.Offset;
            var span = cursor.Span;
            var i = 0;

            while (i < span.Length && span[i] == 'a')
            {
                i++;
            }

            if (i == span.Length)
            {
                cursor.MarkHitEnd();
                cursor.RecordFailure(start + i, "truncated");
                context.ExitParser(this);
                return false;
            }

            if (span[i] != 'x')
            {
                context.ExitParser(this);
                return false;
            }

            var text = span.Slice(0, i + 1).ToString();
            cursor.Advance(i + 1);
            result.Set(start, cursor.Offset, text);

            context.ExitParser(this);
            return true;
        }
    }

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
