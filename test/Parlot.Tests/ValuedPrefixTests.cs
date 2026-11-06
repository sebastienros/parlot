#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

/// <summary>
/// The prefix levels of an operator table whose factories receive the values of the operators, and the factories of the
/// levels which receive the <see cref="ParseContext"/>. They must parse the same values, with the same spans and the same
/// cursor, as the levels without values.
/// </summary>
public class ValuedPrefixTests
{
    private static readonly Parser<string> Atom = PrecedenceParserTests.Atom;

    private static Func<string, string, string> Binary(string op) => PrecedenceParserTests.Binary(op);

    private static Func<string, string> Prefix(string op) => PrecedenceParserTests.Prefix(op);

    private static string Suffix(string x, string op) => $"({x}{op})";

    private static string Run(Parser<string> parser, string input, ParseContext? context = null)
    {
        context ??= new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();
        var success = parser.Parse(context, ref result);

        return success
            ? $"{result.Value} [{result.Start}..{result.End}] @{context.Scanner.Cursor.Offset}"
            : $"failed @{context.Scanner.Cursor.Offset}";
    }

    private enum Shape
    {
        // Every level without values
        Plain,

        // The tight prefix level with the values of its operators, the loose one without
        Valued,

        // Both prefix levels with values, of different types, and the binary and postfix levels with the context
        AllValued,

        // The prefix levels with the context and the values of their operators
        Context,
    }

    /// <summary>
    /// Two prefix levels around a binary level, with a postfix level tighter than both, and binary levels looser than both.
    /// </summary>
    private static Parser<string> Grammar(Shape shape)
    {
        var expression = Deferred<string>();
        var primary = Atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        var table = primary.Operators();

        table = shape == Shape.Plain
            ? table.Postfix((Terms.Text("++"), static x => Suffix(x, "++")))
            : table.Postfix((Terms.Text("++"), static (ParseContext context, string x, string op) => Suffix(x, op)));

        table = shape switch
        {
            Shape.Plain => table.Unary((Terms.Char('-'), Prefix("-")), (Terms.Char('!'), Prefix("!"))),
            Shape.Context => table.Unary(
                (Terms.Char('-'), static (ParseContext context, string x, char op) => $"({op}{x})"),
                (Terms.Char('!'), static (ParseContext context, string x, char op) => $"({op}{x})")),

            // A single factory for the operators of the level
            _ => table.Unary((Terms.Char('-'), static (string x, char op) => $"({op}{x})"), (Terms.Char('!'), static (string x, char op) => $"({op}{x})")),
        };

        table = table.RightAssociative((Terms.Text("**"), Binary("**")));

        table = shape switch
        {
            Shape.Plain or Shape.Valued => table.Unary((Terms.Text("typeof"), Prefix("typeof ")), (Terms.Text("~"), Prefix("~"))),
            Shape.Context => table.Unary(
                (Terms.Text("typeof").Then(static (_, start, _, _) => start), static (ParseContext context, string x, int _) => $"(typeof {x})"),
                (Terms.Char('~').Then(static (_, start, _, _) => start), static (ParseContext context, string x, int _) => $"(~{x})")),

            // The positions of the operators, which are not in the result
            _ => table.Unary(
                (Terms.Text("typeof").Then(static (_, start, _, _) => start), static (string x, int _) => $"(typeof {x})"),
                (Terms.Char('~').Then(static (_, start, _, _) => start), static (string x, int _) => $"(~{x})")),
        };

        table = shape == Shape.AllValued
            ? table.LeftAssociative((Terms.Char('*'), static (ParseContext context, string a, string b) => $"({a}*{b})"))
            : table.LeftAssociative((Terms.Char('*'), Binary("*")));

        expression.Parser = table.LeftAssociative((Terms.Char('+'), Binary("+")), (Terms.Char('-'), Binary("-")));
        return expression;
    }

    [Theory]
    [InlineData("a")]
    [InlineData("-a")]
    [InlineData("- - !a")]
    [InlineData("- - !a ** b")]
    [InlineData("typeof -a ** b")]
    [InlineData("-typeof a")]
    [InlineData("~ - ~a * b")]
    [InlineData("-a++ ** -b++ * ~c + d")]
    [InlineData("a - -b")]
    [InlineData("(-a) ** typeof (b)")]
    [InlineData("-")]
    [InlineData("- -")]
    [InlineData("typeof")]
    [InlineData("a * -")]
    [InlineData("a * typeof ~")]
    [InlineData("-(- a")]
    public void ShouldParseLikeThePlainPrefixLevels(string input)
    {
        var expected = Run(Grammar(Shape.Plain), input);

        Assert.Equal(expected, Run(Grammar(Shape.Valued), input));
        Assert.Equal(expected, Run(Grammar(Shape.AllValued), input));
        Assert.Equal(expected, Run(Grammar(Shape.Context), input));
    }

