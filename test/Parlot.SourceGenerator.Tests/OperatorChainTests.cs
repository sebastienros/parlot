#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class OperatorChainGrammars
{
    public static partial bool TryParseChain(string text, out string value);
    public static partial bool TryParseChain(TextReader reader, out string value);
    public static partial bool TryParseNested(string text, out string value);
    public static partial bool TryParseNested(TextReader reader, out string value);
    public static partial bool TryParseChainPrefixAboveExponent(string text, out string value);
    public static partial bool TryParseNestedPrefixAboveExponent(string text, out string value);
}

/// <summary>
/// A generated chain of operator levels parses like the runtime chain and like the generated nested chain.
/// </summary>
public class OperatorChainTests
{
    private static readonly string[] FixedInputs =
    [
        "", " ", "a", "a <= b", "a < = b", "a && b", "a & & b", "a &= b", "a & = b", "a => b", "a = > b", "a == b",
        "a = = b", "a <= b <= c", "a ** b ** -c", "-a ** b", "a ** -b", "--a", "-", "a -", "a - - -", "a ** ** b",
        "(a <= b", "a | b || c |= d", "a !b", "!a != !b", "a << b <= c < d", "a ? b", ")", "(a + b) * -(c - d)",
        "a ** b ** c ** d", "a = b = c", "1 + 2 * 3 - 4 / 5 % 6",
    ];

    [Fact]
    public void Generated_Chain_Parses_Like_The_Runtime_And_The_Generated_Nested_Chain()
    {
        var runtime = RuntimeChain(prefixAboveExponent: false);
        var inputs = new List<string>(FixedInputs);
        inputs.AddRange(PrecedenceTests.RandomExpressions(11, 2000));

        foreach (var input in inputs)
        {
            var expected = PrecedenceTests.Run(runtime, input);

            Assert.Equal(expected, PrecedenceTests.Run(OperatorChainGrammars.TryParseNested, input));
            Assert.Equal(expected, PrecedenceTests.Run(OperatorChainGrammars.TryParseChain, input));
        }

        // The streaming entry points read the operators again from a compacting buffer
        foreach (var input in inputs.GetRange(0, 400))
        {
            var expected = PrecedenceTests.Run(runtime, input);

            foreach (var chunk in new[] { 1, 3, int.MaxValue })
            {
                Assert.Equal(expected, PrecedenceTests.Run(OperatorChainGrammars.TryParseChain, input, chunk));
                Assert.Equal(expected, PrecedenceTests.Run(OperatorChainGrammars.TryParseNested, input, chunk));
            }
        }
    }

    [Fact]
    public void Generated_Chain_With_A_Prefix_Level_Above_A_Binary_One_Parses_Like_The_Runtime()
    {
        var runtime = RuntimeChain(prefixAboveExponent: true);
        var inputs = new List<string>(FixedInputs);
        inputs.AddRange(PrecedenceTests.RandomExpressions(13, 2000));

        foreach (var input in inputs)
        {
            var expected = PrecedenceTests.Run(runtime, input);

            Assert.Equal(expected, PrecedenceTests.Run(OperatorChainGrammars.TryParseNestedPrefixAboveExponent, input));
            Assert.Equal(expected, PrecedenceTests.Run(OperatorChainGrammars.TryParseChainPrefixAboveExponent, input));
        }
    }

    // The runtime nested chain of OperatorChainGrammars.Chain: each level is separated from its operand
    private static Parser<string> RuntimeChain(bool prefixAboveExponent)
    {
        static Parser<string> Level(Parser<string> parser) => parser.Then(static x => x);

        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        (Parser<string> op, Func<string, string> factory)[] prefix =
        [
            (Terms.Text("-"), static x => PrecedenceAst.Prefix("-", x)),
            (Terms.Text("!"), static x => PrecedenceAst.Prefix("!", x)),
            (Terms.Text("~"), static x => PrecedenceAst.Prefix("~", x)),
        ];

        var chain = prefixAboveExponent
            ? Level(Level(primary.RightAssociative(PrecedenceTests.Ops("**"))).Unary(prefix))
            : Level(Level(primary.Unary(prefix)).RightAssociative(PrecedenceTests.Ops("**")));

        foreach (var level in new[] { PrecedenceTests.Ops("*", "/", "%"), PrecedenceTests.Ops("+", "-"), PrecedenceTests.Ops("<<", ">>"), PrecedenceTests.Ops("<", ">", "<=", ">="), PrecedenceTests.Ops("==", "!=", "="), PrecedenceTests.Ops("&"), PrecedenceTests.Ops("^"), PrecedenceTests.Ops("|"), PrecedenceTests.Ops("&&"), PrecedenceTests.Ops("||") })
        {
            chain = Level(chain.LeftAssociative(level));
        }

        expression.Parser = chain.RightAssociative(PrecedenceTests.Ops("<=", "&=", "|=", "=>"));
        return PrecedenceTests.WithRest(expression);
    }
}
