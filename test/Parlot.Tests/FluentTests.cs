using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Xunit;

using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class FluentTests
{
    [Fact]
    public void WhenShouldFailParserWhenFalse()
    {
        var evenIntegers = Literals.Integer().When((c, x) => x % 2 == 0);

        Assert.True(evenIntegers.TryParse("1234", out var result1));
        Assert.Equal(1234, result1);

        Assert.False(evenIntegers.TryParse("1235", out var result2));
        Assert.Equal(default, result2);
    }

    [Fact]
    public void IfShouldNotInvokeParserWhenFalse()
    {
        bool invoked = false;

#pragma warning disable CS0618 // Type or member is obsolete
        var evenState = If(predicate: (context, x) => x % 2 == 0, state: 0, parser: Literals.Integer().Then(x => invoked = true));
        var oddState = If(predicate: (context, x) => x % 2 == 0, state: 1, parser: Literals.Integer().Then(x => invoked = true));
#pragma warning restore CS0618 // Type or member is obsolete

        Assert.False(oddState.TryParse("1234", out var result1));
        Assert.False(invoked);

        Assert.True(evenState.TryParse("1234", out var result2));
        Assert.True(invoked);
    }

    [Fact]
    public void WhenShouldResetPositionWhenFalse()
    {
        var evenIntegers = ZeroOrOne(Literals.Integer().When((c, x) => x % 2 == 0)).And(Literals.Integer());

        Assert.True(evenIntegers.TryParse("1235", out var result1));
        Assert.Equal(1235, result1.Item2);
    }

    [Fact]
    public void ShouldCast()
    {
        var parser = Literals.Integer().Then<decimal>();

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);
    }

    [Fact]
    public void ShouldReturnElse()
    {
        var parser = Literals.Integer().Then<decimal>().Else(0);

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Equal(0, result2);
    }

    [Fact]
    public void ShouldReturnElseFromFunction()
    {
        var parser = Literals.Integer().Then<decimal>().Else(context => -1);

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Equal(-1, result2);
    }

    [Fact]
    public void ElseFunctionShouldReceiveContext()
    {
        var parser = Literals.Integer().Then<int>().Else(context => context.Scanner.Cursor.Position.Offset);

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        // When parser fails, it should return the current position (which is 0 before whitespace is skipped)
        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Equal(0, result2);
    }

    [Fact]
    public void ElseFunctionWithNullableValue()
    {
        var parser = Literals.Integer().Then<long?>(x => x).Else(context => (long?)null);

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Null(result2);
    }

    [Fact]
    public void ShouldThenElse()
    {
        var parser = Literals.Integer().ThenElse<long?>(x => x, null);

        Assert.True(parser.TryParse("123", out var result1));
        Assert.Equal(123, result1);

        Assert.True(parser.TryParse(" 123", out var result2));
        Assert.Null(result2);
    }

    [Fact]
    public void IntegerShouldResetPositionWhenItFails()
    {
        var parser = OneOf(Terms.Integer(NumberOptions.AllowLeadingSign).Then(x => "a"), Literals.Text("+").Then(x => "b"));

        // The + sign will advance the first parser and should reset the position for the second to read it successfully

        Assert.True(parser.TryParse("+abc", out var result1));
        Assert.Equal("b", result1);
    }

    [Fact]
    public void DecimalShouldResetPositionWhenItFails()
    {
        var parser = OneOf(Terms.Decimal(NumberOptions.AllowLeadingSign).Then(x => "a"), Literals.Text("+").Then(x => "b"));

        // The + sign will advance the first parser and should reset the position for the second to read it successfully

        Assert.True(parser.TryParse("+abc", out var result1));
        Assert.Equal("b", result1);
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
        var code = OneOf(Terms.Text("subtract"), Terms.Text("substitute"));

        Assert.False(code.TryParse("sublime", out _));
        Assert.True(code.TryParse("subtract", out _));
        Assert.True(code.TryParse("substitute", out _));
    }

    [Fact]
    public void TextWithWhiteSpaceShouldResetPosition()
    {
        var code = OneOf(Terms.Text("a"), Literals.Text(" b"));

        Assert.True(code.TryParse(" b", out _));
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
    public void SkipAndShouldResetPosition()
    {
        var code =
            OneOf(
                Terms.Text("hello").SkipAnd(Terms.Text("world")),
                Terms.Text("hello").AndSkip(Terms.Text("universe"))
            );

        Assert.False(code.TryParse("hello country", out _));
        Assert.True(code.TryParse("hello universe", out _));
        Assert.True(code.TryParse("hello world", out _));
    }


    [Fact]
    public void ShouldSkipSequences()
    {
        var parser = Terms.Char('a').And(Terms.Char('b')).AndSkip(Terms.Char('c')).And(Terms.Char('d'));

        Assert.True(parser.TryParse("abcd", out var result1));
        Assert.Equal("abd", result1.Item1.ToString() + result1.Item2 + result1.Item3);
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
                case "d:": return Literals.Decimal().Then(x => x.ToString(CultureInfo.InvariantCulture));
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

    [Fact]
    public void SelectShouldPickParserUsingRuntimeLogic()
    {
        var allowWhiteSpace = true;
        var parser = Select<long>(_ => allowWhiteSpace ? Terms.Integer() : Literals.Integer());

        Assert.True(parser.TryParse(" 42", out var result1));
        Assert.Equal(42, result1);

        allowWhiteSpace = false;

        Assert.True(parser.TryParse("42", out var result2));
        Assert.Equal(42, result2);

        Assert.False(parser.TryParse(" 42", out _));
    }

    [Fact]
    public void SelectShouldFailWhenSelectorReturnsNull()
    {
        var parser = Select<long>(_ => null!);

        Assert.False(parser.TryParse("123", out _));
    }

    [Fact]
    public void SelectShouldHonorConcreteParseContext()
    {
        var parser = Select<CustomParseContext, string>(context => context.PreferYes ? Literals.Text("yes") : Literals.Text("no"));

        var yesContext = new CustomParseContext(new Scanner("yes")) { PreferYes = true };
        Assert.True(parser.TryParse(yesContext, out var yes, out _));
        Assert.Equal("yes", yes);

        var noContext = new CustomParseContext(new Scanner("no")) { PreferYes = false };
        Assert.True(parser.TryParse(noContext, out var no, out _));
        Assert.Equal("no", no);
    }

    private sealed class CustomParseContext : ParseContext
    {
        public CustomParseContext(Scanner scanner) : base(scanner)
        {
        }

        public bool PreferYes { get; set; }
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
    public void IntegersShouldAcceptSignByDefault()
    {
        Assert.True(Terms.Integer().TryParse("-123", out _));
        Assert.True(Terms.Integer().TryParse("+123", out _));
    }

    [Fact]
    public void DecimalsShouldAcceptSignByDefault()
    {
        Assert.True(Terms.Decimal().TryParse("-123", out _));
        Assert.True(Terms.Decimal().TryParse("+123", out _));
    }

    [Fact]
    public void NumbersShouldAcceptSignIfAllowed()
    {
        Assert.Equal(-123, Terms.Decimal(NumberOptions.AllowLeadingSign).Parse("-123"));
        Assert.Equal(-123, Terms.Integer(NumberOptions.AllowLeadingSign).Parse("-123"));
        Assert.Equal(123, Terms.Decimal(NumberOptions.AllowLeadingSign).Parse("+123"));
        Assert.Equal(123, Terms.Integer(NumberOptions.AllowLeadingSign).Parse("+123"));
    }

    [Fact]
    public void NumbersShouldNotAcceptSignIfNotAllowed()
    {
        Assert.False(Terms.Decimal(NumberOptions.None).TryParse("-123", out _));
        Assert.False(Terms.Integer(NumberOptions.None).TryParse("-123", out _));
        Assert.False(Terms.Decimal(NumberOptions.None).TryParse("+123", out _));
        Assert.False(Terms.Integer(NumberOptions.None).TryParse("+123", out _));
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
        Assert.Equal("abc", Terms.NonWhiteSpace(includeNewLines: true).Parse("abc\nb"));
        Assert.Equal("abc\nb", Terms.NonWhiteSpace(includeNewLines: false).Parse("abc\nb"));
        Assert.Equal("abc", Terms.NonWhiteSpace().Parse("abc"));

        Assert.False(Terms.NonWhiteSpace().TryParse("", out _));
        Assert.False(Terms.NonWhiteSpace().TryParse(" ", out _));
    }

    [Fact]
    public void ShouldParseWhiteSpace()
    {
        Assert.Equal("\n\r\v\f ", Literals.WhiteSpace(true).Parse("\n\r\v\f a"));
        Assert.Equal("  \f", Literals.WhiteSpace(false).Parse("  \f\n\r\v a"));
    }

    [Fact]
    public void WhiteSpaceShouldFailOnEmpty()
    {
        Assert.True(Literals.WhiteSpace().TryParse(" ", out _));
        Assert.False(Literals.WhiteSpace().TryParse("", out _));
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
        Parser<char> WordChar = Literals.Pattern(char.IsLetterOrDigit).Then<char>(x => x.Span[0]);
        Parser<IReadOnlyList<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar, Dot, Plus, Minus));
        Parser<IReadOnlyList<char>> WordDotMinus = OneOrMany(OneOf(WordChar, Dot, Minus));
        Parser<IReadOnlyList<char>> WordMinus = OneOrMany(OneOf(WordChar, Minus));
        Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

        string _email = "sebastien.ros@gmail.com";

        Assert.True(Email.TryParse(_email, out var result));
        Assert.Equal(_email, result.ToString());
    }

    [Fact]
    public void ShouldParseEmailsWithAnyOf()
    {
        var letterOrDigitChars = "01234567890abcdefghijklmnopqrstuvwxyz";

        var Dot = Literals.AnyOf(".");
        var LetterOrDigit = Literals.AnyOf(letterOrDigitChars);
        var LetterOrDigitDotPlusMinus = Literals.AnyOf(letterOrDigitChars + ".+-");
        var LetterOrDigitDotMinus = Literals.AnyOf(letterOrDigitChars + ".-");
        var LetterOrDigitMinus = Literals.AnyOf(letterOrDigitChars + "-");

        Parser<char> At = Literals.Char('@');
        Parser<TextSpan> Email = Capture(LetterOrDigitDotPlusMinus.And(At).And(LetterOrDigitMinus).And(Dot).And(LetterOrDigitDotMinus));

        string _email = "sebastien.ros@gmail.com";

        Assert.True(Email.TryParse(_email, out var result));
        Assert.Equal(_email, result.ToString());
    }

    [Fact]
    public void ShouldParseEof()
    {
        Assert.True(Always<object>().Eof().TryParse("", out _));
        Assert.False(Always<object>().Eof().TryParse(" ", out _));
        Assert.True(Terms.Decimal().Eof().TryParse("123", out var result) && result == 123);
        Assert.False(Terms.Decimal().Eof().TryParse("123 ", out _));
    }

    [Fact]
    public void EmptyShouldAlwaysSucceed()
    {
        Assert.True(Always<object>().TryParse("123", out var result) && result == null);
        Assert.True(Always(1).TryParse("123", out var r2) && r2 == 1);
    }


    [Fact]
    public void FailShouldFail()
    {
        Assert.False(Fail<object>().TryParse("123", out var result));
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
#pragma warning disable CS0618 // Type or member is obsolete
        Assert.True(Terms.Decimal().Discard<bool>().TryParse("123", out var r1) && r1 == false);
        Assert.True(Terms.Decimal().Discard<bool>(true).TryParse("123", out var r2) && r2 == true);
        Assert.False(Terms.Decimal().Discard<bool>(true).TryParse("abc", out _));
#pragma warning restore CS0618 // Type or member is obsolete
    
        Assert.True(Terms.Decimal().Then<int>().TryParse("123", out var t1) && t1 == 123);
        Assert.True(Terms.Decimal().Then(true).TryParse("123", out var t2) && t2 == true);
        Assert.False(Terms.Decimal().Then(true).TryParse("abc", out _));
    }

    [Fact]
    public void ErrorShouldThrowIfParserSucceeds()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").TryParse("a", out _, out var error));
        Assert.Equal("'a' was not expected", error.Message);

        Assert.False(Literals.Char('a').Error<int>("'a' was not expected").TryParse("a", out _, out error));
        Assert.Equal("'a' was not expected", error.Message);
    }

    [Fact]
    public void ErrorShouldReturnFalseThrowIfParserFails()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").TryParse("b", out _, out var error));
        Assert.Null(error);

        Assert.False(Literals.Char('a').Error<int>("'a' was not expected").TryParse("b", out _, out error));
        Assert.Null(error);
    }

    [Fact]
    public void ErrorShouldThrow()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").TryParse("a", out _, out var error));
        Assert.Equal("'a' was not expected", error.Message);
    }

    [Fact]
    public void ErrorShouldResetPosition()
    {
        Assert.False(Literals.Char('a').Error("'a' was not expected").TryParse("a", out _, out var error));
        Assert.Equal("'a' was not expected", error.Message);
    }

    [Fact]
    public void ElseErrorShouldThrowIfParserFails()
    {
        Assert.False(Literals.Char('a').ElseError("'a' was expected").TryParse("b", out _, out var error));
        Assert.Equal("'a' was expected", error.Message);
    }

    [Fact]
    public void ElseErrorShouldFlowResultIfParserSucceeds()
    {
        Assert.True(Literals.Char('a').ElseError("'a' was expected").TryParse("a", out var result));
        Assert.Equal('a', result);
    }

    [Fact]
    public void TextBeforeShouldReturnAllCharBeforeDelimiter()
    {
        Assert.False(AnyCharBefore(Literals.Char('a')).TryParse("", out _));
        Assert.True(AnyCharBefore(Literals.Char('a'), canBeEmpty: true).TryParse("", out var result1));

        Assert.True(AnyCharBefore(Literals.Char('a')).TryParse("hello", out var result2));
        Assert.Equal("hello", result2);
        Assert.True(AnyCharBefore(Literals.Char('a'), canBeEmpty: false).TryParse("hello", out _));
        Assert.False(AnyCharBefore(Literals.Char('a'), failOnEof: true).TryParse("hello", out _));
    }

    [Fact]
    public void TextBeforeShouldStopAtDelimiter()
    {
        Assert.True(AnyCharBefore(Literals.Char('a')).TryParse("hellao", out var result1));
        Assert.Equal("hell", result1);
    }

    [Fact]
    public void TextBeforeShouldNotConsumeDelimiter()
    {
        Assert.True(AnyCharBefore(Literals.Char('a')).And(Literals.Char('a')).TryParse("hellao", out _));
        Assert.False(AnyCharBefore(Literals.Char('a'), consumeDelimiter: true).And(Literals.Char('a')).TryParse("hellao", out _));
    }

    [Fact]
    public void TextBeforeShouldBeValidAtEof()
    {
        Assert.True(AnyCharBefore(Literals.Char('a')).TryParse("hella", out var result1));
        Assert.Equal("hell", result1);
    }

    [Fact]
    public void BetweenShouldResetPosition()
    {
        Assert.True(Between(Terms.Char('['), Terms.Text("abcd"), Terms.Char(']')).Then(x => x.ToString()).Or(Literals.Text(" [abc")).TryParse(" [abc]", out var result1));
        Assert.Equal(" [abc", result1);
    }

    [Fact]
    public void SeparatedShouldSplit()
    {
        var parser = Separated(Terms.Char(','), Terms.Decimal());

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
    public void SeparatedShouldNotBeConsumedIfNotFollowedByValue()
    {
        // This test ensures that the separator is not consumed if there is no valid net value.

        var parser = Separated(Terms.Char(','), Terms.Decimal()).AndSkip(Terms.Char(',')).And(Terms.Identifier()).Then(x => true);

        Assert.False(parser.Parse("1"));
        Assert.False(parser.Parse("1,"));
        Assert.True(parser.Parse("1,x"));
    }

    [Fact]
    public void ShouldSkipWhiteSpace()
    {
        var parser = SkipWhiteSpace(Literals.Text("abc"));

        Assert.Null(parser.Parse(""));
        Assert.True(parser.TryParse("abc", out var result1));
        Assert.Equal("abc", result1);

        Assert.True(parser.TryParse("  abc", out var result2));
        Assert.Equal("abc", result2);
    }

    [Fact]
    public void SkipWhiteSpaceShouldResetPosition()
    {
        var parser = SkipWhiteSpace(Literals.Text("abc")).Or(Literals.Text(" ab"));

        Assert.True(parser.TryParse(" ab", out var result1));
        Assert.Equal(" ab", result1);
    }

    [Fact]
    public void OneOfShouldNotFailWithLookupConflicts()
    {
        var parser = Literals.Text("abc").Or(Literals.Text("ab")).Or(Literals.Text("a"));

        Assert.True(parser.TryParse("a", out _));
        Assert.True(parser.TryParse("ab", out _));
        Assert.True(parser.TryParse("abc", out _));
    }

    [Fact]
    public void OneOfShouldHandleSkipWhiteSpaceMix()
    {
        var parser = Literals.Text("a").Or(Terms.Text("b"));

        Assert.True(parser.TryParse("a", out _));
        Assert.True(parser.TryParse("b", out _));
        Assert.False(parser.TryParse(" a", out _));
        Assert.True(parser.TryParse(" b", out _));
    }

    [Fact]
    public void OneOfShouldHandleParsedWhiteSpace()
    {
        var parser = Literals.Text("a").Or(AnyCharBefore(Literals.Text("c"), false, true).Then(x => x.ToString()));

        Assert.True(parser.TryParse("a", out _));
        Assert.False(parser.TryParse("b", out _));
        Assert.False(parser.TryParse(" a", out _));
        Assert.True(parser.TryParse("\rcde", out _));
    }

    [Fact]
    public void OneOfShouldHandleContextualWhiteSpace()
    {
        var parser = Terms.Text("a").Or(Terms.Text("b"));

        Assert.True(parser.TryParse(new ParseContext(new Scanner("\rb")), out _, out _));
        Assert.True(parser.TryParse(new ParseContext(new Scanner(" b")), out _, out _));
        Assert.False(parser.TryParse(new ParseContext(new Scanner("\rb"), useNewLines: true), out _, out _));
        Assert.True(parser.TryParse(new ParseContext(new Scanner(" b"), useNewLines: true), out _, out _));
    }

    [Fact]
    public void SkipWhiteSpaceShouldResponseParseContextUseNewLines()
    {
        // Default behavior, newlines are skipped like any other space. The grammar is not "New Line Aware"

        Assert.True(
            SkipWhiteSpace(Literals.Text("ab"))
            .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: false),
            out var _, out var _));

        // Here newlines are not skipped

        Assert.False(
            SkipWhiteSpace(Literals.Text("ab"))
            .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: true),
            out var _, out var _));

        // Here newlines are not skipped, and the grammar reads them explicitly

        Assert.True(
            SkipWhiteSpace(Literals.WhiteSpace(includeNewLines: true).SkipAnd(Literals.Text("ab")))
            .TryParse(new ParseContext(new Scanner(" \nab"), useNewLines: true),
            out var _, out var _));
    }

    [Fact]
    public void ZeroOrManyShouldHandleAllSizes()
    {
        var parser = ZeroOrMany(Terms.Text("+").Or(Terms.Text("-")).And(Terms.Integer()));

        Assert.Equal([], parser.Parse(""));
        Assert.Equal([("+", 1L)], parser.Parse("+1"));
        Assert.Equal([("+", 1L), ("-", 2)], parser.Parse("+1-2"));

    }

    [Fact]
    public void ShouldParseSequence()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');
        var d = Literals.Char('d');
        var e = Literals.Char('e');
        var f = Literals.Char('f');
        var g = Literals.Char('g');
        var h = Literals.Char('h');

        Assert.True(a.And(b).TryParse("ab", out var r));
        Assert.Equal(('a', 'b'), r);

        Assert.True(a.And(b).And(c).TryParse("abc", out var r1));
        Assert.Equal(('a', 'b', 'c'), r1);

        Assert.True(a.And(b).AndSkip(c).TryParse("abc", out var r2));
        Assert.Equal(('a', 'b'), r2);

        Assert.True(a.And(b).SkipAnd(c).TryParse("abc", out var r3));
        Assert.Equal(('a', 'c'), r3);
    }

    [Fact]
    public void ShouldParseSequenceAndSkip()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');
        var d = Literals.Char('d');
        var e = Literals.Char('e');
        var f = Literals.Char('f');
        var g = Literals.Char('g');
        var h = Literals.Char('h');

        Assert.True(a.AndSkip(b).TryParse("ab", out var r));
        Assert.Equal(('a'), r);

        Assert.True(a.AndSkip(b).And(c).TryParse("abc", out var r1));
        Assert.Equal(('a', 'c'), r1);

        Assert.True(a.AndSkip(b).AndSkip(c).TryParse("abc", out var r2));
        Assert.Equal(('a'), r2);

        Assert.True(a.AndSkip(b).SkipAnd(c).TryParse("abc", out var r3));
        Assert.Equal(('c'), r3);
    }

    [Fact]
    public void ShouldParseSequenceSkipAnd()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');
        var d = Literals.Char('d');
        var e = Literals.Char('e');
        var f = Literals.Char('f');
        var g = Literals.Char('g');
        var h = Literals.Char('h');

        Assert.True(a.SkipAnd(b).TryParse("ab", out var r));
        Assert.Equal(('b'), r);

        Assert.True(a.SkipAnd(b).And(c).TryParse("abc", out var r1));
        Assert.Equal(('b', 'c'), r1);

        Assert.True(a.SkipAnd(b).AndSkip(c).TryParse("abc", out var r2));
        Assert.Equal(('b'), r2);

        Assert.True(a.SkipAnd(b).SkipAnd(c).TryParse("abc", out var r3));
        Assert.Equal(('c'), r3);
    }

    [Fact]
    public void ShouldReturnConstantResult()
    {
        var a = Literals.Char('a').Then(123);
        var b = Literals.Char('b').Then("1");

        Assert.Equal(123, a.Parse("a"));
        Assert.Equal("1", b.Parse("b"));
    }

    [Fact]
    public void ShouldParseWithCaseSensitivity()
    {
        var parser1 = Literals.Text("not", caseInsensitive: true);

        Assert.Equal("not", parser1.Parse("not"));
        Assert.Equal("not", parser1.Parse("nOt"));
        Assert.Equal("not", parser1.Parse("NOT"));

        var parser2 = Terms.Text("not", caseInsensitive: true);

        Assert.Equal("not", parser2.Parse("not"));
        Assert.Equal("not", parser2.Parse("nOt"));
        Assert.Equal("not", parser2.Parse("NOT"));
    }

    [Fact]
    public void ShouldBuildCaseInsensitiveLookupTable()
    {
        var parser = OneOf(
            Literals.Text("not", caseInsensitive: true),
            Literals.Text("abc", caseInsensitive: false),
            Literals.Text("aBC", caseInsensitive: false)
            );

        Assert.Equal("not", parser.Parse("not"));
        Assert.Equal("not", parser.Parse("nOt"));
        Assert.Equal("abc", parser.Parse("abc"));
        Assert.Equal("aBC", parser.Parse("aBC"));
        Assert.Null(parser.Parse("ABC"));
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("2 ^ 3", 8)]
    [InlineData("2 ^ 2 ^ 3", 256)]
    public void ShouldParseRightAssociativity(string expression, double result)
    {
        var primary = Terms.Number<double>(NumberOptions.Float);
        var exponent = Terms.Char('^');

        var exponentiation = primary.RightAssociative(
            (exponent, static (a, b) => System.Math.Pow(a, b))
            );

        Assert.Equal(result, exponentiation.Parse(expression));
    }


    [Theory]
    [InlineData("2", 2)]
    [InlineData("2 / 4", 0.5)]
    [InlineData("2 / 2 * 3", 3)]
    public void ShouldParseLeftAssociativity(string expression, double result)
    {
        var primary = Terms.Number<double>(NumberOptions.Float);

        var multiplicative = primary.LeftAssociative(
            (Terms.Char('*'), static (a, b) => a * b),
            (Terms.Char('/'), static (a, b) => a / b)
            );

        Assert.Equal(result, multiplicative.Parse(expression));
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("-2", -2)]
    [InlineData("--2", 2)]
    public void ShouldParsePrefix(string expression, double result)
    {
        var primary = Terms.Number<double>(NumberOptions.Float);

        var unary = primary.Unary(
            (Terms.Char('-'), static (a) => 0 - a)
            );

        Assert.Equal(result, unary.Parse(expression));
    }

    [Fact]
    public void ShouldZeroOrOne()
    {
        var parser = ZeroOrOne(Terms.Text("hello"));

        Assert.Equal("hello", parser.Parse(" hello world hello"));
        Assert.Null(parser.Parse(" foo"));
    }

    [Fact]
    public void OptionalShouldSucceed()
    {
        var parser = Terms.Text("hello").Optional();

        Assert.Equal("hello", parser.Parse(" hello world hello").Value);
        Assert.Null(parser.Parse(" foo").Value);
    }

    [Fact]
    public void ZeroOrOneShouldNotBeSeekable()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');

        var oneOf = OneOf(ZeroOrOne(a), b);

        // This should succeed, the ZeroOrOne(a) should always return true 
        Assert.True(oneOf.TryParse("c", out _));
    }

   [Fact]
    public void ZeroOrManyShouldNotBeSeekable()
    {
        var a = Literals.Char('a');
        var b = Literals.Char('b');
        var c = Literals.Char('c');

        var oneOf = OneOf(ZeroOrMany(a).Then('a'), b);

        // This should succeed, the ZeroOrMany(a) should always return true 
        Assert.True(oneOf.TryParse("c", out _));
    }

    [Fact]
    public void ShouldZeroOrOneWithDefault()
    {
        var parser = ZeroOrOne(Terms.Text("hello"), "world");

        Assert.Equal("world", parser.Parse(" this is an apple"));
        Assert.Equal("hello", parser.Parse(" hello world"));
    }

    [Fact]
    public void NumberReturnsAnyType()
    {
        Assert.Equal((byte)123, Literals.Number<byte>().Parse("123"));
        Assert.Equal((sbyte)123, Literals.Number<sbyte>().Parse("123"));
        Assert.Equal((int)123, Literals.Number<int>().Parse("123"));
        Assert.Equal((uint)123, Literals.Number<uint>().Parse("123"));
        Assert.Equal((long)123, Literals.Number<long>().Parse("123"));
        Assert.Equal((ulong)123, Literals.Number<ulong>().Parse("123"));
        Assert.Equal((short)123, Literals.Number<short>().Parse("123"));
        Assert.Equal((ushort)123, Literals.Number<ushort>().Parse("123"));
        Assert.Equal((decimal)123, Literals.Number<decimal>().Parse("123"));
        Assert.Equal((double)123, Literals.Number<double>().Parse("123"));
        Assert.Equal((float)123, Literals.Number<float>().Parse("123"));
#if NET6_0_OR_GREATER
        Assert.Equal((Half)123, Literals.Number<Half>().Parse("123"));
#endif
        Assert.Equal((BigInteger)123, Literals.Number<BigInteger>().Parse("123"));
#if NET8_0_OR_GREATER
        Assert.Equal((nint)123, Literals.Number<nint>().Parse("123"));
        Assert.Equal((nuint)123, Literals.Number<nuint>().Parse("123"));
        Assert.Equal((Int128)123, Literals.Number<Int128>().Parse("123"));
        Assert.Equal((UInt128)123, Literals.Number<UInt128>().Parse("123"));
#endif
    }

    [Fact]
    public void NumberCanReadExponent()
    {
        var e = NumberOptions.AllowExponent;

        Assert.Equal((byte)120, Literals.Number<byte>(e).Parse("12e1"));
        Assert.Equal((sbyte)120, Literals.Number<sbyte>(e).Parse("12e1"));
        Assert.Equal((int)120, Literals.Number<int>(e).Parse("12e1"));
        Assert.Equal((uint)120, Literals.Number<uint>(e).Parse("12e1"));
        Assert.Equal((long)120, Literals.Number<long>(e).Parse("12e1"));
        Assert.Equal((ulong)120, Literals.Number<ulong>(e).Parse("12e1"));
        Assert.Equal((short)120, Literals.Number<short>(e).Parse("12e1"));
        Assert.Equal((ushort)120, Literals.Number<ushort>(e).Parse("12e1"));
        Assert.Equal((decimal)120, Literals.Number<decimal>(e).Parse("12e1"));
        Assert.Equal((double)120, Literals.Number<double>(e).Parse("12e1"));
        Assert.Equal((float)120, Literals.Number<float>(e).Parse("12e1"));
#if NET6_0_OR_GREATER
        Assert.Equal((Half)120, Literals.Number<Half>(e).Parse("12e1"));
#endif
        Assert.Equal((BigInteger)120, Literals.Number<BigInteger>(e).Parse("12e1"));
#if NET8_0_OR_GREATER
        Assert.Equal((nint)120, Literals.Number<nint>(e).Parse("12e1"));
        Assert.Equal((nuint)120, Literals.Number<nuint>(e).Parse("12e1"));
        Assert.Equal((Int128)120, Literals.Number<Int128>(e).Parse("12e1"));
        Assert.Equal((UInt128)120, Literals.Number<UInt128>(e).Parse("12e1"));
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
        Assert.Equal(expected, Literals.Number<decimal>(NumberOptions.Any).Parse(source));
    }

    [Fact]
    public void NumberParsesCustomDecimalSeparator()
    {
        Assert.Equal((decimal)123.456, Literals.Number<decimal>(NumberOptions.Any, decimalSeparator: '|').Parse("123|456"));
    }

    [Fact]
    public void NumberParsesCustomGroupSeparator()
    {
        Assert.Equal((decimal)123456, Literals.Number<decimal>(NumberOptions.Any, groupSeparator: '|').Parse("123|456"));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("256")]
    public void NumberShouldNotParseOverflow(string source)
    {
        Assert.False(Literals.Number<byte>().TryParse(source, out var _));
    }

    [Theory]
    [InlineData("a", "a", "a")]
    [InlineData("a", "aa", "aa")]
    [InlineData("a", "aaaa", "aaaa")]
    [InlineData("ab", "ab", "ab")]
    [InlineData("ba", "ab", "ab")]
    [InlineData("abc", "aaabbbccc", "aaabbbccc")]
    [InlineData("a", "aaab", "aaa")]
    [InlineData("aa", "aaaaab", "aaaaa")]
    public void AnyOfShouldMatch(string chars, string source,  string expected)
    {
        Assert.Equal(expected, Literals.AnyOf(chars).Parse(source).ToString());
    }

    [Theory]
    [InlineData("a", "b")]
    [InlineData("a", "bbb")]
    [InlineData("abc", "dabc")]
    public void AnyOfShouldNotMatch(string chars, string source)
    {
        Assert.False(Literals.AnyOf(chars).TryParse(source, out var _));
    }

    [Fact]
    public void AnyOfShouldRespectSizeConstraints()
    {
        Assert.True(Literals.AnyOf("a", minSize: 0).TryParse("aaa", out var r) && r.ToString() == "aaa");
        Assert.True(Literals.AnyOf("a", minSize: 0).TryParse("bbb", out _));
        Assert.False(Literals.AnyOf("a", minSize: 4).TryParse("aaa", out _));
        Assert.False(Literals.AnyOf("a", minSize: 2).TryParse("ab", out _));
        Assert.False(Literals.AnyOf("a", minSize: 3).TryParse("ab", out _));
        Assert.Equal("aa", Literals.AnyOf("a", minSize: 2, maxSize: 2).Parse("aa"));
        Assert.Equal("aa", Literals.AnyOf("a", minSize: 2, maxSize: 3).Parse("aa"));
        Assert.Equal("a", Literals.AnyOf("a", maxSize: 1).Parse("aa"));
        Assert.Equal("aaaa", Literals.AnyOf("a", minSize: 2, maxSize: 4).Parse("aaaaaa"));
        Assert.False(Literals.AnyOf("a", minSize: 2, maxSize: 2).TryParse("a", out _));
    }

    [Fact]
    public void AnyOfShouldNotBeSeekableIfOptional()
    {
        var parser = Literals.AnyOf("a", minSize: 0) as ISeekable;
        Assert.False(parser.CanSeek);
    }

    [Fact]
    public void AnyOfShouldResetPositionWhenFalse()
    {
        Assert.False(Literals.AnyOf("a", minSize: 3)
            .And(Literals.AnyOf("Z"))
            .TryParse("aaZZ", out _));

        Assert.True(Literals.AnyOf("a", minSize: 3)
             .And(Literals.AnyOf("Z"))
             .TryParse("aaaZZ", out _));
    }

    [Theory]
    [InlineData("a", "b", "b")]
    [InlineData("a", "bb", "bb")]
    [InlineData("a", "bbbb", "bbbb")]
    [InlineData("ab", "cd", "cd")]
    [InlineData("ba", "cd", "cd")]
    [InlineData("abc", "dddeeefff", "dddeeefff")]
    [InlineData("a", "bbba", "bbb")]
    [InlineData("aa", "bbbbba", "bbbbb")]
    public void NoneOfShouldMatch(string chars, string source, string expected)
    {
        Assert.Equal(expected, Literals.NoneOf(chars).Parse(source).ToString());
    }

    [Theory]
    [InlineData("a", "a")]
    [InlineData("a", "aaa")]
    [InlineData("abc", "beee")]
    public void NoneOfShouldNotMatch(string chars, string source)
    {
        Assert.False(Literals.NoneOf(chars).TryParse(source, out var _));
    }

    [Fact]
    public void NoneOfShouldRespectSizeConstraints()
    {
        Assert.True(Literals.NoneOf("a", minSize: 0).TryParse("bbb", out var r) && r.ToString() == "bbb");
        Assert.True(Literals.NoneOf("a", minSize: 0).TryParse("aaa", out _));
        Assert.False(Literals.NoneOf("a", minSize: 4).TryParse("bbb", out _));
        Assert.False(Literals.NoneOf("a", minSize: 2).TryParse("ba", out _));
        Assert.False(Literals.NoneOf("a", minSize: 3).TryParse("ba", out _));
        Assert.Equal("bb", Literals.NoneOf("a", minSize: 2, maxSize: 2).Parse("bb"));
        Assert.Equal("bb", Literals.NoneOf("a", minSize: 2, maxSize: 3).Parse("bb"));
        Assert.Equal("b", Literals.NoneOf("a", maxSize: 1).Parse("bb"));
        Assert.Equal("bbbb", Literals.NoneOf("a", minSize: 2, maxSize: 4).Parse("bbbbb"));
        Assert.False(Literals.NoneOf("a", minSize: 2, maxSize: 2).TryParse("b", out _));
    }

    [Fact]
    public void NoneOfShouldNotBeSeekableIfOptional()
    {
        var parser = Literals.NoneOf("a", minSize: 0) as ISeekable;
        Assert.False(parser.CanSeek);
    }

    [Fact]
    public void NoneOfShouldResetPositionWhenFalse()
    {
        Assert.False(Literals.NoneOf("Z", minSize: 3)
            .And(Literals.NoneOf("a"))
            .TryParse("aaZZ", out _));

        Assert.True(Literals.NoneOf("Z", minSize: 3)
             .And(Literals.NoneOf("a"))
             .TryParse("aaaZZ", out _));
    }

    [Fact]
    public void NoneOfShouldNotBeSeekable()
    {
        var parser = Literals.NoneOf("Z", minSize: 3);

        Assert.True(parser is ISeekable);
        Assert.False(((ISeekable)parser).CanSeek);
    }

    [Fact]
    public void ElseErrorShouldNotBeSeekable()
    {
        Parser<char> a = Terms.Char('a');
        Parser<char> b = Terms.Char('b');
        Parser<object> c = a.Then<object>();

        // Use two parsers to ensure OneOf tries to build a lookup table
        var parser = OneOf(a.ElseError("Error"), b);

        Assert.True(parser.TryParse("a", out _));
        Assert.Throws<ParseException>(() => parser.Parse("b"));
    }

    [Fact]
    public void WithWhiteSpaceParserShouldUseCustomWhiteSpace()
    {
        // Example from the issue: using dots as whitespace
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should succeed with dots as whitespace
        Assert.True(parser.TryParse("..hello.world", out var result));
        Assert.Equal("hello", result.Item1.ToString());
        Assert.Equal("world", result.Item2.ToString());

        // Should succeed with multiple dots
        Assert.True(parser.TryParse("...hello...world", out var result2));
        Assert.Equal("hello", result2.Item1.ToString());
        Assert.Equal("world", result2.Item2.ToString());
    }

    [Fact]
    public void WithWhiteSpaceParserShouldNotSkipRegularWhiteSpace()
    {
        // When using custom whitespace parser, regular spaces should NOT be skipped
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should fail with regular whitespace
        Assert.False(parser.TryParse("hello world", out _));
    }

    [Fact]
    public void WithWhiteSpaceParserShouldRestoreOriginalParser()
    {
        // After the WithWhiteSpaceParser parser completes, the original whitespace parser should be restored
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var inner = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));
        var outer = Terms.Text("outer");
        var parser = inner.And(outer);

        // Inside WithWhiteSpaceParser, dots are whitespace
        // Outside, regular whitespace should work
        Assert.True(parser.TryParse("..hello.world outer", out var result));
        Assert.Equal("hello", result.Item1.Item1.ToString());
        Assert.Equal("world", result.Item1.Item2.ToString());
        Assert.Equal("outer", result.Item2.ToString());
    }

    [Fact]
    public void WithWhiteSpaceParserShouldWorkWithNestedParsers()
    {
        // Test nested WithWhiteSpaceParser calls
        var a = Terms.Text("a");
        var b = Terms.Text("b");
        var c = Terms.Text("c");

        var innerParser = a.And(b).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));
        var outerParser = innerParser.And(c).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('-'))));

        // First test a simpler case
        Assert.True(innerParser.TryParse("a.b", out var inner1));
        Assert.Equal("a", inner1.Item1.ToString());
        Assert.Equal("b", inner1.Item2.ToString());

        // Test outer without nesting first
        var simpleOuter = Terms.Text("ab").And(c).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('-'))));
        Assert.True(simpleOuter.TryParse("-ab-c", out var simple1));
        Assert.Equal("ab", simple1.Item1.ToString());
        Assert.Equal("c", simple1.Item2.ToString());

        // For the nested case:
        // - innerParser uses "." as whitespace for parsing "a.b"
        // - outerParser uses "-" as whitespace for parsing innerParser and c
        // - But innerParser itself doesn't skip whitespace (it's not a Terms parser)
        // - So the input should be "a.b-c" (no leading "-")
        //   The innerParser will parse "a.b", then c will skip "-" and parse "c"
        Assert.True(outerParser.TryParse("a.b-c", out var result));
        Assert.Equal("a", result.Item1.Item1.ToString());
        Assert.Equal("b", result.Item1.Item2.ToString());
        Assert.Equal("c", result.Item2.ToString());
    }

    [Fact]
    public void WithWhiteSpaceParserShouldWorkWithZeroOrMany()
    {
        // Test that custom whitespace works with ZeroOrMany combinator
        var word = Terms.Identifier();
        var parser = ZeroOrMany(word).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char(','))));

        Assert.True(parser.TryParse(",hello,world,foo", out var result));
        Assert.Equal(3, result.Count);
        Assert.Equal("hello", result[0].ToString());
        Assert.Equal("world", result[1].ToString());
        Assert.Equal("foo", result[2].ToString());
    }

    [Fact]
    public void WithWhiteSpaceParserShouldAllowEmptyWhiteSpace()
    {
        // Test that we can parse without any whitespace when custom parser doesn't match
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should succeed with no whitespace between tokens
        Assert.True(parser.TryParse("helloworld", out var result));
        Assert.Equal("hello", result.Item1.ToString());
        Assert.Equal("world", result.Item2.ToString());
    }

    [Fact]
    public void WithWhiteSpaceParserShouldWorkWithCompilation()
    {
        // Test that compilation works correctly with WithWhiteSpaceParser
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Compile the parser
        var compiled = parser.Compile();

        // Should work the same as non-compiled version
        Assert.True(compiled.TryParse("..hello.world", out var result));
        Assert.Equal("hello", result.Item1.ToString());
        Assert.Equal("world", result.Item2.ToString());

        // Should not skip regular whitespace
        Assert.False(compiled.TryParse("hello world", out _));
    }

    [Fact]
    public void WithWhiteSpaceParserShouldWorkWithMultipleCharWhiteSpace()
    {
        // Test using a multi-character whitespace parser
        var hello = Terms.Text("hello");
        var world = Terms.Text("world");
        var parser = hello.And(world).WithWhiteSpaceParser(Capture(ZeroOrMany(Literals.Char('.'))));

        // Should succeed with multiple dots
        Assert.True(parser.TryParse("...hello....world", out var result));
        Assert.Equal("hello", result.Item1.ToString());
        Assert.Equal("world", result.Item2.ToString());
    }

    [Theory]
    [InlineData("-- single line comment")]
    [InlineData("-- ")]
    [InlineData("--")]
    public void ShouldReadSingleLineComments(string text)
    {
        var comments = Literals.Comments("--");
        Assert.Equal(text, comments.Parse(text).ToString());
    }

    [Theory]
    [InlineData("hello-- single line comment\n world")]
    [InlineData("hello-- \n world")]
    [InlineData("hello--\n world")]
    [InlineData("hello  --\n world")]
    public void ShouldSkipSingleLineComments(string text)
    {

        var comments = Terms.Text("hello").And(Terms.Text("world")).WithWhiteSpaceParser(Terms.Comments("--"));
        Assert.True(comments.TryParse(text, out _));
    }

    [Theory]
    [InlineData("hello -- single line comment")]
    [InlineData("hello --")]
    [InlineData("hello--")]
    public void ShouldReadSingleLineCommentsAfterText(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Comments("--"));
        Assert.True(comments.TryParse(text, out _));
    }
    
    [Theory]
    [InlineData("/* multi line comment */")]
    [InlineData("/* multi \nline comment */")]
    [InlineData("/**/")]
    [InlineData("/*\n*/")]
    [InlineData("/* */")]
    public void ShouldReadMultiLineComments(string text)
    {
        var comments = Literals.Comments("/*", "*/");
        Assert.Equal(text, comments.Parse(text).ToString());
    }

    [Theory]
    [InlineData("hello /* multi line comment */world")]
    [InlineData("hello /**/world")]
    [InlineData("hello/* */ world")]
    [InlineData("hello /* multi line \n comment */    world")]
    [InlineData("hello /* multi line \n comment */    world\n")]
    [InlineData("hello /* multi \nline \n comment */   world")]
    [InlineData("hello /* multi line \n\n comment */  world")]
    [InlineData("hello /*\n*/ world")]
    [InlineData("hello/* */ world\n")]
    public void ShouldReadMultiLineCommentsAfterText(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Comments("/*", "*/")).And(Terms.Text("world"));
        Assert.True(comments.TryParse(text, out _));
    }
            
    [Theory]
    [InlineData("hello /* multi line comment ")]
    [InlineData("hello /* asd")]
    [InlineData("hello/* ")]
    public void ShouldFileUnterminataedMultiLineComments(string text)
    {
        var comments = Terms.Text("hello").And(Terms.Comments("/*", "*/"));
        Assert.False(comments.TryParse(text, out _));
    }
}
