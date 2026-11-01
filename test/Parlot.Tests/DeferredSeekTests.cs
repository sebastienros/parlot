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
    private const char OtherSeekableChar = Fluent.OneOf<string>.OtherSeekableChar;

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

    [Fact]
    public void ParsersBuiltBeforeTheirDeferredIsSetShouldForwardItsHints()
    {
        var deferred = Deferred<string>();
        var plus = Terms.Char('+');

#pragma warning disable CS0618 // Discard is obsolete, and still a parser
        object[] parsers =
        [
            deferred.And(Literals.Char(';')),
            deferred.And(Literals.Char(';')).And(Literals.Char(';')),
            deferred.And(Literals.Char(';')).And(Literals.Char(';')).And(Literals.Char(';')),
            deferred.AndSkip(Literals.Char(';')),
            deferred.And(Literals.Char(';')).AndSkip(Literals.Char(';')),
            deferred.SkipAnd(Literals.Char(';')),
            deferred.And(Literals.Char(';')).SkipAnd(Literals.Char(';')),
            deferred.Then(static x => x.Length),
            Between(deferred, Literals.Char(';'), Literals.Char(';')),
            deferred.When(static (_, x) => x.Length > 0),
            Capture(deferred),
            deferred.Error("unexpected"),
            deferred.Error<int>("unexpected"),
            deferred.Eof(),
            deferred.Commit(),
            deferred.Discard(0),
            deferred.Switch(static (_, _) => 0, Literals.Char(';')),
            deferred.WhenFollowedBy(Literals.Char(';')),
            deferred.WhenNotFollowedBy(Literals.Char(';')),
            Separated(Literals.Char(','), deferred),
            OneOrMany(deferred),
            If(static () => true, deferred),
            Select(static () => 0, deferred),
            deferred.Or<string, string, object>(Terms.Text("b")),
            deferred.LeftAssociative((plus, static (x, y) => x + y)),
            deferred.LeftAssociative([plus], static (x, y, _) => x + y),
            deferred.RightAssociative((plus, static (x, y) => x + y)),
            deferred.Unary((Terms.Char('-'), static x => "-" + x)),
        ];
#pragma warning restore CS0618

        Assert.All(parsers, static parser => Assert.False(((ISeekable)parser).CanSeek));

        deferred.Parser = Terms.Text("a");

        Assert.All(parsers, static parser =>
        {
            var seekable = (ISeekable)parser;

            Assert.True(seekable.CanSeek);
            Assert.Contains('a', seekable.ExpectedChars);
            Assert.DoesNotContain(OtherSeekableChar, seekable.ExpectedChars);
            Assert.True(seekable.SkipWhitespace);
        });

        // They follow the parser when it is replaced
        deferred.Parser = Literals.Text("c");

        var sequence = (ISeekable)parsers[0];

        Assert.Equal(['c'], sequence.ExpectedChars);
        Assert.False(sequence.SkipWhitespace);
    }

    [Fact]
    public void WrappersBuiltBeforeTheirDeferredIsSetShouldKeepTheirConditions()
    {
        var deferred = Deferred<string>();

        var empty = (ISeekable)Separated(Literals.Char(','), deferred, min: 0);
        var leading = (ISeekable)Separated(Literals.Char(','), deferred, allowLeadingSeparator: true);
        var set = (ISeekable)deferred.WithWhiteSpaceSet(WhiteSpaceSet.Default);
        var skip = (ISeekable)SkipWhiteSpace(deferred);

        deferred.Parser = Terms.Text("a");

        // A list can be empty or start with a separator, and a choice doesn't know the white space of another set
        Assert.False(empty.CanSeek);
        Assert.False(leading.CanSeek);
        Assert.False(set.CanSeek);
        Assert.True(skip.CanSeek);
        Assert.True(skip.SkipWhitespace);

        deferred.Parser = Literals.Text("a");

        Assert.True(set.CanSeek);
        Assert.Equal(['a'], set.ExpectedChars);
        Assert.False(set.SkipWhitespace);
        Assert.True(skip.SkipWhitespace);
    }

    [Fact]
    public void SequenceBuiltBeforeItsDeferredIsSetShouldBeInTheLookupTable()
    {
        var deferred = Deferred<string>();
        var statement = deferred.AndSkip(Terms.Char(';'));
        var choice = OneOf(statement, Terms.Text("b"));

        // The sequence can't tell whether it skips white space yet, so the choice has no lookup table
        Assert.False(((ISeekable)choice).CanSeek);

        deferred.Parser = Terms.Text("a");

        var invocations = 0;

        (string Value, int Offset) Parse(string input)
        {
            var context = new ParseContext(new Scanner(input))
            {
                OnEnterParser = (parser, _) =>
                {
                    if (ReferenceEquals(parser, statement))
                    {
                        invocations++;
                    }
                },
            };

            var result = new ParseResult<string>();
            return (choice.Parse(context, ref result) ? result.Value : null, context.Scanner.Cursor.Offset);
        }

        Assert.Equal(("b", 2), Parse(" b"));
        Assert.Equal((null, 0), Parse(" c"));
        Assert.Equal(0, invocations);

        Assert.Equal(("a", 4), Parse(" a ;"));
        Assert.Equal(1, invocations);

        // The sequence is tried, and fails without its ';'
        Assert.Equal((null, 0), Parse(" a"));
        Assert.Equal(2, invocations);

        var seekable = (ISeekable)choice;

        Assert.Equal(['a', 'b'], seekable.ExpectedChars.OrderBy(static c => c));
        Assert.True(seekable.SkipWhitespace);
    }

    [Fact]
    public void ChoiceStartingASequenceShouldBeResolvedWithTheOuterOne()
    {
        var deferred = Deferred<string>();
        var inner = OneOf(deferred, Literals.Text("b"));
        var outer = OneOf(inner.AndSkip(Literals.Char(';')), Literals.Text("c"));

        Assert.Contains(OtherSeekableChar, ((ISeekable)outer).ExpectedChars);

        deferred.Parser = Literals.Text("a");

        Assert.Equal("c", outer.Parse("c"));
        Assert.Equal("a", outer.Parse("a;"));
        Assert.Equal("b", outer.Parse("b;"));
        Assert.False(outer.TryParse("a", out _));
        Assert.False(outer.TryParse("d", out _));

        // The inner choice built its table when the outer one did, rather than when it first parsed
        Assert.Equal(['a', 'b', 'c'], ((ISeekable)outer).ExpectedChars.OrderBy(static c => c));
        Assert.Equal(['a', 'b'], ((ISeekable)inner).ExpectedChars.OrderBy(static c => c));
    }

    [Fact]
    public void ChoiceStartingASequenceShouldNotBeResolvedBeforeTheGrammarIsComplete()
    {
        // Reading the hints while the grammar is built doesn't build the table of a choice that holds a deferred not set yet
        var deferred = Deferred<string>();
        var inner = OneOf(deferred, Literals.Text("b"));
        var sequence = inner.AndSkip(Literals.Char(';'));

        Assert.Contains(OtherSeekableChar, ((ISeekable)sequence).ExpectedChars);

        deferred.Parser = Literals.Text("a");

        Assert.Equal("a", sequence.Parse("a;"));
        Assert.Equal(['a', 'b'], ((ISeekable)sequence).ExpectedChars.OrderBy(static c => c));
    }

    [Fact]
    public void SequenceStartingWithItsOwnDeferredShouldNotSeek()
    {
        // expr = expr ';' | 'x' is left-recursive: the sequence can only tell the hints of the deferred while it is resolved
        var expression = Deferred<string>();
        var statement = expression.AndSkip(Literals.Char(';'));
        var choice = OneOf(statement, Literals.Text("x"));
        expression.Parser = choice;

        Assert.Equal("x", choice.Parse("x"));
        Assert.False(choice.TryParse("y", out _));
        Assert.Contains('x', ((ISeekable)choice).ExpectedChars);
    }
}
