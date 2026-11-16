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

    // NoNewLineBefore alternatives of choices which skip the white space for them: an arrow, which can't follow a new line,
    // after an assignment operator which reads its '=', and the arrow after a new line read by the last alternative. The
    // choice is in a sequence, which doesn't keep the text buffered when it's read from a TextReader.
    internal static Parser<string> ArrowChoice()
    {
        var assignment = OneOf(Terms.Text("="), Terms.Text("+=")).And(Terms.Identifier()).Then(static x => x.Item1 + x.Item2.ToString());
        var arrow = NoNewLineBefore(Terms.Text("=>")).SkipAnd(Terms.Identifier()).Then(static x => "=>" + x.ToString());
        var arrowAfterNewLine = Terms.Text("=>").SkipAnd(Terms.Identifier()).Then(static x => "|=>" + x.ToString());
        return Terms.Identifier().And(OneOf(assignment, arrow, arrowAfterNewLine)).Then(static x => x.Item1.ToString() + x.Item2).Eof();
    }

    [GenerateParser(nameof(TryParseArrowChoice))]
    private static Parser<string> BuildArrowChoice() => ArrowChoice();

    internal static Parser<string> ArrowChoiceWithComments() =>
        ArrowChoice().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    [GenerateParser(nameof(TryParseArrowChoiceWithComments))]
    private static Parser<string> BuildArrowChoiceWithComments() => ArrowChoiceWithComments();

    // Postfix operators which can't follow a new line, in a table with binary operators
    internal static Parser<string> PostfixTable()
    {
        var table = Terms.Identifier().Then(static x => x.ToString()).Operators()
            .Postfix((NoNewLineBefore(Terms.Text("++")), static x => "(" + x + "++)"), (NoNewLineBefore(Terms.Text("--")), static x => "(" + x + "--)"))
            .LeftAssociative((Terms.Char('*'), static (a, b) => "(" + a + "*" + b + ")"))
            .LeftAssociative((Terms.Char('+'), static (a, b) => "(" + a + "+" + b + ")"));
        var prefix = Terms.Text("++").SkipAnd(Terms.Identifier()).Then(static x => "++" + x.ToString());
        return ZeroOrMany(OneOf(table, prefix)).Eof().Then(static x => string.Join("|", x));
    }

    [GenerateParser(nameof(TryParsePostfixTable))]
    private static Parser<string> BuildPostfixTable() => PostfixTable();

    internal static Parser<string> PostfixTableWithComments() =>
        PostfixTable().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"));

    [GenerateParser(nameof(TryParsePostfixTableWithComments))]
    private static Parser<string> BuildPostfixTableWithComments() => PostfixTableWithComments();

    // NoNewLineBefore alternatives looking for their own new lines in the white space the choice skips for them
    internal static Parser<string> NewLineSets() =>
        Terms.Identifier().SkipAnd(OneOf(NoNewLineBefore(Terms.Text("b"), "\r"), NoNewLineBefore(Terms.Text("c")), Terms.Text("e"))).Eof();

    [GenerateParser(nameof(TryParseNewLineSets))]
    private static Parser<string> BuildNewLineSets() => NewLineSets();

    // A NoNewLineBefore after a parser skipping the white space, which sees none, and one holding another one: the choice
    // invokes its alternatives before the white space
    internal static Parser<string> SkippedNewLines() =>
        Terms.Identifier().SkipAnd(OneOf(SkipWhiteSpace(NoNewLineBefore(Literals.Text("b"))), NoNewLineBefore(NoNewLineBefore(Terms.Text("d")), "\r"), NoNewLineBefore(Terms.Text("c")))).Eof();

    [GenerateParser(nameof(TryParseSkippedNewLines))]
    private static Parser<string> BuildSkippedNewLines() => SkippedNewLines();
}
