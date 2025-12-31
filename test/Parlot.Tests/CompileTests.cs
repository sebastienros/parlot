using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CompileTests
{
    [Fact]
    public void ShouldCompileTextLiterals()
    {
        var parser = Terms.Text("hello").Compile();

        var result = parser.Parse(" hello world");

        Assert.NotNull(result);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ShouldReturnParsedTextNotRequestedTextForCaseInsensitiveMatch()
    {
        var parser = Terms.Text("hello", caseInsensitive: true).Compile();

        var result = parser.Parse(" HELLO world");

        Assert.NotNull(result);
        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void ShouldCompileStringLiterals()
    {
        var parser = Terms.String().Compile();

        var result = parser.Parse("'hello'");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ShouldCompileCharLiterals()
    {
        var parser = Terms.Char('h').Compile();

        var result = parser.Parse(" hello world");

        Assert.Equal('h', result);
    }

    [Fact]
    public void ShouldCompileRangeLiterals()
    {
        var parser = Terms.Pattern(static c => Character.IsInRange(c, 'a', 'z')).Compile();

        var result = parser.Parse("helloWorld");

        Assert.Equal("hello", result);
    }

    [Fact]
    public void ShouldCompileDecimalLiterals()
    {
        var parser = Terms.Decimal().Compile();

        var result = parser.Parse(" 123");

        Assert.Equal(123, result);
    }

    [Fact]
    public void ShouldCompileLiteralsWithoutSkipWhiteSpace()
    {
        var parser = Literals.Text("hello").Compile();

        var result = parser.Parse(" hello world");

        Assert.Null(result);
    }

    [Fact]
    public void ShouldCompileCustomStringLiterals()
    {
        var parser = new StringLiteral('|').Compile();

        var result = parser.Parse("|hello world|");

        Assert.Equal("hello world", result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShouldParseStringLiterals(bool compile)
    {
        var parser = Literals.String();

        if (compile)
        {
            parser = parser.Compile();
        }

        var result = parser.Parse("\"ab\\nc\"");

        Assert.Equal("ab\nc", result);
    }

    [Fact]
    public void ShouldCompileCustomBacktickStringLiterals()
    {
        var parser = new StringLiteral(StringLiteralQuotes.Backtick).Compile();

        var result = parser.Parse("`hello world`");

        Assert.Equal("hello world", result);
    }

    [Fact]
    public void ShouldCompileOrs()
    {
        var parser = Terms.Text("hello").Or(Terms.Text("world")).Compile();

        var result = parser.Parse(" hello world");

        Assert.NotNull(result);
        Assert.Equal("hello", result);

        result = parser.Parse(" world");

        Assert.NotNull(result);
        Assert.Equal("world", result);
    }

    [Fact]
    public void ShouldCompileAnds()
    {
        var parser = Terms.Text("hello").And(Terms.Text("world")).Compile();

        var result = parser.Parse(" hello world");

        Assert.Equal(("hello", "world"), result);
    }

    [Fact]
    public void ShouldCompileThens()
    {
        var parser = Terms.Text("hello").And(Terms.Text("world")).Then(x => x.Item1.ToUpper()).Compile();

        var result = parser.Parse(" hello world");

        Assert.Equal("HELLO", result);
    }

    [Fact]
    public void ShouldCompileDeferreds()
    {
        var deferred = Deferred<string>();

        deferred.Parser = Terms.Text("hello");

        var parser = deferred.And(deferred).Compile();

        var result = parser.Parse(" hello hello hello");

        Assert.Equal(("hello", "hello"), result);
    }

    [Fact]
    public void ShouldCompileCyclicDeferred()
    {
        var openParen = Terms.Char('(');
        var closeParen = Terms.Char(')');
        var expression = Deferred<decimal>();

        var groupExpression = Between(openParen, expression, closeParen);
        expression.Parser = Terms.Decimal().Or(groupExpression);

        var parser = ZeroOrMany(expression).Compile();

        var result = parser.Parse("1 (2) 3");

        Assert.Equal(new decimal[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void ShouldCompileMultipleDeferred()
    {
        var deferred1 = Deferred<decimal>();
        var deferred2 = Deferred<decimal>();

        deferred1.Parser = Terms.Decimal();
        deferred2.Parser = Terms.Decimal();

        var parser = deferred1.And(deferred2).Then(x => x.Item1 + x.Item2).Compile();

        var result = parser.Parse("1 2");

        Assert.Equal(3, result);
    }

    [Fact]
    public void ShouldCompileRecursive()
    {
        var number = Terms.Decimal();
        var minus = Terms.Char('-');

        var unary = Recursive<decimal>((u) =>
            minus.And(u)
                .Then(static x => 0 - x.Item2)
            .Or(number)
            );

        var parser = unary.Compile();

        var result = parser.Parse("--1");

        Assert.Equal(1, result);
    }

    [Fact]
    public void ShouldCompileZeroOrMany()
    {
        var parser = ZeroOrMany(Terms.Text("+").Or(Terms.Text("-")).And(Terms.Integer())).Compile();

        Assert.Equal([], parser.Parse(""));
        Assert.Equal([("+", 1L)], parser.Parse("+1"));
        Assert.Equal([("+", 1L), ("-", 2)], parser.Parse("+1-2"));
        Assert.Equal([("+", 1L), ("-", 2), ("+", 3)], parser.Parse("+1-2+3"));
    }

    [Fact]
    public void ShouldCompileOneOrMany()
    {
        var parser = OneOrMany(Terms.Text("hello")).Compile();

        var result = parser.Parse(" hello hello hello");

        Assert.Equal(new[] { "hello", "hello", "hello" }, result);
    }

    [Fact]
    public void ShouldZeroOrOne()
    {
        var parser = ZeroOrOne(Terms.Text("hello")).Compile();

        Assert.Equal("hello", parser.Parse(" hello world hello"));
        Assert.Null(parser.Parse(" foo"));
    }

    [Fact]
    public void OptionalShouldSucceed()
    {
        var parser = Terms.Text("hello").Optional().Compile();

        Assert.Equal("hello", parser.Parse(" hello world hello").Value);
        Assert.Null(parser.Parse(" foo").Value);
    }

    [Fact]
    public void ShouldZeroOrOneWithDefault()
    {
        var parser = ZeroOrOne(Terms.Text("hello"), "world").Compile();

        Assert.Equal("world", parser.Parse(" this is an apple"));
        Assert.Equal("hello", parser.Parse(" hello world"));
    }

    [Fact]
    public void ShouldCompileBetweens()
    {
        var parser = Between(Terms.Text("hello"), Terms.Text("world"), Terms.Text("hello")).Compile();

        var result = parser.Parse(" hello world hello");

        Assert.Equal("world", result);
    }

    [Fact]
    public void ShouldcompiledSeparated()
    {
        var parser = Separated(Terms.Char(','), Terms.Decimal()).Compile();

        Assert.Single(parser.Parse("1"));
        Assert.Equal(2, parser.Parse("1,2").Count);
        Assert.Null(parser.Parse(",1,"));
        Assert.Null(parser.Parse(""));

        var result = parser.Parse("1, 2,3");

        Assert.Equal(1, result[0]);
        Assert.Equal(2, result[1]);
        Assert.Equal(3, result[2]);
    }

    [Fact]
    public void SeparatedShouldNotBeConsumedIfNotFollowedByValueCompiled()
    {
        // This test ensures that the separator is not consumed if there is no valid next value.

        var parser = Separated(Terms.Char(','), Terms.Decimal()).AndSkip(Terms.Char(',')).And(Terms.Identifier()).Then(x => true).Compile();

        Assert.False(parser.Parse("1"));
        Assert.False(parser.Parse("1,"));
        Assert.True(parser.Parse("1,x"));
    }

    [Fact]
    public void ShouldCompileExpressionParser()
    {
        var parser = Calc.FluentParser.Expression.Compile();

        var result = parser.Parse("(2 + 1) * 3");

        Assert.Equal(9, result.Evaluate());
    }

    [Fact]
    public void ShouldCompileCapture()
    {
        Parser<char> Dot = Literals.Char('.');
        Parser<char> Plus = Literals.Char('+');
        Parser<char> Minus = Literals.Char('-');
        Parser<char> At = Literals.Char('@');
        Parser<char> WordChar = Literals.Pattern(char.IsLetterOrDigit).Then<char>(x => x.Span[0]);
        Parser<IReadOnlyList<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar, Dot, Plus, Minus));
        Parser<IReadOnlyList<char>> WordDotMinus = OneOrMany(OneOf(WordChar, Dot, Minus));
        Parser<IReadOnlyList<char>> WordMinus = OneOrMany(OneOf(WordChar, Minus));
        Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

        string _email = "sebastien.ros@gmail.com";

        var parser = Email.Compile();
        var result = parser.Parse(_email);

        Assert.Equal(_email, result.ToString());
    }

    private sealed class NonCompilableCharLiteral : Parser<char>
    {
        public NonCompilableCharLiteral(char c, bool skipWhiteSpace = true)
        {
            Char = c;
            SkipWhiteSpace = skipWhiteSpace;
        }

        public char Char { get; }

        public bool SkipWhiteSpace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            context.EnterParser(this);

            if (SkipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if (context.Scanner.ReadChar(Char))
            {
                result.Set(start, context.Scanner.Cursor.Offset, Char);

                context.ExitParser(this);
                return true;
            }

            context.ExitParser(this);
            return false;
        }
    }

    private sealed class CustomCompileParseContext : ParseContext
    {
        public CustomCompileParseContext(Scanner scanner) : base(scanner)
        {
        }

        public bool PreferYes { get; set; }
    }

    [Fact]
    public void ShouldCompileNonCompilableCharLiterals()
    {
        var parser = new NonCompilableCharLiteral('h').Compile();

        var result = parser.Parse(" hello world");

        Assert.Equal('h', result);
    }

    [Fact]
    public void ShouldCompileOneOfABT()
    {
        var a = Literals.Char('a');
        var b = Literals.Decimal();

        var o2 = a.Or<char, decimal, object>(b).Compile();

        Assert.True(o2.TryParse("a", out var c) && (char)c == 'a');
        Assert.True(o2.TryParse("1", out var d) && (decimal)d == 1);
    }

    [Fact]
    public void ShouldCompileAndSkip()
    {
        var code = Terms.Text("hello").AndSkip(Terms.Integer()).Compile();

        Assert.False(code.TryParse("hello country", out var result));
        Assert.True(code.TryParse("hello 1", out result) && result == "hello");
    }

    [Fact]
    public void ShouldCompileSkipAnd()
    {
        var code = Terms.Text("hello").SkipAnd(Terms.Integer()).Compile();

        Assert.False(code.TryParse("hello country", out var result));
        Assert.True(code.TryParse("hello 1", out result) && result == 1);
    }

    [Fact]
    public void ShouldCompileEmpty()
    {
        Assert.True(Always().Compile().TryParse("123", out var result) && result == null);
        Assert.True(Always(1).Compile().TryParse("123", out var r2) && r2 == 1);
    }

    [Fact]
    public void ShouldCompileEof()
    {
        Assert.True(Always().Eof().Compile().TryParse("", out _));
        Assert.False(Always().Eof().Compile().TryParse(" ", out _));
        Assert.True(Terms.Decimal().Eof().Compile().TryParse("123", out var result) && result == 123);
        Assert.False(Terms.Decimal().Eof().Compile().TryParse("123 ", out _));
    }

    [Fact]
    public void ShouldCompileNot()
    {
        Assert.False(Not(Terms.Decimal()).Compile().TryParse("123", out _));
        Assert.True(Not(Terms.Decimal()).Compile().TryParse("Text", out _));
    }

    [Fact]
    public void ShouldCompileDiscard()
    {
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.True(Terms.Decimal().Discard<bool>().Compile().TryParse("123", out var r1) && r1 == false);
        Assert.True(Terms.Decimal().Discard<bool>(true).Compile().TryParse("123", out var r2) && r2 == true);
        Assert.False(Terms.Decimal().Discard<bool>(true).Compile().TryParse("abc", out _));
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.True(Terms.Decimal().Then<int>().Compile().TryParse("123", out var t1) && t1 == 123);
        Assert.True(Terms.Decimal().Then(true).Compile().TryParse("123", out var t2) && t2 == true);
        Assert.False(Terms.Decimal().Then(true).Compile().TryParse("abc", out _));
    }

    [Fact]
    public void ShouldCompileNonWhiteSpace()
    {
        Assert.Equal("a", Terms.NonWhiteSpace(includeNewLines: true).Compile().Parse(" a"));
    }

    [Fact]
    public void ShouldCompileWhiteSpace()
    {
        Assert.Equal("\n\r\v\f ", Literals.WhiteSpace(true).Compile().Parse("\n\r\v\f a"));
        Assert.Equal("  ", Literals.WhiteSpace(false).Compile().Parse("  \n\r\v\f a"));
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("foo", "foo")]
    [InlineData("$_", "$_")]
    [InlineData("a-foo.", "a")]
    [InlineData("abc=3", "abc")]
    [InlineData("abc3", "abc3")]
    [InlineData("abc123", "abc123")]
    [InlineData("abc_3", "abc_3")]
    public void CompiledIdentifierShouldParseValidIdentifiers(string text, string identifier)
    {
        Assert.Equal(identifier, Literals.Identifier().Compile().Parse(text).ToString());
    }

    [Theory]
    [InlineData("-foo")]
    [InlineData("-")]
    [InlineData("  ")]
    public void CompiledIdentifierShouldNotParseInvalidIdentifiers(string text)
    {
        Assert.False(Literals.Identifier().Compile().TryParse(text, out _));
    }

    [Theory]
    [InlineData("-foo")]
    [InlineData("/foo")]
    [InlineData("foo@asd")]
    [InlineData("foo*")]
    public void CompiledIdentifierShouldAcceptExtraChars(string text)
    {
        static bool start(char c) => c == '-' || c == '/';
        static bool part(char c) => c == '@' || c == '*';

        Assert.Equal(text, Literals.Identifier(start, part).Compile().Parse(text).ToString());
    }

    [Fact]
    public void CompiledWhenShouldFailParserWhenFalse()
    {
        var evenIntegers = Literals.Integer().When((c, x) => x % 2 == 0).Compile();

        Assert.True(evenIntegers.TryParse("1234", out var result1));
        Assert.Equal(1234, result1);

        Assert.False(evenIntegers.TryParse("1235", out var result2));
        Assert.Equal(default, result2);
    }

    [Fact]
    public void CompiledWhenShouldResetPositionWhenFalse()
    {
        var evenIntegers = ZeroOrOne(Literals.Integer().When((c, x) => x % 2 == 0)).And(Literals.Integer()).Compile();

        Assert.True(evenIntegers.TryParse("1235", out var result1));
        Assert.Equal(1235, result1.Item2);
    }

    [Fact]
    public void CompiledIfShouldNotInvokeParserWhenFalse()
    {
        bool invoked = false;

#pragma warning disable CS0618 // Type or member is obsolete
        var evenState = If(predicate: (context, x) => x % 2 == 0, state: 0, parser: Literals.Integer().Then(x => invoked = true)).Compile();
        var oddState = If(predicate: (context, x) => x % 2 == 0, state: 1, parser: Literals.Integer().Then(x => invoked = true)).Compile();
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.False(oddState.TryParse("1234", out var result1));
        Assert.False(invoked);

        Assert.True(evenState.TryParse("1234", out var result2));
        Assert.True(invoked);
    }

    [Fact]
    public void ErrorShouldThrowIfParserSucceeds()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").Compile().TryParse("a", out _, out var error));
        Assert.Equal("'a' was not expected", error.Message);

        Assert.False(Literals.Char('a').Error<int>("'a' was not expected").Compile().TryParse("a", out _, out error));
        Assert.Equal("'a' was not expected", error.Message);
    }

    [Fact]
    public void ErrorShouldReturnFalseThrowIfParserFails()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").Compile().TryParse("b", out _, out var error));
        Assert.Null(error);

        Assert.False(Literals.Char('a').Error<int>("'a' was not expected").Compile().TryParse("b", out _, out error));
        Assert.Null(error);
    }

    [Fact]
    public void ErrorShouldThrow()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").Compile().TryParse("a", out _, out var error));
        Assert.Equal("'a' was not expected", error.Message);
    }

    [Fact]
    public void ElseErrorShouldThrowIfParserFails()
    {
        Assert.False(Literals.Char('a').ElseError("'a' was expected").Compile().TryParse("b", out _, out var error));
        Assert.Equal("'a' was expected", error.Message);
    }

    [Fact]
    public void ElseErrorShouldFlowResultIfParserSucceeds()
    {
        Assert.True(Literals.Char('a').ElseError("'a' was expected").Compile().TryParse("a", out var result));
        Assert.Equal('a', result);
    }

    [Fact]
    public void ShouldCompileSwitch()
    {
        var d = Literals.Text("d:");
        var i = Literals.Text("i:");
        var s = Literals.Text("s:");

        var parsers = new Parser<object>[]
        {
            Literals.Decimal().Then<object>(x => x),
            Literals.Integer().Then<object>(x => x),
            Literals.String().Then<object>(x => x),
        };

        var parser = d.Or(i).Or(s)
            .Switch((context, result) => result switch
            {
                "d:" => 0,
                "i:" => 1,
                "s:" => 2,
                _ => -1
            }, parsers)
            .Compile();

        Assert.True(parser.TryParse("d:123.456", out var resultD));
        Assert.Equal((decimal)123.456, resultD);

        Assert.True(parser.TryParse("i:123", out var resultI));
        Assert.Equal((long)123, resultI);

        Assert.True(parser.TryParse("s:'123'", out var resultS));
        Assert.Equal("123", ((TextSpan)resultS).ToString());
    }

    [Fact]
    public void SelectShouldCompilePickParserUsingRuntimeLogic()
    {
        var allowWhiteSpace = true;
        var terms = Terms.Integer();
        var literals = Literals.Integer();

        var parser = Select<long>(_ => allowWhiteSpace ? 0 : 1, terms, literals).Compile();

        Assert.True(parser.TryParse(" 42", out var result1));
        Assert.Equal(42, result1);

        allowWhiteSpace = false;

        Assert.True(parser.TryParse("42", out var result2));
        Assert.Equal(42, result2);

        Assert.False(parser.TryParse(" 42", out _));
    }

    [Fact]
    public void SelectShouldCompileFailWhenSelectorReturnsNull()
    {
        var parser = Select<long>(_ => -1, Terms.Integer()).Compile();

        Assert.False(parser.TryParse("123", out _));
    }

    [Fact]
    public void SelectShouldCompileHonorConcreteParseContext()
    {
        var yesParser = Literals.Text("yes");
        var noParser = Literals.Text("no");

        var parser = Select<CustomCompileParseContext, string>(context => context.PreferYes ? 0 : 1, yesParser, noParser).Compile();

        var yesContext = new CustomCompileParseContext(new Scanner("yes")) { PreferYes = true };
        Assert.True(parser.TryParse(yesContext, out var yes, out _));
        Assert.Equal("yes", yes);

        var noContext = new CustomCompileParseContext(new Scanner("no")) { PreferYes = false };
        Assert.True(parser.TryParse(noContext, out var no, out _));
        Assert.Equal("no", no);
    }

    [Fact]
    public void LeftAssociativeCompiledShouldRestoreCursorWhenRightOperandMissing()
    {
        var parser = Terms.Decimal().LeftAssociative((Terms.Char('+'), static (a, b) => a + b)).Compile();

        var context = new ParseContext(new Scanner("1+"));
        var result = new ParseResult<decimal>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(1m, result.Value);
        Assert.Equal(0, result.Start);
        Assert.Equal(1, result.End);

        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void LeftAssociativeWithContextCompiledShouldRestoreCursorWhenRightOperandMissing()
    {
        var parser = Terms.Decimal().LeftAssociative((Terms.Char('+'), static (ParseContext _, decimal a, decimal b) => a + b)).Compile();

        var context = new ParseContext(new Scanner("1+"));
        var result = new ParseResult<decimal>();

        Assert.True(parser.Parse(context, ref result));
        Assert.Equal(1m, result.Value);
        Assert.Equal(0, result.Start);
        Assert.Equal(1, result.End);

        Assert.Equal(1, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void UnaryCompiledShouldRestoreCursorWhenOperandMissing()
    {
        var parser = Terms.Decimal().Unary((Terms.Char('-'), static d => -d)).Compile();

        var context = new ParseContext(new Scanner("-"));
        var result = new ParseResult<decimal>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void UnaryWithContextCompiledShouldRestoreCursorWhenOperandMissing()
    {
        var parser = Terms.Decimal().Unary((Terms.Char('-'), static (ParseContext _, decimal d) => -d)).Compile();

        var context = new ParseContext(new Scanner("-"));
        var result = new ParseResult<decimal>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldCompileTextBefore()
    {
        Assert.True(AnyCharBefore(Literals.Char('a')).Compile().TryParse("hellao", out var result1));
        Assert.Equal("hell", result1);

        Assert.True(AnyCharBefore(Literals.Char('a')).And(Literals.Char('a')).Compile().TryParse("hellao", out _));
        Assert.False(AnyCharBefore(Literals.Char('a'), consumeDelimiter: true).And(Literals.Char('a')).TryParse("hellao", out _));

        Assert.True(AnyCharBefore(Literals.Char('a')).Compile().TryParse("hella", out var result2));
        Assert.Equal("hell", result2);
    }

    [Fact]
    public void ShouldCompileAndSkipWithAnd()
    {
        var parser = Terms.Char('a').And(Terms.Char('b')).AndSkip(Terms.Char('c')).And(Terms.Char('d')).Compile();

        Assert.True(parser.TryParse("abcd", out var result1));
        Assert.Equal("abd", result1.Item1.ToString() + result1.Item2 + result1.Item3);
    }

    [Fact]
    public void ShouldCompileSkipAndWithAnd()
    {
        var parser = Terms.Char('a').And(Terms.Char('b')).SkipAnd(Terms.Char('c')).And(Terms.Char('d')).Compile();

        Assert.True(parser.TryParse("abcd", out var result1));
        Assert.Equal("acd", result1.Item1.ToString() + result1.Item2 + result1.Item3);
    }

    [Fact]
    public void BetweenCompiledShouldResetPosition()
    {
        Assert.True(Between(Terms.Char('['), Terms.Text("abcd"), Terms.Char(']')).Then(x => x.ToString()).Or(Literals.Text(" [abc").Compile()).TryParse(" [abc]", out var result1));
        Assert.Equal(" [abc", result1);
    }

    [Fact]
    public void TextWithWhiteSpaceCompiledShouldResetPosition()
    {
        var code = OneOf(Terms.Text("a"), Literals.Text(" b")).Compile();

        Assert.True(code.TryParse(" b", out _));
    }

    [Fact]
    public void ShouldSkipWhiteSpaceCompiled()
    {
        var parser = SkipWhiteSpace(Literals.Text("abc")).Compile();

        Assert.Null(parser.Parse(""));
        Assert.True(parser.TryParse("abc", out var result1));
        Assert.Equal("abc", result1);

        Assert.True(parser.TryParse("  abc", out var result2));
        Assert.Equal("abc", result2);
    }

    [Fact]
    public void SkipWhiteSpaceCompiledShouldResetPosition()
    {
        var parser = SkipWhiteSpace(Literals.Text("abc")).Or(Literals.Text(" ab")).Compile();

        Assert.True(parser.TryParse(" ab", out var result1));
        Assert.Equal(" ab", result1);
    }

    [Fact]
    public void SkipWhiteSpaceCompiledShouldResponseParseContextUseNewLines()
    {
        // Default behavior, newlines are skipped like any other space. The grammar is not "New Line Aware"

        Assert.True(
            SkipWhiteSpace(Literals.Text("ab")).Compile()
            .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: false),
            out var _, out var _));

        // Here newlines are not skipped

        Assert.False(
            SkipWhiteSpace(Literals.Text("ab")).Compile()
            .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: true),
            out var _, out var _));

        // Here newlines are not skipped, and the grammar reads them explicitly

        Assert.True(
            SkipWhiteSpace(Literals.WhiteSpace(includeNewLines: true).SkipAnd(Literals.Text("ab"))).Compile()
            .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: true),
            out var _, out var _));
    }

    [Fact]
    public void OneOfCompileShouldNotFailWithLookupConflicts()
    {
        var parser = OneOf(Literals.Text(">="), Literals.Text(">"), Literals.Text("<="), Literals.Text("<")).Compile();

        Assert.Equal("<", parser.Parse("<"));
        Assert.Equal("<=", parser.Parse("<="));
        Assert.Equal(">", parser.Parse(">"));
        Assert.Equal(">=", parser.Parse(">="));
    }

    [Fact]
    public void CanCompileSubTree()
    {
        Parser<char> Dot = Literals.Char('.');
        Parser<char> Plus = Literals.Char('+');
        Parser<char> Minus = Literals.Char('-');
        Parser<char> At = Literals.Char('@');
        Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit).Compile();
        Parser<IReadOnlyList<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Plus, Minus));
        Parser<IReadOnlyList<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Minus));
        Parser<IReadOnlyList<char>> WordMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Minus));
        Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

        string _email = "sebastien.ros@gmail.com";

        var parser = Email.Compile();
        var result = parser.Parse(_email);

        Assert.Equal(_email, result.ToString());
    }

    [Fact]
    public void ShouldSkipSequences()
    {
        var parser = Terms.Char('a').And(Terms.Char('b')).AndSkip(Terms.Char('c')).And(Terms.Char('d')).Compile();

        Assert.True(parser.TryParse("abcd", out var result1));
        Assert.Equal("abd", result1.Item1.ToString() + result1.Item2 + result1.Item3);
    }

    [Fact]
    public void ShouldCompileSequencesWithOneOf()
    {
        OneOf(Terms.Char('+').And(Terms.Char('-')))
       .And(Terms.Integer())
       .Compile();
    }

    [Fact]
    public void ShouldParseSequenceCompile()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');
        var d = Literals.Char('d');
        var e = Literals.Char('e');
        var f = Literals.Char('f');
        var g = Literals.Char('g');
        var h = Literals.Char('h');

        Assert.True(a.And(b).Compile().TryParse("ab", out var r));
        Assert.Equal(('a', 'b'), r);

        Assert.True(a.And(b).And(c).Compile().TryParse("abc", out var r1));
        Assert.Equal(('a', 'b', 'c'), r1);

        Assert.True(a.And(b).AndSkip(c).Compile().TryParse("abc", out var r2));
        Assert.Equal(('a', 'b'), r2);

        Assert.True(a.And(b).SkipAnd(c).Compile().TryParse("abc", out var r3));
        Assert.Equal(('a', 'c'), r3);
    }

    [Fact]
    public void ShouldParseSequenceAndSkipCompile()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');
        var d = Literals.Char('d');
        var e = Literals.Char('e');
        var f = Literals.Char('f');
        var g = Literals.Char('g');
        var h = Literals.Char('h');

        Assert.True(a.AndSkip(b).Compile().TryParse("ab", out var r));
        Assert.Equal(('a'), r);

        Assert.True(a.AndSkip(b).And(c).Compile().TryParse("abc", out var r1));
        Assert.Equal(('a', 'c'), r1);

        Assert.True(a.AndSkip(b).AndSkip(c).Compile().TryParse("abc", out var r2));
        Assert.Equal(('a'), r2);

        Assert.True(a.AndSkip(b).SkipAnd(c).Compile().TryParse("abc", out var r3));
        Assert.Equal(('c'), r3);
    }

    [Fact]
    public void ShouldParseSequenceSkipAndCompile()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');
        var d = Literals.Char('d');
        var e = Literals.Char('e');
        var f = Literals.Char('f');
        var g = Literals.Char('g');
        var h = Literals.Char('h');

        Assert.True(a.SkipAnd(b).Compile().TryParse("ab", out var r));
        Assert.Equal(('b'), r);

        Assert.True(a.SkipAnd(b).And(c).Compile().TryParse("abc", out var r1));
        Assert.Equal(('b', 'c'), r1);

        Assert.True(a.SkipAnd(b).AndSkip(c).Compile().TryParse("abc", out var r2));
        Assert.Equal(('b'), r2);

        Assert.True(a.SkipAnd(b).SkipAnd(c).Compile().TryParse("abc", out var r3));
        Assert.Equal(('c'), r3);
    }

    [Fact]
    public void ShouldReturnConstantResult()
    {
        var a = Literals.Char('a').Then(123).Compile();
        var b = Literals.Char('b').Then("1").Compile();

        Assert.Equal(123, a.Parse("a"));
        Assert.Equal("1", b.Parse("b"));
    }

    [Fact]
    public void ZeroOrManyShouldHandleAllSizes()
    {
        var parser = ZeroOrMany(Terms.Text("+").Or(Terms.Text("-")).And(Terms.Integer())).Compile();

        Assert.Equal([], parser.Parse(""));
        Assert.Equal([("+", 1L)], parser.Parse("+1"));
        Assert.Equal([("+", 1L), ("-", 2)], parser.Parse("+1-2"));

    }

    [Fact]
    public void ShouldParseWithCaseSensitivity()
    {
        var parser1 = Literals.Text("not", caseInsensitive: true).Compile();

        Assert.Equal("not", parser1.Parse("not"));
        Assert.Equal("nOt", parser1.Parse("nOt"));
        Assert.Equal("NOT", parser1.Parse("NOT"));

        var parser2 = Terms.Text("not", caseInsensitive: true).Compile();

        Assert.Equal("not", parser2.Parse("not"));
        Assert.Equal("nOt", parser2.Parse("nOt"));
        Assert.Equal("NOT", parser2.Parse("NOT"));
    }

    [Fact]
    public void ShouldBuildCaseInsensitiveLookupTable()
    {
        var parser = OneOf(
            Literals.Text("not", caseInsensitive: true),
            Literals.Text("abc", caseInsensitive: false),
            Literals.Text("aBC", caseInsensitive: false)
            ).Compile();

        Assert.Equal("not", parser.Parse("not"));
        Assert.Equal("nOt", parser.Parse("nOt"));
        Assert.Equal("abc", parser.Parse("abc"));
        Assert.Equal("aBC", parser.Parse("aBC"));
        Assert.Null(parser.Parse("ABC"));
    }


    [Fact]
    public void ShouldReturnElse()
    {
        var parser = Literals.Integer().Then<long?>(x => x).Else((long?)null).Compile();

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Null(result2);
    }

    [Fact]
    public void ShouldReturnElseFromFunction()
    {
        var parser = Literals.Integer().Then<decimal>().Else(context => -1m).Compile();

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Equal(-1m, result2);
    }

    [Fact]
    public void ElseFunctionShouldReceiveContext()
    {
        var parser = Literals.Integer().Then<int>().Else(context => context.Scanner.Cursor.Position.Offset).Compile();

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        // When parser fails, it should return the current position (which is 0 before whitespace is skipped)
        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Equal(0, result2);
    }

    [Fact]
    public void ElseFunctionWithNullableValue()
    {
        var parser = Literals.Integer().Then<long?>(x => x).Else(context => (long?)null).Compile();

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Null(result2);
    }

    [Fact]
    public void ShouldThenElse()
    {
        var parser = Literals.Integer().ThenElse<long?>(x => x, null).Compile();

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Null(result2);
    }

    private class LogicalExpression { }

    private class ValueExpression(decimal Value) : LogicalExpression
    {
        public decimal Value { get; } = Value;
    }

    [Fact]
    public void IntegersShouldAcceptSignByDefault()
    {
        Assert.True(Terms.Integer().Compile().TryParse("-123", out _));
        Assert.True(Terms.Integer().Compile().TryParse("+123", out _));
    }

    [Fact]
    public void DecimalsShouldAcceptSignByDefault()
    {
        Assert.True(Terms.Decimal().Compile().TryParse("-123", out _));
        Assert.True(Terms.Decimal().Compile().TryParse("+123", out _));
    }

    [Fact]
    public void NumbersShouldAcceptSignIfAllowed()
    {
        Assert.Equal(-123, Terms.Decimal(NumberOptions.AllowLeadingSign).Compile().Parse("-123"));
        Assert.Equal(-123, Terms.Integer(NumberOptions.AllowLeadingSign).Compile().Parse("-123"));
        Assert.Equal(123, Terms.Decimal(NumberOptions.AllowLeadingSign).Compile().Parse("+123"));
        Assert.Equal(123, Terms.Integer(NumberOptions.AllowLeadingSign).Compile().Parse("+123"));
    }

    [Fact]
    public void NumbersShouldNotAcceptSignIfNotAllowed()
    {
        Assert.False(Terms.Decimal(NumberOptions.None).Compile().TryParse("-123", out _));
        Assert.False(Terms.Integer(NumberOptions.None).Compile().TryParse("-123", out _));
        Assert.False(Terms.Decimal(NumberOptions.None).Compile().TryParse("+123", out _));
        Assert.False(Terms.Integer(NumberOptions.None).Compile().TryParse("+123", out _));
    }


    [Fact]
    public void NumberReturnsAnyType()
    {
        Assert.Equal((byte)123, Literals.Number<byte>().Compile().Parse("123"));
        Assert.Equal((sbyte)123, Literals.Number<sbyte>().Compile().Parse("123"));
        Assert.Equal((int)123, Literals.Number<int>().Compile().Parse("123"));
        Assert.Equal((uint)123, Literals.Number<uint>().Compile().Parse("123"));
        Assert.Equal((long)123, Literals.Number<long>().Compile().Parse("123"));
        Assert.Equal((ulong)123, Literals.Number<ulong>().Compile().Parse("123"));
        Assert.Equal((short)123, Literals.Number<short>().Compile().Parse("123"));
        Assert.Equal((ushort)123, Literals.Number<ushort>().Compile().Parse("123"));
        Assert.Equal((decimal)123, Literals.Number<decimal>().Compile().Parse("123"));
        Assert.Equal((double)123, Literals.Number<double>().Compile().Parse("123"));
        Assert.Equal((float)123, Literals.Number<float>().Compile().Parse("123"));
        Assert.Equal((Half)123, Literals.Number<Half>().Compile().Parse("123"));
        Assert.Equal((BigInteger)123, Literals.Number<BigInteger>().Compile().Parse("123"));
#if NET8_0_OR_GREATER
        Assert.Equal((nint)123, Literals.Number<nint>().Compile().Parse("123"));
        Assert.Equal((nuint)123, Literals.Number<nuint>().Compile().Parse("123"));
        Assert.Equal((Int128)123, Literals.Number<Int128>().Compile().Parse("123"));
        Assert.Equal((UInt128)123, Literals.Number<UInt128>().Compile().Parse("123"));
#endif
    }

    [Fact]
    public void NumberCanReadExponent()
    {
        var e = NumberOptions.AllowExponent;

        Assert.Equal((byte)120, Literals.Number<byte>(e).Compile().Parse("12e1"));
        Assert.Equal((sbyte)120, Literals.Number<sbyte>(e).Compile().Parse("12e1"));
        Assert.Equal((int)120, Literals.Number<int>(e).Compile().Parse("12e1"));
        Assert.Equal((uint)120, Literals.Number<uint>(e).Compile().Parse("12e1"));
        Assert.Equal((long)120, Literals.Number<long>(e).Compile().Parse("12e1"));
        Assert.Equal((ulong)120, Literals.Number<ulong>(e).Compile().Parse("12e1"));
        Assert.Equal((short)120, Literals.Number<short>(e).Compile().Parse("12e1"));
        Assert.Equal((ushort)120, Literals.Number<ushort>(e).Compile().Parse("12e1"));
        Assert.Equal((decimal)120, Literals.Number<decimal>(e).Compile().Parse("12e1"));
        Assert.Equal((double)120, Literals.Number<double>(e).Compile().Parse("12e1"));
        Assert.Equal((float)120, Literals.Number<float>(e).Compile().Parse("12e1"));
        Assert.Equal((Half)120, Literals.Number<Half>(e).Compile().Parse("12e1"));
        Assert.Equal((BigInteger)120, Literals.Number<BigInteger>(e).Compile().Parse("12e1"));
#if NET8_0_OR_GREATER
        Assert.Equal((nint)120, Literals.Number<nint>(e).Compile().Parse("12e1"));
        Assert.Equal((nuint)120, Literals.Number<nuint>(e).Compile().Parse("12e1"));
        Assert.Equal((Int128)120, Literals.Number<Int128>(e).Compile().Parse("12e1"));
        Assert.Equal((UInt128)120, Literals.Number<UInt128>(e).Compile().Parse("12e1"));
#endif
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(1, "+1")]
    [InlineData(-1, "-1")]
    [InlineData(1, "1.0")]
    [InlineData(1, "1.00")]
    [InlineData(.1, ".1")]
    [InlineData(1.1, "1.1")]
    [InlineData(1.123, "1.123")]
    [InlineData(1.123, "+1.123")]
    [InlineData(-1.123, "-1.123")]
    [InlineData(1123, "1,123")]
    [InlineData(1123, "1,1,,2,3")]
    [InlineData(1123, "+1,123")]
    [InlineData(-1123, "-1,1,,2,3")]
    [InlineData(1123.123, "1,123.123")]
    [InlineData(1123.123, "1,1,,2,3.123")]
    [InlineData(10, "1e1")]
    [InlineData(11, "1.1e1")]
    [InlineData(1, ".1e1")]
    [InlineData(10, "1e+1")]
    [InlineData(11, "1.1e+1")]
    [InlineData(1, ".1e+1")]
    [InlineData(0.1, "1e-1")]
    [InlineData(0.11, "1.1e-1")]
    [InlineData(0.01, ".1e-1")]
    public void NumberParsesAllNumbers(decimal expected, string source)
    {
        Assert.Equal(expected, Literals.Number<decimal>(NumberOptions.Any).Compile().Parse(source));
    }

    [Fact]
    public void NumberParsesCustomDecimalSeparator()
    {
        Assert.Equal((decimal)123.456, Literals.Number<decimal>(NumberOptions.Any, decimalSeparator: '|').Compile().Parse("123|456"));
    }

    [Fact]
    public void NumberParsesCustomGroupSeparator()
    {
        Assert.Equal((decimal)123456, Literals.Number<decimal>(NumberOptions.Any, groupSeparator: '|').Compile().Parse("123|456"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("+")]
    [InlineData("+++")]
    public void ZeroOrManyShouldSucceed(string source)
    {
        var parser = ZeroOrMany(Literals.Char('+')).Compile();

        Assert.True(parser.TryParse(source, out var result));
        Assert.Equal(source.Length, result.Count);
    }
}
