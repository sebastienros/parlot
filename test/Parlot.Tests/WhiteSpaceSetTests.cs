using System;
using System.Collections.Generic;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class WhiteSpaceSetTests
{
    // The chars of the default set which aren't ASCII white space: the Unicode spaces and new lines
    public static TheoryData<char> UnicodeSpaces()
    {
        var data = new TheoryData<char>();

        foreach (var c in Character.WhiteSpaceOrNewLines)
        {
            if (c > 127)
            {
                data.Add(c);
            }
        }

        return data;
    }

    private static ParseContext AsciiContext(string text, bool useNewLines = false)
        => new(new Scanner(text), useNewLines) { WhiteSpaceSet = WhiteSpaceSet.Ascii };

    [Fact]
    public void CharacterShouldTellTheCharsOfASet()
    {
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var c = (char)i;

            Assert.Equal(Character.IsWhiteSpace(c), Character.IsWhiteSpace(c, WhiteSpaceSet.Default));
            Assert.Equal(Character.IsWhiteSpaceOrNewLine(c), Character.IsWhiteSpaceOrNewLine(c, WhiteSpaceSet.Default));
            Assert.Equal(c is ' ' or '\t' or '\f', Character.IsWhiteSpace(c, WhiteSpaceSet.Ascii));
            Assert.Equal(c is ' ' or '\t' or '\f' or '\n' or '\r' or '\v', Character.IsWhiteSpaceOrNewLine(c, WhiteSpaceSet.Ascii));

            // Every set is part of the default one, which is what the white space literals declare to OneOf
            Assert.True(!Character.IsWhiteSpaceOrNewLine(c, WhiteSpaceSet.Ascii) || Character.IsWhiteSpaceOrNewLine(c));
        }

        Assert.Equal(Character.AsciiWhiteSpaceOrNewLines.Length, Character.AsciiWhiteSpaces.Length + Character.AsciiNewLines.Length);
    }

    [Fact]
    public void UnknownSetsShouldThrow()
    {
        var context = new ParseContext(new Scanner("a"));

        Assert.Throws<ArgumentOutOfRangeException>(() => context.WhiteSpaceSet = (WhiteSpaceSet)2);
        Assert.Throws<ArgumentOutOfRangeException>(() => Terms.Text("a").WithWhiteSpaceSet((WhiteSpaceSet)2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Character.IsWhiteSpace(' ', (WhiteSpaceSet)2));
        Assert.Equal(WhiteSpaceSet.Default, context.WhiteSpaceSet);
    }

    [Theory]
    [MemberData(nameof(UnicodeSpaces))]
    public void TermsShouldNotSkipUnicodeSpacesWithAsciiSet(char space)
    {
        var text = $"{space}a";

        Assert.True(Terms.Text("a").TryParse(text, out _));
        Assert.False(Terms.Text("a").WithWhiteSpaceSet(WhiteSpaceSet.Ascii).TryParse(text, out _));
        Assert.False(Terms.Text("a").TryParse(AsciiContext(text), out _, out _));

        // The text is not consumed
        var context = AsciiContext(text);
        Assert.False(Terms.Identifier().TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // The space is a char like any other
        Assert.True(Terms.Text($"{space}a").WithWhiteSpaceSet(WhiteSpaceSet.Ascii).TryParse($" \t{space}a", out _));
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData("\f")]
    [InlineData("\n")]
    [InlineData("\r")]
    [InlineData("\v")]
    [InlineData(" \t\f\n\r\v")]
    public void TermsShouldSkipAsciiWhiteSpaceWithAsciiSet(string spaces)
    {
        var parser = Terms.Identifier().And(Terms.Integer()).And(Terms.Char(';')).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

        Assert.True(parser.TryParse($"{spaces}a{spaces}1{spaces};", out var result));
        Assert.Equal("a", result.Item1.ToString());
        Assert.Equal(1, result.Item2);
    }

    [Fact]
    public void UseNewLinesShouldNotSkipNewLinesWithAsciiSet()
    {
        var parser = Terms.Text("a");

        foreach (var newLine in Character.AsciiNewLines)
        {
            Assert.False(parser.TryParse(AsciiContext($"{newLine}a", useNewLines: true), out _, out _));
            Assert.True(parser.TryParse(AsciiContext($"{newLine}a", useNewLines: false), out _, out _));
        }

        Assert.True(parser.TryParse(AsciiContext(" \t\fa", useNewLines: true), out _, out _));
        Assert.False(parser.TryParse(AsciiContext(" \u00A0a", useNewLines: true), out _, out _));
    }

    [Fact]
    public void SkipWhiteSpaceShouldUseTheSet()
    {
        var context = AsciiContext(" \t\n\u00A0 a");

        context.SkipWhiteSpace();
        Assert.Equal(3, context.Scanner.Cursor.Offset);
        Assert.Equal(2, context.Scanner.Cursor.Position.Line);

        // Changing the set doesn't reuse the white space skipped from the same offset with the previous one
        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        context.WhiteSpaceSet = WhiteSpaceSet.Default;
        context.SkipWhiteSpace();
        Assert.Equal(5, context.Scanner.Cursor.Offset);

        context.Scanner.Cursor.ResetPosition(TextPosition.Start);
        context.WhiteSpaceSet = WhiteSpaceSet.Ascii;
        context.SkipWhiteSpace();
        Assert.Equal(3, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void LiteralsWhiteSpaceShouldUseTheSet()
    {
        Assert.True(Literals.WhiteSpace(includeNewLines: true).TryParse(AsciiContext(" \n\u00A0"), out var span, out _));
        Assert.Equal(" \n", span.ToString());

        Assert.True(Literals.WhiteSpace().TryParse(AsciiContext("\t\f\n"), out span, out _));
        Assert.Equal("\t\f", span.ToString());

        var context = AsciiContext("\u00A0 ");
        Assert.False(Literals.WhiteSpace(includeNewLines: true).TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        Assert.True(Literals.WhiteSpace(includeNewLines: true).TryParse(" \n\u00A0", out span));
        Assert.Equal(" \n\u00A0", span.ToString());
    }

    [Fact]
    public void NonWhiteSpaceShouldUseTheSet()
    {
        var text = "a\u00A0b\u3000c\td";
        Assert.True(Literals.NonWhiteSpace().TryParse(AsciiContext(text), out var span, out _));
        Assert.Equal("a\u00A0b\u3000c", span.ToString());

        Assert.True(Literals.NonWhiteSpace().TryParse(text, out span));
        Assert.Equal("a", span.ToString());

        // New lines end the run only when they are white space
        Assert.True(Literals.NonWhiteSpace(includeNewLines: false).TryParse(AsciiContext("a\u00A0\nb\fc"), out span, out _));
        Assert.Equal("a\u00A0\nb", span.ToString());

        Assert.True(Terms.NonWhiteSpace().TryParse(AsciiContext(" \u00A0a b"), out span, out _));
        Assert.Equal("\u00A0a", span.ToString());

        // A long run is searched rather than read char by char
        var longText = new string('a', 100) + "\u00A0" + new string('b', 100) + " c";
        Assert.True(Literals.NonWhiteSpace().TryParse(AsciiContext(longText), out span, out _));
        Assert.Equal(201, span.Length);

        var lines = new string('a', 40) + "\n" + new string('b', 40) + " c";
        var context = AsciiContext(lines);
        Assert.True(Literals.NonWhiteSpace(includeNewLines: false).TryParse(context, out span, out _));
        Assert.Equal(81, span.Length);
        Assert.Equal(2, context.Scanner.Cursor.Position.Line);
        Assert.Equal(41, context.Scanner.Cursor.Position.Column);
    }

    [Fact]
    public void OneOfShouldSkipWhiteSpaceWithTheSet()
    {
        var parser = ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text("\u00A0"))).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

        Assert.True(parser.TryParse(" for\u00A0if \u00A0", out var values));
        Assert.Equal(["for", "\u00A0", "if", "\u00A0"], values);

        // With the default set the no-break spaces are skipped
        Assert.True(ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text("\u00A0"))).TryParse(" for\u00A0if \u00A0", out values));
        Assert.Equal(["for", "if"], values);
    }

    [Fact]
    public void CommentsShouldUseTheSet()
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii)
            .Eof();

        Assert.True(parser.TryParse(" a # comment\n\tb", out var values));
        Assert.Equal(2, values.Count);

        Assert.False(parser.TryParse("a\u00A0# comment", out _));
        Assert.False(parser.TryParse("a \u00A0b", out _));
    }

    [Fact]
    public void WithWhiteSpaceSetShouldRestoreThePreviousSet()
    {
        // The inner grammar is ASCII only, the outer one keeps the default set
        var inner = Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')')).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        var parser = inner.AndSkip(Terms.Char(';'));

        Assert.True(parser.TryParse("( 1 )\u00A0;", out var value));
        Assert.Equal(1, value);
        Assert.False(parser.TryParse("(\u00A01);", out _));

        var context = new ParseContext(new Scanner("(\u00A01)"));
        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(WhiteSpaceSet.Default, context.WhiteSpaceSet);

        // And the default set can be selected back in an inner parser
        var nested = Terms.Char('[').SkipAnd(Terms.Integer().WithWhiteSpaceSet(WhiteSpaceSet.Default)).AndSkip(Terms.Char(']')).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        Assert.True(nested.TryParse("[\u00A01 ]", out value));
        Assert.Equal(1, value);
        Assert.False(nested.TryParse("[1\u00A0]", out _));
    }

    [Fact]
    public void WithWhiteSpaceSetShouldRestoreTheSetOnExceptions()
    {
        var parser = Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'")).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        var context = new ParseContext(new Scanner("(1 x"));

        Assert.Throws<ParseException>(() => parser.Parse(context));
        Assert.Equal(WhiteSpaceSet.Default, context.WhiteSpaceSet);
    }

    [Fact]
    public void WithWhiteSpaceSetShouldBeSeekableWhenItsParserDoesNotSkipWhiteSpace()
    {
        var literal = (Parlot.Rewriting.ISeekable)Literals.Char('a').WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        Assert.True(literal.CanSeek);
        Assert.Equal(['a'], literal.ExpectedChars);

        // OneOf would skip white space with its own set before looking the first char up
        var term = (Parlot.Rewriting.ISeekable)Terms.Char('a').WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        Assert.False(term.CanSeek);

        var parser = OneOf(Terms.Char('a').WithWhiteSpaceSet(WhiteSpaceSet.Ascii), Terms.Char('b'));
        Assert.False(parser.TryParse("\u00A0a", out _));
        Assert.True(parser.TryParse("\u00A0b", out _));
        Assert.True(parser.TryParse(" a", out _));
    }

    [Fact]
    public void WhiteSpaceParserShouldTakePrecedence()
    {
        // A custom parser skips white space instead of the set
        var parser = Terms.Text("a").WithWhiteSpaceParser(Literals.Char('_').Then(static (_, _) => default(TextSpan))).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

        Assert.True(parser.TryParse("_a", out _));
        Assert.False(parser.TryParse(" a", out _));
    }

    [Fact]
    public void ThenWithOffsetsShouldSkipWithTheSet()
    {
        var parser = Terms.Identifier().Then(static (context, start, end, value) => (start, end)).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

        Assert.True(parser.TryParse(" \tab", out var offsets));
        Assert.Equal((2, 4), offsets);
        Assert.False(parser.TryParse("\u00A0ab", out _));
    }

    [Fact]
    public void ScannerShouldSkipTheWhiteSpaceOfASet()
    {
        var scanner = new Scanner(" \u00A0a");
        Assert.True(scanner.SkipWhiteSpace(WhiteSpaceSet.Ascii));
        Assert.Equal(1, scanner.Cursor.Offset);
        Assert.False(scanner.SkipWhiteSpaceOrNewLine(WhiteSpaceSet.Ascii));
        Assert.True(scanner.SkipWhiteSpaceOrNewLine(WhiteSpaceSet.Default));
        Assert.Equal(2, scanner.Cursor.Offset);

        scanner = new Scanner("\n\r\v \u3000");
        Assert.False(scanner.SkipWhiteSpace(WhiteSpaceSet.Ascii));
        Assert.True(scanner.SkipWhiteSpaceOrNewLine(WhiteSpaceSet.Ascii));
        Assert.Equal(4, scanner.Cursor.Offset);
    }

    [Fact]
    public void ContextFactoryShouldSelectTheSetForStreams()
    {
        var parser = Separated(Terms.Char(','), Terms.Integer());
        var options = new StreamParseOptions { BufferSize = 1, ContextFactory = static (scanner, _) => new ParseContext(scanner) { WhiteSpaceSet = WhiteSpaceSet.Ascii } };

        Assert.Equal([1L, 2L], parser.Parse(new System.IO.StringReader("1 ,\n 2"), options));
        Assert.Equal([1L], parser.Parse(new System.IO.StringReader("1 ,\u00A02"), options));
        Assert.Equal([1L, 2L], parser.Parse(new System.IO.StringReader("1 ,\u00A02")));
    }

    // The default set is the Unicode White_Space property (PropList.txt)
    private const string UnicodeWhiteSpace = "\t\n\v\f\r \u0085\u00A0\u1680\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u2028\u2029\u202F\u205F\u3000";
    private const string UnicodeNewLines = "\n\v\r\u0085\u2028\u2029";

    [Fact]
    public void DefaultSetShouldBeUnicodeWhiteSpace()
    {
        for (var i = 0; i <= char.MaxValue; i++)
        {
            var c = (char)i;
            var isNewLine = UnicodeNewLines.IndexOf(c) >= 0;

            Assert.Equal(UnicodeWhiteSpace.IndexOf(c) >= 0, Character.IsWhiteSpaceOrNewLine(c));
            Assert.Equal(Character.IsWhiteSpaceOrNewLine(c) && !isNewLine, Character.IsWhiteSpace(c));
            Assert.Equal(isNewLine, Character.IsNewLine(c));
#if NET8_0_OR_GREATER
            Assert.Equal(char.IsWhiteSpace(c), Character.IsWhiteSpaceOrNewLine(c));
#endif
        }

        Assert.Equal(UnicodeWhiteSpace.Length, Character.WhiteSpaceOrNewLines.Length);
        Assert.Equal(UnicodeNewLines.Length, Character.NewLines.Length);
        Assert.Equal(Character.WhiteSpaceOrNewLines.Length, Character.WhiteSpaces.Length + Character.NewLines.Length);
    }

    [Theory]
    [InlineData('\u0085')]
    [InlineData('\u2028')]
    [InlineData('\u2029')]
    public void TermsShouldSkipUnicodeNewLines(char newLine)
    {
        var parser = Terms.Text("a");

        Assert.True(parser.TryParse($"{newLine}a", out _));
        Assert.True(parser.TryParse($" {newLine}\ta", out _));

        // With UseNewLines they are new lines, which are not skipped
        var context = new ParseContext(new Scanner($" {newLine}a"), useNewLines: true);
        Assert.False(parser.TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        // Literals.WhiteSpace reads them only with the new lines
        Assert.False(Literals.WhiteSpace().TryParse($"{newLine} ", out _));
        Assert.True(Literals.WhiteSpace(includeNewLines: true).TryParse($"{newLine} a", out var span));
        Assert.Equal($"{newLine} ", span.ToString());

        // Like \r and \v they don't start a line, only \n does
        var scanner = new Scanner($"{newLine}a");
        Assert.True(scanner.SkipWhiteSpaceOrNewLine());
        Assert.Equal(1, scanner.Cursor.Offset);
        Assert.Equal(1, scanner.Cursor.Position.Line);
    }

    [Theory]
    [InlineData('\uFEFF')]
    [InlineData('\u180E')]
    public void TermsShouldNotSkipCharsOutsideUnicodeWhiteSpace(char c)
    {
        // The byte order mark and the Mongolian vowel separator are not white space
        var context = new ParseContext(new Scanner($"{c}a"));
        Assert.False(Terms.Text("a").TryParse(context, out _, out _));
        Assert.Equal(0, context.Scanner.Cursor.Offset);

        Assert.False(Literals.WhiteSpace(includeNewLines: true).TryParse($"{c} ", out _));
        Assert.True(Literals.NonWhiteSpace().TryParse($"{c}a b", out var span));
        Assert.Equal($"{c}a", span.ToString());

        // A grammar can match them
        Assert.True(Terms.Char(c).SkipAnd(Terms.Text("a")).TryParse($" {c} a", out _));
    }

    [Fact]
    public void StreamsShouldSkipUnicodeWhiteSpace()
    {
        var parser = Separated(Terms.Char(','), Terms.Integer());
        var options = new StreamParseOptions { BufferSize = 1 };

        Assert.Equal([1L, 2L, 3L], parser.Parse(new System.IO.StringReader("1\u2028,\u20292\u0085,\u00A03"), options));
        Assert.Equal([1L], parser.Parse(new System.IO.StringReader("1,\uFEFF2"), options));
    }
}
