using Parlot.SourceGenerator.Tests;
using Xunit;

namespace Parlot.SourceGenerator.Tests
{
    public class SourceableParsersTests
    {
        [Fact]
        public void TermsText_Works()
        {
            var p = Grammars.TermsTextParser();
            Assert.Equal("hello", p.Parse("hello"));
            Assert.Null(p.Parse("world"));
        }

        [Fact]
        public void TermsChar_Works()
        {
            var p = Grammars.TermsCharParser();
            Assert.Equal('h', p.Parse("h"));
            Assert.Equal(default(char), p.Parse("x"));
        }

        [Fact]
        public void TermsString_Works()
        {
            var p = Grammars.TermsStringParser();
            var span = p.Parse("\"hello\"");
            Assert.Equal("hello", span.ToString());
        }

        [Fact]
        public void TermsPattern_Works()
        {
            var p = Grammars.TermsPatternParser();
            var span = p.Parse("abc123");
            Assert.Equal("abc", span.ToString());
            Assert.False(p.TryParse("123", out TextSpan _));
        }

        [Fact]
        public void TermsIdentifier_Works()
        {
            var p = Grammars.TermsIdentifierParser();
            var span = p.Parse("  foo123 ");
            Assert.Equal("foo123", span.ToString());
            Assert.False(p.TryParse("  123foo", out TextSpan _));
        }

        [Fact]
        public void TermsWhiteSpace_Works()
        {
            var p = Grammars.TermsWhiteSpaceParser();
            var span = p.Parse("   \tfoo");
            Assert.Equal("   \t", span.ToString());
            Assert.False(p.TryParse("foo", out TextSpan _));
        }

        [Fact]
        public void TermsNonWhiteSpace_Works()
        {
            var p = Grammars.TermsNonWhiteSpaceParser();
            var span = p.Parse("  hello world");
            Assert.Equal("hello", span.ToString());
            Assert.Equal("world", p.Parse("  world").ToString());
        }

        [Fact]
        public void TermsDecimal_Works()
        {
            var p = Grammars.TermsDecimalParser();
            Assert.Equal(123m, p.Parse("123"));
            Assert.Equal(-45.67m, p.Parse("  -45.67"));
            Assert.False(p.TryParse("abc", out decimal _));
        }

        [Fact]
        public void TermsKeyword_Works()
        {
            var p = Grammars.TermsKeywordParser();
            Assert.Equal("if", p.Parse("if "));
            Assert.Null(p.Parse("ifx"));
        }

        [Fact]
        public void LiteralsText_Works()
        {
            var p = Grammars.LiteralsTextParser();
            Assert.Equal("hello", p.Parse("hello"));
            Assert.False(p.TryParse(" hello", out string _));
        }

        [Fact]
        public void LiteralsChar_Works()
        {
            var p = Grammars.LiteralsCharParser();
            Assert.Equal('h', p.Parse("hello"));
            Assert.False(p.TryParse("x", out char _));
        }

        [Fact]
        public void LiteralsWhiteSpace_Works()
        {
            var p = Grammars.LiteralsWhiteSpaceParser();
            var span = p.Parse("   foo");
            Assert.Equal("   ", span.ToString());
            Assert.False(p.TryParse("foo", out TextSpan _));
        }

        [Fact]
        public void LiteralsNonWhiteSpace_Works()
        {
            var p = Grammars.LiteralsNonWhiteSpaceParser();
            var span = p.Parse("hello world");
            Assert.Equal("hello", span.ToString());
            Assert.False(p.TryParse("   ", out TextSpan _));
        }

        [Fact]
        public void LiteralsDecimal_Works()
        {
            var p = Grammars.LiteralsDecimalParser();
            Assert.Equal(123m, p.Parse("123"));
            Assert.Equal(-45.67m, p.Parse("-45.67"));
            Assert.False(p.TryParse(" abc", out decimal _));
        }

        [Fact]
        public void LiteralsKeyword_Works()
        {
            var p = Grammars.LiteralsKeywordParser();
            Assert.Equal("if", p.Parse("if"));
            Assert.False(p.TryParse("ifx", out string _));
        }

