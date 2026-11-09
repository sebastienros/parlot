#nullable enable

using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests;

public class StringEscapesTests
{
    // A text longer than the decoder's vectorized chunk, around the escape sequences
    private const string Long = "0123456789abcdef";

    public static TheoryData<string, string> JavaScriptStrings => new()
    {
        // Any char stands for itself
        { @"'\/'", "/" },
        { @"'\$'", "$" },
        { @"'\.'", "." },
        { @"'\a'", "a" },
        { @"'\8\9'", "89" },
        { @"'\z\%'", "z%" },
        { "'\\\u00e9'", "\u00e9" },
        // Line continuations are removed
        { "'a\\\nb'", "ab" },
        { "'a\\\r\nb'", "ab" },
        { "'a\\\rb'", "ab" },
        { "'a\\\r\\\rb'", "ab" },
        { "'a\\\u2028b'", "ab" },
        { "'a\\\u2029b'", "ab" },
        { "'\\\n'", "" },
        { "'a\\\n\\\nb'", "ab" },
        // Raw U+2028 and U+2029 are allowed
        { "'a\u2028b'", "a\u2028b" },
        // Braced code points
        { @"'\u{41}'", "A" },
        { @"'\u{0000041}'", "A" },
        { @"'\u{1F600}'", "\U0001F600" },
        { @"'\u{10FFFF}'", "\U0010FFFF" },
        { @"'\u{FFFF}\u{10000}'", "\uFFFF\U00010000" },
        { @"'\u0041\u{42}'", "AB" },
        // Legacy octal escapes
        { @"'\101'", "A" },
        { @"'\0'", "\0" },
        { @"'\01'", "\u0001" },
        { @"'\08'", "\08" },
        { @"'\377'", "\u00FF" },
        { @"'\400'", " 0" },
        { @"'\77'", "?" },
        { @"'\1234'", "S4" },
        { @"'\7'", "\u0007" },
        // Two hex digits
        { @"'\x41'", "A" },
        { @"'\x41B'", "AB" },
        // The other escape sequences
        { @"'\b\f\n\r\t\v'", "\b\f\n\r\t\v" },
        { @"'\'\""\\'", "'\"\\" },
        { @"""a\""b""", "a\"b" },
        { "'" + Long + @"\$" + Long + @"\u{1F600}" + Long + "\\\r\n" + Long + "'", Long + "$" + Long + "\U0001F600" + Long + Long },
    };

    [Theory]
    [MemberData(nameof(JavaScriptStrings))]
    public void JavaScriptStringsAreDecoded(string text, string expected)
    {
        var parser = Literals.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript);

