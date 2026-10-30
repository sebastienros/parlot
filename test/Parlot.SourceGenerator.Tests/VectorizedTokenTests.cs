#nullable enable

using System;
using System.IO;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated TextBefore, Identifier and Pattern parsers search or count on the cursor's span like the runtime
// ones. Their results, from strings and from readers filled one char at a time, must match the runtime parser's.
public class VectorizedTokenTests
{
    private delegate bool StringParser<T>(string text, out T value);
    private delegate bool ReaderParser<T>(TextReader reader, out T value);

    // The generated reader entry points read 4096 chars at a time
    private const int BufferSize = 4096;

    [Theory]
    [InlineData("abc!def")]
    [InlineData("abc--def")]
    [InlineData("a-b!c--d")]
    [InlineData("abcdef")]
    [InlineData("")]
    [InlineData("--")]
    public void Delimiter_Which_Is_Not_Seekable_In_A_OneOf_Is_Found(string text)
    {
        var delimiter = Literals.Text("--").Or(Literals.Pattern(static c => c == '!').Then(static x => x.ToString()));
        Assert.Contains(Parlot.Fluent.OneOf<string>.OtherSeekableChar, ((Parlot.Rewriting.ISeekable)delimiter).ExpectedChars);

        Check(Text(AnyCharBefore(delimiter)), VectorizedTokenGrammars.TryParseOtherSeekableDelimiter, VectorizedTokenGrammars.TryParseOtherSeekableDelimiter, text);
    }

    [Theory]
    [InlineData("abc   end")]
    [InlineData("abc\n end")]
    [InlineData("abcend")]
    [InlineData("abc   en")]
    [InlineData("end")]
    [InlineData("  end")]
    public void Delimiter_Skipping_White_Space_Is_Found(string text)
    {
        Check(Text(AnyCharBefore(Terms.Text("end"))), VectorizedTokenGrammars.TryParseWhiteSpaceDelimiter, VectorizedTokenGrammars.TryParseWhiteSpaceDelimiter, text);
    }

