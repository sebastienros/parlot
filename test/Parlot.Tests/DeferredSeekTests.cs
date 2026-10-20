using System;
using System.Linq;
using System.Threading.Tasks;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// A Deferred parser declares what its parser can start with, even when it is set after a choice was built.
/// </summary>
public class DeferredSeekTests
{
    private const char OtherSeekableChar = Parlot.Fluent.OneOf<string>.OtherSeekableChar;

    [Fact]
    public void DeferredShouldNotSeekUntilItsParserIsSet()
    {
        var deferred = Deferred<string>();
        var seekable = (ISeekable)deferred;

        Assert.False(seekable.CanSeek);
        Assert.Empty(seekable.ExpectedChars);
        Assert.False(seekable.SkipWhitespace);

        deferred.Parser = Terms.Text("a");

        Assert.True(seekable.CanSeek);
        Assert.Equal(['a'], seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);

        deferred.Parser = Literals.Text("b");

        Assert.True(seekable.CanSeek);
        Assert.Equal(['b'], seekable.ExpectedChars);
        Assert.False(seekable.SkipWhitespace);

        // A parser that can't seek hides the hints
        deferred.Parser = Literals.Identifier(static c => c == '-').Then(static x => x.ToString());

        Assert.False(seekable.CanSeek);
    }

    [Fact]
    public void DeferredSetAfterTheChoiceShouldBeInTheLookupTable()
    {
        var deferred = Deferred<string>();
        var choice = OneOf(deferred, Literals.Text("b"));
        var seekable = (ISeekable)choice;

        // Until the deferred is set the choice can start with anything
        Assert.True(seekable.CanSeek);
        Assert.Contains(OtherSeekableChar, seekable.ExpectedChars);

        var invocations = 0;
        deferred.Parser = new Models.RewriteTests.FakeParser<string> { CanSeek = true, ExpectedChars = ['a'], Success = true, Result = "a", OnParse = _ => invocations++ };

        Assert.Equal("b", choice.Parse("b"));

        // The deferred declared 'a' when the choice first parsed, so it is not tried for 'b'
        Assert.Equal(0, invocations);
        Assert.Equal(['a', 'b'], seekable.ExpectedChars.OrderBy(static c => c));
        Assert.DoesNotContain(OtherSeekableChar, seekable.ExpectedChars);

        Assert.Equal("a", choice.Parse("a"));
        Assert.Equal(1, invocations);

        Assert.False(choice.TryParse("c", out _));
        Assert.Equal(1, invocations);
    }

    [Fact]
    public void DeferredSetAfterTheChoiceShouldHoistTheWhiteSpaceSkip()
    {
        var deferred = Deferred<string>();
        var choice = OneOf(deferred, Terms.Text("b"));
        var seekable = (ISeekable)choice;

        Assert.False(seekable.SkipWhitespace);

        deferred.Parser = Terms.Text("a");

        Assert.Equal("a", choice.Parse("  a"));
        Assert.True(seekable.SkipWhitespace);
        Assert.Equal(['a', 'b'], seekable.ExpectedChars.OrderBy(static c => c));
        Assert.Equal("b", choice.Parse("  b"));
        Assert.False(choice.TryParse("  c", out _));
    }

    [Fact]
    public void NestedChoicesShouldBeResolvedWithTheOuterOne()
    {
        var deferred = Deferred<string>();
        var inner = OneOf(deferred, Literals.Text("b"));
        var outer = OneOf(inner, Literals.Text("c"));

        Assert.Contains(OtherSeekableChar, ((ISeekable)outer).ExpectedChars);

        deferred.Parser = Literals.Text("a");

        Assert.Equal("c", outer.Parse("c"));
        Assert.Equal("a", outer.Parse("a"));
        Assert.Equal("b", outer.Parse("b"));
        Assert.False(outer.TryParse("d", out _));

        Assert.Equal(['a', 'b', 'c'], ((ISeekable)outer).ExpectedChars.OrderBy(static c => c));
        Assert.Equal(['a', 'b'], ((ISeekable)inner).ExpectedChars.OrderBy(static c => c));
    }

    [Fact]
    public void DirectSelfReferenceShouldNotSeek()
    {
        // expr = expr | 'x' is left-recursive, the loop detection fails the inner reference
        var expression = Deferred<string>();
        var choice = OneOf(expression, Literals.Text("x"));
        expression.Parser = choice;

        Assert.Equal("x", expression.Parse("x"));
        Assert.Equal("x", choice.Parse("x"));
        Assert.False(expression.TryParse("y", out _));

        // The deferred couldn't declare its chars while the choice was built, so it is tried for any char
        Assert.Contains(OtherSeekableChar, ((ISeekable)choice).ExpectedChars);
        Assert.Contains('x', ((ISeekable)choice).ExpectedChars);
    }

