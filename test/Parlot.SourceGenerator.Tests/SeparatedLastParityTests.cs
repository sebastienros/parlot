#nullable enable

using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Lists which end with a last element that no separator follows (#588): the runtime parser of a string is the reference
/// for the runtime parser of a <see cref="TextReader"/>, whatever its buffer holds, and for the generated parsers of both.
/// </summary>
public class SeparatedLastParityTests
{
    private delegate bool StringParser(string text, out string value, out int failure);
    private delegate bool ReaderParser(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    private static readonly Dictionary<string, (Parser<string> Runtime, StringParser Generated, ReaderParser GeneratedReader)> Grammars = new()
    {
        ["Plain"] = (SeparatedLastRuntimeGrammars.BuildPlain(), SeparatedLastGrammars.TryParsePlain, SeparatedLastGrammars.TryParsePlain),
        ["Min"] = (SeparatedLastRuntimeGrammars.BuildMin(), SeparatedLastGrammars.TryParseMin, SeparatedLastGrammars.TryParseMin),
        ["Trailing"] = (SeparatedLastRuntimeGrammars.BuildTrailing(), SeparatedLastGrammars.TryParseTrailing, SeparatedLastGrammars.TryParseTrailing),
        ["EmptyEntries"] = (SeparatedLastRuntimeGrammars.BuildEmptyEntries(), SeparatedLastGrammars.TryParseEmptyEntries, SeparatedLastGrammars.TryParseEmptyEntries),
        ["AllOptions"] = (SeparatedLastRuntimeGrammars.BuildAllOptions(), SeparatedLastGrammars.TryParseAllOptions, SeparatedLastGrammars.TryParseAllOptions),
        ["Captured"] = (SeparatedLastRuntimeGrammars.BuildCaptured(), SeparatedLastGrammars.TryParseCaptured, SeparatedLastGrammars.TryParseCaptured),
        ["Followed"] = (SeparatedLastRuntimeGrammars.BuildFollowed(), SeparatedLastGrammars.TryParseFollowed, SeparatedLastGrammars.TryParseFollowed),
        ["Choice"] = (SeparatedLastRuntimeGrammars.BuildChoice(), SeparatedLastGrammars.TryParseChoice, SeparatedLastGrammars.TryParseChoice),
    };

    private static readonly string[] Texts =
    [
        "", ")", "a", " a ", "...r", " ... r", "a, b", "a , b , ...r", "a, ...r, b", "a, ...r,", "a, ...r, )", "a, b,", "a, b, )",
        "a, , ...r", "a,,,b", "a, ,", "a, ...", "a, ...1", ", a", ",", "...r, a", "1", " 12 ", "a, 1", "a ...r", "a, ...r , )",
    ];

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();

        foreach (var grammar in new[] { "Plain", "Min", "Trailing", "EmptyEntries", "AllOptions", "Captured", "Followed", "Choice" })
        {
            foreach (var text in Texts)
            {
                data.Add(grammar, text);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parsers_Agree_On_The_Value_The_Cursor_And_The_Furthest_Failure(string grammar, string text)
    {
        // After 4096 chars or about, the list is read across two buffers of a generated TextReader parser
        foreach (var padding in new[] { 0, 4093, 4095, 4097 })
        {
            AssertParity(grammar, new string(' ', padding) + text, $"'{text}' after {padding} spaces");
        }
    }

    [Theory]
    [InlineData("Plain", "", "|0")]
    [InlineData("Plain", "a, b, ...r", "a,b,...r|10")]
    [InlineData("Plain", "a, ...r, b", "a,...r|7")]
    [InlineData("Plain", "a, b, )", "a,b|4")]
    [InlineData("Min", "a", null)]
    [InlineData("Min", "a, ...r", "a,...r|7")]
    [InlineData("Trailing", "a, b, )", "a,b|5")]
    [InlineData("Trailing", "a, ...r, )", "a,...r|7")]
    [InlineData("EmptyEntries", "a, , ...r", "a,...r|9")]
    [InlineData("AllOptions", "a, ,", "a|4")]
    [InlineData("Captured", " a, ...r, b", " a, ...r|8")]
    [InlineData("Followed", "a, ...r, )", "a,...r|10")]
    [InlineData("Followed", "a, ...r )", null)]
    [InlineData("Choice", "...r", "...r|4")]
    [InlineData("Choice", "12", "12|2")]
    public void Parsers_Return_The_Expected_Value(string grammar, string text, string? expected)
    {
        var (runtime, _, _) = Grammars[grammar];

        Assert.Equal(expected != null, runtime.TryParse(text, out var value));
        Assert.Equal(expected, value);

        AssertParity(grammar, text, $"'{text}'");
    }

    private static void AssertParity(string name, string text, string description)
    {
        var (runtime, generated, generatedReader) = Grammars[name];

        var context = new ParseContext(new Scanner(text));
        var expected = runtime.TryParse(context, out var expectedValue, out _);
        var expectedFailure = expected ? 0 : context.Scanner.Cursor.FurthestOffset;

        // A failed parse leaves the cursor where it was
        Assert.True(expected || context.Scanner.Cursor.Offset == 0, $"{name} on {description}: the cursor is at {context.Scanner.Cursor.Offset}");

        void AssertSame(string parser, bool actual, string? actualValue, int actualFailure)
        {
            Assert.True(expected == actual && expectedValue == actualValue,
                $"{name} on {description} ({parser}): {(actual ? $"'{actualValue}'" : "fails")}, the runtime string parser {(expected ? $"returns '{expectedValue}'" : "fails")}");
            Assert.True(expectedFailure == actualFailure, $"{name} on {description} ({parser}): the furthest failure is {actualFailure}, expected {expectedFailure}");
        }

        foreach (var bufferSize in new[] { 1, 3, 4096 })
        {
            ParseContext readerContext = null!;
            var options = new StreamParseOptions
            {
                BufferSize = bufferSize,
                ContextFactory = (scanner, token) => readerContext = new ParseContext(scanner, token),
            };

            var actual = runtime.TryParse(new OneCharReader(text), out var actualValue, out _, options);
            AssertSame($"runtime, reader with a buffer of {bufferSize}", actual, actualValue, actual ? 0 : readerContext.Scanner.Cursor.FurthestOffset);
        }

        var success = generated(text, out var value, out var failure);
        AssertSame("generated, string", success, success ? value : null, failure);

        success = generatedReader(new OneCharReader(text), out value, out var readerFailure);
        AssertSame("generated, reader", success, success ? value : null, readerFailure.Offset);

        success = generatedReader(new StringReader(text), out value, out readerFailure);
        AssertSame("generated, reader of the whole text", success, success ? value : null, readerFailure.Offset);
    }

    // Returns one char per read, so that a TextReader parser refills its buffer as often as possible
    private sealed class OneCharReader : TextReader
    {
        private readonly string _text;
        private int _position;

        public OneCharReader(string text) => _text = text;

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position == _text.Length || count == 0)
            {
                return 0;
            }

            buffer[index] = _text[_position++];
            return 1;
        }

        public override int Peek() => _position < _text.Length ? _text[_position] : -1;

        public override int Read() => _position < _text.Length ? _text[_position++] : -1;
    }
}

// The runtime copies of SeparatedLastGrammars, which is only read by the generator
internal static class SeparatedLastRuntimeGrammars
{
    private static readonly Parser<string> Item = Terms.Identifier().Then(static x => x.ToString());
    private static readonly Parser<string> Rest = Terms.Text("...").SkipAnd(Terms.Identifier()).Then(static x => "..." + x.ToString());

    private static Parser<string> Join(this Parser<IReadOnlyList<string>> parser) =>
        parser.Then(static list => string.Join(",", list));

    private static Parser<string> Located(this Parser<string> parser) =>
        parser.Then(static (context, value) => $"{value}|{context.Scanner.Cursor.Offset}");

    public static Parser<string> BuildPlain() => Separated(Terms.Char(','), Item, Rest).Join().Located();

    public static Parser<string> BuildMin() => Separated(Terms.Char(','), Item, Rest, min: 2).Join().Located();

    public static Parser<string> BuildTrailing() => Separated(Terms.Char(','), Item, Rest, allowTrailingSeparator: true).Join().Located();

    public static Parser<string> BuildEmptyEntries() => Separated(Terms.Char(','), Item, Rest, removeEmptyEntries: true).Join().Located();

    public static Parser<string> BuildAllOptions() => Separated(Terms.Char(','), Item, Rest, min: 1, allowTrailingSeparator: true, removeEmptyEntries: true).Join().Located();

    public static Parser<string> BuildCaptured() => Capture(Separated(Terms.Char(','), Item, Rest, min: 1)).Then(static x => x.ToString()).Located();

    public static Parser<string> BuildFollowed() => Separated(Terms.Char(','), Item, Rest).AndSkip(Terms.Char(',')).AndSkip(Terms.Char(')')).Join().Located();

    public static Parser<string> BuildChoice() => OneOf(Separated(Terms.Char(','), Item, Rest, min: 1).Join(), Terms.Integer().Then(static x => x.ToString())).Located();
}

public static partial class SeparatedLastGrammars
{
    public static partial bool TryParsePlain(string text, out string value, out int failure);
    public static partial bool TryParsePlain(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseMin(string text, out string value, out int failure);
    public static partial bool TryParseMin(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTrailing(string text, out string value, out int failure);
    public static partial bool TryParseTrailing(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseEmptyEntries(string text, out string value, out int failure);
    public static partial bool TryParseEmptyEntries(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseAllOptions(string text, out string value, out int failure);
    public static partial bool TryParseAllOptions(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCaptured(string text, out string value, out int failure);
    public static partial bool TryParseCaptured(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseFollowed(string text, out string value, out int failure);
    public static partial bool TryParseFollowed(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseChoice(string text, out string value, out int failure);
    public static partial bool TryParseChoice(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
}