    [Theory]
    [InlineData("abc-->def")]
    [InlineData("a-b->c-->d")]
    [InlineData("a==>b")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("-->")]
    [InlineData("--")]
    [InlineData("a-=-=b==>")]
    public void Seekable_Delimiter_Is_Found(string text)
    {
        Check(Text(AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>")))), VectorizedTokenGrammars.TryParseSeekableDelimiter, VectorizedTokenGrammars.TryParseSeekableDelimiter, text);
    }

    [Theory]
    [InlineData("abc;def")]
    [InlineData(";")]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("a\nb;c")]
    public void Single_Char_Delimiter_Is_Found(string text)
    {
        Check(Text(AnyCharBefore(Literals.Char(';'))), VectorizedTokenGrammars.TryParseSingleCharDelimiter, VectorizedTokenGrammars.TryParseSingleCharDelimiter, text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--")]
    [InlineData("abc")]
    [InlineData("abc--")]
    [InlineData("abc--def")]
    [InlineData("a-b--c")]
    [InlineData("abc-")]
    [InlineData("-")]
    [InlineData("a\nb--c")]
    public void TextBefore_Options_Match_The_Runtime(string text)
    {
        Check(Text(AnyCharBefore(Literals.Text("--"), canBeEmpty: true)), VectorizedTokenGrammars.TryParseCanBeEmpty, VectorizedTokenGrammars.TryParseCanBeEmpty, text);
        Check(Text(AnyCharBefore(Literals.Text("--"), failOnEof: true)), VectorizedTokenGrammars.TryParseFailOnEof, VectorizedTokenGrammars.TryParseFailOnEof, text);
        Check(Text(AnyCharBefore(Literals.Text("--"), failOnEof: true, consumeDelimiter: true)), VectorizedTokenGrammars.TryParseConsumeDelimiterFailOnEof, VectorizedTokenGrammars.TryParseConsumeDelimiterFailOnEof, text);

        var consumeDelimiter = AnyCharBefore(Literals.Text("--"), consumeDelimiter: true)
            .Then(static (ParseContext context, TextSpan span) => (span.ToString(), context.Scanner.Cursor.Offset));
        Check(consumeDelimiter, VectorizedTokenGrammars.TryParseConsumeDelimiter, VectorizedTokenGrammars.TryParseConsumeDelimiter, text);
    }

    [Fact]
    public void TextBefore_Results_Do_Not_Depend_On_The_Buffer_Boundaries()
    {
        // Each input ends up at every position relative to the end of the first and second buffers
        CheckPadded(Text(AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>")))), VectorizedTokenGrammars.TryParseSeekableDelimiter, VectorizedTokenGrammars.TryParseSeekableDelimiter, 'a', "-->", "==>", "b-->", "-=->", "--", "");
        CheckPadded(Text(AnyCharBefore(Literals.Text("--"), failOnEof: true, consumeDelimiter: true)), VectorizedTokenGrammars.TryParseConsumeDelimiterFailOnEof, VectorizedTokenGrammars.TryParseConsumeDelimiterFailOnEof, 'a', "--", "-", "-b--", "");
        CheckPadded(Text(AnyCharBefore(Literals.Char(';'))), VectorizedTokenGrammars.TryParseSingleCharDelimiter, VectorizedTokenGrammars.TryParseSingleCharDelimiter, 'a', ";", "b;", "");
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("abc def")]
    [InlineData("_a1-b")]
    [InlineData("@x")]
    [InlineData("@")]
    [InlineData("1abc")]
    [InlineData("a\n")]
    [InlineData("x")]
    [InlineData("a-")]
    [InlineData("$é")]
    public void Identifier_Matches_The_Runtime(string text)
    {
        Check(Text(Literals.Identifier()), VectorizedTokenGrammars.TryParseIdentifier, VectorizedTokenGrammars.TryParseIdentifier, text);
        Check(Text(Literals.Identifier(static c => c == '@', static c => c == '-')), VectorizedTokenGrammars.TryParseIdentifierWithPredicates, VectorizedTokenGrammars.TryParseIdentifierWithPredicates, text);
        Check(Text(Literals.Identifier(extraPart: static c => c == '-')), VectorizedTokenGrammars.TryParseIdentifierWithExtraPart, VectorizedTokenGrammars.TryParseIdentifierWithExtraPart, text);

        var atEof = Literals.Identifier(static c => c == '\0')
            .Then(static (ParseContext context, TextSpan span) => (span.ToString(), context.Scanner.Cursor.Offset));
        Check(atEof, VectorizedTokenGrammars.TryParseIdentifierAtEof, VectorizedTokenGrammars.TryParseIdentifierAtEof, text);
    }

    [Fact]
    public void Identifier_Results_Do_Not_Depend_On_The_Buffer_Boundaries()
    {
        CheckPadded(Text(Literals.Identifier()), VectorizedTokenGrammars.TryParseIdentifier, VectorizedTokenGrammars.TryParseIdentifier, 'a', "", "b", " ", "-b");
        CheckPadded(Text(Literals.Identifier(static c => c == '@', static c => c == '-')), VectorizedTokenGrammars.TryParseIdentifierWithPredicates, VectorizedTokenGrammars.TryParseIdentifierWithPredicates, 'a', "", "-b", " ");
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("12")]
    [InlineData("1234")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData("a12")]
    [InlineData("1\n2")]
    public void Pattern_Matches_The_Runtime(string text)
    {
        Check(Text(Literals.Pattern(static c => char.IsDigit(c))), VectorizedTokenGrammars.TryParsePattern, VectorizedTokenGrammars.TryParsePattern, text);
        Check(Text(Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4)), VectorizedTokenGrammars.TryParseBoundedPattern, VectorizedTokenGrammars.TryParseBoundedPattern, text);
        Check(Text(Literals.Pattern(static c => char.IsDigit(c), minSize: 0)), VectorizedTokenGrammars.TryParseEmptyPattern, VectorizedTokenGrammars.TryParseEmptyPattern, text);
    }

    [Theory]
    [InlineData("ab\ncd ef")]
    [InlineData("\n\n\n")]
    [InlineData("\r\n\r\n\r\n")]
    [InlineData("abcdefgh")]
    [InlineData("a\nb\nc\nd\ne")]
    [InlineData(" ")]
    public void Pattern_With_New_Lines_Tracks_The_Position_Like_The_Runtime(string text)
    {
        var pattern = Literals.Pattern(static c => c != ' ', maxSize: 6)
            .Then(static (ParseContext context, TextSpan span) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (span.ToString(), position.Offset, position.Line, position.Column);
            });

        Check(pattern, VectorizedTokenGrammars.TryParsePatternPosition, VectorizedTokenGrammars.TryParsePatternPosition, text);
    }

    [Fact]
    public void Pattern_Results_Do_Not_Depend_On_The_Buffer_Boundaries()
    {
        CheckPadded(Text(Literals.Pattern(static c => char.IsDigit(c))), VectorizedTokenGrammars.TryParsePattern, VectorizedTokenGrammars.TryParsePattern, '1', "", "2", "a");
        CheckPadded(Text(Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4)), VectorizedTokenGrammars.TryParseBoundedPattern, VectorizedTokenGrammars.TryParseBoundedPattern, '1', "", "2", "a");

        // A single digit before the boundary doesn't meet the minimum size until the buffer is refilled
        Check(Text(Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4)), VectorizedTokenGrammars.TryParseBoundedPattern, VectorizedTokenGrammars.TryParseBoundedPattern, new string('a', BufferSize - 1) + "12");
        Check(Text(Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4).Or(Literals.Pattern(static c => c == 'a', minSize: 1))), VectorizedTokenGrammars.TryParseBoundedPatternOrPadding, VectorizedTokenGrammars.TryParseBoundedPatternOrPadding, new string('a', BufferSize - 1) + "12");
        Check(Text(Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4).Or(Literals.Pattern(static c => c == 'a', minSize: 1))), VectorizedTokenGrammars.TryParseBoundedPatternOrPadding, VectorizedTokenGrammars.TryParseBoundedPatternOrPadding, new string('a', BufferSize - 1) + "1a");
    }

    [Fact]
    public void Seekable_Delimiter_Is_Searched_With_SearchValues()
    {
        var context = new SourceGenerationContext();
        var body = Generate(AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>"))), context);

        Assert.Contains("IndexOfAny(", body, StringComparison.Ordinal);
        Assert.Contains("#if NET8_0_OR_GREATER", body, StringComparison.Ordinal);
        Assert.Contains(context.StaticFields, static field => field.Contains("global::System.Buffers.SearchValues.Create(", StringComparison.Ordinal));
    }

    [Fact]
    public void Single_Char_Delimiter_Is_Searched_With_IndexOf()
    {
        var context = new SourceGenerationContext();
        var body = Generate(AnyCharBefore(Literals.Char(';')), context);

        Assert.Contains(".IndexOf(';')", body, StringComparison.Ordinal);
        Assert.DoesNotContain("IndexOfAny(", body, StringComparison.Ordinal);
        Assert.Empty(context.StaticFields);
    }

    [Fact]
    public void Delimiters_Which_Can_Start_Anywhere_Are_Not_Searched()
    {
        var context = new SourceGenerationContext();

        var otherSeekable = Generate(AnyCharBefore(Literals.Text("--").Or(Literals.Pattern(static c => c == '!').Then(static x => x.ToString()))), context);
        Assert.DoesNotContain("IndexOf", otherSeekable, StringComparison.Ordinal);

        var whiteSpace = Generate(AnyCharBefore(Terms.Text("end")), context);
        Assert.DoesNotContain("IndexOf", whiteSpace, StringComparison.Ordinal);

        Assert.Empty(context.StaticFields);
    }

    [Fact]
    public void Identifier_Counts_On_The_Span_And_Moves_The_Cursor_Once()
    {
        var body = Generate(new Identifier(), new SourceGenerationContext());

        Assert.Contains(".Slice(1).IndexOfAnyExcept(global::Parlot.Character._identifierPart)", body, StringComparison.Ordinal);
        Assert.Contains(".AdvanceBy(", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".AdvanceNoNewLines(1)", body, StringComparison.Ordinal);

        var withPredicates = Generate(new Identifier(static c => c == '@', static c => c == '-'), new SourceGenerationContext());

        Assert.DoesNotContain("IndexOfAnyExcept", withPredicates, StringComparison.Ordinal);
        Assert.Contains(".AdvanceBy(", withPredicates, StringComparison.Ordinal);
        Assert.Contains(".AdvanceNoNewLines(1)", withPredicates, StringComparison.Ordinal);
    }

    [Fact]
    public void Pattern_Counts_On_The_Span_And_Does_Not_Reset_The_Cursor()
    {
        var body = Generate(Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4), new SourceGenerationContext());

        Assert.Contains(".AdvanceBy(", body, StringComparison.Ordinal);
        Assert.Contains(".MarkHitEnd()", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetPosition", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".Advance();", body, StringComparison.Ordinal);
    }

    private static Parser<string> Text(Parser<TextSpan> parser) => parser.Then(static span => span.ToString());

    private static string Generate<T>(Parser<T> parser, SourceGenerationContext context) =>
        string.Join(Environment.NewLine, Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context).Body);

    private static void CheckPadded<T>(Parser<T> parser, StringParser<T> parseString, ReaderParser<T> parseReader, char padding, params string[] inputs)
    {
        foreach (var input in inputs)
        {
            for (var split = 0; split <= input.Length + 1; split++)
            {
                Check(parser, parseString, parseReader, new string(padding, BufferSize - split) + input);
                Check(parser, parseString, parseReader, new string(padding, 2 * BufferSize - split) + input);
            }
        }
    }

    private static void Check<T>(Parser<T> parser, StringParser<T> parseString, ReaderParser<T> parseReader, string input)
    {
        var expected = parser.TryParse(input, out var expectedValue);

        Assert.Equal(expected, parseString(input, out var value));
        Assert.Equal(expected ? expectedValue : default, value);

        foreach (var reader in new TextReader[] { new StringReader(input), new ChunkedReader(input, 1), new ChunkedReader(input, 13) })
        {
            Assert.Equal(expected, parseReader(reader, out value));
            Assert.Equal(expected ? expectedValue : default, value);
        }
    }

    // A reader returning at most a fixed number of chars per read
    internal sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly int _chunk;
        private int _position;

        public ChunkedReader(string text, int chunk)
        {
            _text = text;
            _chunk = chunk;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            var length = Math.Min(Math.Min(count, _chunk), _text.Length - _position);
            _text.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        public override int Read(Span<char> buffer)
        {
            var length = Math.Min(Math.Min(buffer.Length, _chunk), _text.Length - _position);
            _text.AsSpan(_position, length).CopyTo(buffer);
            _position += length;
            return length;
        }
    }
}

