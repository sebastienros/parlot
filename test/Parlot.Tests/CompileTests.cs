using Parlot.Fluent;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests
{
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
        public void ShouldCompileCyclicDeferreds()
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
        public void ShouldCompileZeroOrManys()
        {
            var parser = ZeroOrMany(Terms.Text("hello").Or(Terms.Text("world"))).Compile();

            var result = parser.Parse(" hello world hello");

            Assert.Equal(new[] { "hello", "world", "hello" }, result);
        }

        [Fact]
        public void ShouldCompileOneOrManys()
        {
            var parser = OneOrMany(Terms.Text("hello").Or(Terms.Text("world"))).Compile();

            var result = parser.Parse(" hello world hello");

            Assert.Equal(new[] { "hello", "world", "hello" }, result);
        }

        [Fact]
        public void ShouldCompileZeroOrOne()
        {
            var parser = ZeroOrOne(Terms.Text("hello")).Compile();

            Assert.Equal("hello", parser.Parse(" hello world hello"));
            Assert.Null(parser.Parse(" foo"));
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
            // This test ensures that the separator is not consumed if there is no valid net value.

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
            Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit);
            Parser<List<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Plus, Minus));
            Parser<List<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Minus));
            Parser<List<char>> WordMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Minus));
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
                    return true;
                }

                return false;
            }
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
            Assert.True(Empty().Compile().TryParse("123", out var result) && result == null);
            Assert.True(Empty(1).Compile().TryParse("123", out var r2) && r2 == 1);
        }

        [Fact]
        public void ShouldCompileEof()
        {
            Assert.True(Empty().Eof().Compile().TryParse("", out _));
            Assert.False(Empty().Eof().Compile().TryParse(" ", out _));
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
            Assert.True(Terms.Decimal().Discard<bool>().Compile().TryParse("123", out var r1) && r1 == false);
            Assert.True(Terms.Decimal().Discard<bool>(true).Compile().TryParse("123", out var r2) && r2 == true);
            Assert.False(Terms.Decimal().Discard<bool>(true).Compile().TryParse("abc", out _));
        }

        [Fact]
        public void ShouldCompileNonWhiteSpace()
        {
            Assert.Equal("a", Terms.NonWhiteSpace(includeNewLines: true).Compile().Parse(" a"));
        }

        [Fact]
        public void ShouldCompileWhiteSpace()
        {
            Assert.Equal("\n\r\v ", Literals.WhiteSpace(true).Compile().Parse("\n\r\v a"));
            Assert.Equal("  ", Literals.WhiteSpace(false).Compile().Parse("  \n\r\v a"));
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData("foo", "foo")]
        [InlineData("$_", "$_")]
        [InlineData("a-foo.", "a")]
        [InlineData("abc=3", "abc")]
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
            var evenIntegers = Literals.Integer().When(x => x % 2 == 0).Compile();

            Assert.True(evenIntegers.TryParse("1234", out var result1));
            Assert.Equal(1234, result1);

            Assert.False(evenIntegers.TryParse("1235", out var result2));
            Assert.Equal(default, result2);
        }

        [Fact]
        public void CompiledWhenShouldResetPositionWhenFalse()
        {
            var evenIntegers = ZeroOrOne(Literals.Integer().When(x => x % 2 == 0)).And(Literals.Integer()).Compile();

            Assert.True(evenIntegers.TryParse("1235", out var result1));
            Assert.Equal(1235, result1.Item2);
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

            var parser = d.Or(i).Or(s).Switch((context, result) =>
            {
                switch (result)
                {
                    case "d:": return Literals.Decimal().Then<object>(x => x);
                    case "i:": return Literals.Integer().Then<object>(x => x);
                    case "s:": return Literals.String().Then<object>(x => x);
                }
                return null;
            }).Compile();

            Assert.True(parser.TryParse("d:123.456", out var resultD));
            Assert.Equal((decimal)123.456, resultD);

            Assert.True(parser.TryParse("i:123", out var resultI));
            Assert.Equal((long)123, resultI);

            Assert.True(parser.TryParse("s:'123'", out var resultS));
            Assert.Equal("123", ((TextSpan)resultS).ToString());
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
        public void BetweenCompiledShouldresetPosition()
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
            var parser = Literals.Text("abc").Or(Literals.Text("ab")).Or(Literals.Text("a")).Compile();

            Assert.True(parser.TryParse("a", out _));
            Assert.True(parser.TryParse("ab", out _));
            Assert.True(parser.TryParse("abc", out _));
        }

        [Fact]
        public void CanCompileSubTree()
        {
            Parser<char> Dot = Literals.Char('.');
            Parser<char> Plus = Literals.Char('+');
            Parser<char> Minus = Literals.Char('-');
            Parser<char> At = Literals.Char('@');
            Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit).Compile();
            Parser<List<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Plus, Minus));
            Parser<List<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Minus));
            Parser<List<char>> WordMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Minus));
            Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

            string _email = "sebastien.ros@gmail.com";

            var parser = Email.Compile();
            var result = parser.Parse(_email);

            Assert.Equal(_email, result.ToString());
        }
    }
}
