#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// <c>Operators()</c> tests the first char of its operators before it invokes its prefix and operator choices. A table whose operators hide their hints invokes them always, and parses the same.
/// </summary>
public class OperatorStartTests
{
    // An operator without hints, which a table can't skip
    private sealed class NoHints<T> : Parser<T>
    {
        private readonly Parser<T> _parser;

        public NoHints(Parser<T> parser)
        {
            _parser = parser;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);
            var success = _parser.Parse(context, ref result);
            context.ExitParser(this);
            return success;
        }
    }

    private static readonly Parser<string> Atom = Terms.Identifier().Then(static x => x.ToString())
        .Or(Terms.Integer().Then(static x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static Func<string, string, string> Binary(string op) => (a, b) => $"({a}{op}{b})";

    private static Func<string, string> Prefix(string op) => a => $"({op}{a})";

    // Prefix levels whose operators skip the white space next to ones whose operators don't, a valued one, valued postfix
    // operators, and operators of both kinds after the operands, whose choice has no lookup table
    private static Parser<string> Mixed(Func<Parser<string>, Parser<string>> op, Parser<string>? atom = null)
    {
        var expression = Deferred<string>();
        var primary = (atom ?? Atom).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((op(Terms.Char('.').SkipAnd(Terms.Identifier()).Then(static x => x.ToString())), static (string x, string name) => $"{x}.{name}"))
            .Unary((op(Literals.Char('-').Then(static _ => "-")), Prefix("-")))
            .Unary((op(Terms.Text("typeof")), Prefix("typeof ")))
            .Unary((op(Terms.Char('!').Then(static (_, start, _, _) => start.ToString(System.Globalization.CultureInfo.InvariantCulture))), static (string x, string start) => $"(!@{start}{x})"))
            .RightAssociative((op(Terms.Text("**")), Binary("**")))
            .LeftAssociative((op(Literals.Char('*').Then(static _ => "*")), Binary("*")), (op(Terms.Char('/').Then(static _ => "/")), Binary("/")))
            .LeftAssociative((op(Terms.Char('+').Then(static _ => "+")), Binary("+")), (op(Literals.Char('-').Then(static _ => "-")), Binary("-")))
            .LeftAssociative([op(Terms.Text("<"))], static (string a, string b, string op) => $"({a}{op}{b})")
            .LeftAssociative((op(Terms.Text("&&")), Binary("&&")));

        return expression;
    }

    // Only Terms operators, whose choices skip the white space
    private static Parser<string> Skipping(Func<Parser<string>, Parser<string>> op) => Atom.Operators()
        .Unary((op(Terms.Char('-').Then(static _ => "-")), Prefix("-")), (op(Terms.Char('~').Then(static _ => "~")), Prefix("~")))
        .LeftAssociative((op(Terms.Char('*').Then(static _ => "*")), Binary("*")))
        .LeftAssociative((op(Terms.Char('+').Then(static _ => "+")), Binary("+")), (op(Terms.Char('-').Then(static _ => "-")), Binary("-")));

    // Only Literals operators, whose choices don't skip it
    private static Parser<string> Literal(Func<Parser<string>, Parser<string>> op) => Literals.Identifier().Then(static x => x.ToString()).Operators()
        .Unary((op(Literals.Char('-').Then(static _ => "-")), Prefix("-")))
        .LeftAssociative((op(Literals.Char('*').Then(static _ => "*")), Binary("*")))
        .LeftAssociative((op(Literals.Char('+').Then(static _ => "+")), Binary("+")));

    // A looser prefix level whose operators skip the white space, which isn't tried for the operands of the binary operators
    private static Parser<string> LooserPrefix(Func<Parser<string>, Parser<string>> op) => Literals.Identifier().Then(static x => x.ToString()).Operators()
        .Unary((op(Literals.Char('-').Then(static _ => "-")), Prefix("-")))
        .LeftAssociative((op(Literals.Char('*').Then(static _ => "*")), Binary("*")))
        .Unary((op(Terms.Text("typeof")), Prefix("typeof ")));

    private static Parser<string> Same(Parser<string> parser) => parser;

    private static Parser<string> Hidden(Parser<string> parser) => new NoHints<string>(parser);

    private static readonly string[] Inputs =
    [
        "a", "a + b", "a - b", "a -b", "a- b", "-a", "- a", "--a", "- -a", "typeof a", "typeofa", "typeof -a", "-typeof a",
        "!a", "! !a", "!-a", "a.b.c", "a .b", "a. b", "-a.b", "a ** b ** c", "a * b + c", "a*b", "a * -b", "a < b && c",
        "a <", "a +", "a + ", "a &&", "a & b", "a , b", "a)", "(a + b) * c", "(a", "a + (b", "", " ", "  a  ", "a  ,",
        "a  +  ", "-", "- ", "!", "typeof", "a ++ b", "a +* b", "a ? b", "1 + 2 * 3", "-1", "a\n+\nb", "a\t*\t-b",
        "a + b c", "é", "a é", "a + é",
    ];

    public static IEnumerable<object[]> InputData()
    {
        foreach (var input in Inputs)
        {
            yield return [input];
        }
    }

    [Theory]
    [MemberData(nameof(InputData))]
    public void ShouldParseLikeOperatorsWithoutHints(string input)
    {
        Assert.Equal(Run(Mixed(Hidden), input), Run(Mixed(Same), input));
        Assert.Equal(Run(Skipping(Hidden), input), Run(Skipping(Same), input));
        Assert.Equal(Run(Literal(Hidden), input), Run(Literal(Same), input));
        Assert.Equal(Run(LooserPrefix(Hidden), input), Run(LooserPrefix(Same), input));
    }

    [Fact]
    public void ShouldParseRandomExpressionsLikeOperatorsWithoutHints()
    {
        var grammars = new (Parser<string> Expected, Parser<string> Actual)[]
        {
            (Mixed(Hidden), Mixed(Same)),
            (Skipping(Hidden), Skipping(Same)),
            (Literal(Hidden), Literal(Same)),
            (LooserPrefix(Hidden), LooserPrefix(Same)),
        };

        foreach (var input in RandomInputs(580, 3000))
        {
            foreach (var (expected, actual) in grammars)
            {
                Assert.Equal(Run(expected, input), Run(actual, input));
            }
        }
    }

    [Theory]
    [MemberData(nameof(InputData))]
    public void ShouldParseReadersLikeStrings(string input)
    {
        var parser = Mixed(Same);
        var expected = Run(parser, input);

        foreach (var chunk in new[] { 1, 3, 4096 })
        {
            var success = parser.TryParse(new StreamingTests.ChunkedReader(input, () => chunk, yield: false), out var value);
            Assert.Equal(expected.Success, success);

            if (success)
            {
                Assert.Equal(expected.Value, value);
            }
        }
    }

    [Fact]
    public void ShouldUseOperatorsWithoutHints()
    {
        // The prefix operators have no hints, and no table: they are tried before every operand
        var parser = Atom.Operators()
            .Unary((new NoHints<char>(Terms.Char('-')), Prefix("-")), (Terms.Char('~'), Prefix("~")))
            .LeftAssociative((new NoHints<char>(Terms.Char('+')), Binary("+")), (Terms.Char('*'), Binary("*")));

        Assert.Equal("((-a)+(~b))", parser.Parse("-a + ~b"));
        Assert.Equal("(a*b)", parser.Parse("a * b"));
        Assert.Equal("a", parser.Parse("a , b"));
    }

    [Theory]
    [InlineData("a , b", 1, 2)]
    [InlineData("a,b", 1, 1)]
    [InlineData("a  )", 1, 3)]
    public void ShouldRestoreTheCursorWhenNoOperatorFollows(string input, int end, int furthest)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.True(Skipping(Same).Parse(context, ref result));
        Assert.Equal("a", result.Value);
        Assert.Equal(end, context.Scanner.Cursor.Offset);

        // The choice of the operators would have failed after the white space
        Assert.Equal(furthest, context.Scanner.Cursor.FurthestOffset);
    }

    [Theory]
    [InlineData("  )", 2)]
    [InlineData(")", 0)]
    public void ShouldRestoreTheCursorWhenNoOperandFollows(string input, int furthest)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.False(Skipping(Same).Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(furthest, context.Scanner.Cursor.FurthestOffset);
    }

    // An operand which doesn't skip the white space, after prefix operators which skip it and ones which don't
    private static readonly Parser<string> LiteralOperand = Literals.Identifier().Then(static x => x.ToString()).Operators()
        .Unary((Literals.Char('-').Then(static _ => "-"), Prefix("-")), (Literals.Text("é"), Prefix("é")))
        .Unary((Terms.Text("typeof"), Prefix("typeof ")))
        .LeftAssociative((Terms.Char('+'), Binary("+")));

    [Theory]
    // A char of an operator which doesn't skip the white space, at the cursor
    [InlineData("-a", true, "(-a)", 2, 2)]
    [InlineData("éa", true, "(éa)", 2, 2)]
    // A char of an operator which skips it, after it
    [InlineData("typeofa", true, "(typeof a)", 7, 7)]
    [InlineData("  typeofa", true, "(typeof a)", 9, 9)]
    // None: the operators which skip the white space would fail where it ends, the operand where it starts
    [InlineData(" -a", false, null, 0, 1)]
    [InlineData("   a", false, null, 0, 3)]
    [InlineData("è", false, null, 0, 0)]
    [InlineData("  ", false, null, 0, 2)]
    [InlineData("", false, null, 0, 0)]
    [InlineData("a  ", true, "a", 1, 3)]
    [InlineData("a  -", true, "a", 1, 3)]
    public void ShouldTestTheCharAtTheCursorOrAfterTheWhiteSpace(string input, bool success, string? value, int offset, int furthest)
    {
        var expected = (success, value, offset, furthest);

        Assert.Equal(expected, Run(LiteralOperand, input));
    }

    [Theory]
    // The operand of - or * is read with the tighter levels: the white space isn't skipped for typeof
    [InlineData("-  ", 1)]
    [InlineData("a*  b", 2)]
    [InlineData("-  a", 1)]
    // typeof is tried before the first operand
    [InlineData("  a", 2)]
    public void ShouldOnlyTestTheLevelsWhichAreTried(string input, int furthest)
    {
        var (success, _, offset, actual) = Run(LooserPrefix(Same), input);

        Assert.Equal(input.Length > 0 && input[0] == 'a' ? (true, 1) : (false, 0), (success, offset));
        Assert.Equal(furthest, actual);
    }

    [Fact]
    public void ShouldInvokeOperatorsWhichCanStartWithAnyChar()
    {
        // The table can't tell the chars of a pattern, or of a choice whose operators skip the white space differently
        var parser = Atom.Operators()
            .Unary((Literals.Pattern(static c => c == '-', maxSize: 1), static x => $"(-{x})"))
            .LeftAssociative((Terms.Char('+').Or(Literals.Char('*')), Binary("?")));

        Assert.Equal("((-a)?b)", parser.Parse("-a + b"));
        Assert.Equal("((-a)?b)", parser.Parse("-a* b"));
        Assert.Equal("a", parser.Parse("a , b"));
    }

    [Fact]
    public void ShouldLetEveryNonAsciiCharThroughWhenAnOperatorCanStartWithAny()
    {
        // The identifiers of code points can start with any non-ASCII char
        var parser = Atom.Operators()
            .LeftAssociative((Terms.Identifier(static c => c >= 0x80, static c => c >= 0x80, IdentifierOptions.None), Binary(" ")));

        Assert.Equal("(a b)", parser.Parse("a é b"));
        Assert.Equal("a", parser.Parse("a  +"));
    }

    private static (bool Success, string? Value, int Offset, int Furthest) Run(Parser<string> parser, string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();
        var success = parser.Parse(context, ref result);
        var cursor = context.Scanner.Cursor;

        return (success, success ? result.Value : null, cursor.Offset, cursor.FurthestOffset);
    }

    private static IEnumerable<string> RandomInputs(int seed, int count)
    {
        string[] tokens = ["a", "b", "1", "-", "+", "*", "/", "**", "!", "typeof", ".", "<", "&&", "(", ")", ",", " ", " ", "  ", "\n", "é"];
        var random = new Random(seed);
        var sb = new StringBuilder();

        for (var i = 0; i < count; i++)
        {
            sb.Clear();
            var length = random.Next(1, 12);

            for (var j = 0; j < length; j++)
            {
                sb.Append(tokens[random.Next(tokens.Length)]);
            }

            yield return sb.ToString();
        }
    }
}
