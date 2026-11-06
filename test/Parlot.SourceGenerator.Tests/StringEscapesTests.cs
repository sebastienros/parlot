#nullable enable

using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Generated string literals with escapes other than the default ones must match the runtime parser, from strings and
// from readers.
public class StringEscapesTests
{
    // The runtime parsers of StringEscapesGrammars.parlot.cs, which isn't compiled in the tests
    private static class Runtime
    {
        public static Parser<IReadOnlyList<string>> Strings(StringEscapes escapes) =>
            ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, escapes).Then(static span => span.ToString())).Eof();

        public static Parser<IReadOnlyList<string>> PooledStrings(StringEscapes escapes) =>
            ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, escapes).ToPooledString()).Eof();

        public static Parser<int> Captured(StringEscapes escapes) =>
            Capture(ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, escapes))).Then(static span => span.Length).Eof();
    }

    private delegate bool StringParser<T>(string text, out T value);
    private delegate bool ReaderParser<T>(TextReader reader, out T value);

    private static void Check<T>(Parser<T> runtime, StringParser<T> generated, ReaderParser<T> reader, string input)
    {
        var expected = runtime.TryParse(input, out var expectedValue);

        Assert.Equal(expected, generated(input, out var value));
        Assert.Equal(expected, reader(new StringReader(input), out var readValue));

        if (expected)
        {
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedValue, readValue);
        }
    }

    public static TheoryData<string> Inputs => new()
    {
        "",
        "'plain' \"plain\"",
        @"'\/' '\$' '\a' '\8'",
        "'a\\\nb' 'a\\\r\nb' 'a\\\rb' 'a\\\u2028b'",
        @"'\u{41}' '\u{1F600}' '\u{10FFFF}' '\u0041'",
        @"'\101' '\0' '\08' '\377' '\1234'",
        @"'\x41B' '\x4' '\e' '\U0001F600' '\s' '\d'",
        @"'it''s' '' ''''",
        @"'C:\temp\' 'x'",
        @"""\""\\\/\b\f\n\r\t\u0041""",
        "'a\nb'",
        "\"a\tb\"",
        @"'\u{110000}'",
        @"'\u{12'",
        @"'\u12'",
        @"'\x4g'",
        @"'unterminated",
        "'" + new string('a', 40) + @"\$" + new string('b', 40) + @"\u{1F600}" + new string('c', 40) + "'",
    };

    [Theory]
    [MemberData(nameof(Inputs))]
    public void JavaScript_Matches_The_Runtime_Parser(string input)
    {
        Check(Runtime.Strings(StringEscapes.JavaScript), StringEscapesGrammars.TryParseJavaScript, StringEscapesGrammars.TryParseJavaScript, input);
        Check(Runtime.PooledStrings(StringEscapes.JavaScript), StringEscapesGrammars.TryParsePooledJavaScript, StringEscapesGrammars.TryParsePooledJavaScript, input);
        Check(Runtime.Captured(StringEscapes.JavaScript), StringEscapesGrammars.TryParseCapturedJavaScript, StringEscapesGrammars.TryParseCapturedJavaScript, input);
    }

    [Theory]
    [MemberData(nameof(Inputs))]
    public void Presets_Match_The_Runtime_Parser(string input)
    {
        Check(Runtime.Strings(StringEscapes.CSharp), StringEscapesGrammars.TryParseCSharp, StringEscapesGrammars.TryParseCSharp, input);
        Check(Runtime.Strings(StringEscapes.Json), StringEscapesGrammars.TryParseJson, StringEscapesGrammars.TryParseJson, input);
        Check(Runtime.Strings(StringEscapes.Python), StringEscapesGrammars.TryParsePython, StringEscapesGrammars.TryParsePython, input);
        Check(Runtime.Strings(StringEscapes.Java), StringEscapesGrammars.TryParseJava, StringEscapesGrammars.TryParseJava, input);
        Check(Runtime.Strings(StringEscapes.Sql), StringEscapesGrammars.TryParseSql, StringEscapesGrammars.TryParseSql, input);
        Check(Runtime.PooledStrings(StringEscapes.Sql), StringEscapesGrammars.TryParsePooledSql, StringEscapesGrammars.TryParsePooledSql, input);
        Check(Runtime.Captured(StringEscapes.Sql), StringEscapesGrammars.TryParseCapturedSql, StringEscapesGrammars.TryParseCapturedSql, input);
    }

    [Theory]
    [InlineData(@"'\u{1F600}' 'a\$b'", new[] { "\U0001F600", "a$b" })]
    [InlineData("'a\\\r\nb'", new[] { "ab" })]
    public void JavaScript_Strings_Are_Decoded(string input, string[] expected)
    {
        Assert.True(StringEscapesGrammars.TryParseJavaScript(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("'a\nb'")]
    [InlineData(@"'\u12'")]
    [InlineData(@"'a\")]
    public void Invalid_JavaScript_Strings_Fail(string input)
    {
        Assert.False(StringEscapesGrammars.TryParseJavaScript(input, out _));
        Assert.False(StringEscapesGrammars.TryParseCapturedJavaScript(input, out _));
    }

    [Fact]
    public void Sql_Strings_Are_Decoded()
    {
        Assert.True(StringEscapesGrammars.TryParseSql(@"'it''s' 'C:\temp\'", out var value));
        Assert.Equal(new[] { "it's", @"C:\temp\" }, value);
    }
}

public static partial class StringEscapesGrammars
{
    public static partial bool TryParseJavaScript(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseJavaScript(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParsePooledJavaScript(string text, out IReadOnlyList<string> value);
    public static partial bool TryParsePooledJavaScript(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseCapturedJavaScript(string text, out int value);
    public static partial bool TryParseCapturedJavaScript(TextReader reader, out int value);
    public static partial bool TryParseCSharp(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseCSharp(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseJson(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseJson(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParsePython(string text, out IReadOnlyList<string> value);
    public static partial bool TryParsePython(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseJava(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseJava(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseSql(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSql(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParsePooledSql(string text, out IReadOnlyList<string> value);
    public static partial bool TryParsePooledSql(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseCapturedSql(string text, out int value);
    public static partial bool TryParseCapturedSql(TextReader reader, out int value);
}
