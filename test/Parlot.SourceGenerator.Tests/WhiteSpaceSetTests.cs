#nullable enable

using System;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated parsers honor WithWhiteSpaceSet like the runtime ones, from strings and from readers filled one
// char at a time.
public class WhiteSpaceSetTests
{
    private delegate bool StringParser<T>(string text, out T value);
    private delegate bool ReaderParser<T>(TextReader reader, out T value);

    [Theory]
    [InlineData("1 , 2")]
    [InlineData("1 , 2")]
    [InlineData("1 ,　 2")]
    [InlineData("1\t,\n2\r\n,\v\f3")]
    [InlineData("  1")]
    [InlineData("")]
    public void Terms_Skip_The_Ascii_Set(string text)
    {
        var parser = Separated(Terms.Char(','), Terms.Integer()).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static list => string.Join(",", list));
        Check(parser, WhiteSpaceSetGrammars.TryParseSeparated, WhiteSpaceSetGrammars.TryParseSeparated, text);
    }

    [Fact]
    public void Generated_Terms_Do_Not_Skip_Unicode_Spaces()
    {
        Assert.True(WhiteSpaceSetGrammars.TryParseSeparated("1 ,\n\t2", out var value));
        Assert.Equal("1,2", value);

        foreach (var space in Character.WhiteSpaceOrNewLines.Where(static c => c > 127))
        {
            Assert.True(WhiteSpaceSetGrammars.TryParseSeparated($"1 ,{space}2", out value));
            Assert.Equal("1", value);
        }
    }

    [Theory]
    [InlineData("1 , 2")]
    [InlineData("1\u2028,\u20292\u0085,\u00A03")]
    [InlineData("1,\uFEFF2")]
    [InlineData("1,\u180E2")]
    [InlineData("\uFEFF1")]
    public void Terms_Skip_The_Default_Set(string text)
    {
        var parser = Separated(Terms.Char(','), Terms.Integer()).Then(static list => string.Join(",", list));
        Check(parser, WhiteSpaceSetGrammars.TryParseDefaultSeparated, WhiteSpaceSetGrammars.TryParseDefaultSeparated, text);
    }

    [Fact]
    public void Generated_Terms_Skip_Unicode_White_Space()
    {
        Assert.True(WhiteSpaceSetGrammars.TryParseDefaultSeparated("1\u2028,\u20292\u0085,\u00A03", out var value));
        Assert.Equal("1,2,3", value);

        // The byte order mark and U+180E are not white space
        Assert.True(WhiteSpaceSetGrammars.TryParseDefaultSeparated("1,\uFEFF2", out value));
        Assert.Equal("1", value);
        Assert.False(WhiteSpaceSetGrammars.TryParseDefaultSeparated("\uFEFF1", out _));
    }

    [Theory]
    [InlineData("for if")]
    [InlineData("for if")]
    [InlineData("  if")]
    [InlineData("  　")]
    [InlineData("if\tfor\nif")]
    [InlineData("")]
    public void Choices_Skip_The_Ascii_Set(string text)
    {
        var parser = ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text(" "))).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static list => string.Join("|", list));
        Check(parser, WhiteSpaceSetGrammars.TryParseChoice, WhiteSpaceSetGrammars.TryParseChoice, text);
    }

    [Theory]
    [InlineData(" \t\n\r\v\f x")]
    [InlineData(" ")]
    [InlineData("  ")]
    [InlineData("x")]
    public void WhiteSpace_Reads_The_Ascii_Set(string text)
    {
        Check(Literals.WhiteSpace(includeNewLines: true).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static span => span.ToString()),
            WhiteSpaceSetGrammars.TryParseWhiteSpace, WhiteSpaceSetGrammars.TryParseWhiteSpace, text);
    }

    [Theory]
    [InlineData("a b c")]
    [InlineData("a　b\tc")]
    [InlineData("a \nb\fc")]
    [InlineData(" x")]
    [InlineData("")]
    public void NonWhiteSpace_Reads_Up_To_The_Ascii_Set(string text)
    {
        Check(Literals.NonWhiteSpace(includeNewLines: false).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static span => span.ToString()),
            WhiteSpaceSetGrammars.TryParseNonWhiteSpace, WhiteSpaceSetGrammars.TryParseNonWhiteSpace, text);

        var longText = new string('a', 40) + text + new string('b', 40) + " c";
        Check(Literals.NonWhiteSpace(includeNewLines: false).WithWhiteSpaceSet(WhiteSpaceSet.Ascii).Then(static span => span.ToString()),
            WhiteSpaceSetGrammars.TryParseNonWhiteSpace, WhiteSpaceSetGrammars.TryParseNonWhiteSpace, longText);
    }

    [Theory]
    [InlineData(" a # comment\n\tb")]
    [InlineData("a # comment")]
    [InlineData("a  b")]
    [InlineData("a # b")]
    public void Comments_Skip_The_Ascii_Set(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii)
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, WhiteSpaceSetGrammars.TryParseComments, WhiteSpaceSetGrammars.TryParseComments, text);
    }

    [Theory]
    [InlineData("[ 1 ]")]
    [InlineData("[1 ]")]
    [InlineData(" [1]")]
    [InlineData(" [ 1 ] ")]
    public void Inner_Sets_Are_Restored(string text)
    {
        var parser = Terms.Char('[').SkipAnd(Terms.Integer().WithWhiteSpaceSet(WhiteSpaceSet.Default)).AndSkip(Terms.Char(']')).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        Check(parser, WhiteSpaceSetGrammars.TryParseNested, WhiteSpaceSetGrammars.TryParseNested, text);
    }

    [Theory]
    [InlineData(" \tab")]
    [InlineData(" ab")]
    [InlineData("ab")]
    public void Offsets_Follow_The_Ascii_Set(string text)
    {
        var parser = Terms.Identifier().Then(static (context, start, end, value) => (start, end)).WithWhiteSpaceSet(WhiteSpaceSet.Ascii);
        Check(parser, WhiteSpaceSetGrammars.TryParseOffsets, WhiteSpaceSetGrammars.TryParseOffsets, text);
    }

    [Theory]
    [InlineData("1 , 2")]
    [InlineData("1\t,\r\n2\n,\r3")]
    [InlineData("1 ,\f2")]
    [InlineData("1 ,\v2")]
    [InlineData("1 ,\u00A02")]
    [InlineData("\f1")]
    [InlineData("")]
    public void Terms_Skip_The_Json_Set(string text)
    {
        var parser = Separated(Terms.Char(','), Terms.Integer()).WithWhiteSpaceSet(WhiteSpaceSet.Json).Then(static list => string.Join(",", list));
        Check(parser, WhiteSpaceSetGrammars.TryParseJsonSeparated, WhiteSpaceSetGrammars.TryParseJsonSeparated, text);
    }

    [Fact]
    public void Generated_Terms_Skip_Only_The_Json_Set()
    {
        Assert.True(WhiteSpaceSetGrammars.TryParseJsonSeparated("1 ,\r\n\t2", out var value));
        Assert.Equal("1,2", value);

        foreach (var space in Character.WhiteSpaceOrNewLines.Where(static c => Character.JsonWhiteSpaceOrNewLines.IndexOf(c) < 0))
        {
            Assert.True(WhiteSpaceSetGrammars.TryParseJsonSeparated($"1 ,{space}2", out value));
            Assert.Equal("1", value);
        }
    }

    [Theory]
    [InlineData("for if")]
    [InlineData("for\fif")]
    [InlineData("\f\r\n if")]
    [InlineData("  \v")]
    [InlineData("if\tfor\r\nif")]
    [InlineData("")]
    public void Choices_Skip_The_Json_Set(string text)
    {
        var parser = ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text("\f"))).WithWhiteSpaceSet(WhiteSpaceSet.Json).Then(static list => string.Join("|", list));
        Check(parser, WhiteSpaceSetGrammars.TryParseJsonChoice, WhiteSpaceSetGrammars.TryParseJsonChoice, text);
    }

    [Theory]
    [InlineData(" \t\n\r x")]
    [InlineData(" \r\n\v")]
    [InlineData("\f")]
    [InlineData("x")]
    public void WhiteSpace_Reads_The_Json_Set(string text)
    {
        Check(Literals.WhiteSpace(includeNewLines: true).WithWhiteSpaceSet(WhiteSpaceSet.Json).Then(static span => span.ToString()),
            WhiteSpaceSetGrammars.TryParseJsonWhiteSpace, WhiteSpaceSetGrammars.TryParseJsonWhiteSpace, text);
    }

    [Theory]
    [InlineData("a\fb\vc d")]
    [InlineData("a\u00A0b\tc")]
    [InlineData("a\nb\rc d")]
    [InlineData(" x")]
    [InlineData("")]
    public void NonWhiteSpace_Reads_Up_To_The_Json_Set(string text)
    {
        var parser = Literals.NonWhiteSpace(includeNewLines: false).WithWhiteSpaceSet(WhiteSpaceSet.Json).Then(static span => span.ToString());
        Check(parser, WhiteSpaceSetGrammars.TryParseJsonNonWhiteSpace, WhiteSpaceSetGrammars.TryParseJsonNonWhiteSpace, text);

        var longText = new string('a', 40) + text + new string('b', 40) + " c";
        Check(parser, WhiteSpaceSetGrammars.TryParseJsonNonWhiteSpace, WhiteSpaceSetGrammars.TryParseJsonNonWhiteSpace, longText);
    }

    [Theory]
    [InlineData(" a # comment\r\n\tb")]
    [InlineData("a # comment")]
    [InlineData("a\fb")]
    [InlineData("a\v# b")]
    public void Comments_Skip_The_Json_Set(string text)
    {
        var parser = ZeroOrMany(Terms.Identifier())
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
            .WithWhiteSpaceSet(WhiteSpaceSet.Json)
            .Eof()
            .Then(static list => string.Join("|", list.Select(static x => x.ToString())));

        Check(parser, WhiteSpaceSetGrammars.TryParseJsonComments, WhiteSpaceSetGrammars.TryParseJsonComments, text);
    }

    [Theory]
    [InlineData("[ 1 ]")]
    [InlineData("[\f1 ]")]
    [InlineData("[1\f]")]
    [InlineData("\r\n[\v1\r]")]
    public void Inner_Sets_Are_Restored_In_The_Json_Set(string text)
    {
        var parser = Terms.Char('[').SkipAnd(Terms.Integer().WithWhiteSpaceSet(WhiteSpaceSet.Ascii)).AndSkip(Terms.Char(']')).WithWhiteSpaceSet(WhiteSpaceSet.Json);
        Check(parser, WhiteSpaceSetGrammars.TryParseJsonNested, WhiteSpaceSetGrammars.TryParseJsonNested, text);
    }

    private static void Check<T>(Parser<T> parser, StringParser<T> parseString, ReaderParser<T> parseReader, string input)
    {
        var expected = parser.TryParse(input, out var expectedValue);

        Assert.Equal(expected, parseString(input, out var value));
        Assert.Equal(expected ? expectedValue : default, value);

        foreach (var reader in new TextReader[] { new StringReader(input), new ChunkedReader(input, 1), new ChunkedReader(input, 3) })
        {
            Assert.Equal(expected, parseReader(reader, out value));
            Assert.Equal(expected ? expectedValue : default, value);
        }
    }

    // A reader returning at most a fixed number of chars per read
    private sealed class ChunkedReader : TextReader
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

