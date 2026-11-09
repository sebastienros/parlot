#nullable enable

using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars telling whether the white space before the next token contains a new line, compared with the runtime parsers in
// NewLineTests. Each has a string and a TextReader entry point.
public static partial class NewLineGrammars
{
    // Statements ended by a semicolon, a '}', the end of the input or a new line, with "[no LineTerminator here]" before the
    // argument of return and before a postfix operator
    internal static Parser<string> Statements()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), Always().WhenFollowedBy(Terms.Char('}')), SkipWhiteSpace(Eof()), NewLineAhead());
        var returnStatement = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x.ToString()).Else("return"));
        var prefix = Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x.ToString());
        var expression = Terms.Identifier().And(NoNewLineBefore(Terms.Text("++")).Else("")).Then(static x => x.Item1.ToString() + x.Item2);
        return ZeroOrMany(OneOf(returnStatement, prefix, expression).AndSkip(end)).Eof().Then(static x => string.Join("|", x));
    }

    [GenerateParser(nameof(TryParseStatements))]
    private static Parser<string> BuildStatements() => Statements();

    internal static Parser<string> StatementsWithComments() =>
        Statements().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    [GenerateParser(nameof(TryParseStatementsWithComments))]
    private static Parser<string> BuildStatementsWithComments() => StatementsWithComments();

    // JavaScript's line terminators, of which \v isn't one
    internal static Parser<string> CustomNewLines() =>
        Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore("\n\r\u2028\u2029").Then(static x => "return " + x.ToString()).Else("return"))
            .And(NewLineAhead("\n\r\u2028\u2029").Then(static _ => "+nl").Else(""))
            .Then(static x => x.Item1 + x.Item2);

    [GenerateParser(nameof(TryParseCustomNewLines))]
    private static Parser<string> BuildCustomNewLines() => CustomNewLines();

    // The values aren't used: the parsers are generated without them
    internal static Parser<int> Captured() =>
        Capture(Terms.Identifier().And(Terms.Identifier().NoNewLineBefore()).And(NewLineAhead())).Then(static x => x.Length);

    [GenerateParser(nameof(TryParseCaptured))]
    private static Parser<int> BuildCaptured() => Captured();

    // The new lines of the ASCII set: \v is one, U+2028 isn't white space
    internal static Parser<string> AsciiSet() =>
        Terms.Identifier().And(Terms.Identifier().NoNewLineBefore().Then(static x => " " + x.ToString()).Else(""))
            .Then(static x => x.Item1.ToString() + x.Item2)
            .WithWhiteSpaceSet(WhiteSpaceSet.Ascii);

    [GenerateParser(nameof(TryParseAsciiSet))]
    private static Parser<string> BuildAsciiSet() => AsciiSet();

    // A postfix '++' with "[no LineTerminator here]", a choice of the postfix operators of a table which skips the white space
    // once for its alternatives, with comments
    internal static Parser<string> PostfixTable()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), SkipWhiteSpace(Eof()), NewLineAhead());
        var expression = Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix(
                (NoNewLineBefore(Terms.Text("++")), static x => x + "++"),
                (Terms.Text("!"), static x => x + "!"));
        return ZeroOrMany(OneOf(Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x.ToString()), expression).AndSkip(end)).Eof()
            .Then(static x => string.Join("|", x))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));
    }

    [GenerateParser(nameof(TryParsePostfixTable))]
    private static Parser<string> BuildPostfixTable() => PostfixTable();
}
