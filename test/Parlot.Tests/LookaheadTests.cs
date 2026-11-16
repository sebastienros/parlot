#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A sequence which starts with a lookahead has hints: <c>Not(x).SkipAnd(y)</c> those of <c>x</c> and <c>y</c>, and
/// <c>Peek(x).SkipAnd(y)</c> those of <c>x</c>, so that a choice holding it keeps its lookup table, and <c>Not(x)</c> doesn't
/// invoke <c>x</c> where the current char is one <c>x</c> can't start with. These tests check that the grammars match what they
/// match when the parsers declare no hints, with the same positions and furthest failures, and that the parsers aren't
/// entered where they can't start.
/// </summary>
public class LookaheadTests
{
    // The same parser without hints: a sequence which starts with a parser that doesn't read can start with any char
    private static Parser<T> WithoutHints<T>(Parser<T> parser) => Always().SkipAnd(parser);

    private static readonly string[] Inputs =
    [
        "", " ", "}", " }", "a; }", "a ; b; }", "  a ;\n b ; }", "let a }", "let a; }", "letter; }", "let; }", "lett a }",
        "if a; }", "if; }", "iffy; }", "{ }", "{ a; }", "(a); }", " (a) ; }", "a", "a;", "let", "1; }", "1 }", "a 1; }",
        "a;b;let c}", "\n\tlet\tb }", "é; }", " é ; }", " a; }", "a; }", "#", "a #", "( }", "(a }",
    ];

