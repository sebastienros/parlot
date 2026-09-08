#nullable enable

using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using Parlot.SourceGenerator;
using Parlot.Tests.Calc;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

[IncludeUsings("Parlot.Tests.Calc")]
public static partial class Grammars
{
    [GenerateParser(nameof(TryParseHello))]
    private static Parser<string> BuildHello() => Terms.Text("hello");

    [GenerateParser(nameof(TryParseExpression))]
    private static Parser<double> BuildExpression()
    {
        var value = OneOf(
            Terms.Text("one").Then(static _ => 1.0),
            Terms.Text("two").Then(static _ => 2.0),
            Terms.Text("three").Then(static _ => 3.0));
        return value.And(ZeroOrMany(Terms.Char('+').SkipAnd(value))).Then(static tuple =>
        {
            var result = tuple.Item1;
            foreach (var addition in tuple.Item2)
            {
                result += addition;
            }
            return result;
        });
    }

    [GenerateParser(nameof(TryParseLeftAssociative))]
    private static Parser<double> BuildLeftAssociative() =>
        Terms.Decimal().Then(static value => (double)value).LeftAssociative(
            (Terms.Char('+'), static (left, right) => left + right),
            (Terms.Char('-'), static (left, right) => left - right));

    [GenerateParser(nameof(TryParseNestedLeftAssociative))]
    private static Parser<double> BuildNestedLeftAssociative()
    {
        var number = Terms.Decimal().Then(static value => (double)value);
        var product = number.LeftAssociative(
            (Terms.Char('*'), static (left, right) => left * right),
            (Terms.Char('/'), static (left, right) => left / right));
        return product.LeftAssociative(
            (Terms.Char('+'), static (left, right) => left + right),
            (Terms.Char('-'), static (left, right) => left - right));
    }

    [GenerateParser(nameof(TryParseCalculator))]
    private static Parser<Expression> BuildCalculator()
    {
        var expression = Deferred<Expression>();
        var number = Terms.Decimal().Then<Expression>(static value => new Number(value));
        var primary = number.Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        var unary = primary.Unary((Terms.Char('-'), static value => new NegateExpression(value)));
        var product = unary.LeftAssociative(
            (Terms.Char('/'), static (left, right) => new Division(left, right)),
            (Terms.Char('*'), static (left, right) => new Multiplication(left, right)));
        expression.Parser = product.LeftAssociative(
            (Terms.Char('+'), static (left, right) => new Addition(left, right)),
            (Terms.Char('-'), static (left, right) => new Subtraction(left, right)));
        return expression;
    }

    [GenerateParser(nameof(TryParseTermsChar))]
    private static Parser<char> BuildTermsChar() => Terms.Char('h');

