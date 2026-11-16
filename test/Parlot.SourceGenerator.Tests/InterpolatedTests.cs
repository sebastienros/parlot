#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Generated interpolated texts must match the runtime parser, from strings and from readers, with the same failures.
public class InterpolatedTests
{
    // The runtime parsers of InterpolatedGrammars.parlot.cs, which isn't compiled in the tests
    private static class Runtime
    {
        public static Parser<string> Template(InterpolationOptions options)
        {
            var expression = Deferred<string>();
            var template = Terms.Interpolated("`", "`", "${", "}", expression, options).Then(static x =>
            {
                var result = "[" + x.Chunks[0].ToString() + "]";
                for (var i = 0; i < x.Holes.Count; i++)
                {
                    result += "{" + x.Holes[i] + "}[" + x.Chunks[i + 1].ToString() + "]";
                }
                return result;
            });
            expression.Parser = OneOf(Terms.Identifier().Then(static x => x.ToString()), template);
            return template;
        }

        public static readonly Parser<IReadOnlyList<string>> Templates = ZeroOrMany(Template(InterpolationOptions.None)).Eof();

        public static readonly Parser<IReadOnlyList<string>> TightTemplates = ZeroOrMany(Template(InterpolationOptions.NoWhiteSpaceBeforeHoleClose)).Eof();

        public static readonly Parser<int> CapturedTemplates = Capture(ZeroOrMany(Template(InterpolationOptions.None))).Then(static x => x.Length).Eof();

        public static readonly Parser<IReadOnlyList<int>> Strings =
            ZeroOrMany(Terms.Interpolated("$\"", "\"", "{", "}", Terms.Identifier(), InterpolationOptions.NoNewLines).Then(static x => x.Chunks.Count * 100 + x.Chunks[x.Chunks.Count - 1].Length)).Eof();

        public static readonly Parser<IReadOnlyList<string>> SpanHoles =
            ZeroOrMany(Terms.Interpolated("`", "`", "${", "}", Terms.Identifier()).Then(static x =>
            {
                var result = x.Chunks[0].ToString();
                for (var i = 0; i < x.Holes.Count; i++)
                {
                    result += "{" + x.Holes[i].ToString() + "}" + x.Chunks[i + 1].ToString();
                }
                return result;
            })).Eof();

        public static readonly Parser<IReadOnlyList<int>> LongDelimiters =
            ZeroOrMany(Terms.Interpolated("<<", ">>", "#[[", "]]", Literals.Identifier(), escape: '\0').Then(static x => x.Holes.Count * 100 + x.Chunks[0].Length)).Eof();
    }

    private delegate bool StringParser<T>(string text, out T value, out int failure);
    private delegate bool ReaderParser<T>(TextReader reader, out T value, out (int Offset, int Line, int Column) failure);

    private static void Check<T>(Parser<T> runtime, StringParser<T> generated, ReaderParser<T> reader, string input)
    {
        var context = new ParseContext(new Scanner(input));
        var expected = runtime.TryParse(context, out var expectedValue, out _);
        var expectedFailure = context.Scanner.Cursor.FurthestOffset;

        Assert.Equal(expected, generated(input, out var value, out var failure));

        foreach (var chunk in new[] { 1, 3, int.MaxValue })
        {
            Assert.Equal(expected, reader(new ChunkedReader(input, chunk), out var readValue, out var readFailure));

            if (expected)
            {
                Assert.Equal(expectedValue, readValue);
            }
            else
            {
                Assert.Equal(expectedFailure, readFailure.Offset);
            }
        }

        if (expected)
        {
            Assert.Equal(expectedValue, value);
        }
        else
        {
            Assert.Equal(expectedFailure, failure);
        }
    }

    public static TheoryData<string> Templates => new()
    {
        "",
        "``",
        "`abc`",
        "`${a}` `a${b}` `${a}b` `a${b}${c}d`",
        "`a\\`b` `a\\${b}` `a\\\\${b}`",
        "`a$b` `$` `$$${a}` `{a}` `a}b`",
        "`a\nb\r\nc`",
        "`${ a }` `${a\n}`",
        "`a${`b${c}d`}e` `${`${`${a}`}`}`",
        "`" + new string('a', 100) + "${b}" + new string('c', 100) + "`",
        "`",
        "`abc",
        "`a\\",
        "`a$",
        "`a${",
        "`a${b",
        "`a${b`",
        "`a${}`",
        "`a${b c}`",
        "`a${`b`",
        "`a` x",
    };

