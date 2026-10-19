using System.Collections.Generic;
using Parlot.Fluent;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// The generated parsers must leave the cursor at the same line and column as the runtime ones.
/// Only '\n' ends a line; every other char, '\r' included, occupies one column.
/// </summary>
public class PositionParityTests
{
    private delegate bool TryParse(string text, out (int Offset, int Line, int Column) position);

    private static readonly Dictionary<string, (Parser<(int, int, int)> Runtime, TryParse Generated)> Grammars = new()
    {
        ["crlf-text"] = (Literals.Text("\r\n").Then(static (c, _) => Position(c)), PositionGrammars.TryParseCrLfText),
        ["cr-char"] = (Literals.Char('\r').Then(static (c, _) => Position(c)), PositionGrammars.TryParseCrChar),
        ["lf-char"] = (Literals.Char('\n').Then(static (c, _) => Position(c)), PositionGrammars.TryParseLfChar),
        ["noneof"] = (Literals.NoneOf("abc").Then(static (c, _) => Position(c)), PositionGrammars.TryParseNoneOf),
        ["text-choice"] = (OneOf(
            Literals.Text("\r\n"),
            Literals.Text("\n"),
            Literals.Text("\r"),
            Literals.Text("x\ny\nz"),
            Literals.Text("x\r\ny"),
            Literals.Text("a"),
            Literals.Text("b"),
            Literals.Text("c")).Then(static (c, _) => Position(c)), PositionGrammars.TryParseTextChoice),
        ["keyword-choice"] = (OneOf(
            Terms.Keyword("if"),
            Terms.Keyword("else"),
            Terms.Keyword("for"),
            Terms.Keyword("while"),
            Terms.Keyword("do"),
            Terms.Keyword("return"),
            Terms.Keyword("break"),
            Terms.Keyword("continue")).Then(static (c, _) => Position(c)), PositionGrammars.TryParseKeywordChoice),
        ["pattern"] = (Literals.Pattern(static c => c != ' ').Then(static (c, _) => Position(c)), PositionGrammars.TryParsePattern),
    };

    private static (int, int, int) Position(ParseContext context)
    {
        var position = context.Scanner.Cursor.Position;
        return (position.Offset, position.Line, position.Column);
    }

    public static TheoryData<string, string, int, int, int> Cases => new()
    {
        // grammar, input, expected offset, line, column after the match, or -1 when the parser fails
        { "crlf-text", "\n", -1, 0, 0 },
        { "crlf-text", "\r\n", 2, 2, 1 },
        { "crlf-text", "\r\nx", 2, 2, 1 },
        { "crlf-text", "\r", -1, 0, 0 },
        { "crlf-text", "x\ny\nz", -1, 0, 0 },
        { "cr-char", "\n", -1, 0, 0 },
        { "cr-char", "\r\n", 1, 1, 2 },
        { "cr-char", "\r", 1, 1, 2 },
        { "cr-char", "x\ny\nz", -1, 0, 0 },
        { "lf-char", "\n", 1, 2, 1 },
        { "lf-char", "\r\n", -1, 0, 0 },
        { "lf-char", "\r", -1, 0, 0 },
        { "lf-char", "x\ny\nz", -1, 0, 0 },
        { "lf-char", "\nx", 1, 2, 1 },
        { "noneof", "\n", 1, 2, 1 },
        { "noneof", "\r\n", 2, 2, 1 },
        { "noneof", "\r", 1, 1, 2 },
        { "noneof", "x\ny\nz", 5, 3, 2 },
        { "noneof", "x\ny\nz\n", 6, 4, 1 },
        { "noneof", "x\r\ny\rz a", 7, 2, 5 },
        { "noneof", "abc", -1, 0, 0 },
        { "text-choice", "\n", 1, 2, 1 },
        { "text-choice", "\r\n", 2, 2, 1 },
        { "text-choice", "\r", 1, 1, 2 },
        { "text-choice", "x\ny\nz", 5, 3, 2 },
        { "text-choice", "x\r\ny", 4, 2, 2 },
        { "text-choice", "a", 1, 1, 2 },
        { "text-choice", "z", -1, 0, 0 },
        { "keyword-choice", "if", 2, 1, 3 },
        { "keyword-choice", "  for(", 5, 1, 6 },
        { "keyword-choice", "\tcontinue;", 9, 1, 10 },
        { "keyword-choice", "ifx", -1, 0, 0 },
        { "pattern", "abc", 3, 1, 4 },
        { "pattern", "abc def", 3, 1, 4 },
        { "pattern", "ab\r", 3, 1, 4 },
        { "pattern", "ab\rc", 4, 1, 5 },
        { "pattern", "ab\n", 3, 2, 1 },
        { "pattern", "ab\r\n", 4, 2, 1 },
        { "pattern", "ab\r\ncd ", 6, 2, 3 },
        { "pattern", "a\n\nb c", 4, 3, 2 },
        { "pattern", " a", -1, 0, 0 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void Generated_Parsers_Track_The_Same_Line_And_Column(string grammar, string input, int offset, int line, int column)
    {
        var (runtime, generated) = Grammars[grammar];

        var expectedSuccess = runtime.TryParse(input, out var expected);
        var actualSuccess = generated(input, out var actual);

        Assert.Equal(expectedSuccess, actualSuccess);
        Assert.Equal(offset >= 0, expectedSuccess);

        if (expectedSuccess)
        {
            Assert.Equal((offset, line, column), expected);
            Assert.Equal(expected, actual);
        }
    }
}

public static partial class PositionGrammars
{
    public static partial bool TryParseCrLfText(string text, out (int Offset, int Line, int Column) value);
    public static partial bool TryParseCrChar(string text, out (int Offset, int Line, int Column) value);
    public static partial bool TryParseLfChar(string text, out (int Offset, int Line, int Column) value);
    public static partial bool TryParseNoneOf(string text, out (int Offset, int Line, int Column) value);
    public static partial bool TryParseTextChoice(string text, out (int Offset, int Line, int Column) value);
    public static partial bool TryParseKeywordChoice(string text, out (int Offset, int Line, int Column) value);
    public static partial bool TryParsePattern(string text, out (int Offset, int Line, int Column) value);
}
