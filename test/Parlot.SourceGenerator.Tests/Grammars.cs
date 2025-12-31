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

internal sealed class DualCountingCharParser : Parser<char>, ISeekable, ISourceable
{
    private readonly char _expected;
    private readonly bool _skipWhitespace;
    private readonly string _name;

    private static readonly Dictionary<string, int> _runtimeCounts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> _generatedCounts = new(StringComparer.Ordinal);
    private static readonly object _lock = new();

    public DualCountingCharParser(char expected, string name, bool skipWhitespace = false)
    {
        _expected = expected;
        _name = name ?? expected.ToString();
        _skipWhitespace = skipWhitespace;
    }

    public static void Reset()
    {
        lock (_lock)
        {
            _runtimeCounts.Clear();
            _generatedCounts.Clear();
        }
    }

    public static int GetRuntimeCount(string name)
    {
        lock (_lock)
        {
            return _runtimeCounts.TryGetValue(name, out var c) ? c : 0;
        }
    }

    public static int GetGeneratedCount(string name)
    {
        lock (_lock)
        {
            return _generatedCounts.TryGetValue(name, out var c) ? c : 0;
        }
    }

    public static void IncrementRuntime(string name)
    {
        lock (_lock)
        {
            _runtimeCounts[name] = _runtimeCounts.TryGetValue(name, out var c) ? c + 1 : 1;
        }
    }

