#nullable enable

using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// A parser which reads from where it is invoked, though what it reads first skips white space, is invoked there under a
/// <c>When</c> condition, a <c>ThenTry</c> conversion and a <c>Then</c> callback receiving where its value starts, which
/// only read where that white space ends: a capture holds the white space, and a lookahead leaves the cursor before it.
/// The runtime parser of a string is the reference for the runtime parser of a <see cref="TextReader"/>, whatever its
/// buffer holds, and for the generated parsers of both.
/// </summary>
public class LeadingWhiteSpaceParityTests
{
    private delegate bool StringParser(string text, out string value, out int failure);
    private delegate bool ReaderParser(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    private static readonly Dictionary<string, (Parser<string> Runtime, StringParser Generated, ReaderParser GeneratedReader)> Grammars = new()
    {
        ["CaptureWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCaptureWhen(), LeadingWhiteSpaceGrammars.TryParseCaptureWhen, LeadingWhiteSpaceGrammars.TryParseCaptureWhen),
        ["CaptureConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCaptureConversion(), LeadingWhiteSpaceGrammars.TryParseCaptureConversion, LeadingWhiteSpaceGrammars.TryParseCaptureConversion),
        ["CaptureTuple"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCaptureTuple(), LeadingWhiteSpaceGrammars.TryParseCaptureTuple, LeadingWhiteSpaceGrammars.TryParseCaptureTuple),
        ["CaptureOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCaptureOffsets(), LeadingWhiteSpaceGrammars.TryParseCaptureOffsets, LeadingWhiteSpaceGrammars.TryParseCaptureOffsets),
        ["CaptureWhenElse"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCaptureWhenElse(), LeadingWhiteSpaceGrammars.TryParseCaptureWhenElse, LeadingWhiteSpaceGrammars.TryParseCaptureWhenElse),
        ["CaptureTupleElse"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCaptureTupleElse(), LeadingWhiteSpaceGrammars.TryParseCaptureTupleElse, LeadingWhiteSpaceGrammars.TryParseCaptureTupleElse),
        ["CapturedWordsWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCapturedWordsWhen(), LeadingWhiteSpaceGrammars.TryParseCapturedWordsWhen, LeadingWhiteSpaceGrammars.TryParseCapturedWordsWhen),
        ["CapturedWordsConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCapturedWordsConversion(), LeadingWhiteSpaceGrammars.TryParseCapturedWordsConversion, LeadingWhiteSpaceGrammars.TryParseCapturedWordsConversion),
        ["PeekWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildPeekWhen(), LeadingWhiteSpaceGrammars.TryParsePeekWhen, LeadingWhiteSpaceGrammars.TryParsePeekWhen),
        ["PeekTuple"] = (LeadingWhiteSpaceRuntimeGrammars.BuildPeekTuple(), LeadingWhiteSpaceGrammars.TryParsePeekTuple, LeadingWhiteSpaceGrammars.TryParsePeekTuple),
        ["PeekRejectedWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildPeekRejectedWhen(), LeadingWhiteSpaceGrammars.TryParsePeekRejectedWhen, LeadingWhiteSpaceGrammars.TryParsePeekRejectedWhen),
        ["PeekRejectedConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildPeekRejectedConversion(), LeadingWhiteSpaceGrammars.TryParsePeekRejectedConversion, LeadingWhiteSpaceGrammars.TryParsePeekRejectedConversion),
        ["PeekOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildPeekOffsets(), LeadingWhiteSpaceGrammars.TryParsePeekOffsets, LeadingWhiteSpaceGrammars.TryParsePeekOffsets),
        ["SequenceOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildSequenceOffsets(), LeadingWhiteSpaceGrammars.TryParseSequenceOffsets, LeadingWhiteSpaceGrammars.TryParseSequenceOffsets),
        ["OptionalCaptureOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildOptionalCaptureOffsets(), LeadingWhiteSpaceGrammars.TryParseOptionalCaptureOffsets, LeadingWhiteSpaceGrammars.TryParseOptionalCaptureOffsets),
        ["TokenWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildTokenWhen(), LeadingWhiteSpaceGrammars.TryParseTokenWhen, LeadingWhiteSpaceGrammars.TryParseTokenWhen),
        ["TokenConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildTokenConversion(), LeadingWhiteSpaceGrammars.TryParseTokenConversion, LeadingWhiteSpaceGrammars.TryParseTokenConversion),
        ["TokenOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildTokenOffsets(), LeadingWhiteSpaceGrammars.TryParseTokenOffsets, LeadingWhiteSpaceGrammars.TryParseTokenOffsets),
        ["WordsConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildWordsConversion(), LeadingWhiteSpaceGrammars.TryParseWordsConversion, LeadingWhiteSpaceGrammars.TryParseWordsConversion),
        ["WordsOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildWordsOffsets(), LeadingWhiteSpaceGrammars.TryParseWordsOffsets, LeadingWhiteSpaceGrammars.TryParseWordsOffsets),
        ["ChoiceWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildChoiceWhen(), LeadingWhiteSpaceGrammars.TryParseChoiceWhen, LeadingWhiteSpaceGrammars.TryParseChoiceWhen),
        ["ChoiceConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildChoiceConversion(), LeadingWhiteSpaceGrammars.TryParseChoiceConversion, LeadingWhiteSpaceGrammars.TryParseChoiceConversion),
        ["ChoiceOffsets"] = (LeadingWhiteSpaceRuntimeGrammars.BuildChoiceOffsets(), LeadingWhiteSpaceGrammars.TryParseChoiceOffsets, LeadingWhiteSpaceGrammars.TryParseChoiceOffsets),
        ["CommentsWhen"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCommentsWhen(), LeadingWhiteSpaceGrammars.TryParseCommentsWhen, LeadingWhiteSpaceGrammars.TryParseCommentsWhen),
        ["CommentsConversion"] = (LeadingWhiteSpaceRuntimeGrammars.BuildCommentsConversion(), LeadingWhiteSpaceGrammars.TryParseCommentsConversion, LeadingWhiteSpaceGrammars.TryParseCommentsConversion),
    };

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();

        void Add(string[] grammars, params string[] texts)
        {
            foreach (var grammar in grammars)
            {
                foreach (var text in texts)
                {
                    data.Add(grammar, text);
                }
            }
        }

        Add(["CaptureWhen", "CaptureConversion", "CaptureTuple", "CaptureOffsets", "CaptureWhenElse", "CaptureTupleElse"],
            "9", " 9", "  9", "12", " 12", "  12", "123", "  123", "\n\t 9 x", " x", "  ", "");
        Add(["CapturedWordsWhen", "CapturedWordsConversion"], "a1", " a1", "  a 1", "a 12", " a x", "  a", "  ", "x 1 2");
        Add(["PeekWhen", "PeekTuple"], "abc", " abc", "  abc", " ab", " x", "  ", "");
        Add(["PeekRejectedWhen", "PeekRejectedConversion", "PeekOffsets"], "ab", " ab", "  ab x", " abc", "  abcd ", " 1", "  ", "");
        Add(["SequenceOffsets"], "ab", " ab", "   abc x", " 1", "  ");
        Add(["OptionalCaptureOffsets"], "a 1", "  a 1", "  1", " 12 x", "  a x", "  ");
        Add(["TokenWhen", "TokenConversion", "TokenOffsets"], "ab", " ab", "  ab x", " abc", "  abcd ", " 1", "  ", "");
        Add(["WordsConversion", "WordsOffsets"], "a;", " a ;", "  ab  ; x", " a x", "  abc;", "  ");
        Add(["ChoiceWhen", "ChoiceConversion", "ChoiceOffsets"], "9", "  9", " ab", "  abc", "  123 ", " ;", "  ");
        Add(["CommentsWhen", "CommentsConversion"], "9", " 9", "/**/9", " /* a */ 9", "/* a */\n 12 x", "  /* a */ x", " /* long */ 9", "/* a */ /* b */ 123456");

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Parsers_Agree_On_The_Value_The_Cursor_And_The_Furthest_Failure(string grammar, string text)
    {
        // After 4096 chars or about, the white space before the value, or the value, is read across two buffers of a
        // generated TextReader parser
        foreach (var padding in new[] { 0, 4093, 4094, 4095, 4096, 4097 })
        {
            AssertParity(grammar, new string(' ', padding) + text, $"'{text}' after {padding} spaces");
        }
    }

    [Theory]
    [InlineData("CaptureWhen", " 9", " 9|2")]
    [InlineData("CaptureWhen", "  9", null)]
    [InlineData("CaptureConversion", " 9", " 9@0-2|2")]
    [InlineData("CaptureConversion", "  9", null)]
    [InlineData("CaptureTuple", " 9", " 9|2")]
    [InlineData("CaptureTuple", "  9", null)]
    [InlineData("CaptureOffsets", "  9", "  9@0-3|3")]
    [InlineData("CaptureWhenElse", "  9", "none|0")]
    [InlineData("CaptureTupleElse", "  9", "none|0")]
    [InlineData("PeekWhen", "abc", "abc|3")]
    [InlineData("PeekWhen", " abc", null)]
    [InlineData("PeekTuple", " abc", null)]
    [InlineData("PeekRejectedWhen", "  ab", "abab|4")]
    [InlineData("PeekOffsets", "  ab", "ab@0-0ab|4")]
    [InlineData("SequenceOffsets", "  ab", "  ab@0-4|4")]
    [InlineData("TokenConversion", "  ab", "ab@2-4|4")]
    [InlineData("TokenOffsets", "  ab", "ab@2-4|4")]
    [InlineData("WordsOffsets", "  ab ;", "ab@2-6|6")]
    [InlineData("ChoiceConversion", "  ab", "ab@2-4|4")]
    [InlineData("ChoiceOffsets", "  9", "9@2-3|3")]
    public void Parsers_Read_From_Where_The_String_Parser_Does(string grammar, string text, string? expected)
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
            Assert.True(actual || readerContext.Scanner.Cursor.Offset == 0, $"{name} on {description}: the cursor of the reader is at {readerContext.Scanner.Cursor.Offset}");
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

// The runtime copies of LeadingWhiteSpaceGrammars, which is only read by the generator
internal static class LeadingWhiteSpaceRuntimeGrammars
{
    private static Parser<string> Located(this Parser<string> parser) =>
        parser.Then(static (context, value) => $"{value}|{context.Scanner.Cursor.Offset}");

    private static Parser<string> When(Parser<TextSpan> parser) =>
        parser.When(static (_, value) => value.Length < 3).Then(static value => value.ToString());

    private static Parser<string> Conversion(Parser<TextSpan> parser) =>
        parser.ThenTry(static (ParseContext _, int start, int end, TextSpan value, out string result) =>
        {
            result = $"{value}@{start}-{end}";
            return value.Length < 3;
        });

    private static Parser<string> Tuple(Parser<TextSpan> parser) =>
        parser.ThenTry(static value => (value.Length < 3, value.ToString()));

    private static Parser<string> Offsets(Parser<TextSpan> parser) =>
        parser.Then(static (_, start, end, value) => $"{value}@{start}-{end}");

    // A capture holds the white space its parser skips
    public static Parser<string> BuildCaptureWhen() => When(Capture(Terms.Integer())).Located();

    public static Parser<string> BuildCaptureConversion() => Conversion(Capture(Terms.Integer())).Located();

    public static Parser<string> BuildCaptureTuple() => Tuple(Capture(Terms.Integer())).Located();

    public static Parser<string> BuildCaptureOffsets() => Offsets(Capture(Terms.Integer())).Located();

    // Where the cursor is when the value is rejected
    public static Parser<string> BuildCaptureWhenElse() => When(Capture(Terms.Integer())).Else("none").Located();

    public static Parser<string> BuildCaptureTupleElse() => Tuple(Capture(Terms.Integer())).Else("none").Located();

    // The capture of several tokens, and the one a wrapper invokes
    public static Parser<string> BuildCapturedWordsWhen() => When(Capture(Terms.Identifier().And(Terms.Integer())).Then(static value => value)).Located();

    public static Parser<string> BuildCapturedWordsConversion() => Conversion(Capture(Terms.Identifier().And(Terms.Integer())).Then(static value => value)).Located();

    // A lookahead leaves the cursor before the white space its parser skips
    public static Parser<string> BuildPeekWhen() => Peek(Terms.Char('a')).When(static (_, _) => true).SkipAnd(Literals.Text("abc")).Located();

    public static Parser<string> BuildPeekTuple() => Peek(Terms.Char('a')).ThenTry(static value => (true, value)).SkipAnd(Literals.Text("abc")).Located();

    public static Parser<string> BuildPeekRejectedWhen() => When(Peek(Terms.Identifier())).And(Terms.Identifier()).Then(static value => $"{value.Item1}{value.Item2}").Located();

    public static Parser<string> BuildPeekRejectedConversion() => Conversion(Peek(Terms.Identifier())).And(Terms.Identifier()).Then(static value => $"{value.Item1}{value.Item2}").Located();

    public static Parser<string> BuildPeekOffsets() => Offsets(Peek(Terms.Identifier())).And(Terms.Identifier()).Then(static value => $"{value.Item1}{value.Item2}").Located();

    // A capture after a part of a sequence which matched nothing: the sequence starts where the capture does
    public static Parser<string> BuildSequenceOffsets() =>
        Peek(Terms.Identifier()).And(Capture(Terms.Identifier())).Then(static (_, start, end, value) => $"{value.Item2}@{start}-{end}").Located();

    public static Parser<string> BuildOptionalCaptureOffsets() =>
        Capture(Terms.Identifier()).Optional().And(Terms.Integer()).Then(static (_, start, end, value) => $"{value.Item1.OrSome(default)}{value.Item2}@{start}-{end}").Located();

    // The parsers which skip the white space first themselves: it is skipped for them, as before
    public static Parser<string> BuildTokenWhen() => When(Terms.Identifier()).Located();

    public static Parser<string> BuildTokenConversion() => Conversion(Terms.Identifier()).Located();

    public static Parser<string> BuildTokenOffsets() => Offsets(Terms.Identifier()).Located();

    public static Parser<string> BuildWordsConversion() => Conversion(Terms.Identifier().AndSkip(Terms.Char(';'))).Located();

    public static Parser<string> BuildWordsOffsets() => Offsets(Terms.Identifier().AndSkip(Terms.Char(';'))).Located();

    // A choice skips the white space for its alternatives, captures included
    public static Parser<string> BuildChoiceWhen() => When(OneOf(Capture(Terms.Integer()), Capture(Terms.Identifier()))).Located();

    public static Parser<string> BuildChoiceConversion() => Conversion(OneOf(Capture(Terms.Integer()), Capture(Terms.Identifier()))).Located();

    public static Parser<string> BuildChoiceOffsets() => Offsets(OneOf(Capture(Terms.Integer()), Capture(Terms.Identifier()))).Located();

    // Comments are white space too
    public static Parser<string> BuildCommentsWhen() =>
        Capture(Terms.Integer()).When(static (_, value) => value.Length < 9).Then(static value => value.ToString()).Located()
            .WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    public static Parser<string> BuildCommentsConversion() =>
        Capture(Terms.Integer()).ThenTry(static (ParseContext _, int start, int end, TextSpan value, out string result) =>
        {
            result = $"{value}@{start}-{end}";
            return value.Length < 9;
        }).Located().WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));
}

public static partial class LeadingWhiteSpaceGrammars
{
    public static partial bool TryParseCaptureWhen(string text, out string value, out int failure);
    public static partial bool TryParseCaptureWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCaptureConversion(string text, out string value, out int failure);
    public static partial bool TryParseCaptureConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCaptureTuple(string text, out string value, out int failure);
    public static partial bool TryParseCaptureTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCaptureOffsets(string text, out string value, out int failure);
    public static partial bool TryParseCaptureOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCaptureWhenElse(string text, out string value, out int failure);
    public static partial bool TryParseCaptureWhenElse(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCaptureTupleElse(string text, out string value, out int failure);
    public static partial bool TryParseCaptureTupleElse(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCapturedWordsWhen(string text, out string value, out int failure);
    public static partial bool TryParseCapturedWordsWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCapturedWordsConversion(string text, out string value, out int failure);
    public static partial bool TryParseCapturedWordsConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekWhen(string text, out string value, out int failure);
    public static partial bool TryParsePeekWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekTuple(string text, out string value, out int failure);
    public static partial bool TryParsePeekTuple(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekRejectedWhen(string text, out string value, out int failure);
    public static partial bool TryParsePeekRejectedWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekRejectedConversion(string text, out string value, out int failure);
    public static partial bool TryParsePeekRejectedConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeekOffsets(string text, out string value, out int failure);
    public static partial bool TryParsePeekOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseSequenceOffsets(string text, out string value, out int failure);
    public static partial bool TryParseSequenceOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseOptionalCaptureOffsets(string text, out string value, out int failure);
    public static partial bool TryParseOptionalCaptureOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTokenWhen(string text, out string value, out int failure);
    public static partial bool TryParseTokenWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTokenConversion(string text, out string value, out int failure);
    public static partial bool TryParseTokenConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseTokenOffsets(string text, out string value, out int failure);
    public static partial bool TryParseTokenOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWordsConversion(string text, out string value, out int failure);
    public static partial bool TryParseWordsConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseWordsOffsets(string text, out string value, out int failure);
    public static partial bool TryParseWordsOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseChoiceWhen(string text, out string value, out int failure);
    public static partial bool TryParseChoiceWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseChoiceConversion(string text, out string value, out int failure);
    public static partial bool TryParseChoiceConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseChoiceOffsets(string text, out string value, out int failure);
    public static partial bool TryParseChoiceOffsets(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCommentsWhen(string text, out string value, out int failure);
    public static partial bool TryParseCommentsWhen(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseCommentsConversion(string text, out string value, out int failure);
    public static partial bool TryParseCommentsConversion(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
}
