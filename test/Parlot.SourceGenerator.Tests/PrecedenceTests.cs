#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public static partial class PrecedenceGrammars
{
    public static partial bool TryParseTable(string text, out string value);
    public static partial bool TryParseTable(TextReader reader, out string value);
    public static partial bool TryParseZeroWidthPrefix(string text, out string value);
    public static partial bool TryParsePrefixOnly(string text, out string value);
    public static partial bool TryParsePrefixOnly(TextReader reader, out string value);
    public static partial bool TryParseTightestLeft(string text, out string value);
    public static partial bool TryParseTightestLeft(TextReader reader, out string value);
    public static partial bool TryParseTightestRight(string text, out string value);
    public static partial bool TryParseTightestValued(string text, out string value);
    public static partial bool TryParseNoLevels(string text, out string value);
    public static partial bool TryParseNoLevels(TextReader reader, out string value);
    public static partial bool TryParseEmptyOperators(string text, out string value);
    public static partial bool TryParseValued(string text, out string value);
    public static partial bool TryParseValued(TextReader reader, out string value);
    public static partial bool TryParseValuedRight(string text, out string value);
    public static partial bool TryParseValuedRight(TextReader reader, out string value);
    public static partial bool TryParseValuedEmpty(string text, out string value);
    public static partial bool TryParseValuedPrefix(string text, out string value);
    public static partial bool TryParseValuedPrefix(TextReader reader, out string value);
    public static partial bool TryParseValuedPrefixOnly(string text, out string value);
    public static partial bool TryParseValuedPrefixOnly(TextReader reader, out string value);
    public static partial bool TryParseValuedPrefixLimited(string text, out string value);
    public static partial bool TryParseValuedZeroWidthPrefix(string text, out string value);
    public static partial bool TryParseConditionalLast(string text, out string value);
    public static partial bool TryParseConditionalLast(TextReader reader, out string value);
    public static partial bool TryParseConditionalLastWithContext(string text, out string value);
    public static partial bool TryParseConditionalLastWithContext(TextReader reader, out string value);
    public static partial bool TryParseValuedLevels(string text, out string value);
    public static partial bool TryParseValuedLevels(TextReader reader, out string value);
}

internal static class PrecedenceAst
{
    public static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Prefix(string op, string operand) => $"({op}{operand})";

    public static string Suffix(string op, string operand) => $"({operand}{op})";

    public static string Binary(string op, string left, string right) => $"({left}{op}{right})";

    public static string Member(string operand, string name) => $"{operand}.{name}";

    public static string Index(string operand, string index) => $"{operand}[{index}]";

    public static string Conditional(string condition, string whenTrue, string whenFalse) => $"({condition}?{whenTrue}:{whenFalse})";
}

/// <summary>
/// The generated precedence parser of an Operators() table parses like the runtime one.
/// </summary>
public class PrecedenceTests
{
    internal delegate bool StringParser(string text, out string value);

    internal delegate bool ReaderParser(TextReader reader, out string value);