    // A statement list: 'let' declarations, and expression statements which don't start with a reserved word or a '{'
    // (ECMAScript's [lookahead ∉ { ... }]). The expression is a Deferred set after the statement is built.
    private static Parser<string> Statements(bool reference)
    {
        Parser<T> Hints<T>(Parser<T> parser) => reference ? WithoutHints(parser) : parser;

        var expression = Deferred<string>();
        var reserved = Hints(OneOf(Terms.Keyword("let"), Terms.Keyword("if"), Terms.Text("{")));

        var statement = OneOf(
            Hints(Terms.Keyword("let").SkipAnd(Terms.Identifier()).Then(static x => "let " + x)),
            Hints(Terms.Text("if").SkipAnd(expression).AndSkip(Terms.Char(';')).Then(static x => "if " + x)),
            Hints(Not(reserved).SkipAnd(expression).AndSkip(Terms.Char(';')).Then(static x => "expr " + x)));

        expression.Parser = Terms.Identifier().Then(static x => x.ToString())
            .Or(Between(Terms.Char('('), expression, Terms.Char(')')).Then(static x => $"({x})"));

        return ZeroOrMany(statement).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // A positive lookahead: an identifier which starts with an 'a' or a 'b', or a number
    private static Parser<string> Peeked(bool reference)
    {
        Parser<T> Hints<T>(Parser<T> parser) => reference ? WithoutHints(parser) : parser;

        var choice = OneOf(
            Hints(Peek(Hints(Terms.Char('a').Or(Terms.Char('b')))).SkipAnd(Terms.Identifier()).Then(static x => "id " + x)),
            Hints(Terms.Integer().Then(static x => "int " + x)),
            Hints(Terms.Char(';').Then(static _ => ";")));

        return ZeroOrMany(choice).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // A lookahead whose parser doesn't skip white space: what follows it does, but the sequence has no hints, since a choice
    // skipping the white space before it would change what the lookahead sees
    private static Parser<string> Literal(bool reference)
    {
        Parser<T> Hints<T>(Parser<T> parser) => reference ? WithoutHints(parser) : parser;

        var choice = OneOf(
            Hints(Not(Hints(Literals.Char('l'))).SkipAnd(Terms.Identifier()).Then(static x => "id " + x)),
            Hints(Terms.Integer().Then(static x => "int " + x)),
            Hints(Terms.Char(';').Then(static _ => ";")));

        return ZeroOrMany(choice).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // Without white space: the lookahead and what follows it are literals
    private static Parser<string> Literals_(bool reference)
    {
        Parser<T> Hints<T>(Parser<T> parser) => reference ? WithoutHints(parser) : parser;

        var word = Literals.Pattern(static c => c is >= 'a' and <= 'z');
        var choice = OneOf(
            Hints(Not(Hints(Literals.Text("let"))).SkipAnd(word).Then(static x => "word " + x)),
            Hints(Literals.Text("let").Then(static _ => "let")),
            Hints(Literals.Char(' ').Then(static _ => "_")),
            Hints(Peek(Hints(Literals.Char('('))).SkipAnd(Literals.Char('(').SkipAnd(word).AndSkip(Literals.Char(')'))).Then(static x => $"({x})")));

        return ZeroOrMany(choice).AndSkip(Literals.Char('}')).Then(static x => string.Join(",", x));
    }

    public static IEnumerable<object[]> Grammars() => GrammarNames.Select(static name => new object[] { name });

    private static readonly string[] GrammarNames = ["statements", "peeked", "literal", "literals"];

    private static Parser<string> Create(string name, bool reference) => name switch
    {
        "statements" => Statements(reference),
        "peeked" => Peeked(reference),
        "literal" => Literal(reference),
        "literals" => Literals_(reference),
        _ => throw new ArgumentOutOfRangeException(nameof(name)),
    };

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHints(string name)
    {
        var parser = Create(name, reference: false);
        var reference = Create(name, reference: true);

        foreach (var text in Inputs.Concat(["let}", "letx}", "le}", "(ab)}", "(ab}", "a b}", " }"]))
        {
            var expected = Run(reference, text);
            var actual = Run(parser, text);

            Assert.True(expected == actual, $"{name} on '{text}': {actual} instead of {expected}");

            // Followed by a parser which fails, so that the furthest failure is the one a parse would report
            var expectedFurthest = Furthest(reference.AndSkip(Terms.Char('#')), text);
            var actualFurthest = Furthest(parser.AndSkip(Terms.Char('#')), text);

            Assert.True(expectedFurthest == actualFurthest, $"{name} on '{text}': furthest failure {actualFurthest} instead of {expectedFurthest}");
        }
    }

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHintsWithoutValue(string name)
    {
        var parser = Capture(Create(name, reference: false));
        var reference = Capture(Create(name, reference: true));

        foreach (var text in Inputs)
        {
            Assert.Equal(Run(reference, text), Run(parser, text));
        }
    }

    [Theory]
    [MemberData(nameof(Grammars))]
    public void ShouldMatchTheParserWithoutHintsOnStreams(string name)
    {
        var parser = Create(name, reference: false);
        var reference = Create(name, reference: true);

        foreach (var text in Inputs)
        {
            foreach (var bufferSize in new[] { 1, 2, 5 })
            {
                var options = new StreamParseOptions { BufferSize = bufferSize };
                var expected = reference.TryParse(new StreamingTests.ChunkedReader(text, () => 1, yield: false), out var expectedValue, options);
                var actual = parser.TryParse(new StreamingTests.ChunkedReader(text, () => 1, yield: false), out var actualValue, options);

                Assert.True(expected == actual, $"{name} on '{text}' with buffer size {bufferSize}");
                Assert.Equal(expectedValue, actualValue);
            }
        }
    }

    [Fact]
    public void SequenceStartingWithNotShouldHaveTheHintsOfTheNextParser()
    {
        // The parser which follows is a Deferred set after the sequence and the choice are built
        var expression = Deferred<string>();
        var sequence = Not(Terms.Keyword("let")).SkipAnd(expression);
        var choice = OneOf(Terms.Keyword("let").Then(static _ => "let"), sequence);

        expression.Parser = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Char('(').Then(static _ => "("));

        var seekable = Assert.IsAssignableFrom<ISeekable>(sequence);
        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Contains('(', seekable.ExpectedChars);
        Assert.Contains('a', seekable.ExpectedChars);
        Assert.DoesNotContain(Parlot.Fluent.OneOf<string>.OtherSeekableChar, seekable.ExpectedChars);

        // The choice selects the sequence with the char, and doesn't invoke it for the others
        var entered = 0;
        var context = new ParseContext(new Scanner(" ;"))
        {
            OnEnterParser = (parser, _) =>
            {
                if (ReferenceEquals(parser, sequence))
                {
                    entered++;
                }
            },
        };

        var result = new ParseResult<string>();
        Assert.False(choice.Parse(context, ref result));
        Assert.Equal(0, entered);
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        Assert.Equal("let", choice.Parse("let"));
        Assert.Equal("letter", choice.Parse(" letter"));
        Assert.Equal("(", choice.Parse(" ("));
    }

    [Fact]
    public void LongerSequencesShouldHaveTheHintsOfTheParserAfterTheLookahead()
    {
        // The parser of Not is invoked first, with the chars it can start with: it can fail with an error there
        var and = Assert.IsAssignableFrom<ISeekable>(Not(Terms.Char('x')).And(Terms.Char('a')).And(Terms.Char('b')));
        Assert.True(and.CanSeek);
        Assert.Equal(['x', 'a'], and.ExpectedChars);

        var andSkip = Assert.IsAssignableFrom<ISeekable>(Not(Terms.Char('x')).AndSkip(Terms.Char('a')).And(Terms.Char('b')));
        Assert.True(andSkip.CanSeek);
        Assert.Equal(['x', 'a'], andSkip.ExpectedChars);

        var skipAnd = Assert.IsAssignableFrom<ISeekable>(Not(Terms.Char('x')).SkipAnd(Terms.Char('a')).AndSkip(Terms.Char('b')));
        Assert.True(skipAnd.CanSeek);
        Assert.Equal(['x', 'a'], skipAnd.ExpectedChars);
    }

    [Fact]
    public void SequenceStartingWithPeekShouldHaveTheCharsItsParserCanStartWith()
    {
        // Its parser is invoked first, and can fail with an error where it can start, whatever follows
        var both = Assert.IsAssignableFrom<ISeekable>(Peek(OneOf(Terms.Char('a'), Terms.Char('b'))).SkipAnd(OneOf(Terms.Char('b'), Terms.Char('c'))));
        Assert.True(both.CanSeek);
        Assert.Equal(['a', 'b'], both.ExpectedChars);

        var any = Assert.IsAssignableFrom<ISeekable>(Peek(Terms.Char('a')).SkipAnd(OneOf(Terms.Char('a').Then(static _ => TextSpan.Empty), Terms.Pattern(static c => c == 'c'))));
        Assert.True(any.CanSeek);
        Assert.Equal(['a'], any.ExpectedChars);

        // The parser which follows has no hints: the ones of the lookahead are kept, when they don't skip white space or the
        // parser which follows skips it too
        var first = Assert.IsAssignableFrom<ISeekable>(Peek(Literals.Char('a')).SkipAnd(WithoutHints(Terms.Identifier())));
        Assert.True(first.CanSeek);
        Assert.Equal(['a'], first.ExpectedChars);
        Assert.False(Assert.IsAssignableFrom<ISeekable>(Peek(Terms.Char('a')).SkipAnd(WithoutHints(Terms.Identifier()))).CanSeek);

        // Its parser has none: it is invoked for any char
        Assert.False(Assert.IsAssignableFrom<ISeekable>(Peek(WithoutHints(Literals.Char('a'))).SkipAnd(Literals.Char('b'))).CanSeek);
        Assert.False(Assert.IsAssignableFrom<ISeekable>(Not(WithoutHints(Literals.Char('a'))).SkipAnd(Literals.Char('b'))).CanSeek);

        // They don't skip the white space the same way: the ones of the lookahead are kept, as before
        var mixed = Assert.IsAssignableFrom<ISeekable>(Peek(Literals.Char('a')).SkipAnd(Terms.Identifier()));
        Assert.True(mixed.CanSeek);
        Assert.False(mixed.SkipWhitespace);
        Assert.Equal(['a'], mixed.ExpectedChars);

        Assert.Equal("ab", Peek(OneOf(Terms.Char('a'), Terms.Char('b'))).SkipAnd(Terms.Identifier()).Then(static x => x.ToString()).Parse(" ab"));
    }

    [Fact]
    public void SequenceStartingWithPeekShouldKeepTheHintsBeforeAParserWhichTellsNothing()
    {
        // The Peek forwards the hints of its parser, which skips the white space, as before: the parser which follows doesn't
        // tell that it doesn't skip it
        var peek = Assert.IsAssignableFrom<ISeekable>(Peek(Terms.Char('[')).SkipAnd(ZeroOrMany(Terms.Integer())));
        Assert.True(peek.CanSeek);
        Assert.True(peek.SkipWhitespace);
        Assert.Equal(['['], peek.ExpectedChars);

        var none = Assert.IsAssignableFrom<ISeekable>(Peek(Terms.Pattern(char.IsDigit)).SkipAnd(Terms.Pattern(char.IsLetterOrDigit)));
        Assert.False(none.CanSeek);
        Assert.True(none.SkipWhitespace);

        var choice = OneOf(Terms.Char('x').Then(static _ => -1), Peek(Terms.Char('[')).SkipAnd(ZeroOrMany(Terms.Integer())).Then(static x => x.Count));
        Assert.Equal(0, choice.Parse("  ["));
        Assert.Equal(-1, choice.Parse("  x"));

        // A Not sequence without hints tells nothing about the white space, as before
        var not = Assert.IsAssignableFrom<ISeekable>(Not(Terms.Char('x')).SkipAnd(Terms.Pattern(char.IsLetterOrDigit)));
        Assert.False(not.CanSeek);
        Assert.False(not.SkipWhitespace);
    }

    [Fact]
    public void SequenceShouldHaveNoHintsWhenTheLookaheadDoesntSkipTheWhiteSpaceThatFollows()
    {
        // A choice skipping the white space before the sequence would change what the lookahead sees
        var sequence = Assert.IsAssignableFrom<ISeekable>(Not(Literals.Char('a')).SkipAnd(Terms.Identifier()));
        Assert.False(sequence.CanSeek);

        // " a": the lookahead sees the white space, and the identifier is read after it
        var choice = OneOf(Not(Literals.Char('a')).SkipAnd(Terms.Identifier()).Then(static x => x.ToString()), Terms.Integer().Then(static x => x.ToString()));
        Assert.Equal("a", choice.Parse(" a"));
        Assert.Null(choice.Parse("a"));

        // The same with Peek: its parser skips the white space, the one which follows doesn't
        var peek = Assert.IsAssignableFrom<ISeekable>(Peek(Terms.Char('a')).SkipAnd(Literals.Char('a')));
        Assert.False(peek.CanSeek);
        Assert.False(peek.SkipWhitespace);
        var peekChoice = OneOf(Terms.Char('b'), Peek(Terms.Char('a')).SkipAnd(Literals.Char('a')));
        Assert.Equal('a', peekChoice.Parse("a"));
        Assert.Equal('b', peekChoice.Parse(" b"));
        Assert.Equal(default, peekChoice.Parse(" a"));

        // Without white space, the hints are kept
        var literal = Assert.IsAssignableFrom<ISeekable>(Not(Literals.Char('a')).SkipAnd(Literals.Char('b')));
        Assert.True(literal.CanSeek);
        Assert.False(literal.SkipWhitespace);
        Assert.Equal(['a', 'b'], literal.ExpectedChars);
    }

    [Fact]
    public void ErrorsOfTheLookaheadsParserShouldBeRaised()
    {
        // The parser of the lookahead can start where the one which follows can't: it is invoked there, and fails with its error
        var error = Literals.Char('-').And(Literals.Char('-').ElseError("expected --"));
        var not = OneOf(Literals.Char('1').Then(static _ => "1"), Not(error).SkipAnd(Literals.Identifier()).Then(static x => x.ToString()));
        var peek = OneOf(Literals.Char('1').Then(static _ => "1"), Peek(error).SkipAnd(Literals.Identifier()).Then(static x => x.ToString()));

        Assert.Throws<ParseException>(() => not.Parse("-x"));
        Assert.Throws<ParseException>(() => peek.Parse("-x"));
        Assert.Throws<ParseException>(() => Not(error).SkipAnd(Literals.Identifier()).Optional().Parse("-x"));
        Assert.Equal("ab", not.Parse("ab"));
        Assert.Null(peek.Parse("ab"));
    }

    [Theory]
    [InlineData("b", 0, "b")]
    [InlineData("  b", 0, "b")]
    [InlineData("x", 1, null)]
    [InlineData("  x", 1, null)]
    [InlineData("", 0, null)]
    [InlineData("  ", 0, null)]
    [InlineData("\u00A0b", 0, "b")]
    [InlineData(" xb", 1, null)]
    public void NotShouldNotInvokeAParserWhichCantStart(string text, int expectedEntered, string? expected)
    {
        var x = Terms.Text("x").Or(Terms.Text("xy"));
        var parser = Not(x).SkipAnd(Terms.Identifier()).Then(static x => x.ToString());

        var entered = 0;
        var context = new ParseContext(new Scanner(text))
        {
            OnEnterParser = (p, _) =>
            {
                if (ReferenceEquals(p, x))
                {
                    entered++;
                }
            },
        };

        var result = new ParseResult<string>();
        var success = parser.Parse(context, ref result);

        Assert.Equal(expected != null, success);
        Assert.Equal(expectedEntered, entered);

        if (success)
        {
            Assert.Equal(expected, result.Value);
        }
        else
        {
            // The cursor is back where it was, and nothing is recorded where the lookahead's parser wasn't invoked
            Assert.Equal(0, context.Scanner.Cursor.Offset);
        }
    }

    [Fact]
    public void NotShouldRecordNothingWhereItsParserCantStart()
    {
        // The lookahead succeeds, then the parser which follows fails: the furthest failure is the one it records
        var parser = Not(Terms.Text("xyz").Or(Terms.Text("xw"))).SkipAnd(Terms.Char('b')).AndSkip(Terms.Char('c'));
        var reference = Not(WithoutHints(Terms.Text("xyz").Or(Terms.Text("xw")))).SkipAnd(Terms.Char('b')).AndSkip(Terms.Char('c'));

        foreach (var text in new[] { "  b d", "  a", "  xyw", "xyz", "b", "  b  c", "" })
        {
            Assert.Equal(Furthest(reference.AndSkip(Terms.Char('#')), text), Furthest(parser.AndSkip(Terms.Char('#')), text));
        }
    }

    private static int Furthest<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();

        Assert.False(parser.Parse(context, ref result));
        return context.Scanner.Cursor.FurthestOffset;
    }

    private static string Run<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();
        var success = parser.Parse(context, ref result);

        return success
            ? $"OK {result.Value} ({result.Start}, {result.End}) at {context.Scanner.Cursor.Offset}"
            : $"FAIL at {context.Scanner.Cursor.Offset}";
    }
}