    public static void IncrementGenerated(string name)
    {
        lock (_lock)
        {
            _generatedCounts[name] = _generatedCounts.TryGetValue(name, out var c) ? c + 1 : 1;
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

        IncrementRuntime(_name);

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

        res.Body.Add($"global::Parlot.SourceGenerator.Tests.DualCountingCharParser.IncrementGenerated(\"{_name}\");");

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
            '"' => "\\\"",
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
    [GenerateParser]
    public static Parser<string> ParserWithNoName()
    {
        return Terms.Text("hello");
    }

    [GenerateParser]
    public static Parser<string> HelloParser()
    {
        return Terms.Text("hello");
    }

    [GenerateParser]
    public static Parser<IReadOnlyList<long>> CancelSeparatedIntegersParser()
    {
        return Separated(Terms.Char(','), Terms.Integer());
    }

    [GenerateParser]
    public static Parser<char> Switch_UsesGeneratedTargetParser()
    {
        var prefix = OneOf(Terms.Char('a'), Terms.Char('b'));

        var x = new DualCountingCharParser('x', "x");
        var y = new DualCountingCharParser('y', "y");

        return prefix.Switch((context, ch) => ch == 'a' ? 0 : ch == 'b' ? 1 : -1, x, y);
    }

    internal sealed class SelectTestContext : ParseContext
    {
        public SelectTestContext(Scanner scanner) : base(scanner)
        {
        }

        public bool PreferX { get; set; }
    }

    [GenerateParser]
    public static Parser<char> Select_UsesGeneratedTargetParser()
    {
        var x = new DualCountingCharParser('x', "x");
        var y = new DualCountingCharParser('y', "y");

        return Select<SelectTestContext, char>(context => context.PreferX ? 0 : 1, x, y);
    }

    [GenerateParser]
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

    [GenerateParser]
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

    [GenerateParser]
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

    [GenerateParser]
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

    // Sourceable parser smoke tests (added incrementally)

    [GenerateParser]
    public static Parser<string> TermsTextParser() => Terms.Text("hello");

    [GenerateParser]
    public static Parser<char> TermsCharParser() => Terms.Char('h');

    [GenerateParser]
    public static Parser<TextSpan> TermsStringParser() => Terms.String();

    [GenerateParser]
    public static Parser<TextSpan> TermsPatternParser() => Terms.Pattern(static c => Character.IsInRange(c, 'a', 'z'));

    [GenerateParser]
    public static Parser<TextSpan> TermsIdentifierParser() => Terms.Identifier();

    [GenerateParser]
    public static Parser<TextSpan> TermsWhiteSpaceParser() => Terms.WhiteSpace();

    [GenerateParser]
    public static Parser<TextSpan> TermsNonWhiteSpaceParser() => Terms.NonWhiteSpace();

    [GenerateParser]
    public static Parser<decimal> TermsDecimalParser() => Terms.Decimal();

    [GenerateParser]
    public static Parser<string> TermsKeywordParser()
    {
        // Inline keyword logic so the source generator can extract the When() predicate
        return Terms
            .Text("if")
            .When(static (context, value) =>
                context.Scanner.Cursor.Eof ||
                (!Character.IsInRange(context.Scanner.Cursor.Current, 'a', 'z') &&
                 !Character.IsInRange(context.Scanner.Cursor.Current, 'A', 'Z')));
    }

    // Literal parsers

    [GenerateParser]
    public static Parser<string> LiteralsTextParser() => Literals.Text("hello");

    [GenerateParser]
    public static Parser<char> LiteralsCharParser() => Literals.Char('h');

    [GenerateParser]
    public static Parser<TextSpan> LiteralsWhiteSpaceParser() => Literals.WhiteSpace();

    [GenerateParser]
    public static Parser<TextSpan> LiteralsNonWhiteSpaceParser() => Literals.NonWhiteSpace();

    [GenerateParser]
    public static Parser<decimal> LiteralsDecimalParser() => Literals.Decimal();

    [GenerateParser]
    public static Parser<string> LiteralsKeywordParser()
    {
        // Inline keyword logic so the source generator can extract the predicate
        return Literals
            .Text("if")
            .When(static (context, value) =>
                context.Scanner.Cursor.Eof ||
                (!Character.IsInRange(context.Scanner.Cursor.Current, 'a', 'z') &&
                 !Character.IsInRange(context.Scanner.Cursor.Current, 'A', 'Z')));
    }

    // Combinator parsers

    [GenerateParser]
    public static Parser<(string, char)> SequenceTextCharParser() => Terms.Text("hi").And(Terms.Char('!'));

    [GenerateParser]
    public static Parser<char> SkipAndParser() => Terms.Text("hi").SkipAnd(Terms.Char('!'));

    [GenerateParser]
    public static Parser<char> AndSkipParser() => Terms.Char('!').AndSkip(Terms.Text("hi"));

    [GenerateParser]
    public static Parser<Option<string>> OptionalTextParser() => Terms.Text("hi").Optional();

    [GenerateParser]
    public static Parser<IReadOnlyList<char>> ZeroOrManyCharParser() => ZeroOrMany(Terms.Char('a'));

    [GenerateParser]
    public static Parser<char> ZeroOrOneCharParser() => Terms.Char('a').Optional().Then(static opt => opt.HasValue ? opt.Value : 'x');

    [GenerateParser]
    public static Parser<string> EofTextParser() => new Eof<string>(Terms.Text("end"));

    [GenerateParser]
    public static Parser<TextSpan> CaptureCharParser() => Capture(Terms.Char('z'));

    [GenerateParser]
    public static Parser<char> OneOfCharParser() => OneOf(Terms.Char('a'), Terms.Char('b'));

    // Advanced combinators

    [GenerateParser]
    public static Parser<TextSpan> BetweenParensIdentifierParser() => Between(Terms.Char('('), Terms.Identifier(), Terms.Char(')'));

    [GenerateParser]
    public static Parser<IReadOnlyList<decimal>> SeparatedDecimalsParser() => Separated(Terms.Char(','), Terms.Decimal());

    [GenerateParser]
    public static Parser<decimal> UnaryNegateDecimalParser() => Terms.Decimal().Unary((Terms.Char('-'), static d => -d));

    [GenerateParser]
    public static Parser<decimal> UnaryNegateDecimalWithContextParser() => Terms.Decimal().Unary((Terms.Char('-'), static (ParseContext arg0, decimal arg1) => -arg1));

    [GenerateParser]
    public static Parser<decimal> LeftAssociativeAdditionParser() => Terms.Decimal().LeftAssociative((Terms.Char('+'), static (a, b) => a + b));

    [GenerateParser]
    public static Parser<decimal> LeftAssociativeAdditionWithContextParser() => Terms.Decimal().LeftAssociative((Terms.Char('+'), static (ParseContext arg0, decimal arg1, decimal arg2) => arg1 + arg2));

    [GenerateParser]
    public static Parser<char> NotXCharParser() => Not(Terms.Char('x'));

    [GenerateParser]
    public static Parser<string> WhenNotFollowedByHelloBangParser()
    {
        // Implement look-ahead without WhenNotFollowedBy to avoid lambda extraction issues
        return Terms.Text("hello")
            .And(Not(Terms.Char('!')))
            .Then(static tuple => tuple.Item1);
    }

    [GenerateParser]
    public static Parser<string> WhenFollowedByHelloBangParser()
    {
        // Implement as sequence consuming '!' to keep generation simple
        return Terms.Text("hello")
            .And(Terms.Char('!'))
            .Then(static tuple => tuple.Item1);
    }

    [GenerateParser]
    public static Parser<char> CountingOneOfParser()
    {
        var a = new CountingParser('a', "a", skipWhitespace: true);
        var b = new CountingParser('b', "b", skipWhitespace: true);

        return OneOf(a, b);
    }

    // Separate methods for different keyword variants (instead of parameterized method)
    [GenerateParser]
    public static Parser<string> FooLowerParser() => Terms.Text("foo");

    [GenerateParser]
    public static Parser<string> FooUpperParser() => Terms.Text("FOO");

    // Sample: generic factory method building a typed node

    internal abstract class NodeBase
    {
    }

    internal sealed class BasicNode : NodeBase
    {
        public BasicNode(object value)
        {
            Value = value;
        }

        public object Value { get; }
    }

    private static readonly Parser<long> Long = Terms.Number<long>(NumberOptions.Integer);
    private static readonly Parser<string> Equal = Terms.Text("==");

    private static Parser<NodeBase> CreatePropertyParser<TComparand>(
        string name,
        Parser<string> @operator,
        Parser<TComparand> comparand)
    {
        return comparand
            .AndSkip(@operator)
            .AndSkip(Terms.Text(name, caseInsensitive: true))
            .And(comparand)
                .Then<NodeBase>(items => new BasicNode(items.Item2!));
    }

    [GenerateParser]
    internal static Parser<NodeBase> GenericPropertyParserSample()
    {
        return CreatePropertyParser(
            "long",
            Equal,
            Long);
    }

    // Test for AnyOf parser (ListOfChars / SearchValuesCharLiteral)
    [GenerateParser]
    public static Parser<TextSpan> AnyOfDigitsParser() => Literals.AnyOf("0123456789");

    [GenerateParser]
    public static Parser<TextSpan> AnyOfLettersParser() => Literals.AnyOf("abcdefghijklmnopqrstuvwxyz", minSize: 2, maxSize: 10);

    [GenerateParser]
    public static Parser<TextSpan> NoneOfWhitespaceParser() => Literals.NoneOf(" \t\r\n");

    #region Number Literal Parsers

    [GenerateParser]
    public static Parser<long> IntegerNumberLiteralParser() => Terms.Integer();

    [GenerateParser]
    public static Parser<decimal> DecimalNumberLiteralParser() => Terms.Decimal();

    [GenerateParser]
    public static Parser<double> DoubleNumberLiteralWithExponentParser() => 
        Terms.Number<double>(NumberOptions.Number | NumberOptions.AllowExponent);

    [GenerateParser]
    public static Parser<decimal> DecimalNumberLiteralWithCommaSeparatorParser() => 
        Terms.Number<decimal>(NumberOptions.AllowLeadingSign | NumberOptions.AllowDecimalSeparator, decimalSeparator: ',');

    [GenerateParser]
    public static Parser<long> IntegerNumberLiteralWithUnderscoreSeparatorParser() => 
        Terms.Number<long>(NumberOptions.Integer | NumberOptions.AllowGroupSeparators, groupSeparator: '_');

    [GenerateParser]
    public static Parser<long> IntegerNumberLiteralNoLeadingSignParser() => 
        Terms.Number<long>(NumberOptions.None);

    [GenerateParser]
    public static Parser<decimal> DecimalNumberLiteralNoDecimalSeparatorParser() => 
        Terms.Number<decimal>(NumberOptions.AllowLeadingSign);

    [GenerateParser]
    public static Parser<float> FloatNumberLiteralParser() => 
        Terms.Number<float>(NumberOptions.Number | NumberOptions.AllowExponent);

    [GenerateParser]
    public static Parser<long> LongNumberLiteralParser() => Terms.Integer(NumberOptions.Integer);

    [GenerateParser]
    public static Parser<decimal> DecimalNumberLiteralCustomCultureParser() => 
        Terms.Number<decimal>(NumberOptions.Number | NumberOptions.AllowGroupSeparators, decimalSeparator: ',', groupSeparator: '_');

    #endregion
}