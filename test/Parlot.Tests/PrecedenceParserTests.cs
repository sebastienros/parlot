using System;
using Parlot.Fluent;
using Parlot.Rewriting;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class PrecedenceParserTests
{
    internal static readonly Parser<string> Atom = Terms.Identifier().Then(static x => x.ToString())
        .Or(Terms.Integer().Then(static x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    internal static Func<string, string, string> Binary(string op) => (a, b) => $"({a}{op}{b})";

    internal static Func<string, string> Prefix(string op) => a => $"({op}{a})";

    private static Func<string, string> Suffix(string op) => a => $"({a}{op})";

    private static PrecedenceParser<string> Arithmetic(Parser<string> operand) => operand.Operators()
        .Unary((Terms.Char('-'), Prefix("-")), (Terms.Char('!'), Prefix("!")))
        .RightAssociative((Terms.Text("**"), Binary("**")))
        .LeftAssociative((Terms.Char('*'), Binary("*")), (Terms.Char('/'), Binary("/")))
        .LeftAssociative((Terms.Char('+'), Binary("+")), (Terms.Char('-'), Binary("-")));

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a + b", "(a+b)")]
    [InlineData("a - b - c", "((a-b)-c)")]
    [InlineData("a * b + c * d", "((a*b)+(c*d))")]
    [InlineData("a + b * c - d", "((a+(b*c))-d)")]
    [InlineData("a ** b ** c", "(a**(b**c))")]
    [InlineData("a ** b ** c * d", "((a**(b**c))*d)")]
    [InlineData("a * b ** c ** d + e", "((a*(b**(c**d)))+e)")]
    [InlineData("-a", "(-a)")]
    [InlineData("- - a", "(-(-a))")]
    [InlineData("-a * b", "((-a)*b)")]
    [InlineData("a * -b", "(a*(-b))")]
    [InlineData("-a ** b", "((-a)**b)")]
    [InlineData("!-a + b", "((!(-a))+b)")]
    [InlineData("a - -b", "(a-(-b))")]
    public void ShouldApplyPrecedenceAndAssociativity(string input, string expected)
    {
        Assert.Equal(expected, Arithmetic(Atom).Parse(input));
    }

    [Theory]
    [InlineData("a +", "a", 1)]
    [InlineData("a + * b", "a", 1)]
    [InlineData("a ** b ** ", "(a**b)", 6)]
    [InlineData("a * b + ", "(a*b)", 5)]
    [InlineData("a ** ", "a", 1)]
    [InlineData("a b", "a", 1)]
    public void ShouldNotReadAnOperatorWithoutOperand(string input, string expected, int offset)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.True(Arithmetic(Atom).Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(offset, context.Scanner.Cursor.Offset);
        Assert.Equal(offset, result.End);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("-")]
    [InlineData("- ")]
    [InlineData("- * a")]
    [InlineData("- -")]
    [InlineData("+ a")]
    public void ShouldRestoreCursorWhenFailing(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.False(Arithmetic(Atom).Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void PrefixOperatorWithoutOperandShouldFailTheExpression()
    {
        // Like Unary, the operand isn't tried when a prefix operator matched, so the alternative is
        var parser = Arithmetic(Atom).Or(Terms.Char('-').Then(static _ => "minus"));

        Assert.Equal("minus", parser.Parse("-"));
        Assert.Equal("(-a)", parser.Parse("-a"));
    }

    [Theory]
    [InlineData("a", "a", 0, 1)]
    [InlineData(" a + b ", "(a+b)", 1, 6)]
    [InlineData(" -a", "(-a)", 1, 3)]
    [InlineData("a ** b ** c +", "(a**(b**c))", 0, 11)]
    public void ShouldReportTheSpanOfTheExpression(string input, string expected, int start, int end)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.True(Arithmetic(Atom).Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(start, result.Start);
        Assert.Equal(end, result.End);
    }

    [Fact]
    public void ShouldParseTheOperandWithoutLevels()
    {
        Assert.Equal("a", Atom.Operators().Parse("a"));
        Assert.Null(Atom.Operators().Parse("+"));
    }

    [Theory]
    [InlineData("a!", "(a!)")]
    [InlineData("a!!", "((a!)!)")]
    [InlineData("-a!", "(-(a!))")]
    [InlineData("a! * b!", "((a!)*(b!))")]
    [InlineData("a!?", "((a!)?)")]
    [InlineData("-a?", "((-a)?)")]
    [InlineData("a * b?", "((a*b)?)")]
    [InlineData("a? * b", "(a?)")]
    [InlineData("a ** b!", "(a**(b!))")]
    [InlineData("a? ** b", "(a?)")]
    public void ShouldApplyPostfixOperators(string input, string expected)
    {
        // '!' binds tighter than the prefix operators, '?' less than '*'
        var parser = Atom.Operators()
            .Postfix((Terms.Char('!'), Suffix("!")))
            .Unary((Terms.Char('-'), Prefix("-")))
            .RightAssociative((Terms.Text("**"), Binary("**")))
            .LeftAssociative((Terms.Char('*'), Binary("*")))
            .Postfix((Terms.Char('?'), Suffix("?")));

        Assert.Equal(expected, parser.Parse(input));
    }

    [Fact]
    public void PostfixOperatorShouldSetTheEndOfTheExpression()
    {
        var parser = Atom.Operators().Postfix((Terms.Char('!'), Suffix("!")));
        var context = new ParseContext(new Scanner("a ! b"));
        var result = new ParseResult<string>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal("(a!)", result.Value);
        Assert.Equal(3, result.End);
        Assert.Equal(3, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a << b", "(a<<b)")]
    [InlineData("a < b", "(a<b)")]
    [InlineData("a << b < c", "((a<<b)<c)")]
    [InlineData("a < b << c", "(a<(b<<c))")]
    [InlineData("a <= b", "(a<=b)")]
    [InlineData("a & b && c", "((a&b)&&c)")]
    [InlineData("a && b & c", "(a&&(b&c))")]
    [InlineData("a == b", "(a==b)")]
    [InlineData("a = b == c", "((a=b)==c)")]
    public void ShouldSelectOperatorsSharingTheirPrefixAcrossLevels(string input, string expected)
    {
        // The tighter levels list operators which are the start of operators of the looser ones: when they are not
        // followed by an operand the looser levels are tried at the same position
        var parser = Atom.Operators()
            .LeftAssociative((Terms.Text("<<"), Binary("<<")), (Terms.Text("="), Binary("=")))
            .LeftAssociative((Terms.Text("<"), Binary("<")), (Terms.Text("&"), Binary("&")))
            .LeftAssociative((Terms.Text("<="), Binary("<=")), (Terms.Text("=="), Binary("==")))
            .LeftAssociative((Terms.Text("&&"), Binary("&&")));

        Assert.Equal(expected, parser.Parse(input));
    }

    [Fact]
    public void OperatorsOfALevelShouldKeepTheirOrder()
    {
        // The first operator wins even when a later one of the same level would match more text, as in LeftAssociative
        var parser = Atom.Operators().LeftAssociative((Terms.Text("<"), Binary("<")), (Terms.Text("<="), Binary("<=")));

        Assert.Equal("(a<b)", parser.Parse("a < b"));
        Assert.Equal("a", parser.Parse("a <= b"));
    }

    [Fact]
    public void ShouldStopWhenOperatorAndOperandAreEmpty()
    {
        var operand = Literals.Text("x").Optional().Then(static x => x.HasValue ? "x" : "");
        var parser = operand.Operators()
            .LeftAssociative((Literals.Text("+").Optional(), Binary("+")))
            .RightAssociative((Literals.Text("^").Optional(), Binary("^")));

        Assert.Equal("(x+x)", parser.Parse("x+x"));
        Assert.Equal("(x^x)", parser.Parse("x^x"));
        Assert.Equal("", parser.Parse(""));
        Assert.Equal("x", parser.Parse("x?"));
    }

    [Fact]
    public void ShouldTreatANonConsumingPrefixOperatorAsNoOperator()
    {
        // As in Unary, the first operator matches without reading anything, the second one applies
        var parser = Terms.Integer().Operators().Unary(
            (ZeroOrMany(Literals.Char('-')).Then(static _ => 0), static value => -value),
            (Literals.Char('+').Then(static _ => 0), static value => value + 100));

        Assert.Equal(105, parser.Parse("+5"));
        Assert.Equal(-5, parser.Parse("-5"));
        Assert.Equal(5, parser.Parse("5"));
    }

    [Fact]
    public void ShouldEnforceMaxRecursionDepthOnPrefixOperators()
    {
        var parser = Literals.Char('a').Then(static _ => "a").Operators().Unary((Literals.Char('-'), Prefix("-")));

        Assert.False(parser.TryParse(new ParseContext(new Scanner("---a"), maxRecursionDepth: 2), out _, out var error));
        Assert.Equal("The maximum parser recursion depth of 2 was exceeded.", error!.Message);

        Assert.True(parser.TryParse(new ParseContext(new Scanner("--a"), maxRecursionDepth: 2), out var value, out _));
        Assert.Equal("(-(-a))", value);
    }

    private static Parser<long> TwoPrefixLevels() => Terms.Integer().Operators()
        .Unary((Literals.Char('-'), static value => -value))
        .LeftAssociative((Literals.Char('*'), static (a, b) => a * b))
        .Unary((Literals.Char('!'), static value => value + 1));

    [Fact]
    public void ShouldParsePrefixChainsLongerThanTheStack()
    {
        var parser = TwoPrefixLevels();

        Assert.Equal(5L, parser.Parse(new string('-', 1_000_000) + "5"));

        // The operators of the levels up to the one of a prefix operator apply to its operand first
        Assert.Equal(-5L, parser.Parse("!-2*3"));
        Assert.Equal(999_994L, parser.Parse(new string('!', 1_000_000) + "-2*3"));

        // The operand of a prefix operator can start with an operator of its level or of a tighter one, not a looser one
        var context = new ParseContext(new Scanner("-!2"));
        var result = new ParseResult<long>();
        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("!-2*3")]
    [InlineData("-2*!3")]
    [InlineData("!!-2*-3*4")]
    [InlineData("!2*!3")]
    [InlineData("--2*3")]
    [InlineData("-!2")]
    [InlineData("!-!-2*3")]
    [InlineData("2*-")]
    [InlineData("!")]
    [InlineData("-")]
    public void PrefixChainsShouldParseLikeTheChainOfLevels(string input)
    {
        var chain = Terms.Integer()
            .Unary((Literals.Char('-'), static value => -value))
            .LeftAssociative((Literals.Char('*'), static (a, b) => a * b))
            .Unary((Literals.Char('!'), static value => value + 1));

        var chainContext = new ParseContext(new Scanner(input));
        var chainResult = new ParseResult<long>();
        var tableContext = new ParseContext(new Scanner(input));
        var tableResult = new ParseResult<long>();

        Assert.Equal(chain.Parse(chainContext, ref chainResult), TwoPrefixLevels().Parse(tableContext, ref tableResult));
        Assert.Equal(chainResult.Value, tableResult.Value);
        Assert.Equal(chainResult.Start, tableResult.Start);
        Assert.Equal(chainContext.Scanner.Cursor.Offset, tableContext.Scanner.Cursor.Offset);
    }

#if NET8_0_OR_GREATER
    [Theory]
    [InlineData("5", 5)]
    [InlineData("~ - -5", ~5)]
    public void ShouldNotAllocateWithOnlyPrefixLevels(string input, long expected)
    {
        // No operator can follow an operand, and none is looked up
        var parser = Terms.Integer().Operators()
            .Unary((Terms.Char('-'), static x => -x))
            .Unary((Terms.Char('~'), static x => ~x));

        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<long>();

        // Warm up, such that the tiering and the lazy initializations are not measured
        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            Assert.True(parser.Parse(context, ref result));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var i = 0; i < 100; i++)
        {
            context.Scanner.Cursor.ResetPosition(TextPosition.Start);
            parser.Parse(context, ref result);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Equal(expected, result.Value);
        Assert.Equal(input.Length, context.Scanner.Cursor.Offset);
    }
#endif

    [Fact]
    public void ShouldDeclareTheCharsOfItsOperandAndPrefixOperators()
    {
        var seekable = (ISeekable)Terms.Integer().Then(static x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)).Operators()
            .Unary((Terms.Char('~'), Prefix("~")))
            .LeftAssociative((Terms.Char('*'), Binary("*")));

        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.Contains('~', seekable.ExpectedChars);
        Assert.Contains('7', seekable.ExpectedChars);
        Assert.DoesNotContain('*', seekable.ExpectedChars);

        // Usable in a choice
        var choice = OneOf((Parser<string>)seekable, Terms.Char('x').Then(static _ => "x"));
        Assert.Equal("((~2)*3)", choice.Parse(" ~2*3"));
        Assert.Equal("x", choice.Parse(" x"));
    }

    [Fact]
    public void ShouldValidateItsArguments()
    {
        Assert.Throws<ArgumentNullException>(() => ((Parser<string>)null!).Operators());
        Assert.Throws<ArgumentException>(() => Atom.Operators().LeftAssociative<char>());
        Assert.Throws<ArgumentNullException>(() => Atom.Operators().LeftAssociative<char>(null!));
        Assert.Throws<ArgumentException>(() => Atom.Operators().Unary(((Parser<char>)null!, Prefix("-"))));
        Assert.Throws<ArgumentException>(() => Atom.Operators().RightAssociative((Terms.Char('^'), (Func<string, string, string>)null!)));
    }

    [Fact]
    public void LevelsShouldBeAddedToNewParsers()
    {
        var multiplicative = Atom.Operators().LeftAssociative((Terms.Char('*'), Binary("*")));
        var additive = multiplicative.LeftAssociative((Terms.Char('+'), Binary("+")));

        Assert.Equal("(a*b)", multiplicative.Parse("a*b+c"));
        Assert.Equal("((a*b)+c)", additive.Parse("a*b+c"));
    }

    [Fact]
    public void ShouldParseRecursiveGrammars()
    {
        var expression = Deferred<string>();
        var primary = Atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        expression.Parser = Arithmetic(primary);

        Assert.Equal("((a+b)*(-(c-d)))", expression.Parse("(a + b) * -(c - d)"));
        Assert.Equal("(a+b)", expression.Parse("(a + b) * -(c - d"));
    }

    [Fact]
    public void ShouldParseLongRightAssociativeChains()
    {
        var parser = Terms.Integer().Operators().RightAssociative((Terms.Char('^'), static (long a, long b) => a - b));
        // 1 - (1 - (1 - ...)) with an odd number of operands, which doesn't recurse for each operator
        var input = "1" + string.Concat(System.Linq.Enumerable.Repeat("^1", 100_000));

        Assert.Equal(1, parser.Parse(input));
    }
}
