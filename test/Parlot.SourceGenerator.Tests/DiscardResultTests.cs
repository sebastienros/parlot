using System;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

public class DiscardResultTests
{
    [Theory]
    [InlineData(StringComparison.Ordinal)]
    [InlineData(StringComparison.OrdinalIgnoreCase)]
    [InlineData(StringComparison.CurrentCulture)]
    [InlineData(StringComparison.CurrentCultureIgnoreCase)]
    [InlineData(StringComparison.InvariantCulture)]
    [InlineData(StringComparison.InvariantCultureIgnoreCase)]
    public void Discarded_Text_Only_Matches_And_Advances(StringComparison comparison)
    {
        var parser = new TextLiteral("keyword", comparison, returnMatchedText: true);
        var source = parser.GenerateSource(new SourceGenerationContext { DiscardResult = true });
        var body = string.Join("\n", source.Body);

        Assert.Contains("cursor.Match(", body, StringComparison.Ordinal);
        Assert.Contains("cursor.AdvanceBy(", body, StringComparison.Ordinal);
        Assert.Contains("value = default;", body, StringComparison.Ordinal);
        Assert.DoesNotContain("GetSpan(", body, StringComparison.Ordinal);
        Assert.DoesNotContain("scanner.Buffer", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".ToString()", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("AndSkip1")]
    [InlineData("AndSkip2")]
    [InlineData("AndSkip3")]
    [InlineData("AndSkip4")]
    [InlineData("AndSkip5")]
    [InlineData("AndSkip6")]
    [InlineData("AndSkip7")]
    [InlineData("SkipAnd")]
    [InlineData("Between")]
    [InlineData("Separated")]
    [InlineData("SeparatedOptions")]
    [InlineData("Not")]
    [InlineData("FollowedBy")]
    [InlineData("NotFollowedBy")]
    [InlineData("TextBefore")]
    [InlineData("Capture")]
    [InlineData("Constant")]
    [InlineData("WhiteSpace")]
    [InlineData("LeftAssociative")]
    [InlineData("LeftAssociativeContext")]
    [InlineData("Unary")]
    [InlineData("UnaryContext")]
    public void Fully_Ignored_Children_Are_Generated_Without_Materialization(string kind)
    {
        var text = Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true);
        foreach (var discard in new[] { false, true })
        {
            foreach (var compacting in new[] { false, true })
            {
                var context = new SourceGenerationContext { DiscardResult = discard, IsCompacting = compacting };
                CreateParser(kind, text).GenerateSource(context);

                var helpers = context.Helpers.Enumerate()
                    .Where(static helper => helper.Result.Body.Any(static line => line.Contains("cursor.Match(\"keyword\"", StringComparison.Ordinal)))
                    .ToArray();

                Assert.NotEmpty(helpers);
                foreach (var helper in helpers)
                {
                    Assert.DoesNotContain(helper.Result.Body, static line => line.Contains(".ToString()", StringComparison.Ordinal));
                }
                Assert.Equal(discard, context.DiscardResult);
                Assert.Equal(compacting, context.IsCompacting);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Shared_Text_Helpers_Are_Isolated_In_Both_Result_Mode_Orders(bool skipFirst)
    {
        var text = Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true);
        Parser<string> parser = skipFirst ? text.SkipAnd(text) : text.AndSkip(text);
        var context = new SourceGenerationContext();
        Source(parser).GenerateSource(context);
        var helpers = context.Helpers.Enumerate().ToArray();

        Assert.Equal(2, helpers.Length);
        Assert.NotEqual(helpers[0].MethodName, helpers[1].MethodName);
        Assert.Equal(!skipFirst, helpers[0].Result.Body.Any(static line => line.Contains(".ToString()", StringComparison.Ordinal)));
        Assert.Equal(skipFirst, helpers[1].Result.Body.Any(static line => line.Contains(".ToString()", StringComparison.Ordinal)));
        Assert.False(context.DiscardResult);
    }

    [Theory]
    [InlineData("AndSkip1")]
    [InlineData("SkipAnd")]
    [InlineData("Between")]
    [InlineData("Separated")]
    [InlineData("SeparatedOptions")]
    [InlineData("Not")]
    [InlineData("FollowedBy")]
    [InlineData("NotFollowedBy")]
    [InlineData("TextBefore")]
    [InlineData("Capture")]
    [InlineData("Constant")]
    [InlineData("LeftAssociative")]
    [InlineData("Unary")]
    public void Discard_Mode_Is_Restored_When_Child_Emission_Throws(string kind)
    {
        var parser = CreateParser(kind, new ThrowingParser());
        foreach (var discard in new[] { false, true })
        {
            var context = new SourceGenerationContext { DiscardResult = discard };
            Assert.Throws<NotSupportedException>(() => parser.GenerateSource(context));
            Assert.Equal(discard, context.DiscardResult);
        }
    }

    [Fact]
    public void Partial_Tuples_And_Callback_Inputs_Remain_Value_Producing()
    {
        var text = Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true);
        foreach (var parser in new[]
        {
            Source(Literals.Char('a').And(text).SkipAnd(Literals.Char('b'))),
            Source(text.Then(static value => value.Length).AndSkip(Literals.Char('!')))
        })
        {
            var context = new SourceGenerationContext();
            parser.GenerateSource(context);
            Assert.Contains(context.Helpers.Enumerate(), static helper =>
                helper.Result.Body.Any(static line => line.Contains(".ToString()", StringComparison.Ordinal)));
        }
    }

    [Theory]
    [InlineData("KeYwOrDKEYWORD!", true, "KEYWORD")]
    [InlineData("KeYwOrDwrong!", false, null)]
    [InlineData("KeYwOrDKEYWORD?", false, null)]
    public void Shared_And_Skipped_Text_Preserves_Values_And_Failures(string input, bool success, string expected)
    {
        Assert.Equal(success, DiscardGrammars.TryParseSkipFirst(input, out var value));
        Assert.Equal(expected, value);
        using var reader = new SingleCharacterReader(input);
        Assert.Equal(success, DiscardGrammars.TryParseSkipFirst(reader, out value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Retained_First_And_Partial_Tuple_Results_Are_Unchanged()
    {
        Assert.True(DiscardGrammars.TryParseRetainFirst("KeYwOrDKEYWORD!", out var value));
        Assert.Equal("KeYwOrD", value);
        Assert.True(DiscardGrammars.TryParsePartialTuple("VaLuEKeYwOrDVALUE", out var tuple));
        Assert.Equal(("VaLuE", "VALUE"), tuple);
        Assert.True(DiscardGrammars.TryParseDeferred("(KeYwOrD)(KEYWORD)", out value));
        Assert.Equal("KEYWORD", value);
    }

    [Theory]
    [InlineData("KeYwOrDVaLuEEnD", true)]
    [InlineData("KeYwOrDVaLuE?", false)]
    [InlineData("?VaLuEEnD", false)]
    [InlineData("KeYwOrD?EnD", false)]
    public void Between_Matches_Runtime_And_Restores_Input_For_Fallback(string input, bool success)
    {
        var runtime = Between(MatchedText("keyword"), MatchedText("value"), MatchedText("end")).Eof();
        Assert.Equal(success, runtime.TryParse(input, out var expected));
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();
        Assert.Equal(success, runtime.Parse(context, ref result));
        Assert.Equal(success ? input.Length : 0, context.Scanner.Cursor.Offset);
        Assert.Equal(success, DiscardGrammars.TryParseBetween(input, out var actual));
        Assert.Equal(expected, actual);
        using var reader = new SingleCharacterReader(input);
        Assert.Equal(success, DiscardGrammars.TryParseBetween(reader, out actual));
        Assert.Equal(expected, actual);
        Assert.True(DiscardGrammars.TryParseFallback(input, out var fallback));
        Assert.Equal(success ? "VaLuE" : input, fallback);
        using var fallbackReader = new SingleCharacterReader(input);
        Assert.True(DiscardGrammars.TryParseFallback(fallbackReader, out fallback));
        Assert.Equal(success ? "VaLuE" : input, fallback);
    }

    [Theory]
    [InlineData("VaLuEKeYwOrDVALUE", 2)]
    [InlineData("VaLuE", 1)]
    public void Separators_Are_Ignored_In_String_And_Reader_Parsers(string input, int count)
    {
        Assert.True(DiscardGrammars.TryParseSeparated(input, out var actual));
        Assert.Equal(count, actual);
        using var reader = new SingleCharacterReader(input);
        Assert.True(DiscardGrammars.TryParseSeparated(reader, out actual));
        Assert.Equal(count, actual);
    }

    [Fact]
    public void Separator_Options_Lookaheads_And_Capture_Preserve_Recognition()
    {
        Assert.True(DiscardGrammars.TryParseSeparatedOptions("KeYwOrDVaLuEKeYwOrDKEYWORDVALUEKeYwOrD", out var count));
        Assert.Equal(2, count);
        Assert.True(DiscardGrammars.TryParseLookahead("VaLuEKeYwOrD", out var value));
        Assert.Equal("VaLuE", value);
        Assert.False(DiscardGrammars.TryParseLookahead("VaLuE?", out _));
        Assert.True(DiscardGrammars.TryParseNegativeLookahead("VaLuE", out value));
        Assert.Equal("VaLuE", value);
        Assert.False(DiscardGrammars.TryParseNegativeLookahead("VaLuEKeYwOrD", out _));
        Assert.True(DiscardGrammars.TryParseNot("VaLuE", out value));
        Assert.Equal("VaLuE", value);
        Assert.False(DiscardGrammars.TryParseNot("KeYwOrD", out _));
        Assert.True(DiscardGrammars.TryParseTextBefore("prefixKeYwOrD", out value));
        Assert.Equal("prefix", value);
        Assert.True(DiscardGrammars.TryParseCapture("KeYwOrD", out value));
        Assert.Equal("KeYwOrD", value);
    }

    [Fact]
    public void Skipped_Callbacks_Still_Receive_Matched_Text_And_Propagate_Exceptions()
    {
        var state = new StringCallbackState();
        Assert.True(DiscardGrammars.TryParseCallback("KeYwOrDVaLuE", state, out var value));
        Assert.Equal("VaLuE", value);
        Assert.Equal("KeYwOrD", Assert.Single(state.Values));
        Assert.Throws<InvalidOperationException>(() =>
            DiscardGrammars.TryParseCallback("KeYwOrDVaLuE", new StringCallbackState { Throw = true }, out _));
    }

    [Theory]
    [InlineData("0!", true)]
    [InlineData("255!", true)]
    [InlineData("256!", false)]
    [InlineData("-1!", false)]
    [InlineData("1.5!", false)]
    [InlineData("999999999999999999999999!", false)]
    public void Skipped_Numbers_Still_Validate_Ranges_And_Restore_On_Failure(string input, bool success)
    {
        var runtime = Literals.Number<byte>(NumberOptions.Integer).SkipAnd(Literals.Text("!")).Eof();
        var context = new ParseContext(new Scanner(input));
        var result = new ParseResult<string>();
        Assert.Equal(success, runtime.Parse(context, ref result));
        Assert.Equal(success ? input.Length : 0, context.Scanner.Cursor.Offset);
        Assert.Equal(success, DiscardGrammars.TryParseSkippedNumber(input, out var value));
        Assert.Equal(success ? "!" : null, value);
        using var reader = new SingleCharacterReader(input);
        Assert.Equal(success, DiscardGrammars.TryParseSkippedNumber(reader, out value));
        Assert.Equal(success ? "!" : null, value);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void Generic_Math_Numeric_Emitter_Validates_Discarded_Values()
    {
        var parser = new NumberLiteral<byte>(NumberOptions.Integer);
        var source = parser.GenerateSource(new SourceGenerationContext { DiscardResult = true });
        var body = string.Join("\n", source.Body);
        Assert.Contains("Numbers.TryParseNumber<", body, StringComparison.Ordinal);
        Assert.Contains("out _)", body, StringComparison.Ordinal);
        Assert.DoesNotContain("parsedValue", body, StringComparison.Ordinal);
    }
#endif

    private static Parser<string> MatchedText(string text) =>
        Literals.Text(text, caseInsensitive: true, returnMatchedText: true);

    private static ISourceable Source<T>(Parser<T> parser) => Assert.IsAssignableFrom<ISourceable>(parser);

    private static ISourceable CreateParser(string kind, Parser<string> text)
    {
        var a = Literals.Char('a');
        return kind switch
        {
            "AndSkip1" => Source(a.AndSkip(text)),
            "AndSkip2" => Source(a.And(a).AndSkip(text)),
            "AndSkip3" => Source(a.And(a).And(a).AndSkip(text)),
            "AndSkip4" => Source(a.And(a).And(a).And(a).AndSkip(text)),
            "AndSkip5" => Source(a.And(a).And(a).And(a).And(a).AndSkip(text)),
            "AndSkip6" => Source(a.And(a).And(a).And(a).And(a).And(a).AndSkip(text)),
            "AndSkip7" => Source(a.And(a).And(a).And(a).And(a).And(a).And(a).AndSkip(text)),
            "SkipAnd" => Source(text.SkipAnd(a)),
            "Between" => Source(Between(text, a, text)),
            "Separated" => Source(Separated(text, a)),
            "SeparatedOptions" => Source(Separated(text, a, removeEmptyEntries: true)),
            "Not" => Source(Not(text)),
            "FollowedBy" => Source(a.WhenFollowedBy(text)),
            "NotFollowedBy" => Source(a.WhenNotFollowedBy(text)),
            "TextBefore" => Source(AnyCharBefore(text)),
            "Capture" => Source(Capture(text)),
            "Constant" => Source(text.Then(42)),
            "WhiteSpace" => Source(a.WithWhiteSpaceParser(Capture(text))),
            "LeftAssociative" => Source(a.LeftAssociative((text, static (left, _) => left))),
            "LeftAssociativeContext" => Source(a.LeftAssociative((text, static (_, left, _) => left))),
            "Unary" => Source(a.Unary((text, static value => value))),
            "UnaryContext" => Source(a.Unary((text, static (_, value) => value))),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    private sealed class ThrowingParser : Parser<string>, ISourceable
    {
        public override bool Parse(ParseContext context, ref ParseResult<string> result) => throw new NotSupportedException();
        public SourceResult GenerateSource(SourceGenerationContext context) => throw new NotSupportedException();
    }

    private sealed class SingleCharacterReader : StringReader
    {
        public SingleCharacterReader(string text) : base(text)
        {
        }

        public override int Read(char[] buffer, int index, int count) => base.Read(buffer, index, Math.Min(count, 1));
        public override int Read(Span<char> buffer) => base.Read(buffer[..Math.Min(buffer.Length, 1)]);
    }
}

public static partial class DiscardGrammars
{
    public static partial bool TryParseSkipFirst(string text, out string value);
    public static partial bool TryParseSkipFirst(TextReader reader, out string value);
    public static partial bool TryParseRetainFirst(string text, out string value);
    public static partial bool TryParseBetween(string text, out string value);
    public static partial bool TryParseBetween(TextReader reader, out string value);
    public static partial bool TryParseFallback(string text, out string value);
    public static partial bool TryParseFallback(TextReader reader, out string value);
    public static partial bool TryParseSeparated(string text, out int value);
    public static partial bool TryParseSeparated(TextReader reader, out int value);
    public static partial bool TryParseSeparatedOptions(string text, out int value);
    public static partial bool TryParseLookahead(string text, out string value);
    public static partial bool TryParseNegativeLookahead(string text, out string value);
    public static partial bool TryParseNot(string text, out string value);
    public static partial bool TryParseTextBefore(string text, out string value);
    public static partial bool TryParseCapture(string text, out string value);
    public static partial bool TryParsePartialTuple(string text, out (string, string) value);
    public static partial bool TryParseDeferred(string text, out string value);
    public static partial bool TryParseCallback(string text, StringCallbackState state, out string value);
    public static partial bool TryParseSkippedNumber(string text, out string value);
    public static partial bool TryParseSkippedNumber(TextReader reader, out string value);
}
