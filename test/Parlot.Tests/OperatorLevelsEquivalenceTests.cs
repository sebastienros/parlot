using System;
using System.Collections.Generic;
using System.Text;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The levels of a chain of LeftAssociative, RightAssociative and Unary parsers are parsed by a single precedence parser.
/// It must parse the same values, with the same spans and the same cursor, as the chain.
/// </summary>
public class OperatorLevelsEquivalenceTests
{
    private static readonly Parser<string> Atom = PrecedenceParserTests.Atom;

    private static Func<string, string, string> Binary(string op) => PrecedenceParserTests.Binary(op);

    private static Func<string, string> Prefix(string op) => PrecedenceParserTests.Prefix(op);

    private enum Shape
    {
        // The chain of levels, each one reading its operands with the next one
        Chain,

        // The same chain, with each level separated from its operand by a parser
        Nested,

        // The table built with Operators()
        Table,
    }

    private static Parser<string> Level(Parser<string> parser, Shape shape) => shape == Shape.Nested ? parser.Then(static x => x) : parser;

    /// <summary>
    /// A C#-like table where the operators of tighter levels are often the start of operators of looser ones, so the
    /// latter apply after the former were not followed by an operand.
    /// </summary>
    private static Parser<string> Grammar(Shape shape, bool prefixAboveExponent)
    {
        var expression = Deferred<string>();
        var primary = Atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        (Parser<string> op, Func<string, string> factory)[] prefix =
            [(Terms.Text("-"), Prefix("-")), (Terms.Text("!"), Prefix("!")), (Terms.Text("~"), Prefix("~"))];
        (Parser<string> op, Func<string, string, string> factory)[] exponent = [(Terms.Text("**"), Binary("**"))];

        (Parser<string> op, Func<string, string, string> factory)[] Ops(params string[] ops)
        {
            var result = new (Parser<string>, Func<string, string, string>)[ops.Length];

            for (var i = 0; i < ops.Length; i++)
            {
                result[i] = (Terms.Text(ops[i]), Binary(ops[i]));
            }

            return result;
        }

        (Parser<string> op, Func<string, string, string> factory)[][] left =
        [
            Ops("*", "/", "%"),
            Ops("+", "-"),
            Ops("<<", ">>"),
            Ops("<", ">", "<=", ">="),
            Ops("==", "!=", "="),
            Ops("&"),
            Ops("^"),
            Ops("|"),
            Ops("&&"),
            Ops("||"),
        ];

        // A right associative level whose operators start with the ones of the tighter levels
        var assignment = Ops("<=", "&=", "|=", "=>");

        if (shape == Shape.Table)
        {
            var table = primary.Operators();
            table = prefixAboveExponent ? table.RightAssociative(exponent).Unary(prefix) : table.Unary(prefix).RightAssociative(exponent);

            foreach (var level in left)
            {
                table = table.LeftAssociative(level);
            }

            expression.Parser = table.RightAssociative(assignment);
            return expression;
        }

        Parser<string> chain = primary;
        chain = prefixAboveExponent
            ? Level(Level(chain.RightAssociative(exponent), shape).Unary(prefix), shape)
            : Level(Level(chain.Unary(prefix), shape).RightAssociative(exponent), shape);

        foreach (var level in left)
        {
            chain = Level(chain.LeftAssociative(level), shape);
        }

        expression.Parser = chain.RightAssociative(assignment);
        return expression;
    }

    private static string Run(Parser<string> parser, string input, bool instrumented = false)
    {
        var context = new ParseContext(new Scanner(input));

        if (instrumented)
        {
            context.OnEnterParser = static (_, _) => { };
        }

        var result = new ParseResult<string>();
        var success = parser.Parse(context, ref result);

        return success
            ? $"{result.Value} [{result.Start}..{result.End}] @{context.Scanner.Cursor.Offset}"
            : $"failed @{context.Scanner.Cursor.Offset}";
    }

    private static readonly string[] Tokens =
    [
        "a", "b", "1", "22", "(", ")", "-", "!", "~", "**", "*", "/", "%", "+", "<<", ">>", "<", ">", "<=", ">=",
        "==", "!=", "=", "&", "^", "|", "&&", "||", "&=", "|=", "=>", "?", ".",
    ];

