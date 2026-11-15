using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class CodePointIdentifierTests
{
    // The identifier chars of JavaScript, from the general categories of ID_Start and ID_Continue
    internal static bool IsJavaScriptStart(int c)
    {
        if (c < 128)
        {
            return c is '$' or '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');
        }

        return GetCategory(c) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber;
    }

    internal static bool IsJavaScriptPart(int c)
    {
        if (c < 128)
        {
            return c is '$' or '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9');
        }

        return c is 0x200C or 0x200D || IsJavaScriptStart(c) || GetCategory(c) is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
    }

    private static UnicodeCategory GetCategory(int c) => c is >= 0xD800 and <= 0xDFFF
        ? UnicodeCategory.Surrogate
        : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(c), 0);

    private static readonly Parser<TextSpan> JavaScript = Literals.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.JavaScript);

    // Lone surrogates don't survive the serialization of the test cases, they are written <hi> and <lo>
    private static string Surrogates(string text) => text.Replace("<hi>", "\ud835").Replace("<lo>", "\udc9c");

    private const string MathA = "\U0001D49C"; // 𝒜, a letter outside the BMP
    private const string MathB = "\U0001D4D0"; // 𝓐

    [Theory]
    [InlineData("abc", "abc", "abc")]
    [InlineData("\\u0061bc", "\\u0061bc", "abc")]
    [InlineData("a\\u0062c", "a\\u0062c", "abc")]
    [InlineData("a\\u{62}c", "a\\u{62}c", "abc")]
    [InlineData("\\u{1D4D0}x", "\\u{1D4D0}x", MathB + "x")]
    [InlineData("\\u{00000061}", "\\u{00000061}", "a")]
    [InlineData("a\\u0031", "a\\u0031", "a1")]
    [InlineData(MathA, MathA, MathA)]
    [InlineData("a" + MathA + "b c", "a" + MathA + "b", "a" + MathA + "b")]
    [InlineData("café x", "café", "café")]
    [InlineData("été", "été", "été")]
    [InlineData("a‌b", "a‌b", "a‌b")]
    [InlineData("a(b)", "a", "a")]
    public void ShouldMatchEscapesAndSurrogatePairs(string input, string expected, string name)
    {
        Assert.True(JavaScript.TryParse(input, out var value));
        Assert.Equal(expected, value.ToString());
        Assert.Equal(name, Character.DecodeIdentifier(value).ToString());
        Assert.Equal(name, Character.DecodeIdentifier(value.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1a")]
    [InlineData("\\u0031")] // A digit can't start an identifier, even escaped
    [InlineData("\\u0020a")] // Not an identifier char
    [InlineData("\\u00")] // Cut by the end of the text
    [InlineData("\\u")]
    [InlineData("\\")]
    [InlineData("\\x41")]
    [InlineData("\\u{}")]
    [InlineData("\\u{110000}")] // Above the last code point
    [InlineData("\\u{FFFFFFFFF}")]
    [InlineData("\\u{61")]
    [InlineData("\\u006g")]
    [InlineData("<hi>")] // Lone high surrogate
    [InlineData("<hi>a")]
    [InlineData("<lo>a")] // Lone low surrogate
    [InlineData("\\uD835\\uDC9C")] // Escaped surrogates are not code points
    [InlineData("×")] // A symbol
    public void ShouldNotMatchInvalidStarts(string input)
    {
        var context = new ParseContext(new Scanner(Surrogates(input)));
        var result = new ParseResult<TextSpan>();

        Assert.False(JavaScript.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("a\\u00", "a")]
    [InlineData("a\\u", "a")]
    [InlineData("a\\", "a")]
    [InlineData("a\\u0020", "a")]
    [InlineData("a\\u{}", "a")]
    [InlineData("a\\u{110000}", "a")]
    [InlineData("a\\u{62", "a")]
    [InlineData("a<hi>", "a")]
    [InlineData("a<hi>b", "a")]
    [InlineData("a<lo>b", "a")]
    [InlineData("ab×", "ab")]
    public void ShouldStopBeforeInvalidParts(string input, string expected)
    {
        var context = new ParseContext(new Scanner(Surrogates(input)));
        var result = new ParseResult<TextSpan>();

        Assert.True(JavaScript.Parse(context, ref result));
        Assert.Equal(expected, result.Value.ToString());
        Assert.Equal(expected.Length, context.Scanner.Cursor.Offset);
    }

    [Theory]
    [InlineData("\\u0061", false)]
    [InlineData("\\u{61}", false)]
    [InlineData(MathA, false)]
    [InlineData("é", true)]
    public void ShouldOnlyReadWhatTheOptionsAllow(string input, bool success)
    {
        var parser = Literals.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.None);
        var context = new ParseContext(new Scanner(Surrogates(input)));
        var result = new ParseResult<TextSpan>();

        Assert.Equal(success, parser.Parse(context, ref result));
        Assert.Equal(success ? input.Length : 0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldReadOnlyTheEscapesTheOptionsAllow()
    {
        var unicode = Literals.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.UnicodeEscapes);
        Assert.Equal("a\\u0062", unicode.Parse("a\\u0062\\u{63}").ToString());

        var codePoint = Literals.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.CodePointEscapes);
        Assert.Equal("a\\u{63}", codePoint.Parse("a\\u{63}\\u0062").ToString());

        var surrogates = Literals.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.SurrogatePairs);
        Assert.Equal(MathA + "a", surrogates.Parse(MathA + "a\\u0062").ToString());
    }

    [Fact]
    public void WithoutSurrogatePairsSurrogatesAreGivenToThePredicates()
    {
        var parser = Literals.Identifier(static c => c is >= 'a' and <= 'z' or >= 0xD800 and <= 0xDFFF, static c => c is >= 'a' and <= 'z' or >= 0xD800 and <= 0xDFFF, IdentifierOptions.None);

        Assert.Equal(Surrogates("<hi>a<lo>"), parser.Parse(Surrogates("<hi>a<lo> b")).ToString());
    }

    [Fact]
    public void BackslashCanBeAnIdentifierCharWithoutEscapes()
    {
        var parser = Literals.Identifier(static c => c is '\\' or 'a', static c => c is '\\' or 'a', IdentifierOptions.SurrogatePairs);

        Assert.Equal("\\a\\", parser.Parse("\\a\\ b").ToString());
    }

    [Fact]
    public void TermsShouldSkipWhiteSpace()
    {
        var parser = Terms.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.JavaScript).And(Terms.Char(';'));

        Assert.True(parser.TryParse("  \\u0061b ;", out var value));
        Assert.Equal("\\u0061b", value.Item1.ToString());
        Assert.Equal("ab", Character.DecodeIdentifier(value.Item1).ToString());
    }

    [Fact]
    public void ShouldReportPositions()
    {
        var context = new ParseContext(new Scanner("(" + MathA + "\\u{62}"));
        var result = new ParseResult<TextSpan>();

        Assert.True(context.Scanner.ReadChar('('));
        Assert.True(JavaScript.Parse(context, ref result));
        Assert.Equal(1, result.Start);
        Assert.Equal(9, result.End);
        Assert.Equal(10, context.Scanner.Cursor.Position.Column); // Columns count UTF-16 chars, like the offsets
    }

    [Fact]
    public void ShouldWorkInChoices()
    {
        var parser = ZeroOrMany(OneOf(
            Terms.Text("if").Then(static _ => "keyword"),
            Terms.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.JavaScript).Then(static x => Character.DecodeIdentifier(x).ToString()),
            Terms.Char('(').Then(static _ => "("))).Eof();

        Assert.Equal(["keyword", "(", "é", MathA, "ab"], parser.Parse("if ( é " + MathA + " \\u0061b"));
        Assert.False(parser.TryParse("if \\u00", out _));
    }

    [Fact]
    public void ShouldRejectNewLines()
    {
        Assert.Throws<InvalidOperationException>(() => Literals.Identifier(static c => c is 'a', static c => c is 'a' or '\n', IdentifierOptions.None));
        Assert.Throws<InvalidOperationException>(() => Literals.Identifier(static c => c is 'a' or '\r', static c => c is 'a', IdentifierOptions.None));
    }

    [Fact]
    public void ShouldRequirePredicates()
    {
        Assert.Throws<ArgumentNullException>(() => Literals.Identifier(null!, IsJavaScriptPart, IdentifierOptions.None));
        Assert.Throws<ArgumentNullException>(() => Literals.Identifier(IsJavaScriptStart, null!, IdentifierOptions.None));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("a\\")]
    [InlineData("a\\n")]
    [InlineData("a\\u00")]
    [InlineData("a\\u{1D4D0}\\u0062\\u{61")]
    public void DecodeIdentifierKeepsWhatIsNotAnEscape(string input)
    {
        var expected = input.Replace("\\u{1D4D0}", MathB).Replace("\\u0062", "b");

        Assert.Equal(expected, Character.DecodeIdentifier(input));
        Assert.Equal(expected, Character.DecodeIdentifier(new TextSpan(input)).ToString());
    }

    [Fact]
    public void DecodeIdentifierReturnsTheSameValueWithoutEscapes()
    {
        var text = "abc";
        Assert.Same(text, Character.DecodeIdentifier(text));

        var span = new TextSpan("(abc)", 1, 3);
        var decoded = Character.DecodeIdentifier(span);
        Assert.Same(span.Buffer, decoded.Buffer);
        Assert.Equal(1, decoded.Offset);

        var longName = new string('a', 200) + "\\u0062";
        Assert.Equal(new string('a', 200) + "b", Character.DecodeIdentifier(longName));
    }

    [Theory]
    [InlineData("\\u00")]
    [InlineData("a\\u00")]
    [InlineData("a\\u{1D4")]
    [InlineData("a\\")]
    [InlineData("a<hi>")]
    public void ShouldMarkHitEndWhenTheTextCutsAnEscapeOrASurrogatePair(string input)
    {
        var context = new ParseContext(new Scanner(Surrogates(input), isFinal: false));
        var result = new ParseResult<TextSpan>();

        JavaScript.Parse(context, ref result);

        Assert.True(context.Scanner.Cursor.HitEnd);
    }

    [Theory]
    [InlineData("a ")]
    [InlineData("a\\u0020")]
    [InlineData("a<hi>b")]
    [InlineData("1")]
    public void ShouldNotMarkHitEndWhenTheTextDecides(string input)
    {
        var context = new ParseContext(new Scanner(Surrogates(input), isFinal: false));
        var result = new ParseResult<TextSpan>();

        JavaScript.Parse(context, ref result);

        Assert.False(context.Scanner.Cursor.HitEnd);
    }

    [Fact]
    public void ShouldDeclareTheCharsItStartsWith()
    {
        var seekable = Assert.IsAssignableFrom<ISeekable>(JavaScript);

        Assert.True(seekable.CanSeek);
        Assert.False(seekable.SkipWhitespace);
        Assert.Contains('$', seekable.ExpectedChars);
        Assert.Contains('_', seekable.ExpectedChars);
        Assert.Contains('a', seekable.ExpectedChars);
        Assert.Contains('Z', seekable.ExpectedChars);
        Assert.Contains('\\', seekable.ExpectedChars);
        Assert.Contains(Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar, seekable.ExpectedChars);
        Assert.DoesNotContain('0', seekable.ExpectedChars);
        Assert.DoesNotContain('(', seekable.ExpectedChars);
        Assert.DoesNotContain(Parlot.Fluent.OneOf<TextSpan>.OtherSeekableChar, seekable.ExpectedChars);

        // The Terms variant skips the white space first
        var terms = Assert.IsAssignableFrom<ISeekable>(Terms.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.JavaScript));
        Assert.True(terms.CanSeek);
        Assert.True(terms.SkipWhitespace);
    }

    [Fact]
    public void ShouldNotDeclareTheBackslashWithoutEscapes()
    {
        var seekable = Assert.IsAssignableFrom<ISeekable>(Literals.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.SurrogatePairs));

        Assert.DoesNotContain('\\', seekable.ExpectedChars);
        Assert.Contains(Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar, seekable.ExpectedChars);

        // A backslash the predicate accepts is an ASCII char like the others
        seekable = Assert.IsAssignableFrom<ISeekable>(Literals.Identifier(static c => c is '\\' or 'a', static c => c == 'a', IdentifierOptions.None));

        Assert.Contains('\\', seekable.ExpectedChars);
    }

    private static Parser<string> Tokens() => OneOf(
        Terms.Text("if"),
        Terms.Text("("),
        Terms.Text(")"),
        Terms.Identifier(IsJavaScriptStart, IsJavaScriptPart, IdentifierOptions.JavaScript).Then(static x => "id:" + Character.DecodeIdentifier(x).ToString()));

    [Fact]
    public void ShouldGiveAChoiceALookupTable()
    {
        var seekable = Assert.IsAssignableFrom<ISeekable>(Tokens());

        Assert.True(seekable.CanSeek);
        Assert.True(seekable.SkipWhitespace);
        Assert.DoesNotContain(Parlot.Fluent.OneOf<string>.OtherSeekableChar, seekable.ExpectedChars);
        Assert.Contains('(', seekable.ExpectedChars);
        Assert.Contains('i', seekable.ExpectedChars);
        Assert.Contains('\\', seekable.ExpectedChars);
        Assert.Contains(Parlot.Fluent.OneOf<string>.NonAsciiSeekableChar, seekable.ExpectedChars);
    }

    [Theory]
    [InlineData("if", "if")]
    [InlineData("(", "(")]
    [InlineData(" )", ")")]
    [InlineData("iffy", "if")]
    [InlineData("abc", "id:abc")]
    [InlineData("\\u0061bc", "id:abc")]
    [InlineData("\\u{1D4D0}x", "id:" + MathB + "x")]
    [InlineData("\u00e9t\u00e9", "id:\u00e9t\u00e9")]
    [InlineData(MathA + "b", "id:" + MathA + "b")]
    public void ShouldMatchFromAChoice(string input, string expected)
    {
        Assert.True(Tokens().TryParse(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData(" 1")]
    [InlineData("\\n")]
    [InlineData("\\u0031")]
    [InlineData("\u00b2")] // ², not a letter
    [InlineData("+")]
    [InlineData("")]
    public void ShouldRestoreTheCursorWhenAChoiceFails(string input)
    {
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();

        Assert.False(Tokens().Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldSeekInLoops()
    {
        var parser = ZeroOrMany(Tokens()).Eof();

        Assert.Equal(["id:abc", "(", "id:\u00e9", ")", "if", "id:b"], parser.Parse("abc ( \\u00e9 ) if\tb"));
        Assert.Null(parser.Parse("abc 1"));
    }

    [Fact]
    public void ShouldInvokeTheParserOfLookupFromAChoice()
    {
        var pattern = Literals.Pattern(static c => c is 'a' or 'b');
        var lookup = pattern.Lookup(false, 'a', 'b');
        var choice = OneOf(lookup, Literals.Text("(").Then(static x => new TextSpan(x)), pattern);
        var entered = new List<object>();

        var context = new ParseContext(new Scanner("ab")) { OnEnterParser = (parser, _) => entered.Add(parser) };
        var result = new ParseResult<TextSpan>();

        Assert.True(choice.Parse(context, ref result));
        Assert.Equal("ab", result.Value.ToString());
        Assert.Contains(pattern, entered);
        Assert.DoesNotContain(lookup, entered);

        // The pattern is listed twice, from the Lookup and by itself: it is invoked once when it fails
        entered.Clear();
        context = new ParseContext(new Scanner("c")) { OnEnterParser = (parser, _) => entered.Add(parser) };

        Assert.False(choice.Parse(context, ref result));
        Assert.Single(entered, x => ReferenceEquals(x, pattern));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }

    [Fact]
    public void ShouldSkipTheWhiteSpaceOnceForTheParserOfLookup()
    {
        // Every alternative skips white space, the choice does it once and invokes the pattern of the Lookup
        var pattern = Literals.Pattern(static c => c is 'a' or 'b');
        var skip = SkipWhiteSpace(pattern);
        var lookup = skip.Lookup(true, 'a', 'b');
        var entered = new List<object>();

        foreach (var choice in new[] { OneOf(Terms.Text("(").Then(static x => new TextSpan(x)), lookup), Longest(Terms.Text("(").Then(static x => new TextSpan(x)), lookup) })
        {
            entered.Clear();

            var context = new ParseContext(new Scanner("  ab")) { OnEnterParser = (parser, _) => entered.Add(parser) };
            var result = new ParseResult<TextSpan>();

            Assert.True(choice.Parse(context, ref result));
            Assert.Equal("ab", result.Value.ToString());
            Assert.Equal(4, context.Scanner.Cursor.Offset);
            Assert.Contains(pattern, entered);
            Assert.DoesNotContain(lookup, entered);
            Assert.DoesNotContain(skip, entered);

            // It fails where it started, before the white space
            context = new ParseContext(new Scanner("  c"));

            Assert.False(choice.Parse(context, ref result));
            Assert.Equal(0, context.Scanner.Cursor.Offset);
        }
    }
}
