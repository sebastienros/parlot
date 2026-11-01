using System;
using System.Collections.Generic;
using System.Text;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The levels of an operator table whose factories receive the values of the operators, and the conditional levels. They
/// must parse the same values, with the same spans and the same cursor, as the folds and the chains of
/// <see cref="Parsers.LeftAssociative{T, TOperator}(Parser{T}, Parser{TOperator}[], Func{T, T, TOperator, T})"/> parsers
/// they replace.
/// </summary>
public class ValuedOperatorsTests
{
    private static readonly Parser<string> Atom = PrecedenceParserTests.Atom;

    private static Func<string, string, string> Binary(string op) => PrecedenceParserTests.Binary(op);

    private static Func<string, string> Prefix(string op) => PrecedenceParserTests.Prefix(op);

    private static string Valued(string left, string right, string op) => $"({left}{op}{right})";

    private static string Conditional(string condition, string whenTrue, string whenFalse) => $"({condition}?{whenTrue}:{whenFalse})";

    private static string Member(string x, TextSpan name) => $"{x}.{name}";

    private static string Index(string x, string index) => $"{x}[{index}]";

    private static string Run(Parser<string> parser, string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();
        var success = parser.Parse(context, ref result);

        return success
            ? $"{result.Value} [{result.Start}..{result.End}] @{context.Scanner.Cursor.Offset}"
            : $"failed @{context.Scanner.Cursor.Offset}";
    }

    private static Parser<string>[] Texts(params string[] texts) => Array.ConvertAll(texts, static text => (Parser<string>)Terms.Text(text));

    /// <summary>
    /// A table with member accesses and indexers, valued and plain binary levels, a conditional and a looser coalesce, and
    /// the same grammar written with a fold of the postfix operators and a chain of levels.
    /// </summary>
    private static Parser<string> Grammar(bool table, bool rightAssociativeConditional)
    {
        var expression = Deferred<string>();
        var primary = Atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        var member = Terms.Char('.').SkipAnd(Terms.Identifier());
        var index = Between(Terms.Char('['), expression, Terms.Char(']'));
        var question = Terms.Char('?');
        var colon = Terms.Char(':');

        if (table)
        {
            var levels = primary.Operators()
                .Postfix((member, static (x, name) => Member(x, name)), (index, static (x, i) => Index(x, i)))
                .Unary((Terms.Char('-'), Prefix("-")), (Terms.Char('!'), Prefix("!")))
                .RightAssociative(Texts("**"), Valued)
                .LeftAssociative(Texts("*", "/"), Valued)
                .LeftAssociative((Terms.Char('+'), Binary("+")), (Terms.Char('-'), Binary("-")))
                .LeftAssociative(Texts("<=", "<", "=="), Valued)
                .Ternary(question, expression, colon, Conditional, rightAssociativeConditional)
                .LeftAssociative((Terms.Text("??"), Binary("??")))
                .RightAssociative(Texts("=", "+="), Valued);

            expression.Parser = levels;
            return expression;
        }

        var postfix = OneOf(
            member.Then(static name => (Name: (TextSpan?)name, Index: (string)null)),
            index.Then(static i => (Name: (TextSpan?)null, Index: i)));

        // Without postfix operators the span is the primary's, and not the empty one of ZeroOrMany after it
        var folded = primary.And(OneOrMany(postfix)).Then(static x =>
        {
            var value = x.Item1;

            foreach (var (name, i) in x.Item2)
            {
                value = name is { } n ? Member(value, n) : Index(value, i);
            }

            return value;
        });

        var operand = folded.Or(primary);

        Parser<string>[] conditional = [question.SkipAnd(expression).AndSkip(colon)];

        var chain = operand
            .Unary((Terms.Char('-'), Prefix("-")), (Terms.Char('!'), Prefix("!")))
            .RightAssociative(Texts("**"), Valued)
            .LeftAssociative(Texts("*", "/"), Valued)
            .LeftAssociative((Terms.Char('+'), Binary("+")), (Terms.Char('-'), Binary("-")))
            .LeftAssociative(Texts("<=", "<", "=="), Valued);

        chain = rightAssociativeConditional
            ? chain.RightAssociative(conditional, static (c, whenFalse, whenTrue) => Conditional(c, whenTrue, whenFalse))
            : chain.LeftAssociative(conditional, static (c, whenFalse, whenTrue) => Conditional(c, whenTrue, whenFalse));

        expression.Parser = chain
            .LeftAssociative((Terms.Text("??"), Binary("??")))
            .RightAssociative(Texts("=", "+="), Valued);

        return expression;
    }