        [Fact]
        public void SequenceTextChar_Works()
        {
            var p = Grammars.SequenceTextCharParser();
            var (text, ch) = p.Parse("hi!");
            Assert.Equal("hi", text);
            Assert.Equal('!', ch);
            Assert.False(p.TryParse("hi?", out _));
        }

        [Fact]
        public void SkipAnd_Works()
        {
            var p = Grammars.SkipAndParser();
            Assert.Equal('!', p.Parse("hi!"));
            Assert.False(p.TryParse("hi?", out char _));
        }

        [Fact]
        public void AndSkip_Works()
        {
            var p = Grammars.AndSkipParser();
            Assert.Equal('!', p.Parse("!hi"));
            Assert.False(p.TryParse("!by", out char _));
        }

        [Fact]
        public void OptionalText_Works()
        {
            var p = Grammars.OptionalTextParser();
            var some = p.Parse("hi");
            Assert.True(some.HasValue);
            Assert.Equal("hi", some.Value);

            var none = p.Parse("hello");
            Assert.False(none.HasValue);
        }

        [Fact]
        public void ZeroOrManyChar_Works()
        {
            var p = Grammars.ZeroOrManyCharParser();
            var list = p.Parse("aaab");
            Assert.Equal(new[] { 'a', 'a', 'a' }, list);
            var empty = p.Parse("bbb");
            Assert.Empty(empty);
        }

        [Fact]
        public void ZeroOrOneChar_Works()
        {
            var p = Grammars.ZeroOrOneCharParser();
            Assert.Equal('a', p.Parse("a"));
            Assert.Equal('x', p.Parse("b"));
        }

        [Fact]
        public void EofText_Works()
        {
            var p = Grammars.EofTextParser();
            Assert.Equal("end", p.Parse("end"));
            Assert.False(p.TryParse("end!", out string _));
        }

        [Fact]
        public void CaptureChar_Works()
        {
            var p = Grammars.CaptureCharParser();
            var span = p.Parse("z");
            Assert.Equal("z", span.ToString());
            Assert.False(p.TryParse("a", out TextSpan _));
        }

        [Fact]
        public void OneOfChar_Works()
        {
            var p = Grammars.OneOfCharParser();
            Assert.Equal('a', p.Parse("a"));
            Assert.Equal('b', p.Parse("b"));
            Assert.False(p.TryParse("c", out char _));
        }

        [Fact]
        public void BetweenParensIdentifier_Works()
        {
            var p = Grammars.BetweenParensIdentifierParser();
            var span = p.Parse("(foo)");
            Assert.Equal("foo", span.ToString());
            Assert.False(p.TryParse("foo", out TextSpan _));
        }

        [Fact]
        public void SeparatedDecimals_Works()
        {
            var p = Grammars.SeparatedDecimalsParser();
            var list = p.Parse("1,2,3");
            Assert.Equal(new decimal[] { 1m, 2m, 3m }, list);
            var empty = p.Parse("abc");
            Assert.Empty(empty);
        }

        [Fact]
        public void UnaryNegateDecimal_Works()
        {
            var p = Grammars.UnaryNegateDecimalParser();
            Assert.Equal(-1m, p.Parse("-1"));
            Assert.Equal(2m, p.Parse("2"));
            Assert.False(p.TryParse("abc", out decimal _));
        }

        [Fact]
        public void LeftAssociativeAddition_Works()
        {
            var p = Grammars.LeftAssociativeAdditionParser();
            Assert.Equal(6m, p.Parse("1+2+3"));
            Assert.Equal(1m, p.Parse("1"));
        }

        [Fact]
        public void NotXChar_Works()
        {
            var p = Grammars.NotXCharParser();
            Assert.Equal(default(char), p.Parse("a"));
            Assert.False(p.TryParse("x", out char _));
        }

        [Fact]
        public void WhenNotFollowedByHelloBang_Works()
        {
            var p = Grammars.WhenNotFollowedByHelloBangParser();
            Assert.Equal("hello", p.Parse("hello"));
            Assert.False(p.TryParse("hello!", out string _));
        }

        [Fact]
        public void WhenFollowedByHelloBang_Works()
        {
            var p = Grammars.WhenFollowedByHelloBangParser();
            Assert.Equal("hello", p.Parse("hello!")); // consumes '!'
            Assert.False(p.TryParse("hello", out string _));
        }

        // Additional parser tests will be enabled incrementally
    }
}
