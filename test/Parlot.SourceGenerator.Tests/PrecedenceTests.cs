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
    public static partial bool TryParseEmptyOperators(string text, out string value);
}

internal static class PrecedenceAst
{
    public static string Integer(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Prefix(string op, string operand) => $"({op}{operand})";

    public static string Suffix(string op, string operand) => $"({operand}{op})";

    public static string Binary(string op, string left, string right) => $"({left}{op}{right})";
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