public static partial class WhiteSpaceSetGrammars
{
    public static partial bool TryParseDefaultSeparated(string text, out string value);
    public static partial bool TryParseDefaultSeparated(TextReader reader, out string value);
    public static partial bool TryParseSeparated(string text, out string value);
    public static partial bool TryParseSeparated(TextReader reader, out string value);
    public static partial bool TryParseChoice(string text, out string value);
    public static partial bool TryParseChoice(TextReader reader, out string value);
    public static partial bool TryParseWhiteSpace(string text, out string value);
    public static partial bool TryParseWhiteSpace(TextReader reader, out string value);
    public static partial bool TryParseNonWhiteSpace(string text, out string value);
    public static partial bool TryParseNonWhiteSpace(TextReader reader, out string value);
    public static partial bool TryParseComments(string text, out string value);
    public static partial bool TryParseComments(TextReader reader, out string value);
    public static partial bool TryParseNested(string text, out long value);
    public static partial bool TryParseNested(TextReader reader, out long value);
    public static partial bool TryParseOffsets(string text, out (int, int) value);
    public static partial bool TryParseOffsets(TextReader reader, out (int, int) value);
    public static partial bool TryParseJsonSeparated(string text, out string value);
    public static partial bool TryParseJsonSeparated(TextReader reader, out string value);
    public static partial bool TryParseJsonChoice(string text, out string value);
    public static partial bool TryParseJsonChoice(TextReader reader, out string value);
    public static partial bool TryParseJsonWhiteSpace(string text, out string value);
    public static partial bool TryParseJsonWhiteSpace(TextReader reader, out string value);
    public static partial bool TryParseJsonNonWhiteSpace(string text, out string value);
    public static partial bool TryParseJsonNonWhiteSpace(TextReader reader, out string value);
    public static partial bool TryParseJsonComments(string text, out string value);
    public static partial bool TryParseJsonComments(TextReader reader, out string value);
    public static partial bool TryParseJsonNested(string text, out long value);
    public static partial bool TryParseJsonNested(TextReader reader, out long value);
}