    [Fact]
    public void IndirectSelfReferenceShouldNotSeek()
    {
        var a = Deferred<string>();
        var b = Deferred<string>();
        var choiceA = OneOf(b, Literals.Text("a"));
        var choiceB = OneOf(a, Literals.Text("b"));
        a.Parser = choiceA;
        b.Parser = choiceB;

        Assert.Equal("a", a.Parse("a"));
        Assert.Equal("b", a.Parse("b"));
        Assert.Equal("a", b.Parse("a"));
        Assert.Equal("b", b.Parse("b"));
        Assert.False(a.TryParse("c", out _));
        Assert.False(b.TryParse("c", out _));

        Assert.Contains(OtherSeekableChar, ((ISeekable)choiceA).ExpectedChars);
        Assert.Contains(OtherSeekableChar, ((ISeekable)choiceB).ExpectedChars);
    }

    [Fact]
    public void SelfReferenceThroughTheHintsShouldNotRecurseForever()
    {
        // Reading the hints before anything is parsed resolves the cycle from the deferred side
        var expression = Deferred<string>();
        var choice = OneOf(expression, Literals.Text("x"));
        expression.Parser = choice;

        var seekable = (ISeekable)expression;

        Assert.True(seekable.CanSeek);
        Assert.Contains('x', seekable.ExpectedChars);
        Assert.Equal("x", choice.Parse("x"));
    }

    [Fact]
    public void RecursiveGrammarShouldUseTheLookupTable()
    {
        // The usual shape of a grammar: the deferred is set last, from choices that reference it
        var expression = Deferred<string>();
        var group = Between(Terms.Char('('), expression, Terms.Char(')')).Then(static x => "(" + x + ")");
        var number = Terms.Integer().Then(static x => x.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var negative = Terms.Char('-').SkipAnd(expression).Then(static x => "-" + x);
        var choice = OneOf(expression, negative, group, number);

        var literal = Terms.Char('a').Then(static _ => "a");
        expression.Parser = OneOf(literal, group, number);

        var invocations = 0;

        string Parse(string input)
        {
            var context = new ParseContext(new Scanner(input))
            {
                OnEnterParser = (parser, _) =>
                {
                    if (ReferenceEquals(parser, literal))
                    {
                        invocations++;
                    }
                },
            };

            var result = new ParseResult<string>();
            return choice.Parse(context, ref result) ? result.Value : null;
        }

        Assert.Equal("-(1)", Parse("-(1)"));
        Assert.Equal("1", Parse("1"));
        Assert.Null(Parse("x"));

        // The literal was never tried as no input started with 'a'
        Assert.Equal(0, invocations);

        Assert.Equal("((a))", Parse("((a))"));
        Assert.Equal(1, invocations);

        var seekable = (ISeekable)choice;
        Assert.DoesNotContain(OtherSeekableChar, seekable.ExpectedChars);
        Assert.Contains('a', seekable.ExpectedChars);
        Assert.Contains('-', seekable.ExpectedChars);
        Assert.Contains('(', seekable.ExpectedChars);
        Assert.Contains('1', seekable.ExpectedChars);
        Assert.True(seekable.SkipWhitespace);
    }

    [Fact]
    public void ResolutionShouldBeThreadSafe()
    {
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var expression = Deferred<string>();
            var group = Between(Literals.Char('('), expression, Literals.Char(')')).Then(static x => "(" + x + ")");
            var choiceA = OneOf(expression, Literals.Text("a"));
            var choiceB = OneOf(choiceA, Literals.Text("b"));
            expression.Parser = OneOf(group, Literals.Text("c"));

            var inputs = new[] { "a", "b", "c", "(c)", "((c))" };

            // The choices are resolved from both ends at once
            Parallel.For(0, 64, i =>
            {
                var input = inputs[i % inputs.Length];

                Assert.Equal(input, choiceB.Parse(input));
                Assert.Equal(input, OneOf(expression, Literals.Text("a"), Literals.Text("b")).Parse(input));
            });

            Assert.Equal(['(', 'a', 'b', 'c'], ((ISeekable)choiceB).ExpectedChars.OrderBy(static c => c));
            Assert.Equal(['(', 'a', 'c'], ((ISeekable)choiceA).ExpectedChars.OrderBy(static c => c));
        }
    }

    [Fact]
    public void DeferredAlternativesShouldKeepTheirOrder()
    {
        // The first alternative wins even once the table is built
        var deferred = Deferred<string>();
        var choice = OneOf(deferred, Literals.Text("ab"), Literals.Text("a"));
        deferred.Parser = Literals.Text("abc");

        Assert.Equal("abc", choice.Parse("abcd"));
        Assert.Equal("ab", choice.Parse("abd"));
        Assert.Equal("a", choice.Parse("ad"));
        Assert.False(choice.TryParse("b", out _));
    }

    [Fact]
    public void ToStringShouldShowTheResolvedChars()
    {
        var deferred = Deferred<char>();
        var choice = OneOf(deferred, Literals.Char('b'));
        deferred.Parser = Literals.Char('a');

        Assert.Equal('a', choice.Parse("a"));
        Assert.Equal("Char('a') (Deferred) | Char('b') on [a b]", choice.ToString());
    }
}
