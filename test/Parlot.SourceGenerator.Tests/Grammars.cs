using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

internal sealed class CountingParser : Parser<char>, ISeekable, ISourceable
{
    private readonly char _expected;
    private readonly bool _skipWhitespace;
    private readonly string _name;

    private static readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public CountingParser(char expected, string name, bool skipWhitespace = false)
    {
        _expected = expected;
        _name = name ?? expected.ToString();
        _skipWhitespace = skipWhitespace;
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _counts.Clear();
        }
    }

    public static int GetCount(string name)
    {
        lock (_lock)
        {
            return _counts.TryGetValue(name, out var c) ? c : 0;
        }
    }

    public static void Increment(string name)
    {
        lock (_lock)
        {
            _counts[name] = _counts.TryGetValue(name, out var c) ? c + 1 : 1;
        }
    }

    // ISeekable
    public bool CanSeek => true;

    public char[] ExpectedChars => new[] { _expected };

    public bool SkipWhitespace => _skipWhitespace;

    public override bool Parse(ParseContext context, ref ParseResult<char> result)
    {
        if (_skipWhitespace)
        {
            context.SkipWhiteSpace();
        }

        Increment(_name);

        var cursor = context.Scanner.Cursor;
        if (cursor.Current == _expected)
        {
            result.Set(cursor.Offset, cursor.Offset + 1, _expected);
            cursor.Advance();
            return true;
        }

        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        var res = context.CreateResult(typeof(char));
        var ctxName = context.ParseContextName;
        var cursorName = context.CursorName;

        res.Body.Add($"global::Parlot.SourceGenerator.Tests.CountingParser.Increment(\"{_name}\");");

        if (_skipWhitespace)
        {
            res.Body.Add($"{ctxName}.SkipWhiteSpace();");
        }

        res.Body.Add($"if ({cursorName}.Current == {ToCharLiteral(_expected)})");
        res.Body.Add("{");
        res.Body.Add($"    {cursorName}.Advance();");
        res.Body.Add($"    {res.SuccessVariable} = true;");
        res.Body.Add($"    {res.ValueVariable} = {ToCharLiteral(_expected)};");
        res.Body.Add("}");

        return res;
    }

    private static string ToCharLiteral(char c)
    {
        return "'" + (c switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\"' => "\\\"",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            _ when char.IsControl(c) || c > 0x7e => $"\\u{(int)c:X4}",
            _ => c.ToString()
        }) + "'";
    }
}

public static partial class Grammars
{
    [GenerateParser()]
    public static Parser<string> ParserWithNoName()
    {
        return Terms.Text("hello");
    }

    [GenerateParser("Hello")]
    public static Parser<string> HelloParser()
    {
        return Terms.Text("hello");
    }

    [GenerateParser("ParseExpression")]
    public static Parser<double> ExpressionParser()
    {
        var value = OneOf(
            Terms.Text("one").Then(_ => 1.0),
            Terms.Text("two").Then(_ => 2.0),
            Terms.Text("three").Then(_ => 3.0)
        );

        var tail = ZeroOrMany(Terms.Char('+').SkipAnd(value));

        return value.And(tail).Then(tuple =>
        {
            var (value, additions) = tuple;
            
            foreach (var v in additions)
            {
                value += v;
            }
            return value;
        });
    }

    [GenerateParser("ParseLeftAssociative")]
    public static Parser<double> LeftAssociativeParser()
    {
        var number = Terms.Decimal().Then(d => (double)d);
        var plus = Terms.Char('+');
        var minus = Terms.Char('-');

        return number.LeftAssociative(
            (plus, static (a, b) => a + b),
            (minus, static (a, b) => a - b)
        );
    }

    [GenerateParser("ParseNestedLeftAssociative")]
    public static Parser<double> NestedLeftAssociativeParser()
    {
        // Simulates multiplicative/additive precedence like a calculator
        var number = Terms.Decimal().Then(d => (double)d);
        
        var times = Terms.Char('*');
        var divided = Terms.Char('/');
        var plus = Terms.Char('+');
        var minus = Terms.Char('-');

        // multiplicative has higher precedence
        var multiplicative = number.LeftAssociative(
            (times, static (a, b) => a * b),
            (divided, static (a, b) => a / b)
        );

        // additive has lower precedence
        var additive = multiplicative.LeftAssociative(
            (plus, static (a, b) => a + b),
            (minus, static (a, b) => a - b)
        );

        return additive;
    }

    [GenerateParser("ParseCalculator")]
    public static Parser<Expression> CalculatorParser()
    {
        /*
         * Grammar:
         * additive       => multiplicative ( ( "-" | "+" ) multiplicative )* ;
         * multiplicative => unary ( ( "/" | "*" ) unary )* ;
         * unary          => ( "-" ) unary | primary ;
         * primary        => NUMBER | "(" expression ")" ;
        */

        // The Deferred helper creates a parser that can be referenced by others before it is defined
        var expression = Deferred<Expression>();

        var number = Terms.Decimal()
            .Then<Expression>(static d => new Parlot.Tests.Calc.Number(d));

        var divided = Terms.Char('/');
        var times = Terms.Char('*');
        var minus = Terms.Char('-');
        var plus = Terms.Char('+');
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');

        // "(" expression ")"
        var groupExpression = Between(openParen, expression, closeParen);

        // primary => NUMBER | "(" expression ")";
        var primary = number.Or(groupExpression);

        // ( "-" ) unary | primary;
        var unary = primary.Unary(
            (minus, static x => new Parlot.Tests.Calc.NegateExpression(x))
        );

        // multiplicative => unary ( ( "/" | "*" ) unary )* ;
        var multiplicative = unary.LeftAssociative(
            (divided, static (a, b) => new Parlot.Tests.Calc.Division(a, b)),
            (times, static (a, b) => new Parlot.Tests.Calc.Multiplication(a, b))
        );

        // additive => multiplicative(("-" | "+") multiplicative) * ;
        var additive = multiplicative.LeftAssociative(
            (plus, static (a, b) => new Parlot.Tests.Calc.Addition(a, b)),
            (minus, static (a, b) => new Parlot.Tests.Calc.Subtraction(a, b))
        );

        expression.Parser = additive;

        return expression;
    }

    [GenerateParser("ParseCountingOneOf")]
    public static Parser<char> CountingOneOfParser()
    {
        var a = new CountingParser('a', "a", skipWhitespace: true);
        var b = new CountingParser('b', "b", skipWhitespace: true);

        return OneOf(a, b);
    }
}