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
}

public static partial class CodePointIdentifierGrammars
{
    public static partial bool TryParseIdentifiers(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseIdentifiers(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseSpans(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseSpans(TextReader reader, out IReadOnlyList<string> value);
}
