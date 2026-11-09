#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;
using ChunkedReader = Parlot.SourceGenerator.Tests.VectorizedTokenTests.ChunkedReader;

namespace Parlot.SourceGenerator.Tests;

// The generated optional parsers and loops test the char after the white space before invoking their parser, like the runtime
// ones. Their results, from strings and from readers filled one char at a time, must match the runtime parser's.
public class SeekGuardSourceTests
{
    private delegate bool StringParser(string text, out string value);
    private delegate bool ReaderParser(TextReader reader, out string value);

    // The grammars of SeekGuardGrammars, for the runtime parsers
    // The conditional tail of an expression, and assignments in a loop: 'let' a '=' e ';' ... '}'
    private static Parser<string> Statements()
    {
        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString())
            .Or(Between(Terms.Char('('), expression, Terms.Char(')')));
        var tail = Terms.Char('?').SkipAnd(expression).AndSkip(Terms.Char(':')).And(expression);

        expression.Parser = primary.And(tail.Optional())
            .Then(static t => t.Item2.HasValue ? $"({t.Item1}?{t.Item2.Value.Item1}:{t.Item2.Value.Item2})" : t.Item1);

        var statement = Terms.Text("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char('=')).And(expression).AndSkip(Terms.Char(';'))
            .Then(static t => $"{t.Item1}={t.Item2}");

        // A captured optional part keeps the white space before it
        var annotation = Capture(Terms.Char('@').And(expression)).Optional()
            .Then(static x => x.HasValue ? "[" + x.Value.ToString() + "]" : "");

        return ZeroOrMany(statement).And(annotation).And(ZeroOrOne(tail)).AndSkip(Terms.Char('}'))
            .Then(static t => string.Join(";", t.Item1) + t.Item2 + "|" + t.Item3.Count);
    }

    // A list whose separator is a sequence, and repeated tails after a first one
    private static Parser<string> Lists()
    {
        var item = Deferred<string>();
        var separator = Terms.Char(',').AndSkip(Literals.Char('|').Optional());
        var list = Between(Terms.Char('['), Separated(separator, item), Terms.Char(']'))
            .Then(static items => "[" + string.Join(",", items) + "]");
        var tail = Terms.Char('?').SkipAnd(item);

        item.Parser = Terms.Identifier().Then(static x => x.ToString()).Or(list);

        return item.And(OneOrMany(tail).Optional())
            .Then(static t => t.Item1 + (t.Item2.HasValue ? "?" + string.Join("?", t.Item2.Value) : ""));
    }

    [Theory]
    [InlineData("}")]
    [InlineData(" }")]
    [InlineData("let a = b; }")]
    [InlineData("let a = b ? c : d;\n let e = (f ? g : h) ? i : j ; }")]
    [InlineData("let a = b ? c ;}")]
    [InlineData("let a = b; le }")]
    [InlineData("let a = b; let }")]
    [InlineData("let a = b @ c }")]
    [InlineData("let a = b;   @ c ? d : e }")]
    [InlineData("@ }")]
    [InlineData("? a : b }")]
    [InlineData("  ? a : b ? c }")]
    [InlineData("let a = b;")]
    [InlineData("")]
    public void Statements_Match_The_Runtime(string text)
    {
        Check(Statements(), SeekGuardGrammars.TryParseStatements, SeekGuardGrammars.TryParseStatements, text);
    }

    [Theory]
    [InlineData("a")]
    [InlineData("[a]")]
    [InlineData("[a, b ,|c]")]
    [InlineData("[a, ]")]
    [InlineData("[a,| ]")]
    [InlineData("[a b]")]
    [InlineData("[a,[b,c],d] ? e ? [f]")]
    [InlineData("a ? ")]
    [InlineData("a ? b ?")]
    [InlineData("[")]
    [InlineData("")]
    public void Lists_Match_The_Runtime(string text)
    {
        Check(Lists(), SeekGuardGrammars.TryParseLists, SeekGuardGrammars.TryParseLists, text);
    }

    [Fact]
    public void Guards_Are_Emitted_For_Parsers_With_Hints()
    {
        var expression = Deferred<string>();
        expression.Parser = Terms.Identifier().Then(static x => x.ToString());
        var tail = Terms.Char('?').SkipAnd(expression).AndSkip(Terms.Char(':')).And(expression);

        var guarded = Generate(tail.Optional());
        Assert.Contains(guarded, static helper => helper.Contains("_SeekGuard", StringComparison.Ordinal));

        // A parser which can start with any char is invoked
        var unguarded = Generate(Always().SkipAnd(tail).Optional());
        Assert.DoesNotContain(unguarded, static helper => helper.Contains("_SeekGuard", StringComparison.Ordinal));

        static string[] Generate<T>(Parser<T> parser)
        {
            var context = new SourceGenerationContext();
            Assert.IsAssignableFrom<ISourceable>(parser).GenerateSource(context);
            return context.Helpers.Enumerate().Select(static helper => helper.MethodName).ToArray();
        }
    }

