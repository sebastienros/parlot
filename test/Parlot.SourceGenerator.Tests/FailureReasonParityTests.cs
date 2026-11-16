#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Parlot;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public enum FailureReason
{
    ReservedWord,
    IdentifierAfterNumber,
    InvalidDigit,
}

[Flags]
public enum FailureFlags
{
    A = 1,
    B = 2,
}

/// <summary>
/// The reason recorded with the furthest failure: the generated <c>string</c> and <c>TextReader</c> entry points return the
/// one the runtime parsers report in <see cref="ParseError.Reason"/>, with the same offset, on random inputs.
/// </summary>
public class FailureReasonParityTests
{
    private delegate bool StringEntry(string text, out string value, out (int Offset, object? Reason) failure);
    private delegate bool ReaderEntry(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);

    private static Parser<string> Word => Terms.Identifier().Then(static value => value.ToString());

    // The runtime copies of FailureReasonGrammars, the alphabet of their random inputs, and the generated entry points
    private static readonly Dictionary<string, (Parser<string> Runtime, string Alphabet, StringEntry Generated, ReaderEntry GeneratedReader)> Grammars = new()
    {
        ["When"] = (
            OneOf(
                Word.When(static (_, value) => value != "from", "'from' is reserved").AndSkip(Terms.Char(';')),
                Word.When(static (_, value) => value != "from", "not kept: a reason at the same offset"),
                Terms.Text("from!").Then(static _ => "!"))
            .Eof(),
            "from!;, ", FailureReasonGrammars.TryParseWhen, FailureReasonGrammars.TryParseWhen),
        ["Number"] = (
            Terms.Integer().WhenNotFollowedBy(Literals.Identifier(), FailureReason.IdentifierAfterNumber)
                .Then(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .AndSkip(Terms.Char(';')).Eof(),
            "12ab ;", FailureReasonGrammars.TryParseNumber, FailureReasonGrammars.TryParseNumber),
        ["Not"] = (
            Not(Terms.Keyword("let"), FailureFlags.A | FailureFlags.B).SkipAnd(Word).AndSkip(Terms.Char(';')).Eof(),
            "let ;", FailureReasonGrammars.TryParseNot, FailureReasonGrammars.TryParseNot),
        ["ThenTry"] = (
            Word.ThenTry(static value => (value != "from", value), FailureReason.ReservedWord)
                .Or(Terms.Integer().ThenTry(static (ParseContext _, int _, int _, long value, out string result) =>
                {
                    result = "#";
                    return value < 100;
                }, 42))
                .AndSkip(Terms.Char(';')).Eof(),
            "from19 ;", FailureReasonGrammars.TryParseThenTry, FailureReasonGrammars.TryParseThenTry),
        ["Octal"] = (
            Terms.Pattern(static c => c >= '0' && c <= '9').ThenTry(static (ParseContext context, int start, int end, TextSpan value, out string result) =>
            {
                result = value.ToString();

                for (var i = 0; i < result.Length; i++)
                {
                    if (result[i] > '7')
                    {
                        context.Scanner.Cursor.RecordFailure(start + i, FailureReason.InvalidDigit);
                        return false;
                    }
                }

                return true;
            }).AndSkip(Terms.Char(';')).Eof(),
            "0789 ;", FailureReasonGrammars.TryParseOctal, FailureReasonGrammars.TryParseOctal),
        ["Lookahead"] = (
            Not(Word.AndSkip(Terms.Identifier().When(static (_, value) => value.ToString() == "x", "inner")))
                .SkipAnd(Word.AndSkip(Terms.Char('!'))).Eof(),
            "xy !", FailureReasonGrammars.TryParseLookahead, FailureReasonGrammars.TryParseLookahead),
    };

    // grammar, input, the reason, its offset
    public static TheoryData<string, string, object?, int> Reasons => new()
    {
        { "When", "  from;", "'from' is reserved", 2 },
        { "When", "from", "'from' is reserved", 0 },
        { "When", "from ;", "'from' is reserved", 0 },
        { "When", "word ,", null, 5 },
        { "Number", " 12ab;", FailureReason.IdentifierAfterNumber, 3 },
        { "Number", "12 ab;", null, 3 },
        { "Not", "  let;", FailureFlags.A | FailureFlags.B, 2 },
        { "ThenTry", " from;", FailureReason.ReservedWord, 1 },
        { "ThenTry", " 199;", 42, 1 },
        { "ThenTry", " 99", null, 3 },
        { "Octal", " 0719;", FailureReason.InvalidDigit, 4 },
        { "Octal", "8;", FailureReason.InvalidDigit, 0 },
        { "Lookahead", "y z", null, 2 },
    };

    [Theory]
    [MemberData(nameof(Reasons))]
    public void Reason_Is_Reported_With_The_Furthest_Failure(string grammar, string text, object? reason, int offset)
    {
        var runtime = Run(grammar, text);
        Assert.False(runtime.Success);
        Assert.Equal((offset, reason), (runtime.Offset, runtime.Reason));
        AssertParity(grammar, text);
    }

    [Theory]
    [InlineData("When", "word;", "word")]
    [InlineData("When", "from!", "!")]
    [InlineData("When", "word", "word")]
    [InlineData("Number", "12;", "12")]
    [InlineData("Not", "lets;", "lets")]
    [InlineData("ThenTry", "word;", "word")]
    [InlineData("ThenTry", "99;", "#")]
    [InlineData("Octal", "0717;", "0717")]
    [InlineData("Lookahead", "y!", "y")]
    public void Accepted_Input_Has_No_Reason(string grammar, string text, string expected)
    {
        var (runtime, _, generated, generatedReader) = Grammars[grammar];

        Assert.True(runtime.TryParse(text, out var value, out var error));
        Assert.Equal(expected, value);
        Assert.Null(error);

        Assert.True(generated(text, out value, out var failure));
        Assert.Equal(expected, value);
        Assert.Equal(default, failure);

        Assert.True(generatedReader(new StringReader(text), out value, out var readerFailure));
        Assert.Equal(expected, value);
        Assert.Equal(default, readerFailure);
    }

    [Theory]
    [InlineData("When")]
    [InlineData("Number")]
    [InlineData("Not")]
    [InlineData("ThenTry")]
    [InlineData("Octal")]
    [InlineData("Lookahead")]
    public void Generated_Parsers_Report_The_Runtime_Reasons_On_Random_Inputs(string grammar)
    {
        var alphabet = Grammars[grammar].Alphabet;
        var random = new Random(589);
        var text = new StringBuilder();

        for (var i = 0; i < 2000; i++)
        {
            text.Clear();
            var length = random.Next(0, 9);

            for (var j = 0; j < length; j++)
            {
                text.Append(alphabet[random.Next(alphabet.Length)]);
            }

            AssertParity(grammar, text.ToString());
        }
    }

    [Fact]
    public void Reason_Spans_The_Buffers_Of_A_Reader()
    {
        // A generated TextReader parser reads 4096 chars at a time
        foreach (var padding in new[] { 4094, 4095, 4096, 4097 })
        {
            AssertParity("When", new string(' ', padding) + "from;");
            AssertParity("Octal", new string(' ', padding) + "0719;");
            AssertParity("Number", new string(' ', padding) + "12ab;");
        }
    }

    [Fact]
    public void Reason_Is_Generated_As_A_Constant()
    {
        var context = new Parlot.SourceGeneration.SourceGenerationContext();
        var body = string.Join("\n", ((Parlot.SourceGeneration.ISourceable)Not(Terms.Char('a'), FailureFlags.A | FailureFlags.B)).GenerateSource(context).Body);

        // A combination of flags has no name: it is the number, cast, boxed once
        Assert.Contains("private static readonly object", string.Join("\n", context.StaticFields), StringComparison.Ordinal);
        Assert.Contains("((global::Parlot.SourceGenerator.Tests.FailureFlags)(3))", string.Join("\n", context.StaticFields), StringComparison.Ordinal);
        Assert.Contains("RecordRejected(", body, StringComparison.Ordinal);

        var text = string.Join("\n", ((Parlot.SourceGeneration.ISourceable)Literals.Text("from").When(static (_, _) => true, "a \"reason\"")).GenerateSource(new Parlot.SourceGeneration.SourceGenerationContext()).Body);
        Assert.Contains("\"a \\\"reason\\\"\"", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(double.NaN)]
    public void Reason_Which_Isnt_A_Constant_Isnt_Generated(object reason)
    {
        var parser = (Parlot.SourceGeneration.ISourceable)Literals.Text("from").When(static (_, _) => true, reason);

        var exception = Assert.Throws<NotSupportedException>(() => parser.GenerateSource(new Parlot.SourceGeneration.SourceGenerationContext()));
        Assert.Contains("A reason of type", exception.Message, StringComparison.Ordinal);
        Assert.Throws<NotSupportedException>(() => ((Parlot.SourceGeneration.ISourceable)Literals.Text("from").When(static (_, _) => true, new object()))
            .GenerateSource(new Parlot.SourceGeneration.SourceGenerationContext()));
    }

    private static (bool Success, int Offset, object? Reason) Run(string grammar, string text)
    {
        var context = new ParseContext(new Scanner(text));
        var success = Grammars[grammar].Runtime.TryParse(context, out _, out var error);

        if (success)
        {
            return (true, 0, null);
        }

        Assert.Equal(0, context.Scanner.Cursor.Offset);
        Assert.Equal(error?.Reason, context.Scanner.Cursor.FurthestReason);

        if (error is not null)
        {
            Assert.Equal(context.Scanner.Cursor.FurthestOffset, error.Position.Offset);
        }

        return (false, context.Scanner.Cursor.FurthestOffset, context.Scanner.Cursor.FurthestReason);
    }

    private static void AssertParity(string grammar, string text)
    {
        var (runtime, _, generated, generatedReader) = Grammars[grammar];
        var expected = Run(grammar, text);
        var name = $"{grammar} on '{text.TrimStart()}' ({text.Length - text.TrimStart().Length} spaces)";

        var readerSuccess = runtime.TryParse(new OneCharReader(text), out _, out var readerError, new StreamParseOptions { BufferSize = 1 });
        Assert.True(expected.Success == readerSuccess, name + ": runtime reader");
        Assert.True(Equals(expected.Reason, readerError?.Reason), $"{name}: runtime reader reports {readerError?.Reason}, expected {expected.Reason}");

        var success = generated(text, out _, out var failure);
        Assert.True(expected.Success == success, name + ": generated string");
        Assert.True((expected.Offset, expected.Reason).Equals((failure.Offset, failure.Reason)),
            $"{name}: generated string reports {failure}, expected {(expected.Offset, expected.Reason)}");

        success = generatedReader(new OneCharReader(text), out _, out var readerFailure);
        Assert.True(expected.Success == success, name + ": generated reader");
        Assert.True((expected.Offset, expected.Reason).Equals((readerFailure.Offset, readerFailure.Reason)),
            $"{name}: generated reader reports {readerFailure}, expected {(expected.Offset, expected.Reason)}");
        Assert.False(readerFailure.NestedTooDeeply);
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

public static partial class FailureReasonGrammars
{
    public static partial bool TryParseWhen(string text, out string value, out (int Offset, object? Reason) failure);
    public static partial bool TryParseWhen(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);
    public static partial bool TryParseNumber(string text, out string value, out (int Offset, object? Reason) failure);
    public static partial bool TryParseNumber(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);
    public static partial bool TryParseNot(string text, out string value, out (int Offset, object? Reason) failure);
    public static partial bool TryParseNot(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);
    public static partial bool TryParseThenTry(string text, out string value, out (int Offset, object? Reason) failure);
    public static partial bool TryParseThenTry(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);
    public static partial bool TryParseOctal(string text, out string value, out (int Offset, object? Reason) failure);
    public static partial bool TryParseOctal(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);
    public static partial bool TryParseLookahead(string text, out string value, out (int Offset, object? Reason) failure);
    public static partial bool TryParseLookahead(TextReader reader, out string value, out (int Offset, int Line, int Column, object? Reason, bool NestedTooDeeply) failure);
}
