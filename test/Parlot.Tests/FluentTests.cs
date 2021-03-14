using Parlot.Fluent;
using System.Collections.Generic;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests
{
    public class FluentTests
    {
        [Fact]
        public void WhenShouldFailParserWhenFalse()
        {
            var evenIntegers = Literals.Integer().When(x => x % 2 == 0);

            Assert.True(evenIntegers.TryParse("1234", out var result1));
            Assert.Equal(1234, result1);

            Assert.False(evenIntegers.TryParse("1235", out var result2));
            Assert.Equal(default, result2);
        }

        [Fact]
        public void WhenShouldResetPositionWhenFalse()
        {
            var evenIntegers = ZeroOrOne(Literals.Integer().When(x => x % 2 == 0)).And(Literals.Integer());

            Assert.True(evenIntegers.TryParse("1235", out var result1));
            Assert.Equal(1235, result1.Item2);
        }

        [Fact]
        public void ThenShouldConvertParser()
        {
            var evenIntegers = Literals.Integer().Then(x => x % 2);

            Assert.True(evenIntegers.TryParse("1234", out var result1));
            Assert.Equal(0, result1);

            Assert.True(evenIntegers.TryParse("1235", out var result2));
            Assert.Equal(1, result2);
        }

        [Fact]
        public void ThenShouldOnlyBeInvokedIfParserSucceeded()
        {
            var invoked = false;
            var evenIntegers = Literals.Integer().Then(x => invoked = true);

            Assert.False(evenIntegers.TryParse("abc", out var result1));
            Assert.False(invoked);

            Assert.True(evenIntegers.TryParse("1235", out var result2));
            Assert.True(invoked);
        }

        [Fact]
        public void BetweenShouldParseBetweenTwoString()
        {
            var code = Between(Terms.Text("[["), Terms.Integer(), Terms.Text("]]"));

            Assert.True(code.TryParse("[[123]]", out long result));
            Assert.Equal(123, result);

            Assert.True(code.TryParse(" [[ 123 ]] ", out result));
            Assert.Equal(123, result);

            Assert.False(code.TryParse("abc", out _));
            Assert.False(code.TryParse("[[abc", out _));
            Assert.False(code.TryParse("123", out _));
            Assert.False(code.TryParse("[[123", out _));
            Assert.False(code.TryParse("[[123]", out _));
        }

        [Fact]
        public void TextShouldResetPosition()
        {
            var code = OneOf(Terms.Text("substract"), Terms.Text("substitute"));

            Assert.False(code.TryParse("sublime", out _));
            Assert.True(code.TryParse("substract", out _));
            Assert.True(code.TryParse("substitute", out _));
        }

        [Fact]
        public void AndSkipShouldResetPosition()
        {
            var code = 
                OneOf(
                    Terms.Text("hello").AndSkip(Terms.Text("world")),
                    Terms.Text("hello").AndSkip(Terms.Text("universe"))
                    );

            Assert.False(code.TryParse("hello country", out _));
            Assert.True(code.TryParse("hello universe", out _));
            Assert.True(code.TryParse("hello world", out _));
        }

        [Fact]
        public void ParseContextShouldUseNewLines()
        {
            Assert.Equal("a", Terms.NonWhiteSpace().Parse("\n\r\v a"));
        }

        [Fact]
        public void LiteralsShouldNotSkipWhiteSpaceByDefault()
        {
            Assert.False(Literals.Char('a').TryParse(" a", out _));
            Assert.False(Literals.Decimal().TryParse(" 123", out _));
            Assert.False(Literals.String().TryParse(" 'abc'", out _));
            Assert.False(Literals.Text("abc").TryParse(" abc", out _));
        }

        [Fact]
        public void TermsShouldSkipWhiteSpaceByDefault()
        {
            Assert.True(Terms.Char('a').TryParse(" a", out _));
            Assert.True(Terms.Decimal().TryParse(" 123", out _));
            Assert.True(Terms.String().TryParse(" 'abc'", out _));
            Assert.True(Terms.Text("abc").TryParse(" abc", out _));
        }

        [Fact]
        public void CharLiteralShouldBeCaseSensitive()
        {
            Assert.True(Literals.Char('a').TryParse("a", out _));
            Assert.False(Literals.Char('a').TryParse("B", out _));
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData("abc", "abc")]
        [InlineData(" abc", "abc")]
        public void ShouldReadPatterns(string text, string expected)
        {
            Assert.Equal(expected, Terms.Pattern(c => Character.IsHexDigit(c)).Parse(text).ToString());
        }

        [Fact]
        public void ShouldReadPatternsWithSizes()
        {
            Assert.False(Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3).TryParse("ab", out _));
            Assert.Equal("abc", Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3).Parse("abc").ToString());
            Assert.Equal("abc", Terms.Pattern(c => Character.IsHexDigit(c), maxSize: 3).Parse("abcd").ToString());
            Assert.Equal("abc", Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3, maxSize: 3).Parse("abcd").ToString());
            Assert.False(Terms.Pattern(c => Character.IsHexDigit(c), minSize: 3, maxSize: 2).TryParse("ab", out _));
        }

        [Fact]
        public void PatternShouldResetPositionWhenFalse()
        {
            Assert.False(Terms.Pattern(c => c == 'a', minSize: 3)
                .And(Terms.Pattern(c => c == 'Z'))
                .TryParse("aaZZ", out _));

           Assert.True(Terms.Pattern(c => c == 'a', minSize: 3)
                .And(Terms.Pattern(c => c == 'Z'))
                .TryParse("aaaZZ", out _));                
        }

        [Theory]
        [InlineData("'a\nb' ", "a\nb")]
        [InlineData("'a\r\nb' ", "a\r\nb")]
        public void ShouldReadStringsWithLineBreaks(string text, string expected)
        {
            Assert.Equal(expected, Literals.String(StringLiteralQuotes.Single).Parse(text).ToString());
            Assert.Equal(expected, Literals.String(StringLiteralQuotes.SingleOrDouble).Parse(text).ToString());
        }

        [Fact]
        public void OrShouldReturnOneOf()
        {
            var a = Literals.Char('a');
            var b = Literals.Char('b');
            var c = Literals.Char('c');

            var o2 = a.Or(b);
            var o3 = a.Or(b).Or(c);

            Assert.IsType<OneOf<char>>(o2);
            Assert.True(o2.TryParse("a", out _));
            Assert.True(o2.TryParse("b", out _));
            Assert.False(o2.TryParse("c", out _));

            Assert.IsType<OneOf<char>>(o3);
            Assert.True(o3.TryParse("a", out _));
            Assert.True(o3.TryParse("b", out _));
            Assert.True(o3.TryParse("c", out _));
            Assert.False(o3.TryParse("d", out _));
        }

        [Fact]
        public void OrShouldReturnOneOfCommonType()
        {
            var a = Literals.Char('a');
            var b = Literals.Decimal();

            var o2 = a.Or<char, decimal, object>(b);

            Assert.IsType<OneOf<char, decimal, object>>(o2);
            Assert.True(o2.TryParse("a", out var c) && (char)c == 'a');
            Assert.True(o2.TryParse("1", out var d) && (decimal)d == 1);
        }

        [Fact]
        public void AndShouldReturnSequences()
        {
            var a = Literals.Char('a');

            var s2 = a.And(a);
            var s3 = s2.And(a);
            var s4 = s3.And(a);
            var s5 = s4.And(a);
            var s6 = s5.And(a);
            var s7 = s6.And(a);

            Assert.IsType<Sequence<char, char>>(s2);
            Assert.False(s2.TryParse("a", out _));
            Assert.True(s2.TryParse("aab", out _));

            Assert.IsType<Sequence<char, char, char>>(s3);
            Assert.False(s3.TryParse("aa", out _));
            Assert.True(s3.TryParse("aaab", out _));

            Assert.IsType<Sequence<char, char, char, char>>(s4);
            Assert.False(s4.TryParse("aaa", out _));
            Assert.True(s4.TryParse("aaaab", out _));

            Assert.IsType<Sequence<char, char, char, char, char>>(s5);
            Assert.False(s5.TryParse("aaaa", out _));
            Assert.True(s5.TryParse("aaaaab", out _));

            Assert.IsType<Sequence<char, char, char, char, char, char>>(s6);
            Assert.False(s6.TryParse("aaaaa", out _));
            Assert.True(s6.TryParse("aaaaaab", out _));

            Assert.IsType<Sequence<char, char, char, char, char, char, char>>(s7);
            Assert.False(s7.TryParse("aaaaaa", out _));
            Assert.True(s7.TryParse("aaaaaaab", out _));
        }

        [Fact]
        public void SwitchShouldProvidePreviousResult()
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
            });

            Assert.True(parser.TryParse("d:123.456", out var resultD));
            Assert.Equal((decimal)123.456, resultD);

            Assert.True(parser.TryParse("i:123", out var resultI));
            Assert.Equal((long)123, resultI);

            Assert.True(parser.TryParse("s:'123'", out var resultS));
            Assert.Equal("123", ((TextSpan)resultS).ToString());
        }

        [Fact]
        public void SwitchShouldReturnCommonType()
        {
            var d = Literals.Text("d:");
            var i = Literals.Text("i:");
            var s = Literals.Text("s:");

            var parser = d.Or(i).Or(s).Switch((context, result) =>
            {
                switch (result)
                {
                    case "d:": return Literals.Decimal().Then(x => x.ToString());
                    case "i:": return Literals.Integer().Then(x => x.ToString());
                    case "s:": return Literals.String().Then(x => x.ToString());
                }
                return null;
            });

            Assert.True(parser.TryParse("d:123.456", out var resultD));
            Assert.Equal("123.456", resultD);

            Assert.True(parser.TryParse("i:123", out var resultI));
            Assert.Equal("123", resultI);

            Assert.True(parser.TryParse("s:'123'", out var resultS));
            Assert.Equal("123", resultS);
        }

        [Theory]
        [InlineData("a", "a")]
        [InlineData("foo", "foo")]
        [InlineData("$_", "$_")]
        [InlineData("a-foo.", "a")]
        [InlineData("abc=3", "abc")]
        public void IdentifierShouldParseValidIdentifiers(string text, string identifier)
        {
            Assert.Equal(identifier, Literals.Identifier().Parse(text).ToString());
        }

        [Theory]
        [InlineData("-foo")]
        [InlineData("-")]
        [InlineData("  ")]
        public void IdentifierShouldNotParseInvalidIdentifiers(string text)
        {
            Assert.False(Literals.Identifier().TryParse(text, out _));
        }

        [Theory]
        [InlineData("-foo")]
        [InlineData("/foo")]
        [InlineData("foo@asd")]
        [InlineData("foo*")]
        public void IdentifierShouldAcceptExtraChars(string text)
        {
            static bool start(char c) => c == '-' || c == '/';
            static bool part(char c) => c == '@' || c == '*';

            Assert.Equal(text, Literals.Identifier(start, part).Parse(text).ToString());
        }

        [Fact]
        public void NumbersShouldNotAcceptSignByDefault()
        {
            Assert.False(Terms.Decimal().TryParse("-123", out _));
            Assert.False(Terms.Integer().TryParse("-123", out _));
            Assert.False(Terms.Decimal().TryParse("+123", out _));
            Assert.False(Terms.Integer().TryParse("+123", out _));
        }

        [Fact]
        public void NumbersShouldAcceptSignIfAllowed()
        {
            Assert.Equal(-123, Terms.Decimal(NumberOptions.AllowSign).Parse("-123"));
            Assert.Equal(-123, Terms.Integer(NumberOptions.AllowSign).Parse("-123"));
            Assert.Equal(123, Terms.Decimal(NumberOptions.AllowSign).Parse("+123"));
            Assert.Equal(123, Terms.Integer(NumberOptions.AllowSign).Parse("+123"));
        }

        [Fact]
        public void OneOfShouldRestorePosition()
        {
            var choice = OneOf(
                Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('c')).And(Literals.Char('d')),
                Literals.Char('a').And(Literals.Char('b')).And(Literals.Char('e')).And(Literals.Char('d'))
                ).Then(x => x.Item1.ToString() + x.Item2.ToString() + x.Item3.ToString() + x.Item4.ToString());

            Assert.Equal("abcd", choice.Parse("abcd"));
            Assert.Equal("abed", choice.Parse("abed"));
        }

        [Fact]
        public void NonWhiteSpaceShouldStopAtSpaceOrEof()
        {
            Assert.Equal("a", Terms.NonWhiteSpace().Parse(" a"));
            Assert.Equal("a", Terms.NonWhiteSpace().Parse(" a "));
            Assert.Equal("a", Terms.NonWhiteSpace().Parse(" a b"));
            Assert.Equal("a", Terms.NonWhiteSpace().Parse("a b"));
            Assert.Equal("abc", Terms.NonWhiteSpace().Parse("abc b"));
            Assert.Equal("abc", Terms.NonWhiteSpace().Parse("abc\nb"));
            Assert.Equal("abc\nb", Terms.NonWhiteSpace(true).Parse("abc\nb"));
            Assert.Equal("abc", Terms.NonWhiteSpace().Parse("abc"));

            Assert.False(Terms.NonWhiteSpace().TryParse("", out _));
            Assert.False(Terms.NonWhiteSpace().TryParse(" ", out _));
        }

        [Fact]
        public void ShouldParseWhiteSpace()
        {
            Assert.Equal("\n\r\v ", Literals.WhiteSpace(true).Parse("\n\r\v a"));
            Assert.Equal("  ", Literals.WhiteSpace(false).Parse("  \n\r\v a"));
        }

        [Fact]
        public void ShouldCapture()
        {
            Assert.Equal("../foo/bar", Capture(Literals.Text("..").AndSkip(OneOrMany(Literals.Char('/').AndSkip(Terms.Identifier())))).Parse("../foo/bar").ToString());
        }

        [Fact]
        public void ShouldParseEmails()
        {
            Parser<char> Dot = Literals.Char('.');
            Parser<char> Plus = Literals.Char('+');
            Parser<char> Minus = Literals.Char('-');
            Parser<char> At = Literals.Char('@');
            Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit);
            Parser<List<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Discard<char>(), Dot, Plus, Minus));
            Parser<List<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Discard<char>(), Dot, Minus));
            Parser<List<char>> WordMinus = OneOrMany(OneOf(WordChar.Discard<char>(), Minus));
            Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

            string _email = "sebastien.ros@gmail.com";

            Assert.True(Email.TryParse(_email, out var result));
            Assert.Equal(_email, result.ToString());
        }

        [Fact]
        public void ShouldParseEof()
        {
            Assert.True(Empty<object>().Eof().TryParse("", out _));
            Assert.False(Empty<object>().Eof().TryParse(" ", out _));
            Assert.True(Terms.Decimal().Eof().TryParse("123", out var result) && result == 123);
            Assert.False(Terms.Decimal().Eof().TryParse("123 ", out _));
        }

        [Fact]
        public void EmptyShouldAlwaysSucceed()
        {
            Assert.True(Empty<object>().TryParse("123", out var result) && result == null);
            Assert.True(Empty(1).TryParse("123", out var r2) && r2 == 1);
        }

        [Fact]
        public void NotShouldNegateParser()
        {
            Assert.False(Not(Terms.Decimal()).TryParse("123", out _));
            Assert.True(Not(Terms.Decimal()).TryParse("Text", out _));
        }

        [Fact]
        public void DiscardShouldReplaceValue()
        {
            Assert.True(Terms.Decimal().Discard<bool>().TryParse("123", out var r1) && r1 == false);
            Assert.True(Terms.Decimal().Discard<bool>(true).TryParse("123", out var r2) && r2 == true);
            Assert.False(Terms.Decimal().Discard<bool>(true).TryParse("abc", out _));
        }
    }
}
