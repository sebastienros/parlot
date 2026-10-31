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
}
