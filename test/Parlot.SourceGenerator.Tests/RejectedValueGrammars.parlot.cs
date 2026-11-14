#nullable enable

using System;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated copies of the runtime grammars of RejectedValueParityTests: a word which is read and then rejected, by a
// When condition, by a ThenTry conversion and by a ThenTry conversion returning a tuple, where the white space before it
// was skipped by its parser, by another one, with another set or by a white space parser. Each has a string and a
// TextReader entry point returning the furthest failure.
public static partial class RejectedValueGrammars
{
    private static Parser<string> When(Parser<TextSpan> word) =>
        word.When(static (_, value) => value.ToString() != "from").Then(static value => value.ToString());

    private static Parser<string> Conversion(Parser<TextSpan> word) =>
        word.ThenTry(static (ParseContext _, int _, int _, TextSpan value, out string result) =>
        {
            result = value.ToString();
            return result != "from";
        });

    private static Parser<string> Tuple(Parser<TextSpan> word) =>
        word.ThenTry(static value => (value.ToString() != "from", value.ToString()));

    // The word skips the white space before it
    private static Parser<string> Token(Func<Parser<TextSpan>, Parser<string>> reject) =>
        reject(Terms.Identifier()).AndSkip(Terms.Char(';')).Eof();

    // The word doesn't skip any
    private static Parser<string> Literal(Func<Parser<TextSpan>, Parser<string>> reject) =>
        reject(Literals.Identifier()).AndSkip(Terms.Char(';')).Eof();

