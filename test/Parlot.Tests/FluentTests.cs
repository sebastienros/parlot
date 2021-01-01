using Parlot.Fluent;
using System;
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
                    case "d:": return Literals.Decimal(); 
                    case "i:": return Literals.Integer(); 
                    case "s:": return Literals.String(); 
                }
                return null;
            });

            Assert.True(parser.TryParse("d:123.456", out var resultD));
            Assert.Equal((decimal)123.456, resultD);

            Assert.True(parser.TryParse("i:123", out var resultI));
            Assert.Equal((long)123, resultI);

            Assert.True(parser.TryParse("s:'123'", out var resultS));
            Assert.Equal("123", ((TextSpan)resultS).Text);
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
            Assert.Equal(identifier, Literals.Identifier().Parse(text).Text);
        }

        [Theory]
        [InlineData("-foo")]
        [InlineData("-")]
        [InlineData("  ")]
        public void IdentifierShouldNotParseInvalidIdentifiers(string text)
        {
            Assert.Null(Literals.Identifier().Parse(text).Text);
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

            Assert.Equal(text, Literals.Identifier(start, part).Parse(text).Text);
        }

        [Fact]
        public void NumbersShouldNotAcceptSignByDefault()
        {
            Assert.False(Terms.Decimal().TryParse("-123", out _));
            Assert.False(Terms.Integer().TryParse("-123", out _));
        }

        [Fact]
        public void NumbersShouldAcceptSignIfAllowed()
        {
            Assert.Equal(-123, Terms.Decimal(NumberOptions.AllowSign).Parse("-123"));
            Assert.Equal(-123, Terms.Integer(NumberOptions.AllowSign).Parse("-123"));
        }
    }
}