    private static IEnumerable<string> RandomExpressions(int seed, int count)
    {
        var random = new Random(seed);
        string[] operands = ["a", "b", "1", "22"];
        string[] prefixes = ["-", "!", "~"];
        string[] binaries = ["**", "*", "/", "%", "+", "-", "<<", ">>", "<", ">", "<=", ">=", "==", "!=", "=", "&", "^", "|", "&&", "||", "&=", "|=", "=>"];

        string Space() => random.Next(4) switch { 0 => " ", 1 => "  ", _ => "" };

        string Expression(int depth)
        {
            var sb = new StringBuilder();
            var terms = random.Next(1, 6);

            for (var i = 0; i < terms; i++)
            {
                if (i > 0)
                {
                    sb.Append(Space()).Append(binaries[random.Next(binaries.Length)]).Append(Space());
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
            }

            return sb.ToString();
        }

        for (var i = 0; i < count; i++)
        {
            switch (random.Next(4))
            {
                case 0:
                    // Any sequence of tokens
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
    public void OperatorTableShouldParseLikeTheChainOfLevels(bool prefixAboveExponent)
    {
        var nested = Grammar(Shape.Nested, prefixAboveExponent);
        var chain = Grammar(Shape.Chain, prefixAboveExponent);
        var table = Grammar(Shape.Table, prefixAboveExponent);

        var inputs = new List<string>
        {
            "a <= b", "a < = b", "a && b", "a & & b", "a &= b", "a & = b", "a => b", "a = > b", "a == b", "a = = b",
            "a <= b <= c", "a ** b ** -c", "-a ** b", "a ** -b", "--a", "-", "a -", "a - - -", "a ** ** b", "(a <= b",
            "a | b || c |= d", "a !b", "!a != !b", "a << b <= c < d", "a ? b", ")",
        };
        inputs.AddRange(RandomExpressions(prefixAboveExponent ? 7 : 3, 4000));

        var operations = 0;

        foreach (var input in inputs)
        {
            var expected = Run(nested, input);

            // string.StartsWith(char) doesn't exist on .NET Framework
            if (expected.Length > 0 && expected[0] == '(')
            {
                operations++;
            }

            Assert.True(expected == Run(chain, input), $"'{input}': nested {expected}, chain {Run(chain, input)}");
            Assert.True(expected == Run(table, input), $"'{input}': nested {expected}, table {Run(table, input)}");

            // With hooks or a recursion limit
            Assert.True(expected == Run(chain, input, instrumented: true), $"'{input}': nested {expected}, instrumented {Run(chain, input, instrumented: true)}");
        }

        // Most inputs apply some operators
        Assert.True(operations > inputs.Count / 2, $"{operations} of {inputs.Count}");
    }

    [Fact]
    public void InstrumentedChainShouldEnterEveryLevel()
    {
        var unary = Atom.Unary((Terms.Char('-'), Prefix("-"))).Named("unary");
        var multiplicative = unary.LeftAssociative((Terms.Char('*'), Binary("*"))).Named("multiplicative");
        var additive = multiplicative.LeftAssociative((Terms.Char('+'), Binary("+"))).Named("additive");

        var entered = new List<string>();
        var context = new ParseContext(new Scanner("-a * b + c"))
        {
            OnEnterParser = (parser, _) =>
            {
                if (parser is Parser<string> { Name: { } name })
                {
                    entered.Add(name);
                }
            },
        };
        var result = new ParseResult<string>();

        Assert.True(additive.Parse(context, ref result));
        Assert.Equal("(((-a)*b)+c)", result.Value);
        // Unary reads its operators in a loop, it is entered once for "-a"
        Assert.Equal(new[] { "additive", "multiplicative", "unary", "unary", "multiplicative", "unary" }, entered);
    }

    [Fact]
    public void ChainShouldEnforceMaxRecursionDepthLikeUnary()
    {
        var parser = Literals.Char('a').Then(static _ => "a")
            .Unary((Literals.Char('-'), Prefix("-")))
            .LeftAssociative((Literals.Char('*'), Binary("*")));

        // Unary counts its first entry, before an operator
        Assert.True(parser.TryParse(new ParseContext(new Scanner("-a"), maxRecursionDepth: 2), out var value, out _));
        Assert.Equal("(-a)", value);

        Assert.False(parser.TryParse(new ParseContext(new Scanner("--a"), maxRecursionDepth: 2), out _, out var error));
        Assert.Equal("The maximum parser recursion depth of 2 was exceeded.", error!.Message);
    }

    [Fact]
    public void InnerLevelsShouldStillParseOnTheirOwn()
    {
        var multiplicative = Atom.Unary((Terms.Char('-'), Prefix("-"))).LeftAssociative((Terms.Char('*'), Binary("*")));
        var additive = multiplicative.LeftAssociative((Terms.Char('+'), Binary("+")));

        Assert.Equal("(((-a)*b)+c)", additive.Parse("-a*b+c"));
        Assert.Equal("((-a)*b)", multiplicative.Parse("-a*b+c"));
    }
}
