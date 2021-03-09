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
        public void ShouldCompileBetweens()
        {
            var parser = Between(Terms.Text("hello"), Terms.Text("world"), Terms.Text("hello")).Compile();

            var result = parser.Parse(" hello world hello");

            Assert.Equal("world", result);
        }

        [Fact]
        public void ShouldCompileSeparated()
        {
            var parser = Separated(Terms.Char(','), Terms.Decimal()).Compile();

            var result = parser.Parse("1, 2,3");

            Assert.Equal(1, result[0]);
            Assert.Equal(2, result[1]);
            Assert.Equal(3, result[2]);
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
            var code =
                OneOf(
                    Terms.Text("hello").AndSkip(Terms.Text("world")),
                    Terms.Text("hello").AndSkip(Terms.Text("universe"))
                    ).Compile();

            Assert.False(code.TryParse("hello country", out var result) && result == "hello");
            Assert.True(code.TryParse("hello universe", out result) && result == "hello");
            Assert.True(code.TryParse("hello world", out result) && result == "hello");
        }

        [Fact]
        public void ShouldCompileEmpty()
        {
            Assert.True(Empty<object>().Compile().TryParse("123", out var result) && result == null);
            Assert.True(Empty(1).Compile().TryParse("123", out var r2) && r2 == 1);
        }

    }
}