        Assert.Equal(expected, parser.Parse(text).ToString());
        Assert.Equal(expected, Terms.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript).Parse("  " + text).ToString());
        Assert.Equal(expected, parser.ToPooledString().Parse(text));
        Assert.Equal(expected, new StringLiteral(text[0], StringEscapes.JavaScript).Parse(text).ToString());

        // The whole literal is read
        Assert.Equal(text, Capture(parser).Parse(text).ToString());
    }

    [Theory]
    [InlineData(@"'\u{}'")]
    [InlineData(@"'\u{110000}'")]
    [InlineData(@"'\u{FFFFFFFFF}'")]
    [InlineData(@"'\u{12'")]
    [InlineData(@"'\u{12")]
    [InlineData(@"'\u{12g}'")]
    [InlineData(@"'\u{12 }'")]
    [InlineData(@"'\u12'")]
    [InlineData(@"'\u'")]
    [InlineData(@"'\x4'")]
    [InlineData(@"'\x4g'")]
    [InlineData(@"'\x'")]
    [InlineData(@"'\x")]
    [InlineData(@"'\'")]
    [InlineData(@"'a\")]
    [InlineData("'a\\\n")]
    [InlineData("'a\\\r\n")]
    [InlineData(@"'\101")]
    [InlineData(@"'abc")]
    [InlineData(@"""abc'")]
    [InlineData("abc")]
    [InlineData("")]
    // Single line
    [InlineData("'a\nb'")]
    [InlineData("'a\rb'")]
    [InlineData("'\\$a\nb'")]
    [InlineData("'a\\\n\nb'")]
    public void InvalidJavaScriptStringsFailAndRestoreTheCursor(string text)
    {
        AssertFails(new StringLiteral(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript), text);
        AssertFails(new StringLiteral(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript).ToPooledString(), text);
    }

    public static TheoryData<StringEscapes, string, string?> Strings => new()
    {
        // The default escapes are unchanged
        { StringEscapes.Default, @"'\a'", "\a" },
        { StringEscapes.Default, @"'\x41B'", "\u041B" },
        { StringEscapes.Default, @"'\x4'", "\u0004" },
        { StringEscapes.Default, @"'\01'", "\u00001" },
        { StringEscapes.Default, @"'\u0041'", "A" },
        { StringEscapes.Default, "'a\nb'", "a\nb" },
        { StringEscapes.Default, @"'\$'", null },
        { StringEscapes.Default, @"'\101'", null },
        { StringEscapes.Default, @"'\u{41}'", null },
        { StringEscapes.Default, "'a\\\nb'", null },
        { StringEscapes.Default, @"'\8'", null },
        { StringEscapes.Default, @"'\e'", null },

        // Without any flag, or without backslash escapes, a backslash is an ordinary char
        { StringEscapes.None, @"'a\nb'", @"a\nb" },
        { StringEscapes.None, @"'a\'", @"a\" },
        { StringEscapes.None, "'a\nb'", "a\nb" },
        { StringEscapes.SingleLine, "'a\nb'", null },

        // Each flag adds its escape sequences, and a backslash
        { StringEscapes.EscapedQuote, @"'a\'b\\'", @"a'b\" },
        { StringEscapes.EscapedQuote, @"'a\""b'", null },
        { StringEscapes.Quotes, @"'\""\''", "\"'" },
        { StringEscapes.NewLineAndTab, @"'\n\r\t'", "\n\r\t" },
        { StringEscapes.NewLineAndTab, @"'\b'", null },
        { StringEscapes.BackspaceAndFormFeed, @"'\b\f'", "\b\f" },
        { StringEscapes.VerticalTab, @"'\v'", "\v" },
        { StringEscapes.Alert, @"'\a'", "\a" },
        { StringEscapes.Null, @"'\0'", "\0" },
        { StringEscapes.Null, @"'\01'", "\u00001" },
        { StringEscapes.Escape, @"'\e'", "\u001b" },
        { StringEscapes.Slash, @"'\/'", "/" },
        { StringEscapes.QuestionMark, @"'\?'", "?" },
        { StringEscapes.Space, @"'\s'", " " },
        { StringEscapes.Unicode, @"'\u0041'", "A" },
        { StringEscapes.Unicode, @"'\u{41}'", null },
        { StringEscapes.LongUnicode, @"'\U0001F600'", "\U0001F600" },
        { StringEscapes.LongUnicode, @"'\U00000041'", "A" },
        { StringEscapes.LongUnicode, @"'\U00110000'", null },
        { StringEscapes.LongUnicode, @"'\UFFFFFFFF'", null },
        { StringEscapes.LongUnicode, @"'\U0041'", null },
        { StringEscapes.BracedUnicode, @"'\u{41}'", "A" },
        { StringEscapes.BracedUnicode, @"'\u0041'", null },
        { StringEscapes.BracedUnicode | StringEscapes.Unicode, @"'\u0041\u{42}'", "AB" },
        { StringEscapes.Hex, @"'\x41B'", "\u041B" },
        { StringEscapes.Hex, @"'\x41B12'", "\u41B12" },
        { StringEscapes.TwoDigitHex, @"'\x41B'", "AB" },
        { StringEscapes.TwoDigitHex | StringEscapes.Hex, @"'\x41B'", "AB" },
        { StringEscapes.Octal, @"'\101\0'", "A\0" },
        { StringEscapes.Octal, @"'\8'", null },
        { StringEscapes.AnyChar, @"'\$\a'", "$a" },
        { StringEscapes.AnyChar, "'a\\\nb'", "a\nb" },
        { StringEscapes.AnyChar | StringEscapes.Unicode, @"'\u12'", null },
        { StringEscapes.KeepUnknown, @"'\d\$'", @"\d\$" },
        { StringEscapes.KeepUnknown | StringEscapes.AnyChar, @"'\d'", @"\d" },
        { StringEscapes.KeepUnknown | StringEscapes.NewLineAndTab, @"'\d\n\\'", "\\d\n\\" },
        { StringEscapes.LineContinuation, "'a\\\r\nb'", "ab" },
        { StringEscapes.LineContinuation, "'a\\\u0085b'", null },
        { StringEscapes.DoubledQuote, "'it''s'", "it's" },
        { StringEscapes.DoubledQuote, "''''", "'" },
        { StringEscapes.DoubledQuote, "''", "" },
        { StringEscapes.DoubledQuote, "'a'' 'b'", "a' " },
        { StringEscapes.DoubledQuote, @"'a\'", @"a\" },
        { StringEscapes.DoubledQuote, "'a''", null },
        { StringEscapes.DoubledQuote | StringEscapes.Default, @"'it''s \n'", "it's \n" },
        { StringEscapes.SingleLine | StringEscapes.Default, "'a\\nb'", "a\nb" },
        { StringEscapes.SingleLine | StringEscapes.Default, "'a\nb'", null },
        { StringEscapes.SingleLine | StringEscapes.AnyChar, "'a\\\nb'", null },
        { StringEscapes.NoControlChars | StringEscapes.Default, "'a\tb'", null },
        { StringEscapes.NoControlChars | StringEscapes.Default, "'a\\tb\u007f'", "a\tb\u007f" },

        // The presets
        { StringEscapes.CSharp, @"'\a\e\U0001F600\x41B'", "\a\u001b\U0001F600\u041B" },
        { StringEscapes.CSharp, "'a\nb'", null },
        { StringEscapes.CSharp, @"'\$'", null },
        { StringEscapes.Json, @"""\""\\\/\b\f\n\r\t\u0041""", "\"\\/\b\f\n\r\t\u0041" },
        { StringEscapes.Json, @"""\'""", null },
        { StringEscapes.Json, @"""\a""", null },
        { StringEscapes.Json, @"""\v""", null },
        { StringEscapes.Json, @"""\0""", null },
        { StringEscapes.Json, @"""\x41""", null },
        { StringEscapes.Json, "\"a\tb\"", null },
        { StringEscapes.Json, "\"a\u0001b\"", null },
        { StringEscapes.Python, @"'\d\101\x41B\N'", @"\dAAB\N" },
        { StringEscapes.Python, @"'\U0001F600\a'", "\U0001F600\a" },
        { StringEscapes.Python, "'a\\\nb'", "ab" },
        { StringEscapes.Python, "'a\nb'", null },
        { StringEscapes.Java, @"'\s\101\u0041\0'", " AA\0" },
        { StringEscapes.Java, @"'\a'", null },
        { StringEscapes.Java, @"'\v'", null },
        { StringEscapes.Sql, "'it''s'", "it's" },
        { StringEscapes.Sql, @"'C:\temp\'", @"C:\temp\" },
        { StringEscapes.Sql, "'a\nb'", "a\nb" },
        { StringEscapes.Sql, "'it's'", "it" },
    };

    [Theory]
    [MemberData(nameof(Strings))]
    public void EscapesAreReadAndDecoded(StringEscapes escapes, string text, string? expected)
    {
        var parser = new StringLiteral(StringLiteralQuotes.SingleOrDouble, escapes);

        Assert.Equal(escapes, parser.Escapes);

        if (expected == null)
        {
            AssertFails(parser, text);
            AssertFails(parser.ToPooledString(), text);
        }
        else
        {
            Assert.Equal(expected, parser.Parse(text).ToString());
            Assert.Equal(expected, parser.ToPooledString().Parse(text));
        }

        // The default constructor and parser have the default escapes
        if (escapes == StringEscapes.Default)
        {
            Assert.Equal(expected, Literals.String().TryParse(text, out var value) ? value.ToString() : null);
            Assert.Equal(StringEscapes.Default, new StringLiteral(StringLiteralQuotes.SingleOrDouble).Escapes);
        }
    }

    [Fact]
    public void CustomQuotesCanBeEscaped()
    {
        var parser = new StringLiteral('%', StringEscapes.JavaScript);

        Assert.Equal("a%b$", parser.Parse(@"%a\%b\$%").ToString());
        AssertFails(parser, @"%a\%");

        Assert.Equal("a%b", new StringLiteral('%', StringEscapes.Sql).Parse("%a%%b%").ToString());
    }

    [Fact]
    public void BackslashQuotesEndAtTheNextBackslash()
    {
        Assert.Equal("n", new StringLiteral('\\', StringEscapes.JavaScript).Parse(@"\n\rest").ToString());
    }

    [Theory]
    [InlineData(StringEscapes.JavaScript)]
    [InlineData(StringEscapes.Sql)]
    [InlineData(StringEscapes.None)]
    public void StringsWithoutEscapesReuseTheInput(StringEscapes escapes)
    {
        var text = "'plain'";
        var result = Literals.String(StringLiteralQuotes.Single, escapes).Parse(text);

        Assert.Same(text, result.Buffer);
        Assert.Equal("plain", result.ToString());
    }

    [Fact]
    public void ContinuationsKeepLinesAndColumns()
    {
        var parser = Literals.String(StringLiteralQuotes.Single, StringEscapes.JavaScript).And(Literals.Char('!'));
        var context = new ParseContext(new Scanner("'a\\\r\nb\\\nc'!"));

        Assert.True(parser.TryParse(context, out var value, out _));
        Assert.Equal("abc", value.Item1.ToString());
        Assert.Equal(new TextPosition(11, 3, 4), context.Scanner.Cursor.Position);
    }

    private static void AssertFails<T>(Parser<T> parser, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var result = new ParseResult<T>();

        Assert.False(parser.Parse(context, ref result));
        Assert.Equal(0, context.Scanner.Cursor.Offset);
    }
}