    // The grammar of SeekGuardGrammars, for the runtime parsers
    private static Parser<string> NonAsciiChar() =>
        Terms.Scan(static span => span.Length > 0 ? 1 : 0, "a" + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .Then(static x => x.ToString());

    private static Parser<string> NonAsciiSeparator() =>
        Terms.Scan(static span => span.Length > 0 ? 1 : 0, "," + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar)
            .Then(static x => x.ToString());

    [Theory]
    [InlineData("aé")]
    [InlineData("a é")]
    [InlineData("a\n\té")]
    [InlineData("#")]
    [InlineData("a #")]
    [InlineData("")]
    public void NonAsciiZeroOrMany_Match_The_Runtime(string text)
    {
        CheckValue(ZeroOrMany(NonAsciiChar()), SeekGuardGrammars.TryParseNonAsciiWords, SeekGuardGrammars.TryParseNonAsciiWords, text);
    }

    [Theory]
    [InlineData("aé")]
    [InlineData("a é")]
    [InlineData("a\n\té")]
    [InlineData("a #")]
    [InlineData("")]
    public void NonAsciiOneOrMany_Match_The_Runtime(string text)
    {
        CheckValue(OneOrMany(NonAsciiChar()), SeekGuardGrammars.TryParseNonAsciiOneOrMany, SeekGuardGrammars.TryParseNonAsciiOneOrMany, text);
    }

    [Theory]
    [InlineData("é")]
    [InlineData(" é")]
    [InlineData("\n\té")]
    [InlineData("#")]
    [InlineData("a")]
    [InlineData("")]
    public void NonAsciiOptional_Match_The_Runtime(string text)
    {
        CheckValue(NonAsciiChar().Optional().Then(static x => x.HasValue ? x.Value : ""), SeekGuardGrammars.TryParseNonAsciiOptional, SeekGuardGrammars.TryParseNonAsciiOptional, text);
        CheckValue(ZeroOrOne(NonAsciiChar()), SeekGuardGrammars.TryParseNonAsciiZeroOrOne, SeekGuardGrammars.TryParseNonAsciiZeroOrOne, text);
    }

    [Theory]
    [InlineData("aéa")]
    [InlineData("a é a")]
    [InlineData("a #")]
    [InlineData("a")]
    [InlineData("")]
    public void NonAsciiSeparated_Match_The_Runtime(string text)
    {
        CheckValue(Separated(NonAsciiSeparator(), Terms.Char('a').Then(static _ => "a")), SeekGuardGrammars.TryParseNonAsciiSeparated, SeekGuardGrammars.TryParseNonAsciiSeparated, text);
    }

    [Fact]
    public void GeneratedGuardsAcceptAnyNonAsciiChar()
    {
        Assert.True(SeekGuardGrammars.TryParseNonAsciiWords("aé", out var words));
        Assert.Equal(["a", "é"], words);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiWords("a é", out words));
        Assert.Equal(["a", "é"], words);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiWords("a\n\té", out words));
        Assert.Equal(["a", "é"], words);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiOneOrMany("a é", out var many));
        Assert.Equal(["a", "é"], many);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiOptional("é", out var optional));
        Assert.Equal("é", optional);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiZeroOrOne(" é", out var one));
        Assert.Equal(["é"], one);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiSeparated("a é a", out var separated));
        Assert.Equal(["a", "a"], separated);

        // An ASCII char outside the hints is still skipped
        Assert.True(SeekGuardGrammars.TryParseNonAsciiWords("a #", out words));
        Assert.Equal(["a"], words);

        Assert.True(SeekGuardGrammars.TryParseNonAsciiOptional("#", out optional));
        Assert.Equal("", optional);
    }

    private delegate bool StringValueParser<T>(string text, out T value);
    private delegate bool ReaderValueParser<T>(TextReader reader, out T value);

    private static void CheckValue<T>(Parser<T> parser, StringValueParser<T> parseString, ReaderValueParser<T> parseReader, string input)
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

    private static void Check(Parser<string> parser, StringParser parseString, ReaderParser parseReader, string input)
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

public static partial class SeekGuardGrammars
{
    public static partial bool TryParseStatements(string text, out string value);
    public static partial bool TryParseStatements(TextReader reader, out string value);
    public static partial bool TryParseLists(string text, out string value);
    public static partial bool TryParseLists(TextReader reader, out string value);

    public static partial bool TryParseNonAsciiWords(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiWords(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiOneOrMany(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiOneOrMany(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiOptional(string text, out string value);
    public static partial bool TryParseNonAsciiOptional(TextReader reader, out string value);
    public static partial bool TryParseNonAsciiZeroOrOne(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiZeroOrOne(TextReader reader, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiSeparated(string text, out IReadOnlyList<string> value);
    public static partial bool TryParseNonAsciiSeparated(TextReader reader, out IReadOnlyList<string> value);
}