public static partial class VectorizedTokenGrammars
{
    public static partial bool TryParseOtherSeekableDelimiter(string text, out string value);
    public static partial bool TryParseOtherSeekableDelimiter(TextReader reader, out string value);
    public static partial bool TryParseWhiteSpaceDelimiter(string text, out string value);
    public static partial bool TryParseWhiteSpaceDelimiter(TextReader reader, out string value);
    public static partial bool TryParseSeekableDelimiter(string text, out string value);
    public static partial bool TryParseSeekableDelimiter(TextReader reader, out string value);
    public static partial bool TryParseSingleCharDelimiter(string text, out string value);
    public static partial bool TryParseSingleCharDelimiter(TextReader reader, out string value);
    public static partial bool TryParseCanBeEmpty(string text, out string value);
    public static partial bool TryParseCanBeEmpty(TextReader reader, out string value);
    public static partial bool TryParseFailOnEof(string text, out string value);
    public static partial bool TryParseFailOnEof(TextReader reader, out string value);
    public static partial bool TryParseConsumeDelimiter(string text, out (string, int) value);
    public static partial bool TryParseConsumeDelimiter(TextReader reader, out (string, int) value);
    public static partial bool TryParseConsumeDelimiterFailOnEof(string text, out string value);
    public static partial bool TryParseConsumeDelimiterFailOnEof(TextReader reader, out string value);
    public static partial bool TryParseIdentifier(string text, out string value);
    public static partial bool TryParseIdentifier(TextReader reader, out string value);
    public static partial bool TryParseIdentifierWithPredicates(string text, out string value);
    public static partial bool TryParseIdentifierWithPredicates(TextReader reader, out string value);
    public static partial bool TryParseIdentifierWithExtraPart(string text, out string value);
    public static partial bool TryParseIdentifierWithExtraPart(TextReader reader, out string value);
    public static partial bool TryParseIdentifierAtEof(string text, out (string, int) value);
    public static partial bool TryParseIdentifierAtEof(TextReader reader, out (string, int) value);
    public static partial bool TryParsePattern(string text, out string value);
    public static partial bool TryParsePattern(TextReader reader, out string value);
    public static partial bool TryParseBoundedPattern(string text, out string value);
    public static partial bool TryParseBoundedPattern(TextReader reader, out string value);
    public static partial bool TryParseBoundedPatternOrPadding(string text, out string value);
    public static partial bool TryParseBoundedPatternOrPadding(TextReader reader, out string value);
    public static partial bool TryParseEmptyPattern(string text, out string value);
    public static partial bool TryParseEmptyPattern(TextReader reader, out string value);
    public static partial bool TryParsePatternPosition(string text, out (string, int, int, int) value);
    public static partial bool TryParsePatternPosition(TextReader reader, out (string, int, int, int) value);
}