    [Theory]
    [MemberData(nameof(Templates))]
    public void Templates_Match_The_Runtime_Parser(string input)
    {
        Check(Runtime.Templates, InterpolatedGrammars.TryParseTemplates, InterpolatedGrammars.TryParseTemplates, input);
        Check(Runtime.TightTemplates, InterpolatedGrammars.TryParseTightTemplates, InterpolatedGrammars.TryParseTightTemplates, input);
        Check(Runtime.CapturedTemplates, InterpolatedGrammars.TryParseCapturedTemplates, InterpolatedGrammars.TryParseCapturedTemplates, input);
    }

    [Theory]
    [InlineData("")]
    [InlineData("$\"a{b}c\" $\"\" $\"{ b\n}\"")]
    [InlineData("$\"a\\\"b\"")]
    [InlineData("$\"a\nb\"")]
    [InlineData("$\"a\rb\"")]
    [InlineData("$\"a\\\nb\"")]
    [InlineData("$\"a{b}\n\"")]
    [InlineData("$\"a")]
    [InlineData("$a\"")]
    public void Strings_Match_The_Runtime_Parser(string input)
    {
        Check(Runtime.Strings, InterpolatedGrammars.TryParseStrings, InterpolatedGrammars.TryParseStrings, input);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(40)]
    public void Span_Holes_Match_The_Runtime_Parser(int count)
    {
        var text = "`c0" + string.Concat(Enumerable.Range(1, count).Select(static i => $"${{h{i}}}c{i}")) + "`";

        Check(Runtime.SpanHoles, InterpolatedGrammars.TryParseSpanHoles, InterpolatedGrammars.TryParseSpanHoles, text);
        Check(Runtime.SpanHoles, InterpolatedGrammars.TryParseSpanHoles, InterpolatedGrammars.TryParseSpanHoles, text + " " + text);
        Check(Runtime.SpanHoles, InterpolatedGrammars.TryParseSpanHoles, InterpolatedGrammars.TryParseSpanHoles, text.Substring(0, text.Length - 3) + " " + text);

        Assert.True(InterpolatedGrammars.TryParseSpanHoles(text, out var value, out _));
        Assert.Equal("c0" + string.Concat(Enumerable.Range(1, count).Select(static i => $"{{h{i}}}c{i}")), value.Single());
    }

    [Theory]
    [InlineData("")]
    [InlineData("<<a>> <<a\\>> <<a#[[b]]c>> <<a#[b]]>> <<a>b#c>>")]
    [InlineData("<<a#[[b]>>")]
    [InlineData("<<a#[[b]]")]
    [InlineData("<<a#[")]
    [InlineData("<<a>")]
    public void Long_Delimiters_Match_The_Runtime_Parser(string input)
    {
        Check(Runtime.LongDelimiters, InterpolatedGrammars.TryParseLongDelimiters, InterpolatedGrammars.TryParseLongDelimiters, input);
    }

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
    }
}

public static partial class InterpolatedGrammars
{
    public static partial bool TryParseTemplates(string text, out IReadOnlyList<string> value, out int failure);
    public static partial bool TryParseTemplates(TextReader reader, out IReadOnlyList<string> value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTightTemplates(string text, out IReadOnlyList<string> value, out int failure);
    public static partial bool TryParseTightTemplates(TextReader reader, out IReadOnlyList<string> value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCapturedTemplates(string text, out int value, out int failure);
    public static partial bool TryParseCapturedTemplates(TextReader reader, out int value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseStrings(string text, out IReadOnlyList<int> value, out int failure);
    public static partial bool TryParseStrings(TextReader reader, out IReadOnlyList<int> value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseSpanHoles(string text, out IReadOnlyList<string> value, out int failure);
    public static partial bool TryParseSpanHoles(TextReader reader, out IReadOnlyList<string> value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLongDelimiters(string text, out IReadOnlyList<int> value, out int failure);
    public static partial bool TryParseLongDelimiters(TextReader reader, out IReadOnlyList<int> value, out (int Offset, int Line, int Column) failure);
}