    private static readonly string[] Tokens =
    [
        "a", "b", "1", "(", ")", "[", "]", ".", ".b", "-", "!", "**", "*", "/", "+", "<", "<=", "==", "=", "+=", "?", ":", "??",
    ];

    private static IEnumerable<string> RandomExpressions(int seed, int count)
    {
        var random = new Random(seed);
        string[] operands = ["a", "b", "1", "22"];
        string[] prefixes = ["-", "!"];
        string[] binaries = ["**", "*", "/", "+", "-", "<", "<=", "==", "??", "=", "+="];

        string Space() => random.Next(4) switch { 0 => " ", 1 => "  ", _ => "" };

        string Expression(int depth)
        {
            var sb = new StringBuilder();
            var terms = random.Next(1, 6);

            for (var i = 0; i < terms; i++)
            {
                if (i > 0)
                {
                    if (depth < 3 && random.Next(6) == 0)
                    {
                        sb.Append(Space()).Append('?').Append(Space()).Append(Expression(depth + 1)).Append(Space()).Append(':').Append(Space());
                    }
                    else
                    {
                        sb.Append(Space()).Append(binaries[random.Next(binaries.Length)]).Append(Space());
                    }
                }

                while (random.Next(4) == 0)
                {
                    sb.Append(prefixes[random.Next(prefixes.Length)]).Append(Space());
                }

                if (depth < 3 && random.Next(5) == 0)
                {
                    sb.Append('(').Append(Expression(depth + 1)).Append(')');
                }
                else
                {
                    sb.Append(operands[random.Next(operands.Length)]);
                }

                while (random.Next(3) == 0)
                {
                    if (depth < 3 && random.Next(2) == 0)
                    {
                        sb.Append(Space()).Append('[').Append(Expression(depth + 1)).Append(']');
                    }
                    else
                    {
                        sb.Append(Space()).Append('.').Append(Space()).Append(operands[random.Next(2)]);
                    }
                }
            }

            return sb.ToString();
        }

        for (var i = 0; i < count; i++)
        {
            switch (random.Next(4))
            {
                case 0:
                    var sb = new StringBuilder();
                    var length = random.Next(0, 10);

                    for (var j = 0; j < length; j++)
                    {
                        sb.Append(Tokens[random.Next(Tokens.Length)]).Append(Space());
                    }

                    yield return sb.ToString();
                    break;

                case 1:
                    // An expression with a token inserted or removed
                    var expression = Expression(0);
                    var position = random.Next(expression.Length + 1);

                    yield return random.Next(2) == 0 || expression.Length == 0
                        ? expression.Insert(position, Tokens[random.Next(Tokens.Length)])
                        : expression.Remove(Math.Min(position, expression.Length - 1), 1);
                    break;

                default:
                    yield return Expression(0) + Space();
                    break;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TableShouldParseLikeTheFoldAndTheChain(bool rightAssociativeConditional)
    {
        var chain = Grammar(table: false, rightAssociativeConditional);
        var table = Grammar(table: true, rightAssociativeConditional);

        var inputs = new List<string>
        {
            "a.b", "a.b.c", "a[b]", "a[b][c].d", "a.", "a[", "a[b", "a.b[", "-a.b", "a.b * c[d]", "a ? b : c", "a ? b : c ? d : e",
            "a ? b ? c : d : e", "a ?? b", "a ? b ?? c : d", "a ? b : c ?? d", "a ?? b ? c : d", "a ? b", "a ? b :", "a ?",
            "a ?? ", "a = b ? c : d", "a = b = c", "a += b = c ? d : e", "a ** b ** c", "a ** b ** ", "a < b <= c == d", "a[b ? c : d]",
            "a < b ? c + d : e * f", ".a", "?", "[a]", "a[]", "a..b", "a . b", "a [ b ] . c",
        };
        inputs.AddRange(RandomExpressions(rightAssociativeConditional ? 11 : 5, 4000));

        var operations = 0;

        foreach (var input in inputs)
        {
            var expected = Run(chain, input);

            if (expected.Length > 0 && expected[0] == '(')
            {
                operations++;
            }

            Assert.True(expected == Run(table, input), $"'{input}': chain {expected}, table {Run(table, input)}");
        }

        Assert.True(operations > inputs.Count / 2, $"{operations} of {inputs.Count}");
    }

    [Theory]
    [InlineData("a.b", "a.b")]
    [InlineData("a.b.c", "a.b.c")]
    [InlineData("a[b][c].d", "a[b][c].d")]
    [InlineData("a[b + c]", "a[(b+c)]")]
    [InlineData("-a.b", "(-a.b)")]
    [InlineData("a.b * c[d]", "(a.b*c[d])")]
    [InlineData("a ? b : c", "(a?b:c)")]
    [InlineData("a ? b ? c : d : e", "(a?(b?c:d):e)")]
    [InlineData("a < b ? c + d : e * f", "((a<b)?(c+d):(e*f))")]
    [InlineData("a ?? b", "(a??b)")]
    [InlineData("a ? b ?? c : d", "(a?(b??c):d)")]
    [InlineData("a ? b : c ?? d", "((a?b:c)??d)")]
    [InlineData("a ?? b ? c : d", "(a??(b?c:d))")]
    [InlineData("a = b ? c : d", "(a=(b?c:d))")]
    [InlineData("a = b += c", "(a=(b+=c))")]
    [InlineData("a ** b ** c", "(a**(b**c))")]
    [InlineData("a * b / c", "((a*b)/c)")]
    public void ShouldApplyValuedOperators(string input, string expected)
    {
        Assert.Equal(expected, Grammar(table: true, rightAssociativeConditional: false).Parse(input));
    }

    [Theory]
    [InlineData(false, "((a?b:c)?d:e)")]
    [InlineData(true, "(a?b:(c?d:e))")]
    public void ShouldSelectTheAssociativityOfConditionals(bool rightAssociative, string expected)
    {
        Assert.Equal(expected, Grammar(table: true, rightAssociative).Parse("a ? b : c ? d : e"));
    }

    [Theory]
    [InlineData("a.", "a", 1)]
    [InlineData("a[", "a", 1)]
    [InlineData("a[b", "a", 1)]
    [InlineData("a.b[", "a.b", 3)]
    [InlineData("a ? b", "a", 1)]
    [InlineData("a ? b :", "a", 1)]
    [InlineData("a ? b : c ?", "(a?b:c)", 9)]
    [InlineData("a * ", "a", 1)]
    [InlineData("a = b = ", "(a=b)", 5)]
    [InlineData("a ** b ** ", "(a**b)", 6)]
    public void ShouldNotReadAnOperatorWithoutOperand(string input, string expected, int offset)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.True(Grammar(table: true, rightAssociativeConditional: true).Parse(context, ref result));
        Assert.Equal(expected, result.Value);
        Assert.Equal(offset, result.End);
        Assert.Equal(offset, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("")]
    [InlineData(".a")]
    [InlineData("[a]")]
    [InlineData("?")]
    [InlineData("- .a")]
    public void ShouldRestoreCursorWhenFailing(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.False(Grammar(table: true, rightAssociativeConditional: true).Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldReportTheSpanOfTheExpression()
    {
        var context = new ParseContext(new Scanner(" a.b [c] ? d : e "));
        var result = new ParseResult<string>();

        Assert.True(Grammar(table: true, rightAssociativeConditional: true).Parse(context, ref result));
        Assert.Equal("(a.b[c]?d:e)", result.Value);
        Assert.Equal(1, result.Start);
        Assert.Equal(16, result.End);
    }

    [Fact]
    public void ShouldStopWhenAValuedOperatorReadsNothing()
    {
        var operand = Literals.Text("x").Optional().Then(static x => x.HasValue ? "x" : "");
        var parser = operand.Operators()
            .Postfix((Literals.Text("!").Optional(), static (x, op) => op.HasValue ? $"({x}!)" : $"({x})"))
            .LeftAssociative([Literals.Text("+").Optional()], static (a, b, _) => $"({a}+{b})")
            .RightAssociative([Literals.Text("^").Optional()], static (a, b, _) => $"({a}^{b})");

        Assert.Equal("((x!)+(x!))", parser.Parse("x!+x!"));
        Assert.Equal("(x^(x^x))", parser.Parse("x^x^x"));
        Assert.Equal("", parser.Parse(""));
        Assert.Equal("x", parser.Parse("x?"));
    }

    [Fact]
    public void ShouldTryTheOperatorsOfAPostfixLevelInOrder()
    {
        // The first operator which matches applies, even when the second one would read more
        var parser = Atom.Operators().Postfix(
            (Terms.Char('.'), static (x, _) => $"({x}.)"),
            (Terms.Char('.').And(Terms.Identifier()), static (x, m) => $"{x}.{m.Item2}"));

        Assert.Equal("((a.).)", parser.Parse("a.."));
        Assert.Equal("(a.)", parser.Parse("a.b"));
    }

    [Fact]
    public void ShouldApplyPostfixOperatorsOfTheSameTypeWithTheirFactories()
    {
        var parser = Atom.Operators()
            .Postfix((Terms.Char('!'), static (x, op) => $"({x}{op})"), (Terms.Char('?'), static (x, op) => $"[{x}{op}]"))
            .LeftAssociative((Terms.Char('*'), Binary("*")));

        Assert.Equal("([(a!)?]!)", parser.Parse("a!?!"));
        Assert.Equal("((a!)*[b?])", parser.Parse("a! * b?"));
    }

    [Fact]
    public void ValuedPostfixLevelsShouldApplyAfterTheTighterLevels()
    {
        // The member access binds less tightly than the prefix operator
        var parser = Atom.Operators()
            .Unary((Terms.Char('-'), Prefix("-")))
            .Postfix((Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => Member(x, name)))
            .LeftAssociative((Terms.Char('*'), Binary("*")));

        Assert.Equal("(-a).b", parser.Parse("-a.b"));
        Assert.Equal("(a.b*c.d)", parser.Parse("a.b * c.d"));
    }

    [Fact]
    public void ShouldParseLongChains()
    {
        var parser = Terms.Integer().Operators()
            .Postfix((Terms.Char('.').SkipAnd(Terms.Integer()), static (long x, long y) => x + y))
            .RightAssociative([Terms.Char('^')], static (long a, long b, char _) => a - b);

        Assert.Equal(100_001, parser.Parse("1" + string.Concat(System.Linq.Enumerable.Repeat(".1", 100_000))));
        Assert.Equal(1, parser.Parse("1" + string.Concat(System.Linq.Enumerable.Repeat("^1", 100_000))));
    }

    [Fact]
    public void ShouldValidateItsArguments()
    {
        Assert.Throws<ArgumentNullException>(() => Atom.Operators().Postfix((ValueTuple<Parser<char>, Func<string, char, string>>[])null!));
        Assert.Throws<ArgumentException>(() => Atom.Operators().Postfix(((Parser<char>)null!, static (string x, char _) => x)));
        Assert.Throws<ArgumentException>(() => Atom.Operators().Postfix((Terms.Char('!'), (Func<string, char, string>)null!)));
        Assert.Throws<ArgumentException>(() => Atom.Operators().LeftAssociative(Array.Empty<Parser<char>>(), static (a, b, _) => a));
        Assert.Throws<ArgumentNullException>(() => Atom.Operators().LeftAssociative([Terms.Char('+')], (Func<string, string, char, string>)null!));
        Assert.Throws<ArgumentException>(() => Atom.Operators().RightAssociative([(Parser<char>)null!], static (a, b, _) => a));
        Assert.Throws<ArgumentNullException>(() => Atom.Operators().Ternary(Terms.Char('?'), (Parser<string>)null!, Terms.Char(':'), Conditional));
        Assert.Throws<ArgumentNullException>(() => Atom.Operators().Ternary(Terms.Char('?'), Atom, Terms.Char(':'), (Func<string, string, string, string>)null!));
    }

    [Fact]
    public void ShouldDeclareTheCharsOfItsOperandAndPrefixOperators()
    {
        var seekable = (Rewriting.ISeekable)Atom.Operators()
            .Postfix((Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => Member(x, name)))
            .Unary((Terms.Char('~'), Prefix("~")))
            .Ternary(Terms.Char('?'), Atom, Terms.Char(':'), Conditional);

        Assert.True(seekable.CanSeek);
        Assert.Contains('~', seekable.ExpectedChars);
        Assert.DoesNotContain('.', seekable.ExpectedChars);
        Assert.DoesNotContain('?', seekable.ExpectedChars);
    }
}
