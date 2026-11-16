#nullable enable

using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// <see cref="NoNewLineBefore{T}"/> and <see cref="NewLineAhead"/>, which tell whether the white space and comments before
/// the next token contain a new line.
/// </summary>
public class NewLineTests
{
    private static readonly Parser<string> Return = Terms.Keyword("return")
        .SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x).Else("return"));

    private static Parser<T> WithComments<T>(Parser<T> parser)
        => parser.WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    [Theory]
    [InlineData("return a", "return a")]
    [InlineData("return\ta", "return a")]
    [InlineData("return\na", "return")]
    [InlineData("return\r\na", "return")]
    [InlineData("return\ra", "return")]
    [InlineData("return \u2028 a", "return")]
    [InlineData("return \u2029 a", "return")]
    [InlineData("return", "return")]
    [InlineData("return ", "return")]
    [InlineData("return \n", "return")]
    public void NoNewLineBeforeShouldFailOnANewLine(string input, string expected)
    {
        Assert.Equal(expected, Return.Parse(input));
    }

    [Theory]
    [InlineData("return /* x */ a", "return a")]
    [InlineData("return /* x \n y */ a", "return")]
    [InlineData("return // x\n a", "return")]
    [InlineData("return /* x */\n a", "return")]
    [InlineData("return\n/* x */ a", "return")]
    [InlineData("return /* x */ /* y */ a", "return a")]
    public void NoNewLineBeforeShouldSeeTheNewLinesOfComments(string input, string expected)
    {
        Assert.Equal(expected, WithComments(Return).Parse(input));
    }

    [Fact]
    public void NoNewLineBeforeShouldUseACustomWhiteSpaceParser()
    {
        // The white space parser only skips spaces and '#' comments up to the end of the line, the new line included
        var whiteSpace = Capture(ZeroOrMany(OneOf(Literals.Char(' ').Then(static _ => 0), Literals.Char('#').And(AnyCharBefore(Literals.Char('\n'))).And(Literals.Char('\n')).Then(static _ => 0))));
        var parser = Return.WithWhiteSpaceParser(whiteSpace);

        Assert.Equal("return a", parser.Parse("return  a"));
        Assert.Equal("return", parser.Parse("return # x\n a"));
    }

    [Fact]
    public void NoNewLineBeforeShouldRestoreTheCursorOnFailure()
    {
        var parser = Terms.Identifier().NoNewLineBefore();

        // A new line before the identifier
        var context = new ParseContext(new Scanner("  \n  a"));
        var result = new ParseResult<TextSpan>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // No new line, but no identifier
        context = new ParseContext(new Scanner("   1"));
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // A new line in a comment
        context = new ParseContext(new Scanner(" /* \n */ a")) { WhiteSpaceParser = Capture(ZeroOrMany(Literals.Comments("/*", "*/").Or(Literals.WhiteSpace(includeNewLines: true)))) };
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // Matches
        context = new ParseContext(new Scanner(" \t a b"));
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("a", result.Value.ToString());
        Assert.Equal(4, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void NoNewLineBeforeShouldUseTheNewLinesOfTheWhiteSpaceSet()
    {
        // \v and U+0085 are new lines of the default set, white space of none of the others
        Assert.Equal("return", Return.Parse("return \v a"));
        Assert.Equal("return", Return.Parse("return \u0085 a"));
        Assert.Equal("return", Return.WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Parse("return \v a"));
        Assert.Equal("return", Return.WithWhiteSpaceSet(WhiteSpaceSet.SpaceTabCrLf).Parse("return \n a"));

        // With UseNewLines the new lines aren't skipped: the identifier isn't there
        var context = new ParseContext(new Scanner("return \n a"), useNewLines: true);
        Assert.Equal("return", Return.Parse(context));
        Assert.Equal(6, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void NoNewLineBeforeShouldUseCustomNewLines()
    {
        // JavaScript's line terminators: \v is white space there
        var parser = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore("\n\r\u2028\u2029").Then(static x => "return " + x).Else("return"));

        Assert.Equal("return a", parser.Parse("return \v a"));
        Assert.Equal("return", parser.Parse("return \u2028 a"));
        Assert.Equal("return", parser.Parse("return \r a"));

        Assert.Throws<ArgumentException>(() => Terms.Identifier().NoNewLineBefore(""));
        Assert.Throws<ArgumentNullException>(() => Terms.Identifier().NoNewLineBefore(null!));
        Assert.Throws<ArgumentNullException>(() => NoNewLineBefore<TextSpan>(null!));
    }

    // An arrow after a parameter read as an expression, as in JavaScript: the arrow can't follow a new line, an assignment can
    internal static Parser<string> CreateArrowTail()
    {
        var assignmentOperator = OneOf(Terms.Text("="), Terms.Text("+="));
        var assignment = assignmentOperator.And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
        var arrow = NoNewLineBefore(Terms.Text("=>")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);

        return Terms.Identifier().And(OneOf(assignment, arrow).Else("")).Then(static x => x.Item1.ToString() + x.Item2);
    }

    // Postfix operators which can't follow a new line, as in JavaScript, in a table with binary operators
    internal static Parser<string> CreatePostfixTable()
    {
        return Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix((NoNewLineBefore(Terms.Text("++")), static x => "(" + x + "++)"), (NoNewLineBefore(Terms.Text("--")), static x => "(" + x + "--)"))
            .LeftAssociative((Terms.Char('*'), static (a, b) => "(" + a + "*" + b + ")"))
            .LeftAssociative((Terms.Char('+'), static (a, b) => "(" + a + "+" + b + ")"));
    }

    [Theory]
    [InlineData(" =>", "=>")]
    [InlineData("=>", "=>")]
    [InlineData(" += ", "+=")]
    [InlineData("\n+=", "+=")]
    [InlineData("\n=>", null)]
    [InlineData(" \r\n =>", null)]
    public void NoNewLineBeforeShouldKeepTheLookupOfAChoice(string input, string? expected)
    {
        // The choice skips the white space once for its alternatives, and NoNewLineBefore reads the white space it skipped
        var tail = OneOf(Terms.Text("+="), NoNewLineBefore(Terms.Text("=>")));
        var seekable = (ISeekable)tail;

        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Equal(['+', '='], seekable.ExpectedChars.OrderBy(static c => c));

        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.Equal(expected is not null, tail.Parse(context, ref result));

        if (expected is null)
        {
            Assert.Equal(0, context.Scanner.Cursor.Offset);
        }
        else
        {
            Assert.Equal(expected, result.Value);
            Assert.Equal(input.TrimEnd().Length, context.Scanner.Cursor.Offset);
        }
    }

    [Theory]
    [InlineData("a => b", "a=>b")]
    [InlineData("a /* x */ => b", "a=>b")]
    [InlineData("a = b", "a=b")]
    [InlineData("a\n= b", "a=b")]
    [InlineData("a /* x */ += b", "a+=b")]
    [InlineData("a\n=> b", "a")]
    [InlineData("a /* \n */ => b", "a")]
    [InlineData("a // x\n=> b", "a")]
    [InlineData("a\n/* x */ => b", "a")]
    public void NoNewLineBeforeShouldSeeTheWhiteSpaceAChoiceSkipped(string input, string expected)
    {
        // The assignment operator, a nested choice which reads "=", is tried before the arrow and skips white space again,
        // where there is none: NoNewLineBefore still finds the new line skipped by the choice holding both
        Assert.Equal(expected, WithComments(CreateArrowTail()).Parse(input));

        // The white space set, which caches the white space it skips whether it skips any or not
        if (!input.Contains('/'))
        {
            Assert.Equal(expected, CreateArrowTail().Parse(input));
        }
    }

    [Theory]
    [InlineData("a++", "(a++)")]
    [InlineData("a ++ * b", "((a++)*b)")]
    [InlineData("a /* x */ --", "(a--)")]
    [InlineData("a ++--", "((a++)--)")]
    [InlineData("a * b++", "(a*(b++))")]
    [InlineData("a\n++", "a")]
    [InlineData("a /* \n */ ++", "a")]
    [InlineData("a // x\n--", "a")]
    [InlineData("a++\n++", "(a++)")]
    [InlineData("a\n+ b", "(a+b)")]
    [InlineData("a\n* b", "(a*b)")]
    public void NoNewLineBeforeShouldBeAPostfixOperatorOfATable(string input, string expected)
    {
        Assert.Equal(expected, WithComments(CreatePostfixTable()).Parse(input));

        if (!input.Contains('/'))
        {
            Assert.Equal(expected, CreatePostfixTable().Parse(input));
        }
    }

    [Fact]
    public void ATableWithNoNewLineBeforeOperatorsShouldKeepItsLookup()
    {
        // The operators which can't follow the next char aren't tried
        var parser = CreatePostfixTable();
        var entered = 0;
        var context = new ParseContext(new Scanner("a * b")) { OnEnterParser = (p, _) => entered += p.GetType().Name.StartsWith("NoNewLineBefore", StringComparison.Ordinal) ? 1 : 0 };
        var result = new ParseResult<string>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("(a*b)", result.Value);
        Assert.Equal(0, entered);

        context = new ParseContext(new Scanner("a ++")) { OnEnterParser = (p, _) => entered += p.GetType().Name.StartsWith("NoNewLineBefore", StringComparison.Ordinal) ? 1 : 0 };
        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("(a++)", result.Value);
        Assert.Equal(1, entered);
    }

    [Theory]
    [InlineData("a ++", "(a++)")]
    [InlineData("a /* x */ ++", "(a++)")]
    [InlineData("a /* \n */ ++", "a")]
    [InlineData("a\n++", "a")]
    public void AChoiceWithNoNewLineBeforeOperatorsShouldSkipTheWhiteSpaceOnce(string input, string expected)
    {
        // The choice of the operators skips the white space and invokes them after it: the comments are parsed once, and
        // NoNewLineBefore searches the white space the choice skipped
        var comments = OneOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Literals.Comments("/*", "*/"))).Then(static _ => new TextSpan());
        var parser = CreatePostfixTable().Eof().WithWhiteSpaceParser(comments);

        var whiteSpace = new System.Collections.Generic.List<int>();
        var entered = new System.Collections.Generic.List<int>();
        var context = new ParseContext(new Scanner(input))
        {
            OnEnterParser = (p, c) =>
            {
                if (ReferenceEquals(p, comments))
                {
                    whiteSpace.Add(c.Scanner.Cursor.Offset);
                }
                else if (p.GetType().Name.StartsWith("NoNewLineBefore", StringComparison.Ordinal))
                {
                    entered.Add(c.Scanner.Cursor.Offset);
                }
            },
        };

        var result = new ParseResult<string>();
        var matches = expected != "a";

        Assert.Equal(matches, parser.Parse(context, ref result));

        if (matches)
        {
            Assert.Equal(expected, result.Value);
        }

        // The white space after "a" is parsed once, and the operator is tried after it
        Assert.Equal(1, whiteSpace.Count(static x => x == 1));
        Assert.Equal([input.IndexOf('+')], entered);
    }

    [Theory]
    [InlineData(0, " b", "b")]
    [InlineData(0, " \nb", "b")]
    [InlineData(0, " \n c", null)]
    [InlineData(0, " c", "c")]
    [InlineData(1, " \n d", "d")]
    [InlineData(1, " d", "d")]
    [InlineData(1, " \r d", null)]
    [InlineData(1, " \n c", null)]
    public void AChoiceShouldNotTellTheWhiteSpaceItSkippedToANoNewLineBeforeAfterAnotherSkip(int grammar, string input, string? expected)
    {
        // SkipWhiteSpace is invoked after the white space the choice skips, where it skips none, and so is NoNewLineBefore,
        // which sees no new line either way. A NoNewLineBefore holding another one, with other new lines, is the same: the
        // alternatives skip the white space themselves.
        var parser = Literals.Char('a').SkipAnd(grammar == 0
            ? OneOf(SkipWhiteSpace(NoNewLineBefore(Literals.Text("b"))), NoNewLineBefore(Terms.Text("c")), Terms.Text("e"))
            : OneOf(NoNewLineBefore(NoNewLineBefore(Terms.Text("d")), "\r"), NoNewLineBefore(Terms.Text("c")), Terms.Text("e"))).Eof();

        Assert.Equal(expected, parser.Parse("a" + input));
        Assert.Equal(expected, parser.Parse(new System.IO.StringReader("a" + input)));
    }

    [Theory]
    [InlineData("a \n foo", null)]
    [InlineData("a foo", "foo")]
    [InlineData("a \n x", "x")]
    [InlineData("a \n y", "y")]
    public void AChoiceShouldTellTheWhiteSpaceItSkippedThroughALookupOfAChoice(string input, string? expected)
    {
        // The table invokes the choice of Lookup() itself, which skips no white space after the one the outer choice skipped
        var inner = OneOf(Terms.Text("x"), NoNewLineBefore(Terms.Identifier().Then(static x => x.ToString()))).Lookup(skipWhiteSpace: true, 'f', 'x');
        var parser = Literals.Char('a').SkipAnd(OneOf(Terms.Text("y"), inner)).Eof();

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal(expected, parser.Parse(new System.IO.StringReader(input)));

        // The white space is skipped once, before the choice of Lookup() and its NoNewLineBefore are invoked
        if (input.Contains('f'))
        {
            var entered = new System.Collections.Generic.List<int>();
            var context = new ParseContext(new Scanner(input))
            {
                OnEnterParser = (p, c) =>
                {
                    if (p.GetType().Name.StartsWith("NoNewLineBefore", StringComparison.Ordinal))
                    {
                        entered.Add(c.Scanner.Cursor.Offset);
                    }
                },
            };

            var result = new ParseResult<string>();
            parser.Parse(context, ref result);
            Assert.Equal([input.IndexOf('f')], entered);
        }
    }

    [Theory]
    [InlineData("a \r y", "y")]
    [InlineData("a \n y", null)]
    [InlineData("a \r x y", "y")]
    [InlineData("a \n c", null)]
    [InlineData("a \n e", "e")]
    public void ANoNewLineBeforeAfterOneMatchingNothingShouldSeeNoWhiteSpace(string input, string? expected)
    {
        // The first NoNewLineBefore can match nothing, after the white space: the second one is invoked there, where it sees
        // none, as when the alternatives skip the white space themselves
        var parser = Literals.Char('a').SkipAnd(OneOf(
            NoNewLineBefore(ZeroOrOne(Terms.Text("x")), "\n").And(NoNewLineBefore(Terms.Text("y"))).Then(static x => x.Item2),
            NoNewLineBefore(Terms.Text("c")),
            Terms.Text("e"))).Eof();

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal(expected, parser.Parse(new System.IO.StringReader(input)));
    }

    [Theory]
    [InlineData(0, "a \n y", "y")]
    [InlineData(0, "a \n x y", "y")]
    [InlineData(1, "a \n y", "y")]
    [InlineData(1, "a \n zz y", "y")]
    [InlineData(1, "a \n zz \n y", null)]
    [InlineData(0, "a \n c", null)]
    public void ANoNewLineBeforeAfterAParserSkippingTheWhiteSpaceAndMatchingNothingShouldSeeNoWhiteSpace(int grammar, string input, string? expected)
    {
        // SkipWhiteSpace, or a token, skips the white space and can match nothing: the NoNewLineBefore after it sees none
        var first = grammar == 0
            ? SkipWhiteSpace(Literals.Text("x").Optional()).And(NoNewLineBefore(Terms.Text("y"))).Then(static x => x.Item2)
            : Terms.Pattern(static c => c == 'z', minSize: 0).And(NoNewLineBefore(Terms.Text("y"))).Then(static x => x.Item2);
        var parser = Literals.Char('a').SkipAnd(OneOf(first, NoNewLineBefore(Terms.Text("c")), Terms.Text("e"))).Eof();

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal(expected, parser.Parse(new System.IO.StringReader(input)));
    }

    [Fact]
    public void AChoiceShouldForgetTheWhiteSpaceItSkippedWhenAnAlternativeThrows()
    {
        // The second alternative throws after the choice recorded the white space: a NoNewLineBefore after SkipWhiteSpace,
        // where it ends, sees none when the context is used again
        var throwing = Literals.Char('a').SkipAnd(OneOf(NoNewLineBefore(Terms.Text("b")), Terms.Text("c").AndSkip(Terms.Text("d").ElseError("boom"))));
        var skipping = Literals.Char('a').SkipAnd(SkipWhiteSpace(NoNewLineBefore(Literals.Text("c"))));

        var context = new ParseContext(new Scanner("a \n c x"));
        var result = new ParseResult<string>();

        var thrown = false;

        try
        {
            throwing.Parse(context, ref result);
        }
        catch (ParseException)
        {
            thrown = true;
        }

        Assert.True(thrown);

        context.Scanner.Cursor.ResetPosition(0);
        Assert.True(skipping.Parse(context, ref result));
        Assert.Equal("c", result.Value);
    }

    [Fact]
    public void AChoiceReachedAgainWhileItsWhiteSpaceIsResolvedShouldParse()
    {
        // The analysis of the alternatives reaches the choice again through the Lookup()
        var expression = Deferred<string>();
        expression.Parser = OneOf(
            NoNewLineBefore(Terms.Text("x")),
            expression.And(Terms.Char('+')).Then(static x => x.Item1 + "+").Lookup(skipWhiteSpace: true, 'x'));

        Assert.Equal("x", expression.Parse("x"));
        Assert.Equal("x", expression.Parse(" x"));
    }

    [Theory]
    [InlineData("x \n ac", null)]
    [InlineData("x ac", "ac")]
    [InlineData("x \n ab", "ab")]
    public void AChoiceShouldTellTheWhiteSpaceItSkippedAfterAnAlternativeWithAnotherWhiteSpaceParser(string input, string? expected)
    {
        // The first alternative reads "a" and fails with its own white space parser, which doesn't hide the white space the
        // choice skipped from the second one
        var first = Terms.Text("a").And(Terms.Text("b").WithWhiteSpaceParser(Literals.WhiteSpace())).Then(static x => x.Item1 + x.Item2);
        var parser = Literals.Char('x').SkipAnd(OneOf(first, NoNewLineBefore(Terms.Identifier().Then(static x => x.ToString())))).Eof();

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal(expected, parser.Parse(new System.IO.StringReader(input)));
    }

    [Theory]
    [InlineData("a \nb", "b")]
    [InlineData("a \rb", null)]
    [InlineData("a \nc", null)]
    [InlineData("a \rc", null)]
    [InlineData("a c", "c")]
    [InlineData("a \n e", "e")]
    public void NoNewLineBeforeAlternativesShouldFindTheirOwnNewLines(string input, string? expected)
    {
        // Each finds its new lines in the white space the choice skipped
        var parser = Literals.Char('a').SkipAnd(OneOf(
            NoNewLineBefore(Terms.Text("b"), "\r"),
            NoNewLineBefore(Terms.Text("c")),
            Terms.Text("e"))).Eof();

        Assert.Equal(expected, parser.Parse(input));
        Assert.Equal(expected, parser.Parse(new System.IO.StringReader(input)));
    }

    [Theory]
    [InlineData("a   x")]
    [InlineData("a \n x")]
    [InlineData("a   =x")]
    [InlineData("a   +x")]
    [InlineData("a /* c */ x")]
    [InlineData("a   ")]
    public void AChoiceWithNoNewLineBeforeShouldFailWhereAChoiceSkippingTheWhiteSpaceDoes(string input)
    {
        // The alternatives are invoked before the white space, and the choice fails after it, as when it skips it for them
        var skipping = WithComments(Terms.Identifier().And(OneOf(Terms.Text("+="), Terms.Text("=>"))).Eof());
        var restricted = WithComments(Terms.Identifier().And(OneOf(Terms.Text("+="), NoNewLineBefore(Terms.Text("=>")))).Eof());

        var expected = new ParseContext(new Scanner(input));
        var actual = new ParseContext(new Scanner(input));
        var result = new ParseResult<(TextSpan, string)>();

        Assert.False(skipping.Parse(expected, ref result));
        Assert.False(restricted.Parse(actual, ref result));
        Assert.Equal(0, actual.Scanner.Cursor.Offset);
        Assert.Equal(expected.Scanner.Cursor.FurthestOffset, actual.Scanner.Cursor.FurthestOffset);
    }

    // How many times a parse enters the parsers which are counted
    private static (bool Success, T? Value, int Offset, int Furthest, int Entered) ParseCounting<T>(Parser<T> parser, string input, Func<object, bool> counted)
    {
        var entered = 0;
        var context = new ParseContext(new Scanner(input)) { OnEnterParser = (p, _) => entered += counted(p) ? 1 : 0 };
        var result = new ParseResult<T>();
        var success = parser.Parse(context, ref result);

        return (success, success ? result.Value : default, context.Scanner.Cursor.Offset, context.Scanner.Cursor.FurthestOffset, entered);
    }

    [Theory]
    [InlineData("a", "a", 0, 0)]
    [InlineData("a b", "a", 0, 0)]
    [InlineData("a ; b", "a", 0, 0)]
    [InlineData("a += b", "a+=b", 1, 0)]
    [InlineData("a /* x */ += b", "a+=b", 1, 0)]
    [InlineData("a = b", "a=b", 1, 0)]
    [InlineData("a => b", "a=>b", 1, 1)]
    [InlineData("a\n=> b", "a", 1, 1)]
    [InlineData("a /* \n */ => b", "a", 1, 1)]
    public void AChoiceOfTwoCompositeAlternativesBeforeTheWhiteSpaceShouldUseItsTable(string input, string expected, int assignments, int arrows)
    {
        // The alternatives invoke what they start with before they fail: only the ones which can start with the char after the
        // white space are invoked, as when there are more of them (#581)
        var assignmentOperator = OneOf(Terms.Text("="), Terms.Text("+="));
        var assignment = assignmentOperator.And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
        var arrow = NoNewLineBefore(Terms.Text("=>")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
        var parser = WithComments(Terms.Identifier().And(OneOf(assignment, arrow).Else("")).Then(static x => x.Item1.ToString() + x.Item2));

        var outcome = ParseCounting(parser, input, p => ReferenceEquals(p, assignment));
        Assert.True(outcome.Success);
        Assert.Equal(expected, outcome.Value);
        Assert.Equal(assignments, outcome.Entered);
        Assert.Equal(arrows, ParseCounting(parser, input, p => ReferenceEquals(p, arrow)).Entered);
    }

    [Theory]
    [InlineData("a x", 0, 0, 0)]
    [InlineData("a ++", 1, 0, 0)]
    [InlineData("a --", 0, 1, 0)]
    [InlineData("a\n--", 0, 1, 0)]
    [InlineData("a /* x */ += b", 1, 0, 0)]
    [InlineData("a\n=> b", 0, 0, 1)]
    public void AChoiceOfTwoLiteralsAfterTheWhiteSpaceShouldUseItsTable(string input, int increments, int decrements, int arrows)
    {
        // The choice skips the white space once and tells it to its NoNewLineBefore alternatives (#582): only the ones which
        // can start with the text after it are invoked, as for a choice which skips the white space for them
        var increment = NoNewLineBefore(Terms.Text("++"));
        var decrement = NoNewLineBefore(Terms.Text("--"));
        var postfix = WithComments(Terms.Identifier().SkipAnd(OneOf(increment, decrement).Else("")));

        Assert.Equal(increments, ParseCounting(postfix, input, p => ReferenceEquals(p, increment)).Entered);
        Assert.Equal(decrements, ParseCounting(postfix, input, p => ReferenceEquals(p, decrement)).Entered);

        // A text of Terms, with a NoNewLineBefore one
        var assignment = Terms.Text("+=");
        var arrow = NoNewLineBefore(Terms.Text("=>"));
        var tail = WithComments(Terms.Identifier().SkipAnd(OneOf(assignment, arrow).Else("")));

        Assert.Equal(arrows, ParseCounting(tail, input, p => ReferenceEquals(p, arrow)).Entered);
        Assert.Equal(input.EndsWith("+= b", StringComparison.Ordinal), ParseCounting(tail, input, static _ => false).Value == "+=");
    }

    [Theory]
    [InlineData("a x", 1)]
    [InlineData("a ++", 0)]
    [InlineData("a --", 1)]
    [InlineData("a\n--", 1)]
    [InlineData("a /* x */ += b", 1)]
    public void AChoiceOfTwoLiteralsBeforeTheWhiteSpaceShouldTryThemInOrder(string input, int seconds)
    {
        // A NoNewLineBefore holding another one can't be told the white space: the choice invokes its alternatives before it,
        // and trying the one which doesn't match costs about what reading the char after the white space does, so the first
        // one is invoked whatever the char is, and the second one when the first one fails (#581)
        var increment = NoNewLineBefore(NoNewLineBefore(Terms.Text("++")), "\r");
        var decrement = NoNewLineBefore(Terms.Text("--"));
        var postfix = WithComments(Terms.Identifier().SkipAnd(OneOf(increment, decrement).Else("")));

        Assert.Equal(1, ParseCounting(postfix, input, p => ReferenceEquals(p, increment)).Entered);
        Assert.Equal(seconds, ParseCounting(postfix, input, p => ReferenceEquals(p, decrement)).Entered);
    }

    [Theory]
    [InlineData("a => b")]
    [InlineData("a=>b")]
    [InlineData("a += b")]
    [InlineData("a /* x */ = b")]
    [InlineData("a b")]
    [InlineData("a")]
    [InlineData("a =>")]
    [InlineData("a = ")]
    [InlineData("a += ;")]
    [InlineData("a /* x */ =>")]
    [InlineData("a  ")]
    [InlineData("a ;")]
    public void AChoiceOfTwoCompositeAlternativesBeforeTheWhiteSpaceShouldParseAsTheOneSkippingTheWhiteSpace(string input)
    {
        // Selected with its table, it parses as the choice which skips the white space once for them when there is no new line:
        // the same values, positions and furthest failures, when it matches and when it fails
        static Parser<string> Create(Func<Parser<string>, Parser<string>> restrict)
        {
            var assignment = OneOf(Terms.Text("="), Terms.Text("+=")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
            var arrow = restrict(Terms.Text("=>")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
            return WithComments(Terms.Identifier().And(OneOf(assignment, arrow)).Then(static x => x.Item1.ToString() + x.Item2).Eof());
        }

        var expected = ParseCounting(Create(static p => p), input, static _ => false);
        var actual = ParseCounting(Create(NoNewLineBefore), input, static _ => false);

        Assert.Equal(expected, actual);

        if (!actual.Success)
        {
            Assert.Equal(0, actual.Offset);
        }
    }

    [Theory]
    [InlineData("a => b")]
    [InlineData("a += b")]
    [InlineData("a b")]
    [InlineData("a")]
    [InlineData("a =>")]
    [InlineData("a = ")]
    [InlineData("a /* x */ =>")]
    [InlineData("a ;")]
    [InlineData("a\n=> b")]
    [InlineData("a\n= b")]
    [InlineData("a /* \n */ => b")]
    [InlineData("a /* \n */ x")]
    [InlineData("a // c\n=>")]
    [InlineData("a\n")]
    public void AChoiceOfTwoCompositeAlternativesBeforeTheWhiteSpaceShouldParseAsWhenTriedInOrder(string input)
    {
        // The alternatives selected with the table, and the same ones invoked in order where the choice is, which a choice does
        // when they tell no hints, as an ordered choice is defined: the same values, positions and furthest failures, new
        // lines included
        static Parser<string> Create(Func<Parser<string>, Parser<string>> wrap)
        {
            var assignment = OneOf(Terms.Text("="), Terms.Text("+=")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
            var arrow = NoNewLineBefore(Terms.Text("=>")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2);
            return WithComments(Terms.Identifier().And(OneOf(wrap(assignment), wrap(arrow))).Then(static x => x.Item1.ToString() + x.Item2).Eof());
        }

        var expected = ParseCounting(Create(static p => new WithoutHints<string>(p)), input, static _ => false);
        var actual = ParseCounting(Create(static p => p), input, static _ => false);

        Assert.Equal(expected, actual);

        if (!actual.Success)
        {
            Assert.Equal(0, actual.Offset);
        }
    }

    // A parser which tells no hints, so that a choice holding it tries it in order
    private sealed class WithoutHints<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public WithoutHints(Parser<T> parser) => _parser = parser;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);
            var success = _parser.Parse(context, ref result);
            context.ExitParser(this);
            return success;
        }
    }

    [Theory]
    [InlineData("a x", 0, 0, 0)]
    [InlineData("a ++", 1, 0, 1)]
    [InlineData("a --", 0, 1, 1)]
    [InlineData("a\n++", 1, 0, 1)]
    public void AChoiceOfTwoConvertedLiteralsAfterTheWhiteSpaceShouldUseItsTable(string input, int increments, int decrements, int operators)
    {
        // A Then of a literal, as the operators of a level of Operators(), which are wrapped too: the ones which can't start
        // with the char after the white space aren't invoked
        var increment = NoNewLineBefore(Terms.Text("++")).Then(static _ => 1);
        var decrement = NoNewLineBefore(Terms.Text("--")).Then(static _ => -1);
        var postfix = WithComments(Terms.Identifier().SkipAnd(OneOf(increment, decrement).Else(0)));

        Assert.Equal(increments, ParseCounting(postfix, input, p => ReferenceEquals(p, increment)).Entered);
        Assert.Equal(decrements, ParseCounting(postfix, input, p => ReferenceEquals(p, decrement)).Entered);

        // A level of postfix operators alone, tried again after one which matched
        var table = Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix((NoNewLineBefore(Terms.Text("++")), static x => x + "++"), (NoNewLineBefore(Terms.Text("--")), static x => x + "--"));
        var outcome = ParseCounting(WithComments(table), input, static p => p.GetType().Name.StartsWith("NoNewLineBefore", StringComparison.Ordinal));

        Assert.True(outcome.Success);
        Assert.Equal(operators, outcome.Entered);
    }

    [Theory]
    [InlineData("a ;", 0, 0)]
    [InlineData("a of", 0, 1)]
    [InlineData("a in", 1, 1)]
    [InlineData("a 12", 0, 0)]
    [InlineData("a\nb", 0, 1)]
    public void AChoiceOfTwoTokensAfterTheWhiteSpaceShouldUseItsTable(string input, int keywords, int names)
    {
        // Keywords, identifiers and numbers: the NoNewLineBefore one is only invoked before a char it can start with
        var inKeyword = NoNewLineBefore(Terms.Keyword("in"));
        var keyword = WithComments(Terms.Identifier().SkipAnd(OneOf(inKeyword, Terms.Keyword("of")).Else("")));
        Assert.Equal(keywords, ParseCounting(keyword, input, p => ReferenceEquals(p, inKeyword)).Entered);

        var name = NoNewLineBefore(Terms.Identifier()).Then(static x => x.ToString());
        var token = WithComments(Terms.Identifier().SkipAnd(OneOf(name, Terms.Integer().Then(static x => x.ToString())).Else("")));
        Assert.Equal(names, ParseCounting(token, input, p => ReferenceEquals(p, name)).Entered);
    }

    [Theory]
    [InlineData("a ;", 0)]
    [InlineData("a\n;", 0)]
    [InlineData("a +=", 1)]
    [InlineData("a ++", 1)]
    [InlineData("a\n++", 1)]
    public void AChoiceOfTwoWrappedTokensAfterTheWhiteSpaceShouldUseItsTable(string input, int increments)
    {
        // The wrappers of a token which only invoke it tell its hints: the first alternative is only invoked before a char it
        // can start with
        static void AssertEntered<T>(Parser<T> first, Parser<T> second, string input, int entered)
        {
            var parser = WithComments(Terms.Identifier().SkipAnd(OneOf(first, second).Then(static _ => "").Else("")));
            Assert.Equal(entered, ParseCounting(parser, input, p => ReferenceEquals(p, first)).Entered);
        }

        var arrow = NoNewLineBefore(Terms.Text("=>"));

        AssertEntered(NoNewLineBefore(Terms.Text("++")).Warning("postfix"), arrow, input, increments);
    }

    [Fact]
    public void AChoiceOfTwoCompositeAlternativesShouldInvokeFewerParsersThanOneAlternativePerOperator()
    {
        // The tail of a JavaScript conditional expression: an assignment operator and its operand, or an arrow which can't follow
        // a new line. Most expressions have no tail, and the choice of two alternatives invokes none for them, like the choice of
        // an alternative per operator (#581).
        string[] operators = ["=", "+=", "-=", "*=", "/=", "%=", "<<=", ">>=", ">>>=", "&=", "|=", "^=", "**=", "&&=", "||=", "??="];

        var value = Terms.Identifier().Then(static x => x.ToString());
        var arrow = NoNewLineBefore(Terms.Text("=>")).SkipAnd(value).Then(static x => "=>" + x);

        var assignmentOperator = OneOf(operators.Select(static o => Terms.Text(o)).ToArray());
        var twoAlternatives = OneOf(assignmentOperator.And(value).Then(static x => x.Item1 + x.Item2), arrow);

        var perOperator = OneOf(operators.Select(o => Terms.Text(o).SkipAnd(value).Then(x => o + x)).Append(arrow).ToArray());

        static Parser<string> Statements(Parser<string> tail)
            => ZeroOrMany(Terms.Identifier().And(tail.Else("")).Then(static x => x.Item1.ToString() + x.Item2).AndSkip(Terms.Char(';')))
                .Eof().Then(static x => string.Join("|", x));

        var input = string.Concat(Enumerable.Range(0, 50).Select(static i => (i % 5) switch
        {
            0 => "a = b;",
            1 => "c => d;\n",
            2 => "e >>>= f;",
            _ => "g;\n",
        })).TrimEnd();

        var two = ParseCounting(Statements(twoAlternatives), input, static _ => true);
        var per = ParseCounting(Statements(perOperator), input, static _ => true);

        Assert.True(two.Success);
        Assert.True(per.Success);
        Assert.Equal(per.Value, two.Value);

        // The sequence of the operators and their operand is invoked for each assignment, and its choice selects the operator
        // with its table, rather than the operators being tried one by one. When the two alternatives were tried in order, the
        // sequence and the arrow were invoked for every statement: more parsers than with an alternative per operator.
        Assert.True(two.Entered < per.Entered, $"{two.Entered} parsers invoked, {per.Entered} with an alternative per operator");
    }

    [Theory]
    [InlineData("a ++", "++")]
    [InlineData("a /* x */ ++", "++")]
    [InlineData("a\n++", "+")]
    [InlineData("a /* \n */ ++", "+")]
    public void NoNewLineBeforeShouldBeAnAlternativeOfLongest(string input, string expected)
    {
        // The alternatives are invoked where the choice is, so that NoNewLineBefore sees the white space
        var parser = WithComments(Terms.Identifier().SkipAnd(Longest(Terms.Text("+"), NoNewLineBefore(Terms.Text("++")))));

        Assert.Equal(expected, parser.Parse(input));
    }

    [Theory]
    [InlineData("a ++", true, 1)]
    [InlineData("a\n++", false, -1)]
    public void NoNewLineBeforeShouldSeeTheNewLinesUnderAWarning(string input, bool matches, int location)
    {
        // When the diagnostics are collected, the white space isn't skipped before NoNewLineBefore to locate the warning: it
        // is located where NoNewLineBefore is invoked
        var parser = Terms.Identifier().SkipAnd(NoNewLineBefore(Terms.Text("++")).Warning("postfix").Else("none"));
        var context = new ParseContext(new Scanner(input)) { CollectDiagnostics = true };
        var result = new ParseResult<string>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(matches ? "++" : "none", result.Value);

        if (matches)
        {
            Assert.Equal(location, Assert.Single(context.Diagnostics).Position.Offset);
        }
        else
        {
            Assert.Empty(context.Diagnostics);
        }
    }

    [Fact]
    public void NoNewLineBeforeShouldWorkInAChoice()
    {
        // The choice skips the white space once for its alternatives
        var postfix = Terms.Identifier().And(OneOf(NoNewLineBefore(Terms.Text("++")), NoNewLineBefore(Terms.Text("--")), Terms.Text("!")).Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2);

        Assert.Equal("a++", postfix.Parse("a ++"));
        Assert.Equal("a--", postfix.Parse("a --"));
        Assert.Equal("a!", postfix.Parse("a\n!"));
        Assert.Equal("a", postfix.Parse("a\n++"));

        // Wrapping the choice keeps its lookup table
        var wrapped = Terms.Identifier().And(NoNewLineBefore(OneOf(Terms.Text("++"), Terms.Text("--"))).Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2);

        Assert.Equal("a--", wrapped.Parse("a --"));
        Assert.Equal("a", wrapped.Parse("a\n--"));
    }

    [Theory]
    [InlineData("a\nb", true)]
    [InlineData("a\r\nb", true)]
    [InlineData("a  \n  b", true)]
    [InlineData("a\n", true)]
    [InlineData("a b", false)]
    [InlineData("a", false)]
    [InlineData("a   ", false)]
    [InlineData("a.", false)]
    public void NewLineAheadShouldNotConsume(string input, bool expected)
    {
        var context = new ParseContext(new Scanner(input));
        Terms.Identifier().Parse(context);

        var result = new ParseResult<object?>();
        Assert.Equal(expected, NewLineAhead().Parse(context, ref result));
        Assert.Null(result.Value);

        // The cursor is where it was, whether it matched or not
        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a /* x */ b", false)]
    [InlineData("a /* x \n */ b", true)]
    [InlineData("a // x\n b", true)]
    [InlineData("a // x", false)]
    public void NewLineAheadShouldSeeTheNewLinesOfComments(string input, bool expected)
    {
        var parser = WithComments(Terms.Identifier().And(NewLineAhead()).Then(static _ => true).Else(false));
        Assert.Equal(expected, parser.Parse(input));
    }

    [Fact]
    public void NewLineAheadShouldReportAnUnterminatedComment()
    {
        // The comment parser reports the error, as it does for the next token
        var parser = WithComments(Terms.Identifier().And(NewLineAhead()).Then(static _ => true).Else(false));
        Assert.Throws<ParseException>(() => parser.Parse("a /* x"));
    }

    [Fact]
    public void NewLineAheadShouldUseCustomNewLines()
    {
        var parser = Terms.Identifier().And(NewLineAhead("\n")).Then(static _ => true).Else(false);

        Assert.True(parser.Parse("a \n b"));
        Assert.False(parser.Parse("a \r b"));
        Assert.Throws<ArgumentException>(() => NewLineAhead(""));
        Assert.Throws<ArgumentNullException>(() => NewLineAhead(null!));
    }

    [Theory]
    [InlineData("a;b", "a|b")]
    [InlineData("a\nb", "a|b")]
    [InlineData("a\r\nb", "a|b")]
    [InlineData("a /* \n */ b", "a|b")]
    [InlineData("a // c\n b", "a|b")]
    [InlineData("a", "a")]
    [InlineData("a  ", "a")]
    [InlineData("return\na", "return|a")]
    [InlineData("return a\n++b", "return a|++b")]
    [InlineData("a\n++b", "a|++b")]
    [InlineData("a++\nb", "a++|b")]
    [InlineData("a b", null)]
    [InlineData("a /* c */ b", null)]
    [InlineData("a++ b", null)]
    public void ShouldParseAutomaticSemicolons(string input, string? expected)
    {
        Assert.Equal(expected, WithComments(CreateStatements()).Parse(input));
    }

    [Fact]
    public void DocumentationSamplesShouldWork()
    {
        var argument = Terms.Identifier().NoNewLineBefore("\n\r\u2028\u2029").Then(static x => x.ToString());
        var parser = Terms.Keyword("return").SkipAnd(argument.Else("(none)"))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

        Assert.Equal("x", parser.Parse("return x"));
        Assert.Equal("x", parser.Parse("return /* x */ x"));
        Assert.Equal("(none)", parser.Parse("return\nx"));
        Assert.Equal("(none)", parser.Parse("return /* \n */ x"));

        var end = OneOf(
            Terms.Char(';').Then<object?>(static _ => null),
            Always().WhenFollowedBy(Terms.Char('}')),
            SkipWhiteSpace(Eof()),
            NewLineAhead());

        var statement = Terms.Identifier().AndSkip(end);
        var statements = ZeroOrMany(statement).Eof();

        Assert.Equal(["a", "b"], statements.Parse("a; b")!.Select(static x => x.ToString()));
        Assert.Equal(["a", "b"], statements.Parse("a\nb")!.Select(static x => x.ToString()));
        Assert.Null(statements.Parse("a b"));
    }

    // Statements ended by a semicolon, a new line, a '}' or the end of the input, as JavaScript's automatic semicolon insertion
    internal static Parser<string> CreateStatements()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), Always().WhenFollowedBy(Terms.Char('}')), SkipWhiteSpace(Eof()), NewLineAhead());
        var returnStatement = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x).Else("return"));
        var prefix = Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x);
        var expression = Terms.Identifier().And(NoNewLineBefore(Terms.Text("++")).Else("")).Then(static x => x.Item1.ToString() + x.Item2);
        return ZeroOrMany(OneOf(returnStatement, prefix, expression).AndSkip(end)).Eof().Then(static x => string.Join("|", x));
    }
}
