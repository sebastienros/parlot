#nullable enable

using System;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Generated keywords with custom identifier chars must match the runtime parser, from strings and from readers
// filled one char at a time, also when the keyword ends a reader's buffer.
public class KeywordIdentifierPartTests
{
    private const string LiquidIdentifierPart = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-";

    private delegate bool StringParser(string text, out string value);
    private delegate bool ReaderParser(TextReader reader, out string value);

    // The generated reader entry points read 4096 chars at a time
    private const int BufferSize = 4096;

    private static readonly string[] Inputs =
    [
        "", "i", "in", " in", "in ", "in(", "in.x", "inside", "in1", "in_x", "in-x", "IN", "In-", "iné", "INé", "inè",
        "if", "if1", "ifx", "else", "elsif", "elsewhere", "for", "for_", "and", "or", "orb", "with", "with-x",
    ];

    [Fact]
    public void Generated_Keywords_Match_The_Runtime_Parser()
    {
        foreach (var input in Inputs)
        {
            Check(Terms.Keyword("in", LiquidIdentifierPart), KeywordIdentifierPartGrammars.TryParseLiquid, KeywordIdentifierPartGrammars.TryParseLiquid, input);
            Check(Literals.Keyword("in", "é-", caseInsensitive: true, returnMatchedText: true), KeywordIdentifierPartGrammars.TryParseNonAscii, KeywordIdentifierPartGrammars.TryParseNonAscii, input);
            Check(Choice(), KeywordIdentifierPartGrammars.TryParseChoice, KeywordIdentifierPartGrammars.TryParseChoice, input);
        }

        Assert.False(KeywordIdentifierPartGrammars.TryParseLiquid("in-x", out _));
        Assert.False(KeywordIdentifierPartGrammars.TryParseNonAscii("In-", out _));
        Assert.True(KeywordIdentifierPartGrammars.TryParseNonAscii("Inx", out var matched));
        Assert.Equal("In", matched);
        Assert.False(KeywordIdentifierPartGrammars.TryParseChoice("in_x", out _));
        Assert.False(KeywordIdentifierPartGrammars.TryParseChoice("inside", out _));
    }

    [Fact]
    public void Generated_Keywords_Check_The_Boundary_After_A_Buffer_End()
    {
        // The keyword ends a buffer, and the boundary char starts the next one
        foreach (var input in new[] { "in", "in ", "in1", "in-", "in_x", "inside", "in(" })
        {
            for (var split = 0; split <= input.Length + 1; split++)
            {
                foreach (var padding in new[] { BufferSize - split, 2 * BufferSize - split })
                {
                    var text = new string(' ', padding) + input;
                    Check(Terms.Keyword("in", LiquidIdentifierPart), KeywordIdentifierPartGrammars.TryParseLiquid, KeywordIdentifierPartGrammars.TryParseLiquid, text);
                    Check(Choice(), KeywordIdentifierPartGrammars.TryParseChoice, KeywordIdentifierPartGrammars.TryParseChoice, text);
                }
            }
        }
    }

    [Fact]
    public void Keyword_Lookup_Excludes_Custom_Identifier_Chars()
    {
        var keywords = new[] { "if", "else", "elsif", "for", "in", "and", "or", "with" };

        // The same choice with default keywords only is a keyword lookup
        Assert.Contains("MatchKeyword", Generate(OneOf(keywords.Select(static word => Terms.Keyword(word)).ToArray())), StringComparison.Ordinal);
        Assert.DoesNotContain("MatchKeyword", Generate(Choice()), StringComparison.Ordinal);

        // The default boundary still emits the letter check, a custom one the bitset
        Assert.Contains("IsInRange", Generate(Literals.Keyword("in")), StringComparison.Ordinal);
        Assert.DoesNotContain("IsInRange", Generate(Literals.Keyword("in", LiquidIdentifierPart)), StringComparison.Ordinal);
    }

    private static Parser<string> Choice() =>
        OneOf(
            Terms.Keyword("if"), Terms.Keyword("else"), Terms.Keyword("elsif"), Terms.Keyword("for"),
            Terms.Keyword("in", LiquidIdentifierPart), Terms.Keyword("and"), Terms.Keyword("or"), Terms.Keyword("with"));

    private static string Generate(Parser<string> parser) =>
        string.Join(Environment.NewLine, Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(new SourceGenerationContext()).Body);

    private static void Check(Parser<string> parser, StringParser parseString, ReaderParser parseReader, string input)
    {
        var expected = parser.TryParse(input, out var expectedValue);

        Assert.Equal(expected, parseString(input, out var value));
        Assert.Equal(expected ? expectedValue : default, value);

        foreach (var reader in new TextReader[] { new StringReader(input), new VectorizedTokenTests.ChunkedReader(input, 1), new VectorizedTokenTests.ChunkedReader(input, 13) })
        {
            Assert.Equal(expected, parseReader(reader, out value));
            Assert.Equal(expected ? expectedValue : default, value);
        }
    }
}

public static partial class KeywordIdentifierPartGrammars
{
    public static partial bool TryParseLiquid(string text, out string value);
    public static partial bool TryParseLiquid(TextReader reader, out string value);
    public static partial bool TryParseNonAscii(string text, out string value);
    public static partial bool TryParseNonAscii(TextReader reader, out string value);
    public static partial bool TryParseChoice(string text, out string value);
    public static partial bool TryParseChoice(TextReader reader, out string value);
}
