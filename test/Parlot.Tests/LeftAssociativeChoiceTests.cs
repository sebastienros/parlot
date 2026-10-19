using System.Collections.Generic;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The tuple form of LeftAssociative tries its operators through a OneOf, so only the operators that can
/// start with the next char are tried and white space is skipped once.
/// </summary>
public class LeftAssociativeChoiceTests
{
    private static (Parser<decimal> Parser, Parser<char> Unused) Build(bool withContext)
    {
        // The unused operator is listed first, so it was tried at every operator position before
        var unused = Literals.Char('x');
        var plus = Literals.Char('+');
        var minus = Literals.Char('-');

        Parser<decimal> parser = withContext
            ? Terms.Decimal().LeftAssociative(
                (SkipWhiteSpace(unused), static (ParseContext _, decimal a, decimal b) => a * b),
                (SkipWhiteSpace(plus), static (ParseContext _, decimal a, decimal b) => a + b),
                (SkipWhiteSpace(minus), static (ParseContext _, decimal a, decimal b) => a - b))
            : Terms.Decimal().LeftAssociative(
                (SkipWhiteSpace(unused), static (a, b) => a * b),
                (SkipWhiteSpace(plus), static (a, b) => a + b),
                (SkipWhiteSpace(minus), static (a, b) => a - b));

        return (parser, unused);
    }

    [Theory]
    [InlineData(false, "10 - 4 + 2 - 1", 7)]
    [InlineData(true, "10 - 4 + 2 - 1", 7)]
    [InlineData(false, "10 x 4 + 2", 42)]
    [InlineData(true, "10 x 4 + 2", 42)]
    [InlineData(false, " 1 +", 1)]
    [InlineData(true, " 1 +", 1)]
    public void OperatorsShouldBeSelectedByTheirFirstChar(bool withContext, string input, decimal expected)
    {
        var (parser, unused) = Build(withContext);
        var entries = 0;
        var context = new ParseContext(new Scanner(input))
        {
            OnEnterParser = (p, _) =>
            {
                if (ReferenceEquals(p, unused))
                {
                    entries++;
                }
            },
        };
        var result = new ParseResult<decimal>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(expected, result.Value);

        // The operator is only tried when the next char is an 'x'
        Assert.Equal(input.IndexOf('x') >= 0 ? 1 : 0, entries);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperatorsShouldKeepTheirOrder(bool withContext)
    {
        // The first operator wins even when a later one would match more text, as before
        Parser<string> parser = withContext
            ? Terms.Identifier().Then(static x => x.ToString()).LeftAssociative(
                (Terms.Text("<"), static (ParseContext _, string a, string b) => $"({a} < {b})"),
                (Terms.Text("<="), static (ParseContext _, string a, string b) => $"({a} <= {b})"))
            : Terms.Identifier().Then(static x => x.ToString()).LeftAssociative(
                (Terms.Text("<"), static (a, b) => $"({a} < {b})"),
                (Terms.Text("<="), static (a, b) => $"({a} <= {b})"));

        Assert.Equal("(a < b)", parser.Parse("a < b"));
        Assert.Equal("a", parser.Parse("a <= b"));

        Parser<string> reversed = withContext
            ? Terms.Identifier().Then(static x => x.ToString()).LeftAssociative(
                (Terms.Text("<="), static (ParseContext _, string a, string b) => $"({a} <= {b})"),
                (Terms.Text("<"), static (ParseContext _, string a, string b) => $"({a} < {b})"))
            : Terms.Identifier().Then(static x => x.ToString()).LeftAssociative(
                (Terms.Text("<="), static (a, b) => $"({a} <= {b})"),
                (Terms.Text("<"), static (a, b) => $"({a} < {b})"));

        Assert.Equal("(a < b)", reversed.Parse("a < b"));
        Assert.Equal("((a <= b) < c)", reversed.Parse("a <= b < c"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperatorsThatDisagreeOnWhiteSpaceShouldStillBeTried(bool withContext)
    {
        // Without a common hint the choice tries the operators in order
        Parser<decimal> parser = withContext
            ? Terms.Decimal().LeftAssociative(
                (Terms.Char('+'), static (ParseContext _, decimal a, decimal b) => a + b),
                (Literals.Char('-'), static (ParseContext _, decimal a, decimal b) => a - b))
            : Terms.Decimal().LeftAssociative(
                (Terms.Char('+'), static (a, b) => a + b),
                (Literals.Char('-'), static (a, b) => a - b));

        Assert.Equal(7, parser.Parse("10 + 2-5"));
        Assert.Equal(12, parser.Parse("10 + 2 -5"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OperatorsShouldBeTriedAtTheSamePositionAsBefore(bool withContext)
    {
        var positions = new List<int>();
        var op = Terms.Char('+');
        var minus = Terms.Char('-');

        Parser<decimal> parser = withContext
            ? Terms.Decimal().LeftAssociative(
                (op, static (ParseContext _, decimal a, decimal b) => a + b),
                (minus, static (ParseContext _, decimal a, decimal b) => a - b))
            : Terms.Decimal().LeftAssociative(
                (op, static (a, b) => a + b),
                (minus, static (a, b) => a - b));

        var context = new ParseContext(new Scanner(" 1 + 2 +3"))
        {
            OnEnterParser = (p, c) =>
            {
                if (ReferenceEquals(p, op))
                {
                    positions.Add(c.Scanner.Cursor.Offset);
                }
            },
        };
        var result = new ParseResult<decimal>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(6, result.Value);
        Assert.Equal(1, result.Start);
        Assert.Equal(9, result.End);

        // The choice skipped the white space before trying the operator, and didn't try it at the end
        Assert.Equal([3, 7], positions);
    }
}
