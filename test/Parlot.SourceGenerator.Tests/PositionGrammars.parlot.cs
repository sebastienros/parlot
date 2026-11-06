#nullable enable

using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Each entry point returns the cursor position (offset, line, column) after the parser matched, such that the
// line and column tracking of the generated code can be compared with the runtime one.
public static partial class PositionGrammars
{
    [GenerateParser(nameof(TryParseCrLfText))]
    private static Parser<(int Offset, int Line, int Column)> BuildCrLfText() =>
        Literals.Text("\r\n").Then(static (ParseContext context, string _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    [GenerateParser(nameof(TryParseCrChar))]
    private static Parser<(int Offset, int Line, int Column)> BuildCrChar() =>
        Literals.Char('\r').Then(static (ParseContext context, char _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    [GenerateParser(nameof(TryParseLfChar))]
    private static Parser<(int Offset, int Line, int Column)> BuildLfChar() =>
        Literals.Char('\n').Then(static (ParseContext context, char _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    [GenerateParser(nameof(TryParseNoneOf))]
    private static Parser<(int Offset, int Line, int Column)> BuildNoneOf() =>
        Literals.NoneOf("abc").Then(static (ParseContext context, TextSpan _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    // Eight ordinal texts or more are emitted as a single text lookup
    [GenerateParser(nameof(TryParseTextChoice))]
    private static Parser<(int Offset, int Line, int Column)> BuildTextChoice() =>
        OneOf(
            Literals.Text("\r\n"),
            Literals.Text("\n"),
            Literals.Text("\r"),
            Literals.Text("x\ny\nz"),
            Literals.Text("x\r\ny"),
            Literals.Text("a"),
            Literals.Text("b"),
            Literals.Text("c"))
        .Then(static (ParseContext context, string _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    [GenerateParser(nameof(TryParseKeywordChoice))]
    private static Parser<(int Offset, int Line, int Column)> BuildKeywordChoice() =>
        OneOf(
            Terms.Keyword("if"),
            Terms.Keyword("else"),
            Terms.Keyword("for"),
            Terms.Keyword("while"),
            Terms.Keyword("do"),
            Terms.Keyword("return"),
            Terms.Keyword("break"),
            Terms.Keyword("continue"))
        .Then(static (ParseContext context, string _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    [GenerateParser(nameof(TryParsePattern))]
    private static Parser<(int Offset, int Line, int Column)> BuildPattern() =>
        Literals.Pattern(static c => c != ' ').Then(static (ParseContext context, TextSpan _) =>
        {
            var position = context.Scanner.Cursor.Position;
            return (position.Offset, position.Line, position.Column);
        });

    // The first alternative reads the position after '=' then fails, the second reads an earlier position
    [GenerateParser(nameof(TryParseBacktrack))]
    private static Parser<(int Offset, int Line, int Column)> BuildBacktrack() =>
        OneOf(
            Terms.Identifier().SkipAnd(Terms.Char('=').Then(static (ParseContext context, char _) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (position.Offset, position.Line, position.Column);
            })).AndSkip(Terms.Char('(')),
            Terms.Identifier().Then(static (ParseContext context, TextSpan _) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (position.Offset, position.Line, position.Column);
            }));

    // The offsets a callback receives on composites, compared with the runtime in ThenOffsetsTests
    [GenerateParser(nameof(TryParseOffsetsChoice))]
    private static Parser<(int Start, int End)> BuildOffsetsChoice() =>
        Terms.Text("ab").Or(Terms.Text("cd")).Then(static (context, start, end, _) => (start, end));

    [GenerateParser(nameof(TryParseOffsetsLargeChoice))]
    private static Parser<(int Start, int End)> BuildOffsetsLargeChoice() =>
        OneOf(
            Terms.Keyword("if"), Terms.Keyword("else"), Terms.Keyword("for"), Terms.Keyword("while"),
            Terms.Keyword("do"), Terms.Keyword("return"), Terms.Keyword("break"), Terms.Keyword("continue"))
        .Then(static (context, start, end, _) => (start, end));

    [GenerateParser(nameof(TryParseOffsetsSequence))]
    private static Parser<(int Start, int End)> BuildOffsetsSequence() =>
        Terms.Text("ab").And(Terms.Text("cd")).Then(static (context, start, end, _) => (start, end));

    [GenerateParser(nameof(TryParseOffsetsMixedChoice))]
    private static Parser<(int Start, int End)> BuildOffsetsMixedChoice() =>
        Terms.Text("ab").Or(Literals.Text("cd")).Then(static (context, start, end, _) => (start, end));

    // Between reports offsets which span its delimiters, like SkipAnd/AndSkip
    [GenerateParser(nameof(TryParseOffsetsBetween))]
    private static Parser<(int Start, int End)> BuildOffsetsBetween() =>
        Between(Terms.Char('['), Terms.Identifier(), Terms.Char(']')).Then(static (context, start, end, _) => (start, end));

    [GenerateParser(nameof(TryParseOffsetsBetweenLiterals))]
    private static Parser<(int Start, int End)> BuildOffsetsBetweenLiterals() =>
        Between(Literals.Char('['), Literals.Identifier(), Literals.Char(']')).Then(static (context, start, end, _) => (start, end));

    [GenerateParser(nameof(TryParseOffsetsSkipAndAndSkip))]
    private static Parser<(int Start, int End)> BuildOffsetsSkipAndAndSkip() =>
        Terms.Char('[').SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(']')).Then(static (context, start, end, _) => (start, end));

    // A failed alternative gives the white space back to the next parser
    [GenerateParser(nameof(TryParseOffsetsBacktrack))]
    private static Parser<(int Start, int End)> BuildOffsetsBacktrack() =>
        ZeroOrOne(Terms.Text("ab").Or(Terms.Text("cd")).Then(static (context, start, end, _) => (start, end)))
            .SkipAnd(Literals.Text("  x").Then(static (context, start, end, _) => (start, end)));
}
