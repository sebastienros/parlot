#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;
using ChunkedReader = Parlot.SourceGenerator.Tests.VectorizedTokenTests.ChunkedReader;

namespace Parlot.SourceGenerator.Tests;

// The generated lookaheads don't invoke their parser where the current char is one it can't start with, and the choices
// select the sequences which start with a lookahead with the chars of the parser that follows it, like the runtime ones.
// Their results and furthest failures, from strings and from readers filled one char at a time, must match the runtime
// parser's.
public class LookaheadSourceTests
{
    private delegate bool StringParser(string text, out string value, out int failure);
    private delegate bool ReaderParser(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);

    private static readonly Dictionary<string, (Parser<string> Runtime, StringParser Generated, ReaderParser GeneratedReader)> Grammars = new()
    {
        ["Statements"] = (LookaheadRuntimeGrammars.BuildStatements(), LookaheadGrammars.TryParseStatements, LookaheadGrammars.TryParseStatements),
        ["Peeked"] = (LookaheadRuntimeGrammars.BuildPeeked(), LookaheadGrammars.TryParsePeeked, LookaheadGrammars.TryParsePeeked),
        ["Literals"] = (LookaheadRuntimeGrammars.BuildLiterals(), LookaheadGrammars.TryParseLiterals, LookaheadGrammars.TryParseLiterals),
    };

    private static readonly string[] Inputs =
    [
        "", " ", "}", " }", "a; }", "a ; b; }", "  a ;\n b ; }", "let a }", "let a; }", "letter; }", "let; }", "lett a }",
        "if a; }", "if; }", "iffy; }", "{ }", "{ a; }", "(a); }", " (a) ; }", "a", "a;", "let", "1; }", "1 }", "a 1; }",
        "a;b;let c}", "\n\tlet\tb }", " a; }", "a; }", "#", "a #", "( }", "(a }", "let}", "letx}", "le}", "(ab)}",
        "(ab}", "a b}", "ab 12 ;}", "c}", "b1 a2 }",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var name in Grammars.Keys)
        {
            yield return [name];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Generated_Lookaheads_Match_The_Runtime(string name)
    {
        var (runtime, generated, generatedReader) = Grammars[name];

        foreach (var text in Inputs)
        {
            var context = new ParseContext(new Scanner(text));
            var expected = runtime.TryParse(context, out var expectedValue, out _);
            var expectedFailure = expected ? 0 : context.Scanner.Cursor.FurthestOffset;

            void AssertSame(string parser, bool actual, string? actualValue, int actualFailure)
            {
                Assert.True(expected == actual && (!expected || expectedValue == actualValue),
                    $"{name} on '{text}' ({parser}): {(actual ? $"'{actualValue}'" : "fails")}, the runtime parser {(expected ? $"returns '{expectedValue}'" : "fails")}");
                Assert.True(expectedFailure == actualFailure, $"{name} on '{text}' ({parser}): the furthest failure is {actualFailure}, expected {expectedFailure}");
            }

            var success = generated(text, out var value, out var failure);
            AssertSame("string", success, value, failure);

            foreach (var reader in new TextReader[] { new StringReader(text), new ChunkedReader(text, 1), new ChunkedReader(text, 3) })
            {
                success = generatedReader(reader, out value, out var readerFailure);
                AssertSame("reader", success, value, readerFailure.Offset);
            }
        }
    }

    [Fact]
    public void Generated_Lookaheads_Test_The_Char_Before_Their_Parser()
    {
        // The guard of the lookahead's parser is emitted, for a choice too
        var context = new Parlot.SourceGeneration.SourceGenerationContext();
        var parser = Not(OneOf(Terms.Char('a'), Terms.Char('c'))).SkipAnd(Terms.Char('b'));
        Assert.IsAssignableFrom<Parlot.SourceGeneration.ISourceable>(parser).GenerateSource(context);

        Assert.Contains(context.Helpers.Enumerate(), static helper => helper.MethodName.Contains("_SeekGuard", StringComparison.Ordinal));
    }
}

// The grammars of LookaheadGrammars, for the runtime parsers
internal static class LookaheadRuntimeGrammars
{
    // A statement list: 'let' declarations, and expression statements which don't start with a reserved word or a '{'
    // (ECMAScript's [lookahead ∉ { ... }]). The expression is a Deferred set after the statement is built.
    public static Parser<string> BuildStatements()
    {
        var expression = Deferred<string>();
        var reserved = OneOf(Terms.Keyword("let"), Terms.Keyword("if"), Terms.Text("{"));

        var statement = OneOf(
            Terms.Keyword("let").SkipAnd(Terms.Identifier()).Then(static x => "let " + x),
            Terms.Text("if").SkipAnd(expression).AndSkip(Terms.Char(';')).Then(static x => "if " + x),
            Not(reserved).SkipAnd(expression).AndSkip(Terms.Char(';')).Then(static x => "expr " + x));

        expression.Parser = Terms.Identifier().Then(static x => x.ToString())
            .Or(Between(Terms.Char('('), expression, Terms.Char(')')).Then(static x => $"({x})"));

        return ZeroOrMany(statement).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // A positive lookahead: an identifier which starts with an 'a' or a 'b', or a number
    public static Parser<string> BuildPeeked()
    {
        var choice = OneOf(
            Peek(Terms.Char('a').Or(Terms.Char('b'))).SkipAnd(Terms.Identifier()).Then(static x => "id " + x),
            Terms.Integer().Then(static x => "int " + x),
            Terms.Char(';').Then(static _ => ";"));

        return ZeroOrMany(choice).AndSkip(Terms.Char('}')).Then(static x => string.Join(",", x));
    }

    // Without white space: the lookaheads and what follows them are literals
    public static Parser<string> BuildLiterals()
    {
        var word = Literals.Pattern(static c => c is >= 'a' and <= 'z');
        var choice = OneOf(
            Not(Literals.Text("let")).SkipAnd(word).Then(static x => "word " + x),
            Literals.Text("let").Then(static _ => "let"),
            Literals.Char(' ').Then(static _ => "_"),
            Peek(Literals.Char('(')).SkipAnd(Literals.Char('(').SkipAnd(word).AndSkip(Literals.Char(')'))).Then(static x => $"({x})"));

        return ZeroOrMany(choice).AndSkip(Literals.Char('}')).Then(static x => string.Join(",", x));
    }
}

public static partial class LookaheadGrammars
{
    public static partial bool TryParseStatements(string text, out string value, out int failure);
    public static partial bool TryParseStatements(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParsePeeked(string text, out string value, out int failure);
    public static partial bool TryParsePeeked(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
    public static partial bool TryParseLiterals(string text, out string value, out int failure);
    public static partial bool TryParseLiterals(TextReader reader, out string value, out (int Offset, int Line, int Column) failure);
}