    [Theory]
    [InlineData("a!")]
    [InlineData("a!!")]
    [InlineData("-a!")]
    [InlineData("~-a! ** b! ** c")]
    [InlineData("a * b?")]
    [InlineData("a? * b")]
    [InlineData("a? ** b")]
    [InlineData("a?? + b")]
    [InlineData("a + b? << c")]
    [InlineData("a << b? < c")]
    [InlineData("a <= b")]
    [InlineData("a <== b = c <== d")]
    [InlineData("not a < b")]
    [InlineData("not not a = b")]
    [InlineData("a = not b")]
    [InlineData("not")]
    [InlineData("a = not")]
    [InlineData("a <")]
    [InlineData("(a + b)! * -(c)?")]
    [InlineData("-")]
    [InlineData("")]
    public void Generated_Table_Parses_Like_The_Runtime(string input)
    {
        var expected = Run(RuntimeTable(), input);

        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseTable, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseTable, input, chunk));
        }
    }

    // The right operand of the tightest level climbs no level, which isn't resuming the climb of a level after a prefix
    [Theory]
    [InlineData("a")]
    [InlineData("a * b")]
    [InlineData("a * b / c")]
    [InlineData("a * b * c + d")]
    [InlineData("-a")]
    [InlineData("-a * b")]
    [InlineData("-a * b + c")]
    [InlineData("- - a * b")]
    [InlineData("a * -b")]
    [InlineData("a + -b * c")]
    [InlineData("a + -b + c")]
    [InlineData("(a * b) * -(c + d)")]
    [InlineData("a *")]
    [InlineData("a * + b")]
    [InlineData("-a *")]
    [InlineData("-")]
    [InlineData("")]
    public void Generated_Table_With_A_Binary_Tightest_Level_Parses_Like_The_Runtime(string input)
    {
        var left = Run(RuntimeTightest("left"), input);

        Assert.Equal(left, Run(PrecedenceGrammars.TryParseTightestLeft, input));
        Assert.Equal(Run(RuntimeTightest("right"), input), Run(PrecedenceGrammars.TryParseTightestRight, input));
        Assert.Equal(Run(RuntimeTightest("valued"), input), Run(PrecedenceGrammars.TryParseTightestValued, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(left, Run(PrecedenceGrammars.TryParseTightestLeft, input, chunk));
        }
    }

    [Fact]
    public void Generated_Table_With_A_Binary_Tightest_Level_Parses_Random_Expressions_Like_The_Runtime()
    {
        var left = RuntimeTightest("left");
        var right = RuntimeTightest("right");
        var valued = RuntimeTightest("valued");

        foreach (var input in RandomExpressions(23, 2000))
        {
            Assert.Equal(Run(left, input), Run(PrecedenceGrammars.TryParseTightestLeft, input));
            Assert.Equal(Run(right, input), Run(PrecedenceGrammars.TryParseTightestRight, input));
            Assert.Equal(Run(valued, input), Run(PrecedenceGrammars.TryParseTightestValued, input));
        }
    }

    [Fact]
    public void Generated_Table_Parses_Random_Expressions_Like_The_Runtime()
    {
        var runtime = RuntimeTable();

        foreach (var input in RandomExpressions(17, 2000))
        {
            Assert.Equal(Run(runtime, input), Run(PrecedenceGrammars.TryParseTable, input));
        }
    }

    [Theory]
    [InlineData("a")]
    [InlineData("-a")]
    [InlineData("- ~ -a + b")]
    [InlineData("not -a")]
    [InlineData("-not a")]
    [InlineData("not not (- a)")]
    [InlineData("-(not a)")]
    [InlineData("nota")]
    [InlineData("a -b")]
    [InlineData("-")]
    [InlineData("not")]
    [InlineData("-(a")]
    [InlineData("")]
    public void Generated_Prefix_Only_Table_Parses_Like_The_Runtime(string input)
    {
        var expected = Run(RuntimePrefixOnly(), input);

        Assert.Equal(expected, Run(PrecedenceGrammars.TryParsePrefixOnly, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParsePrefixOnly, input, chunk));
        }
    }

    [Theory]
    [InlineData("a", "a | ")]
    [InlineData(" a b", "a |  b")]
    [InlineData("-a", "<failed>")]
    [InlineData("", "<failed>")]
    public void Generated_Table_Without_Levels_Parses_Its_Operand(string input, string expected)
    {
        Assert.Equal(expected, Run(WithRest(Terms.Identifier().Then(static x => x.ToString()).Operators()), input));
        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseNoLevels, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseNoLevels, input, chunk));
        }
    }

    [Theory]
    [InlineData("+5", "(+5) | ")]
    [InlineData("-5", "(-5) | ")]
    [InlineData("--5*+2", "((-5)*(+2)) | ")]
    [InlineData("5*", "5 | *")]
    [InlineData("+", null)]
    public void Generated_Prefix_Operators_Skip_Operators_That_Read_Nothing(string input, string? expected)
    {
        Assert.Equal(expected is not null, PrecedenceGrammars.TryParseZeroWidthPrefix(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("-a")]
    [InlineData("- ~ -a")]
    [InlineData("- ~ !a ** b")]
    [InlineData("!-a")]
    [InlineData("typeof -a ** b * c")]
    [InlineData("-typeof a")]
    [InlineData("void typeof - ~a++ * b + c")]
    [InlineData("a * typeof b ** -c++")]
    [InlineData("await a ? typeof b : - c")]
    [InlineData("await await -a + b")]
    [InlineData("a ? b : c ? d : e")]
    [InlineData("(-a) ** void (b)")]
    [InlineData("-")]
    [InlineData("- -")]
    [InlineData("typeof")]
    [InlineData("a * -")]
    [InlineData("a * void ~")]
    [InlineData("- !")]
    [InlineData("-(- a")]
    [InlineData("await")]
    [InlineData("")]
    public void Generated_Valued_Prefix_Operators_Parse_Like_The_Runtime(string input)
    {
        var expected = Run(RuntimeValuedPrefix(), input);

        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedPrefix, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedPrefix, input, chunk));
        }
    }

    [Theory]
    [InlineData("- - !a", "(-(-(!a))) | ")]
    [InlineData("typeof -a ** b", "typeof@0(((-a)**b)) | ")]
    [InlineData("a * void typeof b", "(a*void@4(typeof@9(b))) | ")]
    [InlineData("await a ? b : c", "await@0((a?b:c)) | ")]
    [InlineData("- !", "<failed>")]
    public void Generated_Valued_Prefix_Operators_Receive_Their_Values(string input, string expected)
    {
        Assert.Equal(expected, Run(RuntimeValuedPrefix(), input));
        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedPrefix, input));
    }

    [Fact]
    public void Generated_Valued_Prefix_Operators_Parse_Random_Expressions_Like_The_Runtime()
    {
        var runtime = RuntimeValuedPrefix();
        var random = new Random(516);
        string[] tokens = ["a", "b", "1", "(", ")", "-", "~", "!", "typeof ", "void ", "await ", "**", "*", "/", "+", "++", "?", ":"];

        for (var i = 0; i < 2000; i++)
        {
            var sb = new StringBuilder();
            var length = random.Next(0, 14);

            for (var j = 0; j < length; j++)
            {
                sb.Append(tokens[random.Next(tokens.Length)]).Append(random.Next(3) == 0 ? " " : "");
            }

            var input = sb.ToString();
            Assert.Equal(Run(runtime, input), Run(PrecedenceGrammars.TryParseValuedPrefix, input));
        }
    }

    [Theory]
    [InlineData("a")]
    [InlineData("-a")]
    [InlineData("- -a")]
    [InlineData("not -a")]
    [InlineData("not not - - (not a)")]
    [InlineData("-not a")]
    [InlineData("-")]
    [InlineData("not -")]
    [InlineData("")]
    public void Generated_Valued_Prefix_Only_Table_Parses_Like_The_Runtime(string input)
    {
        var expected = Run(RuntimeValuedPrefixOnly(), input);

        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedPrefixOnly, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedPrefixOnly, input, chunk));
        }
    }

    [Theory]
    [InlineData("a", "a | ")]
    [InlineData("--a*-a", "(-@0(-@1(a))*-@4(a)) | ")]
    [InlineData("---a", "-@0(-@1(-@2(a))) | ")]
    [InlineData("----a", "<failed>")]
    [InlineData("-a*--a", "(-@0(a)*-@3(-@4(a))) | ")]
    public void Generated_Valued_Prefix_Operators_Count_Levels_Of_Recursion(string input, string expected)
    {
        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedPrefixLimited, input));

        var runtime = RuntimeValuedPrefixLimited();
        var context = new ParseContext(new Scanner(input), maxRecursionDepth: 3);
        Assert.Equal(expected, runtime.TryParse(context, out var value, out _) ? value : "<failed>");
    }

    [Theory]
    [InlineData("+5", "+@0(5) | ")]
    [InlineData("--5", "-2(5) | ")]
    [InlineData("-+5", "-1(+@1(5)) | ")]
    [InlineData("5", "5 | ")]
    [InlineData("+", "<failed>")]
    public void Generated_Valued_Prefix_Operators_Skip_Operators_That_Read_Nothing(string input, string expected)
    {
        Assert.Equal(expected, Run(RuntimeValuedZeroWidthPrefix(), input));
        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedZeroWidthPrefix, input));
    }

    [Theory]
    [InlineData("x+x", "(x+x) | ")]
    [InlineData("x^x^x", "(x^(x^x)) | ")]
    [InlineData("x!+x!", "((x!)+(x!)) | ")]
    [InlineData("", " | ")]
    [InlineData("x?", "x | ?")]
    public void Generated_Operators_Stop_When_Operator_And_Operand_Are_Empty(string input, string expected)
    {
        Assert.True(PrecedenceGrammars.TryParseEmptyOperators(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("a.b")]
    [InlineData("a.b[c].d")]
    [InlineData("a[b + c[d]]")]
    [InlineData("-a.b!")]
    [InlineData("a!#!")]
    [InlineData("a.")]
    [InlineData("a[")]
    [InlineData("a[b")]
    [InlineData("a ** b ** c")]
    [InlineData("a ** b ** ")]
    [InlineData("a * b / c")]
    [InlineData("a <= b < c")]
    [InlineData("a ? b : c")]
    [InlineData("a ? b : c ? d : e")]
    [InlineData("a ? b ? c : d : e")]
    [InlineData("a ?? b")]
    [InlineData("a ? b ?? c : d")]
    [InlineData("a ? b : c ?? d")]
    [InlineData("a ?? b ? c : d")]
    [InlineData("a ? b")]
    [InlineData("a ? b :")]
    [InlineData("a = b += c")]
    [InlineData("a = b ? c : d")]
    [InlineData("a = b = ")]
    [InlineData("(a.b ? c[d] : -e) ** f")]
    [InlineData(".a")]
    [InlineData("")]
    public void Generated_Valued_Operators_Parse_Like_The_Runtime(string input)
    {
        foreach (var right in new[] { false, true })
        {
            var expected = Run(RuntimeValued(right), input);
            StringParser generated = right ? PrecedenceGrammars.TryParseValuedRight : PrecedenceGrammars.TryParseValued;
            ReaderParser reader = right ? PrecedenceGrammars.TryParseValuedRight : PrecedenceGrammars.TryParseValued;

            Assert.Equal(expected, Run(generated, input));

            foreach (var chunk in new[] { 1, 2, int.MaxValue })
            {
                Assert.Equal(expected, Run(reader, input, chunk));
            }
        }
    }

    [Fact]
    public void Generated_Valued_Operators_Parse_Random_Expressions_Like_The_Runtime()
    {
        foreach (var right in new[] { false, true })
        {
            var runtime = RuntimeValued(right);
            StringParser generated = right ? PrecedenceGrammars.TryParseValuedRight : PrecedenceGrammars.TryParseValued;

            foreach (var input in RandomValuedExpressions(right ? 23 : 29, 2000))
            {
                Assert.Equal(Run(runtime, input), Run(generated, input));
            }
        }
    }

    [Theory]
    [InlineData("a")]
    [InlineData("a * b")]
    [InlineData("a ** b ** c")]
    [InlineData("a * * b")]
    [InlineData("a ** ")]
    [InlineData("a >>> b >> c << d")]
    [InlineData("a >> > b")]
    [InlineData("a === b == c != d")]
    [InlineData("a & b && c | d || e ?? f")]
    [InlineData("a & & b")]
    [InlineData("a | | b")]
    [InlineData("- a & - b")]
    [InlineData("a & -")]
    [InlineData("!a ?? !b")]
    [InlineData("a[b * c] ** d[e]")]
    [InlineData("a ? b ** c : d ?? e")]
    [InlineData("a < b ? c : d ? e : f")]
    [InlineData("a ? b :")]
    [InlineData("a ?? ")]
    [InlineData("a +")]
    [InlineData("[a]")]
    [InlineData("!")]
    [InlineData("")]
    public void Generated_Consecutive_Valued_Levels_Parse_Like_The_Runtime(string input)
    {
        var expected = Run(RuntimeValuedLevels(), input);

        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedLevels, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedLevels, input, chunk));
        }
    }

    [Fact]
    public void Generated_Consecutive_Valued_Levels_Parse_Random_Expressions_Like_The_Runtime()
    {
        var runtime = RuntimeValuedLevels();

        foreach (var input in RandomValuedLevelsExpressions(31, 3000))
        {
            var expected = Run(runtime, input);

            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedLevels, input));
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseValuedLevels, input, 3));
        }
    }

    [Theory]
    [InlineData("x!+x!", "((x!)+(x!)) | ")]
    [InlineData("x^x^x", "(x^(x^x)) | ")]
    [InlineData("", " | ")]
    [InlineData("x?", "x | ?")]
    public void Generated_Valued_Operators_Stop_When_Operator_And_Operand_Are_Empty(string input, string expected)
    {
        Assert.True(PrecedenceGrammars.TryParseValuedEmpty(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("a ? b : c = d", "(a?b:(c=d)) | ")]
    [InlineData("a ? b : c ? d : e", "(a?b:(c?d:e)) | ")]
    [InlineData("a || b ? c : d", "((a||b)?c:d) | ")]
    [InlineData("a = b ? c : d = e", "(a=(b?c:(d=e))) | ")]
    [InlineData("a ? b : c ?? d", "(a?b:(c??d)) | ")]
    [InlineData("a ?? b ? c : d", "(a??(b?c:d)) | ")]
    [InlineData("a ? b : c, d", "((a?b:c),d) | ")]
    [InlineData("a.b ? c.d : e.f", "(a.b?c.d:e.f) | ")]
    [InlineData("a ? b", "a |  ? b")]
    [InlineData("a ? b :", "a |  ? b :")]
    [InlineData("a ? b : )", "a |  ? b : )")]
    [InlineData("a ? b : c ? d", "(a?b:c) |  ? d")]
    [InlineData("?", "<failed>")]
    public void Generated_Conditional_Parses_Its_Last_Operand_With_Its_Parser(string input, string expected)
    {
        Assert.Equal(expected, Run(RuntimeConditionalLast(), input));
        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseConditionalLast, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseConditionalLast, input, chunk));
        }
    }

    [Fact]
    public void Generated_Conditional_With_Last_Operand_Parses_Random_Expressions_Like_The_Runtime()
    {
        var runtime = RuntimeConditionalLast();
        string[] tokens = ["a", "b", "1", "(", ")", ".", ".b", "||", "??", "=", "+=", "?", ":", ","];
        var random = new Random(31);

        for (var i = 0; i < 3000; i++)
        {
            var sb = new StringBuilder();
            var length = random.Next(0, 14);

            for (var j = 0; j < length; j++)
            {
                // Mostly operands and operators in turn, so that most inputs apply some operators
                var token = random.Next(3) == 0 ? tokens[random.Next(tokens.Length)] : j % 2 == 0 ? tokens[random.Next(3)] : tokens[random.Next(5, tokens.Length)];
                sb.Append(token).Append(random.Next(3) == 0 ? " " : "");
            }

            var input = sb.ToString();
            var expected = Run(runtime, input);

            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseConditionalLast, input));
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseConditionalLast, input, 1));
        }
    }

    [Theory]
    [InlineData("a ? b : c = d", "(a?b:(c=d))@13 | ")]
    [InlineData("a ? b : c ? d : e", "(a?b:(c?d:e)@17)@17 | ")]
    [InlineData("a || b ? c : d", "((a||b)?c:d)@14 | ")]
    [InlineData("a ? b : c, d", "((a?b:c)@9,d) | ")]
    [InlineData("a ? b :", "a |  ? b :")]
    [InlineData("?", "<failed>")]
    public void Generated_Conditional_With_Last_Operand_Passes_The_Context_To_Its_Factory(string input, string expected)
    {
        Assert.Equal(expected, Run(RuntimeConditionalLastWithContext(), input));
        Assert.Equal(expected, Run(PrecedenceGrammars.TryParseConditionalLastWithContext, input));

        foreach (var chunk in new[] { 1, 2, int.MaxValue })
        {
            Assert.Equal(expected, Run(PrecedenceGrammars.TryParseConditionalLastWithContext, input, chunk));
        }
    }

    [Fact]
    public void Generated_Conditional_With_Last_Operand_And_Context_Parses_The_Same_Trees()
    {
        var plain = RuntimeConditionalLast();
        string[] tokens = ["a", "b", "1", "(", ")", ".", ".b", "||", "??", "=", "+=", "?", ":", ","];
        var random = new Random(37);

        for (var i = 0; i < 3000; i++)
        {
            var sb = new StringBuilder();
            var length = random.Next(0, 14);

            for (var j = 0; j < length; j++)
            {
                var token = random.Next(3) == 0 ? tokens[random.Next(tokens.Length)] : j % 2 == 0 ? tokens[random.Next(3)] : tokens[random.Next(5, tokens.Length)];
                sb.Append(token).Append(random.Next(3) == 0 ? " " : "");
            }

            var input = sb.ToString();

            // The factory with the context builds the tree of the plain one, with the offset of the cursor after each conditional
            Assert.Equal(Run(plain, input), StripOffsets(Run(PrecedenceGrammars.TryParseConditionalLastWithContext, input)));
            Assert.Equal(Run(RuntimeConditionalLastWithContext(), input), Run(PrecedenceGrammars.TryParseConditionalLastWithContext, input));
        }
    }

    private static string StripOffsets(string value) => System.Text.RegularExpressions.Regex.Replace(value, @"@\d+", "");

    private static Parser<string> RuntimeConditionalLastWithContext()
    {
        var expression = Deferred<string>();
        var assignment = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        assignment.Parser = primary.Operators()
            .Postfix((Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => PrecedenceAst.Member(x, name.ToString())))
            .LeftAssociative((Terms.Text("||"), static (a, b) => PrecedenceAst.Binary("||", a, b)))
            .Ternary(Terms.Char('?'), assignment, Terms.Char(':'), assignment, static (ParseContext context, string c, string a, string b) => PrecedenceAst.Conditional(c, a, b) + "@" + context.Scanner.Cursor.Offset)
            .LeftAssociative((Terms.Text("??"), static (a, b) => PrecedenceAst.Binary("??", a, b)))
            .RightAssociative(new[] { Terms.Text("="), Terms.Text("+=") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b));

        expression.Parser = assignment.Operators().LeftAssociative((Terms.Char(','), static (a, b) => PrecedenceAst.Binary(",", a, b)));

        return WithRest(expression);
    }

    private static Parser<string> RuntimeConditionalLast()
    {
        var expression = Deferred<string>();
        var assignment = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        assignment.Parser = primary.Operators()
            .Postfix((Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => PrecedenceAst.Member(x, name.ToString())))
            .LeftAssociative((Terms.Text("||"), static (a, b) => PrecedenceAst.Binary("||", a, b)))
            .Ternary(Terms.Char('?'), assignment, Terms.Char(':'), assignment, static (c, a, b) => PrecedenceAst.Conditional(c, a, b))
            .LeftAssociative((Terms.Text("??"), static (a, b) => PrecedenceAst.Binary("??", a, b)))
            .RightAssociative(new[] { Terms.Text("="), Terms.Text("+=") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b));

        expression.Parser = assignment.Operators().LeftAssociative((Terms.Char(','), static (a, b) => PrecedenceAst.Binary(",", a, b)));

        return WithRest(expression);
    }

    // The tables of PrecedenceGrammars.BuildValuedPrefix() and the following ones
    internal static Parser<string> RuntimeValuedPrefix()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Text("++"), static (ParseContext context, string x, string op) => PrecedenceAst.Suffix(op, x)))
            .Unary((Terms.Char('!'), static x => PrecedenceAst.Prefix("!", x)))
            .Unary((Terms.Char('-'), static (string x, char op) => PrecedenceAst.Prefix(op.ToString(), x)), (Terms.Char('~'), static (string x, char op) => PrecedenceAst.Prefix(op.ToString(), x)))
            .RightAssociative((Terms.Text("**"), static (ParseContext context, string a, string b) => PrecedenceAst.Binary("**", a, b)))
            .Unary(
                (Terms.Text("typeof").Then(static (_, start, _, _) => start), static (ParseContext context, string x, int start) => $"typeof@{start}({x})"),
                (Terms.Text("void").Then(static (_, start, _, _) => start), static (ParseContext context, string x, int start) => $"void@{start}({x})"))
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (ParseContext context, string a, string b, string op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)), (Terms.Char('-'), static (a, b) => PrecedenceAst.Binary("-", a, b)))
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (ParseContext context, string c, string a, string b) => PrecedenceAst.Conditional(c, a, b))
            .Unary((Terms.Text("await").Then(static (_, start, _, _) => start), static (string x, int start) => $"await@{start}({x})"));

        return WithRest(expression);
    }

    // Only prefix levels, after which no operator is looked up, one of them with values
    internal static Parser<string> RuntimeValuedPrefixOnly()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString());
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"))
            .Unary((Terms.Text("not"), static x => PrecedenceAst.Prefix("not ", x)));

        return WithRest(expression);
    }

    // A single valued prefix level whose operators are counted as levels of recursion
    internal static Parser<string> RuntimeValuedPrefixLimited() =>
        WithRest(Literals.Char('a').Then(static _ => "a").Operators()
            .Unary((Literals.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"))
            .LeftAssociative((Literals.Char('*'), static (a, b) => PrecedenceAst.Binary("*", a, b))));

    // A valued prefix level whose first operator can match without reading anything
    internal static Parser<string> RuntimeValuedZeroWidthPrefix() =>
        WithRest(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)).Operators()
            .Unary(
                (ZeroOrMany(Literals.Char('-')).Then(static x => x.Count), static (string x, int count) => $"-{count}({x})"),
                (Literals.Char('+').Then(static (_, start, _, _) => start), static (string x, int start) => $"+@{start}({x})")));

    internal static string Run(Parser<string> parser, string input) =>
        parser.TryParse(input, out var value) ? value! : "<failed>";

    internal static string Run(StringParser parser, string input) =>
        parser(input, out var value) ? value! : "<failed>";

    internal static string Run(ReaderParser parser, string input, int chunk) =>
        parser(new ChunkedReader(input, chunk), out var value) ? value! : "<failed>";

    internal static Parser<string> WithRest(Parser<string> parser) =>
        parser.And(Literals.Pattern(static _ => true, minSize: 0)).Then(static x => x.Item1 + " | " + x.Item2.ToString());

    private static Func<string, string, string> Binary(string op) => (a, b) => PrecedenceAst.Binary(op, a, b);

    internal static (Parser<string> op, Func<string, string, string> factory)[] Ops(params string[] ops)
    {
        var result = new (Parser<string>, Func<string, string, string>)[ops.Length];

        for (var i = 0; i < ops.Length; i++)
        {
            result[i] = (Terms.Text(ops[i]), Binary(ops[i]));
        }

        return result;
    }

    private static Parser<string> RuntimeTable()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Char('!'), static x => PrecedenceAst.Suffix("!", x)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .RightAssociative(Ops("**"))
            .LeftAssociative(Ops("*", "/"))
            .LeftAssociative(Ops("+", "-"))
            .Postfix((Terms.Char('?'), static x => PrecedenceAst.Suffix("?", x)))
            .LeftAssociative(Ops("<<"))
            .LeftAssociative(Ops("<", "<="))
            .Unary((Terms.Text("not"), static x => PrecedenceAst.Prefix("not ", x)))
            .RightAssociative(Ops("<==", "="));

        return WithRest(expression);
    }

    private static Parser<string> RuntimeTightest(string kind)
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        var table = primary.Operators();

        table = kind switch
        {
            "left" => table.LeftAssociative(Ops("*", "/")),
            "right" => table.RightAssociative(Ops("*", "/")),
            _ => table.LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b)),
        };

        expression.Parser = table
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)))
            .LeftAssociative(Ops("+"));

        return WithRest(expression);
    }

    private static Parser<string> RuntimePrefixOnly()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .Unary((Terms.Text("not"), static x => PrecedenceAst.Prefix("not ", x)));

        return WithRest(expression);
    }

    private static Parser<string> RuntimeValued(bool rightAssociative)
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix(
                (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => PrecedenceAst.Member(x, name.ToString())),
                (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => PrecedenceAst.Index(x, index)))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)), (Terms.Char('~'), static x => PrecedenceAst.Prefix("~", x)))
            .Postfix((Terms.Char('!'), static (x, op) => PrecedenceAst.Suffix(op.ToString(), x)), (Terms.Char('#'), static (x, op) => PrecedenceAst.Suffix(op + "" + op, x)))
            .RightAssociative(new[] { Terms.Text("**") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative((Terms.Char('+'), static (a, b) => PrecedenceAst.Binary("+", a, b)), (Terms.Char('-'), static (a, b) => PrecedenceAst.Binary("-", a, b)))
            .LeftAssociative(new[] { Terms.Text("<="), Terms.Text("<") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => PrecedenceAst.Conditional(c, a, b), rightAssociative)
            .LeftAssociative((Terms.Text("??"), static (a, b) => PrecedenceAst.Binary("??", a, b)))
            .RightAssociative(new[] { Terms.Text("="), Terms.Text("+=") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b));

        return WithRest(expression);
    }

    private static Parser<string> RuntimeValuedLevels()
    {
        var expression = Deferred<string>();
        var atom = Terms.Identifier().Then(static x => x.ToString()).Or(Terms.Integer().Then(static x => PrecedenceAst.Integer(x)));
        var primary = atom.Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => PrecedenceAst.Index(x, index)))
            .RightAssociative(new[] { Terms.Text("**") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("*"), Terms.Text("/"), Terms.Text("%") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("+"), Terms.Text("-") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text(">>>"), Terms.Text("<<"), Terms.Text(">>") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("<="), Terms.Text(">="), Terms.Text("<"), Terms.Text(">") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("==="), Terms.Text("=="), Terms.Text("!=") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Unary((Terms.Char('-'), static x => PrecedenceAst.Prefix("-", x)))
            .LeftAssociative(new[] { Terms.Text("&") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative(new[] { Terms.Text("&&") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .LeftAssociative((Terms.Text("|"), static (a, b) => PrecedenceAst.Binary("|", a, b)))
            .LeftAssociative(new[] { Terms.Text("||") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => PrecedenceAst.Conditional(c, a, b))
            .LeftAssociative(new[] { Terms.Text("??") }, static (a, b, op) => PrecedenceAst.Binary(op, a, b))
            .Unary((Terms.Char('!'), static x => PrecedenceAst.Prefix("!", x)));

        return WithRest(expression);
    }

    private static IEnumerable<string> RandomValuedLevelsExpressions(int seed, int count)
    {
        var random = new Random(seed);
        string[] operands = ["a", "b", "1", "(", ")", "[", "]", "-", "!"];
        string[] operators = ["**", "*", "/", "%", "+", "-", "<<", ">>", ">>>", "<", "<=", ">", ">=", "==", "===", "!=", "&", "&&", "|", "||", "??", "?", ":", "[", "]", ")"];

        string Space() => random.Next(3) == 0 ? " " : "";

        for (var i = 0; i < count; i++)
        {
            var sb = new StringBuilder();
            var length = random.Next(0, 16);

            for (var j = 0; j < length; j++)
            {
                // Mostly operands and operators in turn, so that most inputs apply some operators
                var token = random.Next(4) == 0
                    ? (random.Next(2) == 0 ? operands[random.Next(operands.Length)] : operators[random.Next(operators.Length)])
                    : j % 2 == 0 ? operands[random.Next(3)] : operators[random.Next(operators.Length - 5)];
                sb.Append(token).Append(Space());
            }

            yield return sb.ToString();
        }
    }

    private static IEnumerable<string> RandomValuedExpressions(int seed, int count)
    {
        var random = new Random(seed);
        string[] tokens = ["a", "b", "1", "(", ")", "[", "]", ".", ".b", "-", "~", "!", "#", "**", "*", "/", "+", "<", "<=", "=", "+=", "?", ":", "??"];

        string Space() => random.Next(3) == 0 ? " " : "";

        for (var i = 0; i < count; i++)
        {
            var sb = new StringBuilder();
            var length = random.Next(0, 14);

            for (var j = 0; j < length; j++)
            {
                // Mostly operands and operators in turn, so that most inputs apply some operators
                var token = random.Next(3) == 0 ? tokens[random.Next(tokens.Length)] : j % 2 == 0 ? tokens[random.Next(3)] : tokens[random.Next(7, tokens.Length)];
                sb.Append(token).Append(Space());
            }

            yield return sb.ToString();
        }
    }

    private static readonly string[] Tokens =
    [
        "a", "b", "1", "22", "(", ")", "-", "!", "~", "**", "*", "/", "%", "+", "<<", ">>", "<", ">", "<=", ">=",
        "==", "!=", "=", "&", "^", "|", "&&", "||", "&=", "|=", "=>", "?", "not", "<==",
    ];

    internal static IEnumerable<string> RandomExpressions(int seed, int count)
    {
        var random = new Random(seed);
        string[] operands = ["a", "b", "1", "22"];
        string[] prefixes = ["-", "!", "~", "not "];
        string[] operators = ["**", "*", "/", "%", "+", "-", "<<", ">>", "<", ">", "<=", ">=", "==", "!=", "=", "&", "^", "|", "&&", "||", "&=", "|=", "=>", "<==", "!", "?"];

        string Space() => random.Next(4) switch { 0 => " ", 1 => "  ", _ => "" };

        string Expression(int depth)
        {
            var sb = new StringBuilder();
            var terms = random.Next(1, 6);

            for (var i = 0; i < terms; i++)
            {
                if (i > 0)
                {
                    sb.Append(Space()).Append(operators[random.Next(operators.Length)]).Append(Space());
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
            if (random.Next(4) == 0)
            {
                var sb = new StringBuilder();
                var length = random.Next(0, 10);

                for (var j = 0; j < length; j++)
                {
                    sb.Append(Tokens[random.Next(Tokens.Length)]).Append(Space());
                }

                yield return sb.ToString();
            }
            else
            {
                yield return Expression(0) + Space();
            }
        }
    }

    // A reader returning at most a fixed number of chars per read
    private sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;
        private int _position;

        public ChunkedReader(string text, int chunk)
        {
            _text = text;
            _chunk = chunk;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(Math.Min(count, _chunk), _text.Length - _position);
            _text.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        public override int Read(Span<char> buffer)
        {
            var length = Math.Min(Math.Min(buffer.Length, _chunk), _text.Length - _position);
            _text.AsSpan(_position, length).CopyTo(buffer);
            _position += length;
            return length;
        }
    }
}
