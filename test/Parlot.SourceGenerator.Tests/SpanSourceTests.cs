#nullable enable

using System;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;
using ChunkedReader = Parlot.SourceGenerator.Tests.VectorizedTokenTests.ChunkedReader;

namespace Parlot.SourceGenerator.Tests;

// The literals, and the sequences, choices and loops of literals, are generated as span code: they read the text through
// a span local and move the cursor once when they match. Their results, from strings and from readers filled one char at
// a time, must match the runtime parser's.
public class SpanSourceTests
{
    private delegate bool StringParser<T>(string text, out T value);
    private delegate bool ReaderParser<T>(TextReader reader, out T value);

    // The generated reader entry points read 4096 chars at a time
    private const int BufferSize = 4096;

    private static Parser<string> Email()
    {
        var dot = Literals.Char('.');
        var plus = Literals.Char('+');
        var minus = Literals.Char('-');
        var at = Literals.Char('@');
        var wordChar = Literals.Pattern(char.IsLetterOrDigit);
        var wordDotPlusMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), dot, plus, minus));
        var wordDotMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), dot, minus));
        var wordMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), minus));

        return Capture(wordDotPlusMinus.And(at).And(wordMinus).And(dot).And(wordDotMinus))
            .Then(static value => value.ToString());
    }

    [Theory]
    [InlineData("sebastien.ros@gmail.com")]
    [InlineData("a+b-c.d@e-f.g")]
    [InlineData("a@b.c and more")]
    [InlineData("a@b.")]
    [InlineData("a@b")]
    [InlineData("a@")]
    [InlineData("@b.c")]
    [InlineData("a.b")]
    [InlineData("a@.c")]
    [InlineData("")]
    [InlineData("é@ü.ö")]
    public void Email_Matches_The_Runtime(string text)
    {
        Check(Email(), SpanSourceGrammars.TryParseEmail, SpanSourceGrammars.TryParseEmail, text);
    }

    [Theory]
    [InlineData("abc1x!")]
    [InlineData("abc123xyz!")]
    [InlineData("abc123xy!")]
    [InlineData("abc123!")]
    [InlineData("abc!")]
    [InlineData("ab")]
    [InlineData("abc12")]
    [InlineData("abc12x")]
    [InlineData("a")]
    [InlineData("")]
    public void Sequence_Values_Match_The_Runtime(string text)
    {
        var parser = Literals.Char('a')
            .And(Literals.Text("bc"))
            .And(Literals.Pattern(static c => c >= '0' && c <= '9'))
            .And(Literals.AnyOf("xyz", maxSize: 2))
            .And(Literals.Text("!"))
            .Then(static t => $"{t.Item1}|{t.Item2}|{t.Item3}|{t.Item4}|{t.Item5}");

        Check(parser, SpanSourceGrammars.TryParseValues, SpanSourceGrammars.TryParseValues, text);
    }

    [Theory]
    [InlineData("[ab]")]
    [InlineData("[abcd]!")]
    [InlineData("[abcde]")]
    [InlineData("[a]")]
    [InlineData("[ab]!??rest;")]
    [InlineData("[ab]?;")]
    [InlineData("[ab]!?")]
    [InlineData("[ab")]
    [InlineData("[")]
    [InlineData("")]
    public void Skipped_And_Optional_Parts_Match_The_Runtime(string text)
    {
        var parser = Literals.Char('[')
            .SkipAnd(Literals.Pattern(static c => c >= 'a' && c <= 'z', minSize: 2, maxSize: 4))
            .AndSkip(Literals.Char(']'))
            .And(Literals.Char('!').Optional())
            .And(Literals.Text("??").Then(1).Or(Literals.Char('?').Then(2)).ZeroOrOne())
            .AndSkip(Literals.NoneOf(";", minSize: 0))
            .Then(static t => $"{t.Item1}|{(t.Item2.HasValue ? t.Item2.Value : '-')}|{string.Join(",", t.Item3)}");

        Check(parser, SpanSourceGrammars.TryParseSkipped, SpanSourceGrammars.TryParseSkipped, text);
    }

    [Theory]
    [InlineData("(12)")]
    [InlineData("(abacb123)")]
    [InlineData("(abababab12)")]
    [InlineData("(ab12345)")]
    [InlineData("(ab123456)")]
    [InlineData("(ab12312)")]
    [InlineData("(ad12)")]
    [InlineData("(ab1)")]
    [InlineData("(ab12")]
    [InlineData("(a")]
    [InlineData("()")]
    [InlineData("")]
    public void Bounded_Loops_Match_The_Runtime(string text)
    {
        var parser = Between(
            Literals.Char('('),
            ZeroOrMany(Literals.Text("ab").Or(Literals.Text("ac")).Or(Literals.Char('b').Then("B")), 3)
                .And(OneOrMany(Literals.AnyOf("0123456789", minSize: 2, maxSize: 3), 2)),
            Literals.Char(')'))
            .Then(static t => $"{string.Join(",", t.Item1)}|{string.Join(",", t.Item2)}");

        Check(parser, SpanSourceGrammars.TryParseLoops, SpanSourceGrammars.TryParseLoops, text);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("abd")]
    [InlineData("ab")]
    [InlineData(" abc")]
    [InlineData("a")]
    [InlineData("")]
    public void A_Failed_Span_Code_Does_Not_Move_The_Cursor(string text)
    {
        var parser = OneOf(
            Literals.Text("ab").And(Literals.Char('c')).Then(static _ => "abc"),
            Terms.Identifier().Then(static x => "id:" + x.ToString()));

        Check(parser, SpanSourceGrammars.TryParseRestore, SpanSourceGrammars.TryParseRestore, text);
    }

    [Theory]
    [InlineData("a\n")]
    [InlineData("a\nbc;")]
    [InlineData("a\nb\nc")]
    [InlineData("a\nb\r\n\nc;d")]
    [InlineData("a")]
    public void The_Cursor_Is_Where_The_Span_Code_Ended(string text)
    {
        var parser = Literals.Text("a\n").And(Literals.Pattern(static c => c != ';', minSize: 0)).AndSkip(Literals.Char(';').Optional())
            .Then(static (context, _) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (position.Offset, position.Line, position.Column);
            });

        Check(parser, SpanSourceGrammars.TryParsePosition, SpanSourceGrammars.TryParsePosition, text);
    }

    [Theory]
    [InlineData("'abc'")]
    [InlineData("'a\\'b'")]
    [InlineData("'\\u0041\\u00e9z'")]
    [InlineData("'\\u00'")]
    [InlineData("'\\u004'")]
    [InlineData("'\\")]
    [InlineData("'abc")]
    [InlineData("''")]
    [InlineData("'")]
    public void Escape_Sequences_Match_The_Runtime(string text)
    {
        var escapedUnicode = Literals.Text("\\u")
            .SkipAnd(Literals.Pattern(Character.IsHexDigit, 4, 4))
            .Then(static value => ((char)Convert.ToInt32(value.ToString(), 16)).ToString());
        var escapedCharacter = Literals.Char('\\')
            .And(Literals.Pattern(static _ => true, 1, 1))
            .Then(static value => value.Item2.ToString());
        var plain = Literals.Pattern(static c => c != '\'' && c != '\\')
            .Then(static value => value.ToString());
        var parser = Between(Literals.Char('\''), ZeroOrMany(OneOf(escapedUnicode, escapedCharacter, plain)), Literals.Char('\''))
            .Then(static values => string.Concat(values));

        Check(parser, SpanSourceGrammars.TryParseQuoted, SpanSourceGrammars.TryParseQuoted, text);
    }

    [Theory]
    [InlineData("(1, 2.5)")]
    [InlineData("( -12 ,\n 3.25e-2 ) 1,000.5")]
    [InlineData("(+0,.5)x")]
    [InlineData("(1, 5.)")]
    [InlineData("(1, 1e)")]
    [InlineData("(1, 1E+)")]
    [InlineData("(1, 1e+3) 12")]
    [InlineData("(1, 1,5)")]
    [InlineData("(99999999999999999999, 1)")]
    [InlineData("(1,)")]
    [InlineData("(1, -)")]
    [InlineData("(1, 2.5")]
    [InlineData(" ( 1 , 2 ) ")]
    [InlineData("(")]
    [InlineData("")]
    public void Numbers_And_White_Space_Match_The_Runtime(string text)
    {
        var parser = Terms.Char('(')
            .SkipAnd(Terms.Integer())
            .AndSkip(Terms.Char(','))
            .And(Terms.Decimal())
            .AndSkip(Terms.Char(')'))
            .And(Terms.Number<double>(NumberOptions.Any).ZeroOrOne())
            .Then(static t => $"{t.Item1}|{t.Item2}|{string.Join(",", t.Item3)}");

        Check(parser, SpanSourceGrammars.TryParseNumbers, SpanSourceGrammars.TryParseNumbers, text);
    }

    [Theory]
    [InlineData("[ abc ]")]
    [InlineData("[ # comment\n abc # more\n ]")]
    [InlineData("[#\n_a1#\n]")]
    [InlineData("[ abc")]
    [InlineData("[ 1 ]")]
    [InlineData("#\n[a]")]
    [InlineData("[ abc # ]")]
    [InlineData("")]
    public void Custom_White_Space_Matches_The_Runtime(string text)
    {
        var parser = Terms.Char('[')
            .SkipAnd(Terms.Identifier())
            .AndSkip(Terms.Char(']'))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
            .Then(static span => span.ToString());

        Check(parser, SpanSourceGrammars.TryParseCommented, SpanSourceGrammars.TryParseCommented, text);
    }

    private static Parser<string> Delimited() =>
        AnyCharBefore(Literals.Char('-').And(Literals.Char('-')).And(Literals.AnyOf(">!", minSize: 1, maxSize: 1)), consumeDelimiter: true)
            .Then(static span => span.ToString());

    [Theory]
    [InlineData("abc-->")]
    [InlineData("a-b--c--!d")]
    [InlineData("abc--")]
    [InlineData("abc-")]
    [InlineData("abc")]
    [InlineData("-->")]
    [InlineData("")]
    public void Span_Code_In_A_Token_Matches_The_Runtime(string text)
    {
        Check(Delimited(), SpanSourceGrammars.TryParseDelimited, SpanSourceGrammars.TryParseDelimited, text);
    }

    [Fact]
    public void Span_Code_Results_Do_Not_Depend_On_The_Buffer_Boundaries()
    {
        // The reader fills a buffer of 4096 chars: the delimiter, read as span code by a token, spans its end
        CheckPadded(Delimited(), SpanSourceGrammars.TryParseDelimited, SpanSourceGrammars.TryParseDelimited, 'a', "-->", "--!x", "-", "--", "-x->", "");
    }

    [Fact]
    public void Literal_Subgraphs_Are_Generated_As_Span_Code()
    {
        var context = new SourceGenerationContext();
        var body = Generate(Email(), context);

        // The whole grammar is one method, which moves the cursor once and never moves it back
        Assert.Empty(context.Helpers.Enumerate());
        Assert.Contains("= cursor.Span;", body, StringComparison.Ordinal);
        Assert.Contains("cursor.AdvanceWithinBuffer(pos", body, StringComparison.Ordinal);
        Assert.Contains("cursor.MarkHitEnd();", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ResetPosition", body, StringComparison.Ordinal);

        // Each element of a loop checks cancellation, like the helper of each element did
        Assert.Equal(3, body.Split('\n').Count(static line => line.Contains("context.CheckCancellation();", StringComparison.Ordinal)));
        Assert.DoesNotContain("cursor.Advance()", body, StringComparison.Ordinal);
        Assert.DoesNotContain("cursor.Current", body, StringComparison.Ordinal);
        Assert.Single(body.Split('\n'), static line => line.Contains("cursor.Advance", StringComparison.Ordinal));
    }

    [Fact]
    public void Literals_Alone_Keep_Their_Code()
    {
        var context = new SourceGenerationContext();

        Assert.Contains("cursor.Match(", Generate(Literals.Char('a'), context), StringComparison.Ordinal);
        Assert.Contains("cursor.Match(", Generate(Literals.Text("abc"), context), StringComparison.Ordinal);
    }

    [Fact]
    public void Span_Code_Can_Be_Disabled()
    {
        var context = new SourceGenerationContext { GenerateSpanCode = false };
        var body = Generate(Literals.Char('a').And(Literals.Text("bc")), context);

        Assert.DoesNotContain("AdvanceWithinBuffer(pos", body, StringComparison.Ordinal);
        Assert.Equal(2, context.Helpers.Enumerate().Count());
    }

    [Fact]
    public void Span_Code_Is_Not_Generated_For_A_Compacting_Buffer()
    {
        // Each literal is a token, whose text the buffer refills
        var context = new SourceGenerationContext { IsCompacting = true };
        var body = Generate(Literals.Char('a').And(Literals.Text("bc")), context);

        Assert.DoesNotContain("AdvanceWithinBuffer(pos", body, StringComparison.Ordinal);
        Assert.Contains(context.Helpers.Enumerate(), static helper => helper.MethodName.Contains("_Token", StringComparison.Ordinal));
    }

    [Fact]
    public void White_Space_Is_Skipped_By_The_Context()
    {
        var context = new SourceGenerationContext();
        var body = Generate(Terms.Char('a').And(Terms.Text("bc")), context);

        // The cursor is moved to skip white space, then to the end of the match, or back to the start
        Assert.Empty(context.Helpers.Enumerate());
        Assert.Contains("context.WhiteSpaceParser is not null", body, StringComparison.Ordinal);
        Assert.Contains("context.SkipWhiteSpace();", body, StringComparison.Ordinal);
        Assert.Contains("AdvanceWithinBuffer(pos", body, StringComparison.Ordinal);
        Assert.Contains("cursor.ResetPosition(spanStart", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Parsers_Which_Read_The_Context_Are_Not_Span_Code()
    {
        var context = new SourceGenerationContext();

        // A callback receiving the context can read the cursor
        var callback = Generate(Literals.Char('a').Then(static (ParseContext c, char x) => x), context);
        var caseInsensitive = Generate(Literals.Char('a').And(Literals.Text("bc", caseInsensitive: true)), context);

        Assert.DoesNotContain("AdvanceWithinBuffer(pos", callback, StringComparison.Ordinal);
        Assert.DoesNotContain("AdvanceWithinBuffer(pos", caseInsensitive, StringComparison.Ordinal);

        // The sequence of literals the callback receives is span code
        var sequence = Generate(Literals.Char('a').And(Literals.Char('b')).Then(static (ParseContext c, (char, char) x) => x.Item1), context);
        Assert.Contains(context.Helpers.Enumerate(), static helper => helper.Result.Body.Any(static line => line.Contains("AdvanceWithinBuffer(pos", StringComparison.Ordinal)));
        Assert.DoesNotContain("AdvanceWithinBuffer(pos", sequence, StringComparison.Ordinal);
    }

    [Fact]
    public void Choices_Of_Many_Texts_Keep_Their_Lookup()
    {
        var context = new SourceGenerationContext();
        var texts = OneOf(Literals.Text("one"), Literals.Text("two"), Literals.Text("three"), Literals.Text("four"),
            Literals.Text("five"), Literals.Text("six"), Literals.Text("seven"), Literals.Text("eight"));

        var body = Generate(texts.And(Literals.Char('!')), context);

        Assert.DoesNotContain("AdvanceWithinBuffer(pos", body, StringComparison.Ordinal);
        Assert.Contains(context.Helpers.Enumerate(), static helper => helper.Result.Body.Any(static line => line.Contains("MatchText(", StringComparison.Ordinal)));
    }

    [Fact]
    public void Large_Subgraphs_Are_Split()
    {
        // A parser used many times is inlined each time: a subgraph above the maximum weight is not, its parts are
        var context = new SourceGenerationContext();
        Parser<string> parser = Literals.Text("ab");

        for (var i = 0; i < 7; i++)
        {
            parser = parser.And(parser).Then(static x => x.Item1);
        }

        var body = Generate(parser, context);

        Assert.DoesNotContain("AdvanceWithinBuffer(pos", body, StringComparison.Ordinal);
        Assert.Contains(context.Helpers.Enumerate(), static helper => helper.Result.Body.Any(static line => line.Contains("AdvanceWithinBuffer(pos", StringComparison.Ordinal)));
    }

    private static string Generate<T>(Parser<T> parser, SourceGenerationContext context) =>
        string.Join("\n", Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context).Body);

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
}

public static partial class SpanSourceGrammars
{
    public static partial bool TryParseEmail(string text, out string value);
    public static partial bool TryParseEmail(TextReader reader, out string value);
    public static partial bool TryParseValues(string text, out string value);
    public static partial bool TryParseValues(TextReader reader, out string value);
    public static partial bool TryParseSkipped(string text, out string value);
    public static partial bool TryParseSkipped(TextReader reader, out string value);
    public static partial bool TryParseLoops(string text, out string value);
    public static partial bool TryParseLoops(TextReader reader, out string value);
    public static partial bool TryParseRestore(string text, out string value);
    public static partial bool TryParseRestore(TextReader reader, out string value);
    public static partial bool TryParsePosition(string text, out (int, int, int) value);
    public static partial bool TryParsePosition(TextReader reader, out (int, int, int) value);
    public static partial bool TryParseQuoted(string text, out string value);
    public static partial bool TryParseQuoted(TextReader reader, out string value);
    public static partial bool TryParseDelimited(string text, out string value);
    public static partial bool TryParseDelimited(TextReader reader, out string value);
    public static partial bool TryParseNumbers(string text, out string value);
    public static partial bool TryParseNumbers(TextReader reader, out string value);
    public static partial bool TryParseCommented(string text, out string value);
    public static partial bool TryParseCommented(TextReader reader, out string value);
}
