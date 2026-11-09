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

    private static Parser<string> NonAsciiWords()
    {
        var word = Terms.Scan(static span => span.IsEmpty || !(span[0] is 'a' or 'b' || span[0] >= 0x80) ? 0 : span.IndexOfAny(' ', ';') is var end and >= 0 ? end : span.Length, "ab" + Parlot.Fluent.OneOf<TextSpan>.NonAsciiSeekableChar).Then(static x => x.ToString());

        return ZeroOrMany(word).And(Terms.Char(';').SkipAnd(word).Optional())
            .Then(static t => string.Join(",", t.Item1) + (t.Item2.HasValue ? ";" + t.Item2.Value : ""));
    }

    [Theory]
    [InlineData("a é b")]
    [InlineData("é")]
    [InlineData("a 日本 x")]
    [InlineData("x")]
    [InlineData("a ; ü")]
    [InlineData(";é")]
    [InlineData("; x")]
    [InlineData("")]
    public void NonAsciiWords_Match_The_Runtime(string text)
    {
        Check(NonAsciiWords(), SeekGuardGrammars.TryParseNonAsciiWords, SeekGuardGrammars.TryParseNonAsciiWords, text);

        // Every word is read, whatever its first char above ASCII
        if (text == "a 日本 x")
        {
            Assert.True(SeekGuardGrammars.TryParseNonAsciiWords(text, out var value));
            Assert.Equal("a,日本", value);
        }
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
    public static partial bool TryParseNonAsciiWords(string text, out string value);
    public static partial bool TryParseNonAsciiWords(TextReader reader, out string value);
}
