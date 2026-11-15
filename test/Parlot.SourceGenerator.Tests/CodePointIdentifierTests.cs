#nullable enable

using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Generated identifiers of code points must match the runtime parser, from strings and from readers.
public class CodePointIdentifierTests
{
    private delegate bool StringParser(string text, out IReadOnlyList<string> value);
    private delegate bool ReaderParser(TextReader reader, out IReadOnlyList<string> value);

    private static bool IsStart(int c) => c is '$' or '_' || (c < 0x10000 ? char.IsLetter((char)c) : c is >= 0x1D400 and <= 0x1D7FF);

    private static bool IsPart(int c) => c is '$' or '_' or 0x200C or 0x200D || (c < 0x10000 ? char.IsLetterOrDigit((char)c) : c is >= 0x1D400 and <= 0x1D7FF);

    // Lone surrogates don't survive the serialization of the test cases, they are written <hi> and <lo>
    private static string Surrogates(string text) => text.Replace("<hi>", "\ud835").Replace("<lo>", "\udc9c");

    private static void Check(Parser<IReadOnlyList<string>> runtime, StringParser generated, ReaderParser reader, string input, bool success)
    {
        input = Surrogates(input);

        var expected = runtime.TryParse(input, out var expectedValue);

        Assert.Equal(success, expected);
        Assert.Equal(expected, generated(input, out var value));
        Assert.Equal(expected, reader(new StringReader(input), out var readValue));

        if (expected)
        {
            Assert.Equal(expectedValue, value);
            Assert.Equal(expectedValue, readValue);
        }
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("\\u0061bc a\\u{62}c", true)]
    [InlineData("\\u{1D4D0}x \U0001D49C a\U0001D49Cb", true)]
    [InlineData("été a‌b", true)]
    [InlineData("\\u00", false)]
    [InlineData("a\\u00", false)]
    [InlineData("a\\u{1D4", false)]
    [InlineData("a\\u0020", false)]
    [InlineData("\\u0031", false)]
    [InlineData("a<hi>", false)]
    [InlineData("a<hi>b", false)]
    [InlineData("<lo>", false)]
    [InlineData("a\\u{110000}", false)]
    public void Identifiers_Match_The_Runtime_Parser(string input, bool success)
    {
        var runtime = ZeroOrMany(Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript).Then(static x => Character.DecodeIdentifier(x).ToString())).Eof();

        Check(runtime, CodePointIdentifierGrammars.TryParseIdentifiers, CodePointIdentifierGrammars.TryParseIdentifiers, input, success);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("#abc;#\\u0061b;#\U0001D49Cx;", true)]
    [InlineData("#\\u{61};", false)]
    [InlineData("#a\\u00", false)]
    [InlineData("#a\\u00;", false)]
    [InlineData("#a<hi>;", false)]
    [InlineData("#a<hi>", false)]
    [InlineData("#1;", false)]
    public void Span_Code_Matches_The_Runtime_Parser(string input, bool success)
    {
        var runtime = ZeroOrMany(Capture(Literals.Char('#').And(Literals.Identifier(IsStart, IsPart, IdentifierOptions.UnicodeEscapes | IdentifierOptions.SurrogatePairs)).And(Literals.Char(';'))).Then(static x => x.ToString())).Eof();

        Check(runtime, CodePointIdentifierGrammars.TryParseSpans, CodePointIdentifierGrammars.TryParseSpans, input, success);
    }
    [Theory]
    [InlineData("", true)]
    [InlineData("if(iffy (\\u0069f)", true)]
    [InlineData("( \\u{1D4D0} \u00e9t\u00e9 \U0001D49Cb", true)]
    [InlineData("# ## a#)", true)]
    [InlineData("if 1", false)]
    [InlineData("\\u0031", false)]
    [InlineData("a \u00b2", false)]
    [InlineData("a\\u00", false)]
    [InlineData("a<hi>", false)]
    public void Choices_Match_The_Runtime_Parser(string input, bool success)
    {
        var runtime = ZeroOrMany(OneOf(
            Terms.Text("if"),
            Terms.Text("("),
            Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript).Then(static x => "id:" + Character.DecodeIdentifier(x).ToString()),
            Terms.Pattern(static c => c == '#').Lookup(true, '#').Then(static x => x.ToString()),
            Terms.Text(")"))).Eof();

        Check(runtime, CodePointIdentifierGrammars.TryParseTokens, CodePointIdentifierGrammars.TryParseTokens, input, success);
    }

    // The 4096th char, where a reader's buffer ends, falls inside each token in turn, so the choice reads it again
    // with more text
    [Theory]
    [InlineData("\\u{1D4D0}x if(")]
    [InlineData("\U0001D49Cb été)")]
    [InlineData("iffy ## a\\u0062")]
    [InlineData("a\\u00 b")]
    public void Choices_Match_The_Runtime_Parser_Across_The_Buffer(string tokens)
    {
        var runtime = ZeroOrMany(OneOf(
            Terms.Text("if"),
            Terms.Text("("),
            Terms.Identifier(IsStart, IsPart, IdentifierOptions.JavaScript).Then(static x => "id:" + Character.DecodeIdentifier(x).ToString()),
            Terms.Pattern(static c => c == '#').Lookup(true, '#').Then(static x => x.ToString()),
            Terms.Text(")"))).Eof();

        for (var i = 1; i <= tokens.Length; i++)
        {
            var input = new string(' ', 4096 - i) + tokens;
            var expected = runtime.TryParse(input, out var expectedValue);

            Assert.Equal(expected, CodePointIdentifierGrammars.TryParseTokens(input, out var value));
            Assert.Equal(expected, CodePointIdentifierGrammars.TryParseTokens(new StringReader(input), out var readValue));

            if (expected)
            {
                Assert.Equal(expectedValue, value);
                Assert.Equal(expectedValue, readValue);
            }
        }
    }
}

public static partial class CodePointIdentifierGrammars
{
    public static partial bool TryParseIdentifiers(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseIdentifiers(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseSpans(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSpans(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseTokens(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseTokens(TextReader reader, out IReadOnlyList<string> value);
}
