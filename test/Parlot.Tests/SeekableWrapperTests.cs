using System;
using System.Linq;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// Parsers that run another parser first declare what that parser can start with, so a OneOf can skip them.
/// </summary>
public class SeekableWrapperTests
{
    private static ISeekable Seekable<T>(Parser<T> parser) => Assert.IsAssignableFrom<ISeekable>(parser);

    [Fact]
    public void EofShouldForwardTheHints()
    {
        var terms = Seekable(Terms.Text("a").Eof());

        Assert.True(terms.CanSeek);
        Assert.Equal(['a'], terms.ExpectedChars);
        Assert.True(terms.SkipWhitespace);

        var literals = Seekable(Literals.Text("a").Eof());

        Assert.True(literals.CanSeek);
        Assert.Equal(['a'], literals.ExpectedChars);
        Assert.False(literals.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Eof()).CanSeek);

        // The choice doesn't try it for other chars
        var choice = OneOf(Terms.Text("a").Eof(), Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.Equal("a", choice.Parse(" a"));
        Assert.Equal("b", choice.Parse(" b"));
        Assert.False(choice.TryParse(" a ", out _));
        Assert.False(choice.TryParse(" c", out _));
    }

    [Fact]
    public void SwitchShouldForwardTheHintsOfThePreviousParser()
    {
        var parser = Terms.Text("a").Switch(static (_, _) => 0, Literals.Text("x"), Literals.Text("y"));
        var seekable = Seekable(parser);

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Switch(static (_, _) => 0, Literals.Text("x"))).CanSeek);

        var choice = OneOf(parser, Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.Equal("x", choice.Parse(" ax"));
        Assert.Equal("b", choice.Parse(" b"));
        Assert.False(choice.TryParse(" x", out _));
    }

    [Fact]
    public void ErrorShouldForwardTheHints()
    {
        var seekable = Seekable(Terms.Text("a").Error("no a"));

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        var converted = Seekable(Terms.Text("a").Error<int>("no a"));

        Assert.True(converted.CanSeek);
        Assert.Equal(['a'], converted.ExpectedChars);
        Assert.True(converted.SkipWhitespace);

        Assert.False(Seekable(Literals.Identifier(static c => c == '-').Error("no identifier")).CanSeek);

        // The error is only raised when the parser is tried, which the lookup table decides
        var choice = OneOf(Terms.Text("a").Error("no a"), Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.Equal("b", choice.Parse(" b"));
        Assert.False(choice.TryParse(" c", out _));
        Assert.Equal("no a", Assert.Throws<ParseException>(() => choice.Parse(" a")).Message);
    }

    [Fact]
    public void IfShouldDeclareTheUnionOfItsBranches()
    {
        var guard = Seekable(If(static () => true, Terms.Text("a")));

        Assert.True(guard.CanSeek);
        Assert.Equal(['a'], guard.ExpectedChars);
        Assert.True(guard.SkipWhitespace);

        var branches = Seekable(If(static () => true, Terms.Text("a"), Terms.Text("b")));

        Assert.True(branches.CanSeek);
        Assert.Equal(['a', 'b'], branches.ExpectedChars);
        Assert.True(branches.SkipWhitespace);

        var contextual = Seekable(If(static (ParseContext _) => true, Literals.Text("a"), Literals.Text("ab")));

        Assert.True(contextual.CanSeek);
        Assert.Equal(['a'], contextual.ExpectedChars);
        Assert.False(contextual.SkipWhitespace);

        // Branches that disagree on white space, or can't seek, have no common hint
        Assert.False(Seekable(If(static () => true, Terms.Text("a"), Literals.Text("b"))).CanSeek);
        Assert.False(Seekable(If(static () => true, Terms.Text("a"), Terms.Identifier(static c => c == '-').Then(static x => x.ToString()))).CanSeek);
        Assert.False(Seekable(If(static () => true, Terms.Identifier(static c => c == '-'))).CanSeek);
    }

    [Fact]
    public void SelectShouldDeclareTheUnionOfItsBranches()
    {
        var select = Seekable(Select(static () => 0, Terms.Text("a"), Terms.Text("b"), Terms.Text("c")));

        Assert.True(select.CanSeek);
        Assert.Equal(['a', 'b', 'c'], select.ExpectedChars);
        Assert.True(select.SkipWhitespace);

        var contextual = Seekable(Select(static (ParseContext _) => 0, Literals.Text("a"), Literals.Text("ab")));

        Assert.True(contextual.CanSeek);
        Assert.Equal(['a'], contextual.ExpectedChars);
        Assert.False(contextual.SkipWhitespace);

        Assert.False(Seekable(Select(static () => 0, Terms.Text("a"), Literals.Text("b"))).CanSeek);
        Assert.False(Seekable(Select(static () => 0, Terms.Text("a"), Terms.Identifier(static c => c == '-').Then(static x => x.ToString()))).CanSeek);
        Assert.False(Seekable(Select<string>(static () => 0)).CanSeek);
    }

    [Theory]
    [InlineData(0, " a", "a")]
    [InlineData(1, " b", "b")]
    [InlineData(0, " b", null)]
    [InlineData(1, " a", null)]
    [InlineData(0, " z", "z")]
    [InlineData(1, " z", "z")]
    [InlineData(0, " c", null)]
    public void ChoiceShouldOnlyTrySelectForTheCharsOfItsBranches(int index, string input, string expected)
    {
        var evaluations = 0;
        var select = Select(() => { evaluations++; return index; }, Terms.Text("a"), Terms.Text("b"));
        var choice = OneOf(select, Terms.Text("z"));

        Assert.Equal(['a', 'b', 'z'], Seekable(choice).ExpectedChars);
        Assert.True(Seekable(choice).SkipWhitespace);

        Assert.Equal(expected, choice.TryParse(input, out var result) ? result : null);

        // The selector only ran when the next char was one of the branches'
        Assert.Equal(input is " a" or " b" ? 1 : 0, evaluations);
    }

    public static TheoryData<string, string, int> OperandWrappers =>
    new()
    {
        { "LeftAssociative", "ab+ab", 8 },
        { "LeftAssociativeWithContext", "ab+ab", 8 },
        { "LeftAssociativeOperators", "ab+ab", 8 },
        { "RightAssociative", "ab+ab", 8 },
        { "RightAssociativeWithContext", "ab+ab", 8 },
        { "RightAssociativeOperators", "ab+ab", 8 },
        { "Discard", "discarded", 3 },
    };

    // A parser which starts with the operand, and only ends with it when it doesn't read an operator after it
    private static Parser<string> WrapOperand(string wrapper, Parser<string> operand)
    {
        var plus = Terms.Char('+');

        return wrapper switch
        {
            "LeftAssociative" => operand.LeftAssociative((plus, static (x, y) => x + "+" + y)),
            "LeftAssociativeWithContext" => operand.LeftAssociative((plus, static (ParseContext _, string x, string y) => x + "+" + y)),
            "LeftAssociativeOperators" => operand.LeftAssociative([plus], static (x, y, op) => x + op + y),
            "RightAssociative" => operand.RightAssociative((plus, static (x, y) => x + "+" + y)),
            "RightAssociativeWithContext" => operand.RightAssociative((plus, static (ParseContext _, string x, string y) => x + "+" + y)),
            "RightAssociativeOperators" => operand.RightAssociative([plus], static (x, y, op) => x + op + y),
#pragma warning disable CS0618 // Discard is obsolete, and still a parser
            "Discard" => operand.Discard("discarded"),
#pragma warning restore CS0618
            _ => throw new ArgumentOutOfRangeException(nameof(wrapper)),
        };
    }

    [Theory]
    [MemberData(nameof(OperandWrappers))]
    public void OperandWrappersShouldForwardTheHintsOfTheOperand(string wrapper, string expected, int end)
    {
        var parser = WrapOperand(wrapper, Terms.Text("ab"));
        var seekable = Seekable(parser);

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        var literal = Seekable(WrapOperand(wrapper, Literals.Text("ab")));

        Assert.True(literal.CanSeek);
        Assert.Equal(['a'], literal.ExpectedChars);
        Assert.False(literal.SkipWhitespace);

        Assert.False(Seekable(WrapOperand(wrapper, Literals.Identifier(static c => c == '-').Then(static x => x.ToString()))).CanSeek);

        // The choice only tries the parser for the chars of its operand, after skipping the white space once
        var choice = OneOf(parser, Terms.Text("b"));

        Assert.Equal(['a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.True(Seekable(choice).SkipWhitespace);

        Assert.Equal((true, expected, 1, end), ParseCounting(choice, parser, " ab + ab"));
        Assert.Equal((true, "b", 0, 2), ParseCounting(choice, parser, " b"));

        // The cursor is back at the start when the parser isn't tried, and when it fails
        Assert.Equal((false, null, 0, 0), ParseCounting(choice, parser, " c"));
        Assert.Equal((false, null, 1, 0), ParseCounting(choice, parser, " ac"));
    }

    [Fact]
    public void UnaryShouldDeclareTheUnionOfItsOperatorsAndOperand()
    {
        var parser = Terms.Text("ab").Unary((Terms.Char('-'), static x => "-" + x), (Terms.Char('!'), static x => "!" + x));
        var seekable = Seekable(parser);

        Assert.True(seekable.CanSeek);
        Assert.Equal(['!', '-', 'a'], seekable.ExpectedChars.OrderBy(static c => c));
        Assert.True(seekable.SkipWhitespace);

        var contextual = Seekable(Literals.Text("ab").Unary((Literals.Char('-'), static (ParseContext _, string x) => "-" + x)));

        Assert.True(contextual.CanSeek);
        Assert.Equal(['-', 'a'], contextual.ExpectedChars.OrderBy(static c => c));
        Assert.False(contextual.SkipWhitespace);

        // An operator or an operand that disagree on white space, or can't seek, have no common hint
        Assert.False(Seekable(Literals.Text("ab").Unary((Terms.Char('-'), static x => "-" + x))).CanSeek);
        Assert.False(Seekable(Terms.Text("ab").Unary((Terms.Pattern(static c => c == '-'), static x => "-" + x))).CanSeek);
        Assert.False(Seekable(Terms.Pattern(static c => c == 'a').Then(static x => x.ToString()).Unary((Terms.Char('-'), static x => "-" + x))).CanSeek);

        var choice = OneOf(parser, Terms.Text("b"));

        Assert.Equal(['!', '-', 'a', 'b'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.True(Seekable(choice).SkipWhitespace);

        // Each operator invokes the parser again for its operand
        Assert.Equal((true, "-!ab", 3, 7), ParseCounting(choice, parser, " - ! ab"));
        Assert.Equal((true, "ab", 1, 3), ParseCounting(choice, parser, " ab"));
        Assert.Equal((true, "b", 0, 2), ParseCounting(choice, parser, " b"));
        Assert.Equal((false, null, 0, 0), ParseCounting(choice, parser, " c"));

        // An operator without an operand fails, and so does the choice
        Assert.Equal((false, null, 2, 0), ParseCounting(choice, parser, " -c"));
        Assert.Equal((false, null, 1, 0), ParseCounting(choice, parser, " ac"));
    }

    [Fact]
    public void ChoiceOfTwoTypesShouldDeclareTheUnionOfItsParsers()
    {
        var parser = Terms.Text("ab").Or<string, char, object>(Terms.Char('c'));
        var seekable = Seekable(parser);

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a', 'c'], seekable.ExpectedChars.OrderBy(static c => c));
        Assert.True(seekable.SkipWhitespace);

        Assert.False(Seekable(Terms.Text("ab").Or<string, char, object>(Literals.Char('c'))).CanSeek);
        Assert.False(Seekable(Terms.Text("ab").Or<string, TextSpan, object>(Terms.Pattern(static c => c == 'c'))).CanSeek);

        var choice = OneOf(parser, Terms.Text("b").Then<object>(static x => x));

        Assert.Equal(['a', 'b', 'c'], Seekable(choice).ExpectedChars.OrderBy(static c => c));
        Assert.True(Seekable(choice).SkipWhitespace);
        Assert.Equal('c', choice.Parse(" c"));
        Assert.Equal("ab", choice.Parse(" ab"));

        var context = new ParseContext(new Scanner(" ac"));
        var result = new ParseResult<object>();

        Assert.False(choice.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.False(choice.TryParse(" d", out _));
    }

    [Fact]
    public void ElseErrorShouldBeTriedForAnyChar()
    {
        // Skipping it for a char its parser doesn't start with would fail instead of reporting the error
        var parser = Terms.Text("a").ElseError("expected a");

        Assert.False(parser is ISeekable);

        var choice = OneOf(Terms.Text("b"), parser);

        Assert.Equal("b", choice.Parse(" b"));
        Assert.Equal("a", choice.Parse(" a"));
        Assert.Equal("expected a", Assert.Throws<ParseException>(() => choice.Parse(" c")).Message);
    }

    // Parses the input with the choice, and tells how many times it invoked the parser and where it left the cursor
    private static (bool Success, string Value, int Invocations, int Offset) ParseCounting(Parser<string> choice, object parser, string input)
    {
        var invocations = 0;
        var context = new ParseContext(new Scanner(input))
        {
            OnEnterParser = (entered, _) =>
            {
                if (ReferenceEquals(entered, parser))
                {
                    invocations++;
                }
            },
        };

        var result = new ParseResult<string>();
        var success = choice.Parse(context, ref result);

        return (success, success ? result.Value : null, invocations, context.Scanner.Cursor.Offset);
    }
}