    // Another alternative skipped the white space before the word
    private static Parser<string> Alternative(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Text("select").Or(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    // A lookahead which doesn't match skipped it
    private static Parser<string> Lookahead(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Not(Terms.Char('x')).SkipAnd(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    // A lookahead which matches read the word first, and moved the cursor back
    private static Parser<string> Peeked(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Peek(Terms.Identifier()).SkipAnd(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    // White space was skipped before another word first
    private static Parser<string> Second(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Identifier().SkipAnd(reject(Terms.Identifier())).AndSkip(Terms.Char(';')).Eof();

    // Comments are skipped too, by a white space parser
    private static Parser<string> Comments(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Token(reject).WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"));

    // Another alternative skipped the white space before the word, but not the comments its parser skips
    private static Parser<string> InnerComments(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Text("select").Or(reject(Terms.Identifier()).WithComments(static comments => comments.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/")))
            .AndSkip(Terms.Char(';')).Eof();

    // Another alternative skipped the white space of another set before the word
    private static Parser<string> InnerSet(Func<Parser<TextSpan>, Parser<string>> reject) =>
        Terms.Text("select").Or(reject(Terms.Identifier()).WithWhiteSpaceSet(WhiteSpaceSet.Default))
            .AndSkip(Terms.Char(';')).Eof().WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

    [GenerateParser(nameof(TryParseTokenWhen))]
    public static Parser<string> BuildTokenWhen() => Token(When);

    [GenerateParser(nameof(TryParseTokenConversion))]
    public static Parser<string> BuildTokenConversion() => Token(Conversion);

    [GenerateParser(nameof(TryParseTokenTuple))]
    public static Parser<string> BuildTokenTuple() => Token(Tuple);

    [GenerateParser(nameof(TryParseLiteralWhen))]
    public static Parser<string> BuildLiteralWhen() => Literal(When);

    [GenerateParser(nameof(TryParseLiteralConversion))]
    public static Parser<string> BuildLiteralConversion() => Literal(Conversion);

    [GenerateParser(nameof(TryParseLiteralTuple))]
    public static Parser<string> BuildLiteralTuple() => Literal(Tuple);

    [GenerateParser(nameof(TryParseAlternativeWhen))]
    public static Parser<string> BuildAlternativeWhen() => Alternative(When);

    [GenerateParser(nameof(TryParseAlternativeConversion))]
    public static Parser<string> BuildAlternativeConversion() => Alternative(Conversion);

    [GenerateParser(nameof(TryParseAlternativeTuple))]
    public static Parser<string> BuildAlternativeTuple() => Alternative(Tuple);

    [GenerateParser(nameof(TryParseLookaheadWhen))]
    public static Parser<string> BuildLookaheadWhen() => Lookahead(When);

    [GenerateParser(nameof(TryParseLookaheadConversion))]
    public static Parser<string> BuildLookaheadConversion() => Lookahead(Conversion);

    [GenerateParser(nameof(TryParseLookaheadTuple))]
    public static Parser<string> BuildLookaheadTuple() => Lookahead(Tuple);

    [GenerateParser(nameof(TryParsePeekedWhen))]
    public static Parser<string> BuildPeekedWhen() => Peeked(When);

    [GenerateParser(nameof(TryParsePeekedConversion))]
    public static Parser<string> BuildPeekedConversion() => Peeked(Conversion);

    [GenerateParser(nameof(TryParsePeekedTuple))]
    public static Parser<string> BuildPeekedTuple() => Peeked(Tuple);

    [GenerateParser(nameof(TryParseSecondWhen))]
    public static Parser<string> BuildSecondWhen() => Second(When);

    [GenerateParser(nameof(TryParseSecondConversion))]
    public static Parser<string> BuildSecondConversion() => Second(Conversion);

    [GenerateParser(nameof(TryParseSecondTuple))]
    public static Parser<string> BuildSecondTuple() => Second(Tuple);

    [GenerateParser(nameof(TryParseCommentsWhen))]
    public static Parser<string> BuildCommentsWhen() => Comments(When);

    [GenerateParser(nameof(TryParseCommentsConversion))]
    public static Parser<string> BuildCommentsConversion() => Comments(Conversion);

    [GenerateParser(nameof(TryParseCommentsTuple))]
    public static Parser<string> BuildCommentsTuple() => Comments(Tuple);

    [GenerateParser(nameof(TryParseInnerCommentsWhen))]
    public static Parser<string> BuildInnerCommentsWhen() => InnerComments(When);

    [GenerateParser(nameof(TryParseInnerCommentsConversion))]
    public static Parser<string> BuildInnerCommentsConversion() => InnerComments(Conversion);

    [GenerateParser(nameof(TryParseInnerCommentsTuple))]
    public static Parser<string> BuildInnerCommentsTuple() => InnerComments(Tuple);

    [GenerateParser(nameof(TryParseInnerSetWhen))]
    public static Parser<string> BuildInnerSetWhen() => InnerSet(When);

    [GenerateParser(nameof(TryParseInnerSetConversion))]
    public static Parser<string> BuildInnerSetConversion() => InnerSet(Conversion);

    [GenerateParser(nameof(TryParseInnerSetTuple))]
    public static Parser<string> BuildInnerSetTuple() => InnerSet(Tuple);

    // A value of two words: the white space before it isn't the last one which was skipped when it is rejected
    [GenerateParser(nameof(TryParseWordsWhen))]
    public static Parser<string> BuildWordsWhen() =>
        Terms.Identifier().And(Terms.Identifier()).When(static (_, value) => value.Item2.ToString() != "from").Then(static value => value.Item1.ToString())
            .AndSkip(Terms.Char(';')).Eof();

    [GenerateParser(nameof(TryParseWordsConversion))]
    public static Parser<string> BuildWordsConversion() =>
        Terms.Identifier().And(Terms.Identifier()).ThenTry(static (ParseContext _, int _, int _, (TextSpan, TextSpan) value, out string result) =>
        {
            result = value.Item1.ToString();
            return value.Item2.ToString() != "from";
        }).AndSkip(Terms.Char(';')).Eof();

    [GenerateParser(nameof(TryParseWordsTuple))]
    public static Parser<string> BuildWordsTuple() =>
        Terms.Identifier().And(Terms.Identifier()).ThenTry(static value => (value.Item2.ToString() != "from", value.Item1.ToString()))
            .AndSkip(Terms.Char(';')).Eof();

    // White space which is read as a value, by a parser which doesn't skip any, after a lookahead skipped it
    [GenerateParser(nameof(TryParseWhiteSpaceWhen))]
    public static Parser<string> BuildWhiteSpaceWhen() =>
        Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().When(static (_, _) => false).Then(static value => value.ToString())).Eof();

    [GenerateParser(nameof(TryParseWhiteSpaceConversion))]
    public static Parser<string> BuildWhiteSpaceConversion() =>
        Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().ThenTry(static (ParseContext _, int _, int _, TextSpan value, out string result) =>
        {
            result = value.ToString();
            return false;
        })).Eof();

    [GenerateParser(nameof(TryParseWhiteSpaceTuple))]
    public static Parser<string> BuildWhiteSpaceTuple() =>
        Not(Terms.Char('x')).SkipAnd(Literals.WhiteSpace().ThenTry(static value => (false, value.ToString()))).Eof();
}