    [GenerateParser(nameof(TryParseTermsString))]
    private static Parser<string> BuildTermsString() => Terms.String().Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseTermsPattern))]
    private static Parser<string> BuildTermsPattern() =>
        Terms.Pattern(static character => Character.IsInRange(character, 'a', 'z'))
            .Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseTermsIdentifier))]
    private static Parser<string> BuildTermsIdentifier() => Terms.Identifier().Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseTermsWhiteSpace))]
    private static Parser<string> BuildTermsWhiteSpace() => Terms.WhiteSpace().Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseTermsNonWhiteSpace))]
    private static Parser<string> BuildTermsNonWhiteSpace() => Terms.NonWhiteSpace().Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseTermsDecimal))]
    private static Parser<decimal> BuildTermsDecimal() => Terms.Decimal();

    [GenerateParser(nameof(TryParseTermsKeyword))]
    private static Parser<string> BuildTermsKeyword() => Terms.Text("if").When(static (context, _) =>
        context.Scanner.Cursor.Eof
        || (!Character.IsInRange(context.Scanner.Cursor.Current, 'a', 'z')
            && !Character.IsInRange(context.Scanner.Cursor.Current, 'A', 'Z')));

    [GenerateParser(nameof(TryParseLiteralsText))]
    private static Parser<string> BuildLiteralsText() => Literals.Text("hello");

    [GenerateParser(nameof(TryParseLiteralsChar))]
    private static Parser<char> BuildLiteralsChar() => Literals.Char('h');

    [GenerateParser(nameof(TryParseLiteralsQuote))]
    private static Parser<char> BuildLiteralsQuote() => Literals.Char('\'');

    [GenerateParser(nameof(TryParseLiteralsBackslash))]
    private static Parser<char> BuildLiteralsBackslash() => Literals.Char('\\');

    [GenerateParser(nameof(TryParseLiteralsNewLine))]
    private static Parser<char> BuildLiteralsNewLine() => Literals.Char('\n');

    [GenerateParser(nameof(TryParseSequence))]
    private static Parser<(string Text, char Character)> BuildSequence() => Terms.Text("hi").And(Terms.Char('!'));

    [GenerateParser(nameof(TryParseSkipAnd))]
    private static Parser<char> BuildSkipAnd() => Terms.Text("hi").SkipAnd(Terms.Char('!'));

    [GenerateParser(nameof(TryParseAndSkip))]
    private static Parser<char> BuildAndSkip() => Terms.Char('!').AndSkip(Terms.Text("hi"));

    [GenerateParser(nameof(TryParseOptionalText))]
    private static Parser<string?> BuildOptionalText() =>
        Terms.Text("hi").Optional().Then(static option => option.HasValue ? option.Value : null);

    [GenerateParser(nameof(TryParseZeroOrManyChars))]
    private static Parser<IReadOnlyList<char>> BuildZeroOrManyChars() => ZeroOrMany(Terms.Char('a'));

    [GenerateParser(nameof(TryParseZeroOrOneChar))]
    private static Parser<char> BuildZeroOrOneChar() =>
        Terms.Char('a').Optional().Then(static option => option.HasValue ? option.Value : 'x');

    [GenerateParser(nameof(TryParseEofText))]
    private static Parser<string> BuildEofText() => Terms.Text("end").Eof();

    [GenerateParser(nameof(TryParseCaptureChar))]
    private static Parser<string> BuildCaptureChar() => Capture(Terms.Char('z')).Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseOneOfChar))]
    private static Parser<char> BuildOneOfChar() => OneOf(Terms.Char('a'), Terms.Char('b'));

    [GenerateParser(nameof(TryParseBetweenIdentifier))]
    private static Parser<string> BuildBetweenIdentifier() =>
        Between(Terms.Char('('), Terms.Identifier(), Terms.Char(')')).Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseSeparatedDecimals))]
    private static Parser<IReadOnlyList<decimal>> BuildSeparatedDecimals() => Separated(Terms.Char(','), Terms.Decimal());

    [GenerateParser(nameof(TryParseUnaryDecimal))]
    private static Parser<decimal> BuildUnaryDecimal() =>
        Terms.Decimal().Unary((Terms.Char('-'), static value => -value));

    [GenerateParser(nameof(TryParseLeftAssociativeDecimal))]
    private static Parser<decimal> BuildLeftAssociativeDecimal() =>
        Terms.Decimal().LeftAssociative((Terms.Char('+'), static (left, right) => left + right));

    [GenerateParser(nameof(TryParseLeftAssociativeThenPlus))]
    private static Parser<decimal> BuildLeftAssociativeThenPlus() =>
        Terms.Decimal()
            .LeftAssociative((Literals.Char('+'), static (left, right) => left + right))
            .AndSkip(Literals.Char('+'))
            .Eof();

    [GenerateParser(nameof(TryParseUnaryFallback))]
    private static Parser<decimal> BuildUnaryFallback()
    {
        var unary = Terms.Decimal().Unary((Literals.Char('-'), static value => -value));
        return OneOf(unary, Literals.Char('-').Then(static _ => 42m)).Eof();
    }

    [GenerateParser(nameof(TryParseAnyOfDigits))]
    private static Parser<string> BuildAnyOfDigits() => Literals.AnyOf("0123456789").Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseAnyOfLetters))]
    private static Parser<string> BuildAnyOfLetters() =>
        Literals.AnyOf("abcdefghijklmnopqrstuvwxyz", minSize: 2, maxSize: 10)
            .Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseNoneOfWhitespace))]
    private static Parser<string> BuildNoneOfWhitespace() =>
        Literals.NoneOf(" \t\r\n").Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseBlockLambda))]
    private static Parser<string> BuildBlockLambda() => Terms.Identifier().Then(static value =>
    {
        var upper = value.ToString().ToUpperInvariant();
        return "Result: " + upper;
    });

    [GenerateParser(nameof(TryParseNotX))]
    private static Parser<char> BuildNotX() => Not(Terms.Char('x'));

    [GenerateParser(nameof(TryParseHelloNotBang))]
    private static Parser<string> BuildHelloNotBang() =>
        Terms.Text("hello").And(Not(Terms.Char('!'))).Then(static value => value.Item1);

    [GenerateParser(nameof(TryParseHelloBang))]
    private static Parser<string> BuildHelloBang() =>
        Terms.Text("hello").And(Terms.Char('!')).Then(static value => value.Item1);

    [GenerateParser(nameof(TryParseCountingOneOf))]
    private static Parser<char> BuildCountingOneOf() =>
        OneOf(new CountingParser('a', "a", skipWhitespace: true), new CountingParser('b', "b", skipWhitespace: true));

    [GenerateParser(nameof(TryParseCustomSwitch))]
    private static Parser<char> BuildCustomSwitch()
    {
        var prefix = OneOf(Terms.Char('a'), Terms.Char('b'));
        var x = new CountingParser('x', "x", skipWhitespace: false);
        var y = new CountingParser('y', "y", skipWhitespace: false);
        return prefix.Switch(static (_, value) => value == 'a' ? 0 : 1, x, y).Eof();
    }

    [GenerateParser(nameof(TryParseCustomSelect))]
    private static Parser<char> BuildCustomSelect(bool preferX)
    {
        var x = new CountingParser('x', "x", skipWhitespace: false);
        var y = new CountingParser('y', "y", skipWhitespace: false);
        return Select(() => preferX ? 0 : 1, x, y).Eof();
    }

    [GenerateParser(nameof(TryParseZeroOrManyOptional))]
    private static Parser<IReadOnlyList<char>> BuildZeroOrManyOptional() =>
        ZeroOrMany(ZeroOrOne(Literals.Char('a'))).Eof();

    [GenerateParser(nameof(TryParseOneOrManyOptional))]
    private static Parser<IReadOnlyList<char>> BuildOneOrManyOptional() =>
        OneOrMany(ZeroOrOne(Literals.Char('a'))).Eof();

    [GenerateParser(nameof(TryParseSeparatedOptional))]
    private static Parser<IReadOnlyList<char>> BuildSeparatedOptional() =>
        Separated(ZeroOrOne(Literals.Char(',')), ZeroOrOne(Literals.Char('a'))).Eof();

    private static readonly Parser<long> Long = Terms.Number<long>(NumberOptions.Integer);
    private static readonly Parser<string> Equal = Terms.Text("==");

    private static Parser<NodeBase> CreatePropertyParser<T>(
        string name,
        Parser<string> @operator,
        Parser<T> comparand) =>
        comparand
            .AndSkip(@operator)
            .AndSkip(Terms.Text(name, caseInsensitive: true))
            .And(comparand)
            .Then<NodeBase>(items => new BasicNode(items.Item2!));

    [GenerateParser(nameof(TryParseGenericProperty))]
    private static Parser<NodeBase> BuildGenericProperty() =>
        CreatePropertyParser("long", Equal, Long).Eof();

    [GenerateParser(nameof(TryParseInteger))]
    private static Parser<long> BuildInteger() => Terms.Integer().Eof();

    [GenerateParser(nameof(TryParseDecimal))]
    private static Parser<decimal> BuildDecimal() => Terms.Decimal().Eof();

    [GenerateParser(nameof(TryParseDoubleExponent))]
    private static Parser<double> BuildDoubleExponent() =>
        Terms.Number<double>(NumberOptions.Number | NumberOptions.AllowExponent).Eof();

    [GenerateParser(nameof(TryParseCommaDecimal))]
    private static Parser<decimal> BuildCommaDecimal() =>
        Terms.Number<decimal>(
            NumberOptions.AllowLeadingSign | NumberOptions.AllowDecimalSeparator,
            decimalSeparator: ',').Eof();

    [GenerateParser(nameof(TryParseUnderscoreInteger))]
    private static Parser<long> BuildUnderscoreInteger() =>
        Terms.Number<long>(
            NumberOptions.Integer | NumberOptions.AllowGroupSeparators,
            groupSeparator: '_').Eof();

    [GenerateParser(nameof(TryParseUnsignedInteger))]
    private static Parser<long> BuildUnsignedInteger() => Terms.Number<long>(NumberOptions.None).Eof();

    [GenerateParser(nameof(TryParseIntegralDecimal))]
    private static Parser<decimal> BuildIntegralDecimal() =>
        Terms.Number<decimal>(NumberOptions.AllowLeadingSign).Eof();

    [GenerateParser(nameof(TryParseFloat))]
    private static Parser<float> BuildFloat() =>
        Terms.Number<float>(NumberOptions.Number | NumberOptions.AllowExponent).Eof();

    [GenerateParser(nameof(TryParseLong))]
    private static Parser<long> BuildLong() => Terms.Integer(NumberOptions.Integer).Eof();

    [GenerateParser(nameof(TryParseByte))]
    private static Parser<byte> BuildByte() => Terms.Number<byte>(NumberOptions.Integer).Eof();

    [GenerateParser(nameof(TryParseCustomCultureDecimal))]
    private static Parser<decimal> BuildCustomCultureDecimal() =>
        Terms.Number<decimal>(
            NumberOptions.Number | NumberOptions.AllowGroupSeparators,
            decimalSeparator: ',',
            groupSeparator: '_').Eof();

    [GenerateParser(nameof(TryParseHexadecimal))]
    private static Parser<int> BuildHexadecimal() => Terms.Hexadecimal<int>().Eof();

    [GenerateParser(nameof(TryParseOctal))]
    private static Parser<long> BuildOctal() => Terms.Octal<long>().Eof();

    [GenerateParser(nameof(TryParseBinary))]
    private static Parser<byte> BuildBinary() => Terms.Binary<byte>().Eof();
}

internal sealed class CountingParser : Parser<char>, ISeekable, ISourceable
{
    private readonly char _expected;
    private readonly string _name;
    private readonly bool _skipWhitespace;

    public CountingParser(char expected, string name, bool skipWhitespace)
    {
        _expected = expected;
        _name = name;
        _skipWhitespace = skipWhitespace;
    }

    public bool CanSeek => true;
    public char[] ExpectedChars => [_expected];
    public bool SkipWhitespace => _skipWhitespace;

    public override bool Parse(ParseContext context, ref ParseResult<char> result) =>
        throw new NotSupportedException("The build-only parser must be source generated.");

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        var result = context.CreateResult(typeof(char));
        result.Body.Add($"global::Parlot.SourceGenerator.Tests.GeneratedParserCounters.Increment(\"{_name}\");");
        if (_skipWhitespace)
        {
            result.Body.Add($"{context.ParseContextName}.SkipWhiteSpace();");
        }
        result.Body.Add($"if ({context.CursorName}.Current == '{_expected}')");
        result.Body.Add("{");
        result.Body.Add($"    {context.CursorName}.Advance();");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add($"    {result.ValueVariable} = '{_expected}';");
        result.Body.Add("}");
        return result;
    }
}