    [Fact]
    public void ShouldParseRandomExpressionsLikeThePlainPrefixLevels()
    {
        Parser<string>[] parsers = [Grammar(Shape.Plain), Grammar(Shape.Valued), Grammar(Shape.AllValued), Grammar(Shape.Context)];

        foreach (var input in RandomExpressions(516, 2000))
        {
            var expected = Run(parsers[0], input);

            for (var i = 1; i < parsers.Length; i++)
            {
                Assert.True(expected == Run(parsers[i], input), $"{input}: {expected} != {Run(parsers[i], input)}");
            }
        }
    }

    private static IEnumerable<string> RandomExpressions(int seed, int count)
    {
        var random = new Random(seed);
        string[] tokens = ["a", "b", "1", "(", ")", "-", "!", "~", "typeof ", "**", "*", "+", "++"];

        for (var i = 0; i < count; i++)
        {
            var sb = new StringBuilder();
            var length = random.Next(1, 12);

            for (var j = 0; j < length; j++)
            {
                sb.Append(tokens[random.Next(tokens.Length)]);

                if (random.Next(3) == 0)
                {
                    sb.Append(' ');
                }
            }

            yield return sb.ToString();
        }
    }

    // The value of the prefix operators is their position, and the result is a node which starts there
    private static Parser<string> Positions() => Atom.Operators()
        .Postfix((Terms.Text("++"), static (string x, string op) => Suffix(x, op)))
        .Unary((Terms.Char('!'), Prefix("!")))
        .Unary(
            (Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"),
            (Terms.Text("typeof").Then(static (_, start, _, _) => start), static (string x, int start) => $"typeof@{start}({x})"))
        .LeftAssociative((Terms.Char('*'), Binary("*")));

    [Theory]
    [InlineData("- - !x", "-@0(-@2((!x))) [0..6] @6")]
    [InlineData("-typeof -x", "-@0(typeof@1(-@8(x))) [0..10] @10")]
    [InlineData("a * - - b++", "(a*-@4(-@6((b++)))) [0..11] @11")]
    [InlineData(" - !x", "-@1((!x)) [1..5] @5")]
    [InlineData("-x * y", "(-@0(x)*y) [0..6] @6")]
    public void ShouldPassTheValuesOfTheOperators(string input, string expected)
    {
        Assert.Equal(expected, Run(Positions(), input));
    }

    // Two valued levels whose values share a stack, and a plain level between them
    private static Parser<string> SharedStack() => Atom.Operators()
        .Unary((Terms.Char('!').Then(static (_, start, _, _) => start), static (string x, int start) => $"!@{start}({x})"))
        .Unary((Terms.Char('~'), Prefix("~")))
        .Unary((Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"))
        .LeftAssociative((Terms.Char('*'), Binary("*")));

    [Theory]
    [InlineData("-!x", "-@0(!@1(x)) [0..3] @3")]
    [InlineData("- - ! !x", "-@0(-@2(!@4(!@6(x)))) [0..8] @8")]
    [InlineData("-~!x", "-@0((~!@2(x))) [0..4] @4")]
    [InlineData("~!x * -!y", "((~!@1(x))*-@6(!@7(y))) [0..9] @9")]
    [InlineData("-!", "failed @0")]
    [InlineData("-~", "failed @0")]
    public void ShouldApplyTheValuesOfLevelsSharingAType(string input, string expected)
    {
        Assert.Equal(expected, Run(SharedStack(), input));
        Assert.Equal(0, ScratchCount<int>());

        // Each operator counts as a level of recursion, and the chain reads the operators that follow the first one itself
        var context = new ParseContext(new Scanner(input), maxRecursionDepth: 10);
        Assert.Equal(expected, Run(SharedStack(), input, context));
        Assert.Equal(0, ScratchCount<int>());
    }

    [Theory]
    [InlineData("-")]
    [InlineData("- -")]
    [InlineData("!- typeof")]
    [InlineData("- !")]
    [InlineData("-+")]
    public void ShouldRestoreTheCursorWhenAnOperatorHasNoOperand(string input)
    {
        Assert.Equal("failed @0", Run(Positions(), input));

        // The values of the operators were discarded, the next parse sees its own
        Assert.Equal("-@0(x) [0..2] @2", Run(Positions(), "-x"));
    }

    [Fact]
    public void ShouldStopAfterABinaryOperatorWithoutOperand()
    {
        Assert.Equal("a [0..1] @1", Run(Positions(), "a * -"));
        Assert.Equal("(a*-@4(b)) [0..6] @6", Run(Positions(), "a * -b * -"));
    }

    [Fact]
    public void ShouldTreatANonConsumingOperatorAsNoOperator()
    {
        // As in the plain levels, the first operator matches without reading anything, the second one applies
        var parser = Terms.Integer().Operators().Unary(
            (ZeroOrMany(Literals.Char('-')).Then(static _ => 1L), static (long value, long op) => -value * op),
            (Literals.Char('+').Then(static _ => 100L), static (long value, long op) => value + op));

        Assert.Equal(105, parser.Parse("+5"));
        Assert.Equal(-5, parser.Parse("-5"));
        Assert.Equal(5, parser.Parse("5"));
    }

    [Fact]
    public void ShouldParsePrefixChainsLongerThanTheStack()
    {
        var parser = Terms.Integer().Operators()
            .Unary((Literals.Char('-').Then(static (_, start, _, _) => start), static (long value, int start) => value + start))
            .LeftAssociative((Literals.Char('*'), static (a, b) => a * b));

        const int Count = 1_000_000;
        Assert.Equal(5L + ((long)Count * (Count - 1) / 2), parser.Parse(new string('-', Count) + "5"));
    }

    [Fact]
    public void ShouldEnforceMaxRecursionDepthOnValuedPrefixOperators()
    {
        var parser = Literals.Char('a').Then(static _ => "a").Operators()
            .Unary((Literals.Char('!'), Prefix("!")))
            .Unary((Literals.Char('-'), static (string x, char op) => $"({op}{x})"));

        Assert.False(parser.TryParse(new ParseContext(new Scanner("---a"), maxRecursionDepth: 2), out _, out var error));
        Assert.Equal("The maximum parser recursion depth of 2 was exceeded.", error!.Message);

        Assert.False(parser.TryParse(new ParseContext(new Scanner("-!!a"), maxRecursionDepth: 2), out _, out error));
        Assert.Equal("The maximum parser recursion depth of 2 was exceeded.", error!.Message);

        Assert.True(parser.TryParse(new ParseContext(new Scanner("--a"), maxRecursionDepth: 2), out var value, out _));
        Assert.Equal("(-(-a))", value);

        Assert.True(parser.TryParse(new ParseContext(new Scanner("-!a"), maxRecursionDepth: 2), out value, out _));
        Assert.Equal("(-(!a))", value);

        // The values of the operators of the chain which exceeded the limit were discarded
        Assert.Equal("(-(-a)) [0..3] @3", Run(parser, "--a"));
    }

    [Fact]
    public void ShouldDiscardTheValuesWhenAFactoryThrows()
    {
        var parser = Atom.Operators()
            .Unary((Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => x == "boom" ? throw new InvalidOperationException() : $"-@{start}({x})"));

        // The innermost operator throws, the values of the others are discarded
        Assert.Throws<InvalidOperationException>(() => parser.Parse("- - - boom"));
        Assert.Equal(0, ScratchCount<int>());

        // The outermost operator throws after the others applied
        var outer = Atom.Operators()
            .Unary((Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => start == 0 ? throw new InvalidOperationException() : $"-@{start}({x})"));

        Assert.Throws<InvalidOperationException>(() => outer.Parse("- - - x"));
        Assert.Equal(0, ScratchCount<int>());

        Assert.Equal("-@0(x) [0..2] @2", Run(parser, "-x"));
    }

    [Fact]
    public void ShouldKeepTheValuesOfNestedChains()
    {
        var expression = Deferred<string>();
        var primary = Atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"))
            .LeftAssociative((Terms.Char('*'), Binary("*")));

        Assert.Equal("-@0(-@1((-@3(-@4(a))*-@9(b)))) [0..11] @12", Run(expression, "--(--a * -b)"));
        Assert.Equal(0, ScratchCount<int>());
    }

    private static int ScratchCount<T>()
    {
        var type = typeof(Parser<>).Assembly.GetType("Parlot.Fluent.ListScratch`1", throwOnError: true)!.MakeGenericType(typeof(T));
        var current = type.GetProperty("Current")!.GetValue(null)!;
        return (int)type.GetField("Count", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)!.GetValue(current)!;
    }

    private sealed class LineContext : ParseContext
    {
        public LineContext(string text) : base(new Scanner(text))
        {
        }

        public int Calls { get; set; }
    }

    [Fact]
    public void FactoriesShouldReceiveTheContext()
    {
        static string Count(ParseContext context, string value)
        {
            ((LineContext)context).Calls++;
            return value;
        }

        var parser = Atom.Operators()
            .Postfix((Terms.Text("++"), static (ParseContext c, string x, string op) => Count(c, Suffix(x, op))))
            .Unary((Terms.Char('-'), static (ParseContext c, string x, char op) => Count(c, $"({op}{x})")))
            .LeftAssociative((Terms.Char('*'), static (ParseContext c, string a, string b) => Count(c, $"({a}*{b})")))
            .RightAssociative((Terms.Text("**"), static (ParseContext c, string a, string b) => Count(c, $"({a}**{b})")))
            .LeftAssociative([Terms.Char('+')], static (ParseContext c, string a, string b, char op) => Count(c, $"({a}{op}{b})"))
            .RightAssociative([Terms.Char('=')], static (ParseContext c, string a, string b, char op) => Count(c, $"({a}{op}{b})"))
            .Ternary(Terms.Char('?'), Atom, Terms.Char(':'), static (ParseContext c, string condition, string whenTrue, string whenFalse) => Count(c, $"({condition}?{whenTrue}:{whenFalse})"));

        var text = "-a++ * b ** c ** d + e = f = g ? h : i";
        var context = new LineContext(text);

        Assert.Equal("((((((-(a++))*b)**(c**d))+e)=(f=g))?h:i) [0..38] @38", Run(parser, text, context));
        Assert.Equal(9, context.Calls);
    }
}
