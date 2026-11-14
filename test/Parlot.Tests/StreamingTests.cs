#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Parlot.Fluent;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using Parlot.Tests.AccessLog;
using Parlot.Tests.Sql;
using Xunit;
using static Parlot.Fluent.Parsers;
using CalcExpression = Parlot.Tests.Calc.Expression;

namespace Parlot.Tests;

/// <summary>
/// Compares the parsing of streams, and of non-final prefixes, with the parsing of the whole text.
/// </summary>
public class StreamingTests
{
    // Declared before the grammars, which read it
    private static readonly string AboveAscii = new string(Enumerable.Range(0x80, 0xFFFE - 0x80 + 1).Select(static i => (char)i).ToArray());

    private static readonly Dictionary<string, IGrammar> Grammars = new()
    {
        ["calc"] = new Grammar<CalcExpression>(FluentParser.Expression),
        ["calc-eof"] = new Grammar<CalcExpression>(FluentParser.Expression.Eof()),
        ["json"] = new Grammar<IJson>(JsonParser.Json),
        ["json-eof"] = new Grammar<IJson>(JsonParser.Json.Eof()),
        ["log-record"] = new Grammar<int>(LogParser.Record),
        ["log"] = new Grammar<int>(LogParser.Log),
        ["sql"] = new Grammar<StatementList>(CreateSqlParser(), static scanner => new ParseContext(scanner, disableLoopDetection: true)),
        // Operators are read again by the looser levels when they are not followed by an operand
        ["operators"] = new Grammar<string>(CreateOperators()),
        ["operators-eof"] = new Grammar<string>(CreateOperators().Eof()),
        // A chain of operator levels is parsed by a single precedence parser
        ["operator-levels"] = new Grammar<string>(CreateOperatorLevels()),
        // Valued operators are parsed by the level which applies them, and read again by the looser levels when they fail
        ["valued-operators"] = new Grammar<string>(CreateValuedOperators()),
        ["valued-operators-eof"] = new Grammar<string>(CreateValuedOperators().Eof()),
        // Valued prefix operators are matched with the plain ones, their values are kept until they apply
        ["valued-prefix"] = new Grammar<string>(CreateValuedPrefix()),
        // A conditional whose last operand is the whole expression, read after the colon by the level
        ["conditional-last"] = new Grammar<string>(CreateConditionalWithLast()),
        ["conditional-last-eof"] = new Grammar<string>(CreateConditionalWithLast().Eof()),
        ["identifier"] = new Grammar<TextSpan>(Terms.Identifier()),
        // Escape sequences and surrogate pairs cut by the end of the window
        ["codepoint-identifiers"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(Terms.Identifier(CodePointIdentifierTests.IsJavaScriptStart, CodePointIdentifierTests.IsJavaScriptPart, IdentifierOptions.JavaScript).Then(static x => Character.DecodeIdentifier(x).ToString())).Eof()),
        ["codepoint-identifier-calls"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(Terms.Identifier(CodePointIdentifierTests.IsJavaScriptStart, CodePointIdentifierTests.IsJavaScriptPart, IdentifierOptions.JavaScript).AndSkip(Terms.Char('(')).Then(static x => Character.DecodeIdentifier(x).ToString())).Eof()),
        ["integer"] = new Grammar<long>(Terms.Integer()),
        ["standalone-eof"] = new Grammar<object?>(Eof()),
        ["integer-standalone-eof"] = new Grammar<long>(Terms.Integer().AndSkip(Eof())),
        ["integer-eof-error"] = new Grammar<long>(Terms.Integer().AndSkip(Eof().ElseError("Unexpected trailing input"))),
        ["decimal"] = new Grammar<decimal>(Terms.Decimal()),
        ["double"] = new Grammar<double>(Terms.Number<double>(NumberOptions.Float)),
        // Trailing underscores aren't part of the number, but digits could follow them in the next window
        ["underscores"] = new Grammar<decimal>(Terms.Number<decimal>(NumberOptions.Any | NumberOptions.AllowUnderscore)),
        ["underscores-list"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(';'), Terms.Number<long>(NumberOptions.Integer | NumberOptions.AllowUnderscore))),
        ["text"] = new Grammar<string>(Terms.Text("hello")),
        ["text-ci"] = new Grammar<string>(Terms.Text("hello", caseInsensitive: true)),
        ["keyword"] = new Grammar<string>(Terms.Keyword("for")),
        ["choice"] = new Grammar<string>(OneOf(Terms.Text("for"), Terms.Text("foreach"), Terms.Text("forever"), Terms.Text("if"))),
        ["keywords"] = new Grammar<string>(OneOf(Terms.Keyword("for"), Terms.Keyword("foreach"), Terms.Keyword("if"))),
        // The window can end in the middle of the prefix that selects the alternatives
        ["prefix-keywords"] = new Grammar<string>(OneOf(Terms.Keyword("in"), Terms.Keyword("int"), Terms.Keyword("interface"), Terms.Keyword("internal"), Terms.Keyword("is"))),
        ["prefix-texts"] = new Grammar<string>(OneOf(Terms.Text("<<="), Terms.Text("<="), Terms.Text("<<"), Terms.Text("<>"), Terms.Text("<"))),
        ["keyword-part"] = new Grammar<string>(Terms.Keyword("in", "abcdefghijklmnopqrstuvwxyz0123456789_-\u00e9")),
        ["keywords-part"] = new Grammar<string>(OneOf(Literals.Keyword("in", "abcdefghijklmnopqrstuvwxyz0123456789_-"), Literals.Keyword("inc", "-"), Literals.Text("i"))),
        ["keyword-large-set"] = new Grammar<string>(Terms.Keyword("in", "abcdefghijklmnopqrstuvwxyz0123456789" + AboveAscii, caseInsensitive: true)),
        ["keywords-large-set"] = new Grammar<string>(OneOf(Literals.Keyword("in", "abcdefghijklmnopqrstuvwxyz" + AboveAscii), Literals.Keyword("inc", "abcdefghijklmnopqrstuvwxyz" + AboveAscii), Literals.Text("i"))),
        ["anyof"] = new Grammar<TextSpan>(Literals.AnyOf("abc", minSize: 3)),
        ["anyof-max"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Literals.AnyOf("abc", minSize: 1, maxSize: 2))),
        ["noneof"] = new Grammar<TextSpan>(Literals.NoneOf(",;", minSize: 2)),
        ["pattern"] = new Grammar<TextSpan>(Literals.Pattern(char.IsDigit, minSize: 2, maxSize: 4)),
        ["pattern-empty"] = new Grammar<TextSpan>(Literals.Pattern(char.IsDigit, minSize: 0)),
        ["scan-identifier"] = new Grammar<TextSpan>(Literals.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars)),
        ["scan-signature"] = new Grammar<TextSpan>(Literals.Scan(LiquidScanners.VariableSignature, LiquidScanners.StartChars)),
        ["scan-tag"] = new Grammar<IReadOnlyList<TextSpan>>(Terms.Text("{%").SkipAnd(ZeroOrMany(Terms.Scan(LiquidScanners.Identifier, LiquidScanners.StartChars))).AndSkip(Terms.Text("-%}").Or(Terms.Text("%}")))),
        ["string"] = new Grammar<TextSpan>(Terms.String()),
        // Pooled strings copy the text out of the window, and string literals decode into the pool
        ["pooled-strings"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(OneOf(Terms.Identifier().ToPooledString(), Terms.String().ToPooledString())).Eof()),
        ["text-ci-matched"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(OneOf(Terms.Text("hello", caseInsensitive: true, returnMatchedText: true), Terms.Keyword("for", caseInsensitive: true, returnMatchedText: true)))),
        // Escapes that read optional chars, or a "\r\n" pair, can be split by the end of the window
        ["js-string"] = new Grammar<TextSpan>(Terms.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript)),
        // A quote at the end of the window can be the first of a doubled one
        ["sql-string"] = new Grammar<TextSpan>(Terms.String(StringLiteralQuotes.Single, StringEscapes.Sql)),
        ["python-string"] = new Grammar<TextSpan>(Terms.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.Python)),
        ["json-string"] = new Grammar<TextSpan>(Terms.String(StringLiteralQuotes.Double, StringEscapes.Json)),
        ["js-pooled-strings"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(Terms.String(StringLiteralQuotes.SingleOrDouble, StringEscapes.JavaScript).ToPooledString()).Eof()),
        ["backslash-string"] = new Grammar<TextSpan>(new StringLiteral('\\')),
        ["nul-string"] = new Grammar<TextSpan>(new StringLiteral('\0')),
        ["whitespace"] = new Grammar<TextSpan>(Literals.WhiteSpace(includeNewLines: true)),
        ["nonwhitespace"] = new Grammar<TextSpan>(Literals.NonWhiteSpace()),
        ["textbefore"] = new Grammar<TextSpan>(AnyCharBefore(Literals.Text("--"), failOnEof: true, consumeDelimiter: true)),
        ["textbefore-eof"] = new Grammar<TextSpan>(AnyCharBefore(Literals.Text("--"))),
        ["capture"] = new Grammar<TextSpan>(Capture(Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer()))),
        ["not"] = new Grammar<TextSpan>(Not(Literals.Text("ab")).SkipAnd(Literals.NonWhiteSpace())),
        // A lookahead at the start of an alternative, the text it read is read again
        ["peek"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(OneOf(Terms.Text("ab"), Peek(Terms.Identifier()).SkipAnd(Terms.Identifier().Then(static x => x.ToString()))))),
        ["peek-value"] = new Grammar<(long, long)>(Peek(Terms.Integer()).And(Terms.Integer())),
        ["followed"] = new Grammar<TextSpan>(Terms.Identifier().WhenFollowedBy(Terms.Char('('))),
        ["notfollowed"] = new Grammar<long>(Terms.Integer().WhenNotFollowedBy(Literals.Char('.'))),
        // A rejected value gives the cursor back to the next alternative, here the keyword the identifier rejects
        ["thentry"] = new Grammar<string>(ZeroOrMany(Terms.Identifier().ThenTry(static (ParseContext _, int start, int end, TextSpan name, out string result) =>
        {
            result = $"{name}@{start}-{end}";
            return !name.Span.SequenceEqual("for");
        }).Or(Terms.Keyword("for"))).Then(static values => string.Join(",", values))),
        // A parser which reads from where it is invoked, though what it reads first skips white space: a capture holds that
        // white space, and a lookahead leaves the cursor before it. A condition or a conversion of its value doesn't
        // change where it starts, whatever the buffer holds. The value is followed by where the cursor is.
        ["when-capture"] = new Grammar<string>(Capture(Terms.Integer()).When(static (_, value) => value.Length < 3).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")),
        ["when-capture-else"] = new Grammar<string>(Capture(Terms.Integer()).When(static (_, value) => value.Length < 3).Then(static value => value.ToString()).Else("none").Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")),
        ["when-peek"] = new Grammar<string>(Peek(Terms.Char('a')).When(static (_, _) => true).SkipAnd(Literals.Text("abc"))),
        ["when-peek-rejected"] = new Grammar<string>(Peek(Terms.Identifier()).When(static (_, value) => value.Length < 3).SkipAnd(Capture(Literals.WhiteSpace().Optional().And(Literals.Identifier()))).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")),
        ["when-sequence"] = new Grammar<string>(Capture(Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer())).When(static (_, value) => value.Length < 8).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")),
        ["thentry-capture"] = new Grammar<string>(Capture(Terms.Integer()).ThenTry(static value => (value.Length < 3, value.ToString())).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")),
        ["thentry-capture-offsets"] = new Grammar<string>(Capture(Terms.Integer()).ThenTry(static (ParseContext c, int start, int end, TextSpan value, out string result) =>
        {
            result = $"{value}@{start}-{end}@{c.Scanner.Cursor.Offset}";
            return value.Length < 3;
        }).Else("none")),
        ["thentry-peek"] = new Grammar<string>(Peek(Terms.Char('a')).ThenTry(static value => (true, value)).SkipAnd(Literals.Text("abc"))),
        ["thentry-peek-rejected"] = new Grammar<string>(Peek(Terms.Identifier()).ThenTry(static value => (value.Length < 3, value)).SkipAnd(Capture(Literals.WhiteSpace().Optional().And(Literals.Identifier()))).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")),
        // A token skips the white space itself: the same value whoever skips it
        ["when-token"] = new Grammar<string>(ZeroOrMany(Terms.Integer().When(static (_, value) => value < 100).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}")).Then(static values => string.Join(",", values))),
        ["when-comments"] = new Grammar<string>(Capture(Terms.Integer()).When(static (_, value) => value.Length < 9).Then(static (c, value) => $"{value}@{c.Scanner.Cursor.Offset}").WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/"))),
        ["separated"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(','), Terms.Integer())),
        // Optional parsers and loops over sequences don't invoke them when the char after the white space can't start them
        ["guarded-optional"] = new Grammar<string>(Terms.Identifier().And(Terms.Char('?').SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(':')).And(Terms.Identifier()).Optional())
            .Then(static t => t.Item1.ToString() + (t.Item2.HasValue ? $"?{t.Item2.Value.Item1}:{t.Item2.Value.Item2}" : "")).AndSkip(Terms.Char(';'))),
        ["guarded-loop"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Text("let").SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(';'))).AndSkip(Terms.Char('}'))),
        ["guarded-separated"] = new Grammar<IReadOnlyList<TextSpan>>(Separated(Terms.Char(',').AndSkip(Literals.Char('|').Optional()), Terms.Identifier()).AndSkip(Terms.Char(';'))),
        // The first value can match without reading, before a separator (#503)
        ["separated-empty-first"] = new Grammar<IReadOnlyList<string>>(Separated(Terms.Char('.'), Terms.Identifier().Then(static x => x.ToString()).Or(Always(""))).AndSkip(Terms.Char(';'))),
        ["list-eof"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).Eof()),
        // Nested lists of the same element type share the scratch stack of the list parsers
        ["nested-lists"] = new Grammar<object>(CreateNestedLists()),
        // A state set for the parser in braces, which an alternative leaves when it fails after reading
        ["state"] = new Grammar<IReadOnlyList<string>>(CreateStateGrammar(), static scanner => new DepthContext(scanner)),
        ["elseerror"] = new Grammar<long>(Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'"))),
        ["comments"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"))),
        // Statements ended by a semicolon or a new line, with "[no LineTerminator here]" before an argument and a postfix operator
        ["newlines"] = new Grammar<string>(CreateNewLineStatements()),
        ["newlines-comments"] = new Grammar<string>(CreateNewLineStatements().WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/"))),
        ["comments-terminators"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//", "\r\n\u2028\u2029").WithMultiLine("<!--", "-->"))),
        ["comments-only"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithSingleLine("#").WithMultiLine("(*", "*)"))),
        // An unterminated comment read as tokens, or running to the end of the text
        ["comments-not-a-comment"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("/*", maxSize: 1))).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.NotAComment).WithSingleLine("/"))),
        ["comments-to-end"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", UnterminatedComment.ToEnd))),
        // White space parsers that tell their first chars aren't invoked before the other ones, except at the end of the buffer
        ["hinted-comments"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(OneOf(Terms.Identifier(), Terms.AnyOf("./*(", maxSize: 1))).WithWhiteSpaceParser(Capture(OneOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Literals.Comments("//"), Literals.Comments("/*", "*/")))))),
        ["hinted-context"] = new Grammar<IReadOnlyList<long>>(ZeroOrMany(Terms.Integer()), static scanner => new ParseContext(scanner) { WhiteSpaceParser = Capture(ZeroOrMany(Literals.Char('#').Or(Literals.Char(' ')))) }),
        ["ascii-separated"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(','), Terms.Integer()).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)),
        ["ascii-context"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(','), Terms.Integer()), static scanner => new ParseContext(scanner) { WhiteSpaceSet = WhiteSpaceSet.Ascii }),
        ["ascii-choice"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text("\u00A0"))).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)),
        ["ascii-whitespace"] = new Grammar<TextSpan>(Literals.WhiteSpace(includeNewLines: true).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)),
        ["ascii-nonwhitespace"] = new Grammar<TextSpan>(Literals.NonWhiteSpace().WithWhiteSpaceSet(WhiteSpaceSet.Ascii)),
        ["ascii-comments"] = new Grammar<IReadOnlyList<TextSpan>>(ZeroOrMany(Terms.Identifier()).WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("//")).WithWhiteSpaceSet(WhiteSpaceSet.Ascii)),
        ["spacetabcrlf-separated"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(','), Terms.Integer()).WithWhiteSpaceSet(WhiteSpaceSet.SpaceTabCrLf)),
        ["spacetabcrlf-context"] = new Grammar<IReadOnlyList<long>>(Separated(Terms.Char(','), Terms.Integer()), static scanner => new ParseContext(scanner) { WhiteSpaceSet = WhiteSpaceSet.SpaceTabCrLf }),
        ["spacetabcrlf-choice"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(OneOf(Terms.Text("for"), Terms.Text("if"), Terms.Text("\f"))).WithWhiteSpaceSet(WhiteSpaceSet.SpaceTabCrLf)),
        ["spacetabcrlf-whitespace"] = new Grammar<TextSpan>(Literals.WhiteSpace(includeNewLines: true).WithWhiteSpaceSet(WhiteSpaceSet.SpaceTabCrLf)),
        ["spacetabcrlf-nonwhitespace"] = new Grammar<TextSpan>(Literals.NonWhiteSpace().WithWhiteSpaceSet(WhiteSpaceSet.SpaceTabCrLf)),
        // A scanner reading the char before its token, which a refill must keep
        ["scan-line-start"] = new Grammar<string>(ZeroOrMany(Terms.Scan(LineStartScanners.HtmlCloseComment, "-").Then(static _ => "comment").Or(Terms.Text("--")).Or(Terms.Text(">")).Or(Terms.Identifier().Then(static x => x.ToString()))).Then(static list => string.Join("|", list)).Eof()),
        ["scan-line-start-literals"] = new Grammar<string>(ZeroOrMany(Literals.Scan(LineStartScanners.HtmlCloseComment, "-").Then(static _ => "comment").Or(Literals.Text("--")).Or(Literals.Text(">")).Or(Literals.Text("\n")).Or(Literals.Identifier().Then(static x => x.ToString()))).Then(static list => string.Join("|", list)).Eof()),
        // The line and column after the match must not depend on how the text is split
        ["position-text"] = new Grammar<(int, int, int)>(Literals.Text("\r\n").Then(static (c, _) => Position(c))),
        ["position-char"] = new Grammar<(int, int, int)>(Literals.Char('\n').Then(static (c, _) => Position(c))),
        ["position-noneof"] = new Grammar<(int, int, int)>(Literals.NoneOf("abc").Then(static (c, _) => Position(c))),
        ["position-pattern"] = new Grammar<(int, int, int)>(Literals.Pattern(static c => c != ' ').Then(static (c, _) => Position(c))),
        ["position-scan"] = new Grammar<(int, int, int)>(Literals.Scan(static span => span.IndexOf(' ') is var i and >= 0 ? i : span.Length).Then(static (c, _) => Position(c))),
        ["position-textbefore"] = new Grammar<(int, int, int)>(AnyCharBefore(Literals.Text("--"), consumeDelimiter: true).Then(static (c, _) => Position(c))),
        // The line and column are computed when they are read: the error is reported where the assignment started, which a
        // compacting buffer has discarded when the assignment spans a refill
        ["position-elseerror"] = new Grammar<(int, int, int)>(Terms.Text("begin").SkipAnd(Terms.Identifier().And(Terms.Char('=')).And(Terms.Integer()).ElseError("Expected an assignment")).Then(static (c, _) => Position(c))),
        // A position read after one read further, by an alternative which failed
        // The identifier starts with any char above ASCII, which the choice dispatches without an entry for each of them
        ["non-ascii-choice"] = new Grammar<IReadOnlyList<string>>(ZeroOrMany(OneOf(
            Terms.Text("if"),
            Terms.Text("é!").Then(static _ => "e!"),
            Terms.Scan(ScanWord, ("abcdefghijklmnopqrstuvwxyz" + AboveAscii).AsSpan()).Then(static x => x.ToString()),
            Terms.Char('(').Then(static _ => "("))).Eof()),
        // Every alternative is read from the same position, the cursor moves to the end of the longest one
        ["longest-numbers"] = new Grammar<string>(ZeroOrMany(Longest(
            Terms.Integer().Then(static x => "i" + x.ToString(CultureInfo.InvariantCulture)),
            Terms.Decimal().Then(static x => "d" + x.ToString(CultureInfo.InvariantCulture)),
            Terms.Identifier().Then(static x => "w" + x))).Eof().Then(static x => string.Join(",", x))),
        ["longest-statements"] = new Grammar<string>(ZeroOrMany(Longest(
            Terms.Identifier().And(ZeroOrOne(Terms.Text(":=").SkipAnd(Terms.Integer()))).Then(static x => "e:" + x.Item1),
            Terms.Identifier().AndSkip(Terms.Char(':')).Then(static x => "l:" + x))).Eof().Then(static x => string.Join(",", x))),
        ["longest-texts"] = new Grammar<string>(ZeroOrMany(Longest(Terms.Text("<"), Terms.Text("<="), Terms.Text("<<"), Terms.Text("<<="), Terms.Text("<<<<"), Terms.Text("<>")))
            .Eof().Then(static x => string.Join(",", x))),
        // The shortest match, which the cursor moves back to after reading the longer ones
        ["longest-selector"] = new Grammar<string>(ZeroOrMany(Longest(static matches => 0, Terms.Text("ab"), Terms.Text("abab"), Terms.Text("ababab"), Terms.Text("b")))
            .Eof().Then(static x => string.Join(",", x))),
        ["position-backtrack"] = new Grammar<(int, int, int)>(OneOf(
            Terms.Identifier().And(Terms.Char('=').Then(static (c, _) => Position(c))).And(Terms.Char('(')).Then(static (c, x) => x.Item2),
            Terms.Identifier().Then(static (c, _) => Position(c)))),
    };

    private static Parser<IReadOnlyList<string>> CreateStateGrammar()
    {
        var item = Deferred<string>();
        var nested = OneOf(
            Terms.Char('{').And(Terms.Char('!')).Then(static _ => "!"),
            Between(Terms.Char('{'), ZeroOrMany(item), Terms.Char('}')).Then(static items => "{" + string.Join(",", items) + "}"))
            .WithState<DepthContext, int>(static c => c.Depth, static (c, depth) => c.Depth = depth, static depth => depth + 1);
        item.Parser = OneOf(
            nested,
            Terms.Identifier().Then(static (c, x) => x.ToString() + ((DepthContext)c).Depth.ToString(CultureInfo.InvariantCulture)));

        return ZeroOrMany(item).Eof();
    }

    private sealed class DepthContext : ParseContext
    {
        public DepthContext(Scanner scanner) : base(scanner)
        {
        }

        public int Depth { get; set; }
    }

    private static Parser<string> CreateNewLineStatements()
    {
        var end = OneOf(Terms.Char(';').Then<object?>(static _ => null), NewLineAhead(), SkipWhiteSpace(Eof()));
        var returnStatement = Terms.Keyword("return").SkipAnd(Terms.Identifier().NoNewLineBefore().Then(static x => "return " + x).Else("return"));
        var expressionStatement = Terms.Identifier().And(NoNewLineBefore(Terms.Text("++")).Else("")).Then(static x => x.Item1.ToString() + x.Item2);
        return ZeroOrMany(OneOf(returnStatement, expressionStatement).AndSkip(end)).Eof().Then(static x => string.Join("|", x));
    }

    // A word starts with a lowercase ASCII letter or any char above ASCII, and ends before a space or a parenthesis
    private static int ScanWord(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty || span[0] is not ((>= 'a' and <= 'z') or >= '\u0080'))
        {
            return 0;
        }

        var length = 1;

        while (length < span.Length && span[length] is not (' ' or '(' or ')'))
        {
            length++;
        }

        return length;
    }

    private static Parser<object> CreateNestedLists()
    {
        var item = Deferred<object>();
        var separated = Between(Terms.Char('['), Separated(Terms.Char(','), item, min: 0), Terms.Char(']')).Then<object>(static items => items);
        var zeroOrMany = Between(Terms.Char('('), ZeroOrMany(item), Terms.Char(')')).Then<object>(static items => items);
        var oneOrMany = Between(Terms.Char('{'), OneOrMany(item), Terms.Char('}')).Then<object>(static items => items);
        item.Parser = OneOf(Terms.Integer().Then<object>(static value => value), separated, zeroOrMany, oneOrMany);
        return item;
    }

    private static (int Offset, int Line, int Column) Position(ParseContext context)
    {
        var position = context.Scanner.Cursor.Position;
        return (position.Offset, position.Line, position.Column);
    }

    public static TheoryData<string, string> Cases()
    {
        var data = new TheoryData<string, string>();

        void Add(string grammar, params string[] inputs)
        {
            foreach (var input in inputs)
            {
                data.Add(grammar, input);
            }
        }

        string[] calc =
        [
            "", " ", "1", "1+2*3", " 1 + 2 ", "-(3.5*2)", "1.", "1e", "1e5", "12.5e-3", "(1+2", "1+", "1+2)x", "((1))/(2-3)*4", "1 +\n 2",
            "3 - 1 / 2 + 1", "1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2 + 1 - ( 3 + 2.5 ) * 4 - 1 / 2", "-(3 + 2) * -4 + --6",
        ];
        Add("calc", calc);
        Add("calc-eof", calc);

        string[] json =
        [
            "", "{", "{}", "[]", "\"a\"", "{\"property\":\"value\"}", " { \"a\" : [ \"b\" , { } ] } ",
            "{\"property\":[\"value\",\"value\",\"value\"]}", "{\"a\":\"b\\\"c\"}", "{\"a\":\"b", "[\"a\",]", "[\"a\" \"b\"]", "{} {}", "[\"a\"]x",
        ];
        Add("json", json);
        Add("json-eof", json);

        string[] log =
        [
            "",
            "2026-10-02T12:34:56.789Z INFO [http.server] GET /api/items/1 200 15.25ms \"ok\"",
            "2026-10-02T12:34:56.789Z ERROR [db] POST /api/users/3/orders?page=1 201 0.5ms \"said \\\"retry\\\"\"",
            "2026-10-02T12:34:56.789Z WARN [cache.redis] PUT /health 503 999.99ms \"timeout\"\n",
            "2026-10-02T12:34:56Z DEBUG [jobs.scheduler] DELETE /x 404 1ms \"a\"\n2026-10-02T12:34:57Z ERROR [auth] GET /y 200 2ms \"b\"\n",
            "2026-10-02 INFO [http] GET / 500 1ms \"a\" 2026-10-03 INFO [http] GET / 200 1ms \"b\"",
            "2026-10-02T12:34:56Z INFO [http.server] GET /api 200 15.25ms",
            "2026-10-02T12:34:56Z INFO [http.server] GET /api 200 15.25 \"a\"",
            "2026-10-02T12:34:56Z INFO [http.server] GET /api 200 15.25mx \"a\"",
            "2026-10-02T12:34:56Z INF [http.server] GET /api 200 1ms \"a\"",
            "2026-10-02T12:34:56Z INFO [http.server GET /api 200 1ms \"a\"",
            "2026-10-02T12:34:56Z INFO [http.server] GET /api 200 1ms \"unterminated",
            "2026-10-02T12:34:56Z INFO [http] GET / 200 1ms \"a\"\nnot a record",
        ];
        Add("log-record", log);
        Add("log", log);

        string[] operators =
        [
            "", "a", "-a", "- -a", "a!", "-a!", "a + b * c", "a ** b ** c", "a << b < c <= d", "a <= b", "a < = b", "a & b && c",
            "a &", "a & &", "a ** -", "(a + b) * -(c - d)!", "(a + b", "a + (b", "a == b = c", "a = = b", "not a < b", "a + not b",
            "a ++ b", "a << << b", " a  <<  b  <=  c  ", "a\n<=\nb",
        ];
        Add("operators", operators);
        Add("operators-eof", operators);
        Add("operator-levels", operators);

        string[] valuedOperators =
        [
            "", "a", "a.b", "a.b.c", "a[b]", "a[b][c].d", "a.", "a[", "a[b", "-a.b", "a ? b : c", "a ? b : c ? d : e", "a ?? b",
            "a ? b ?? c : d", "a ? b : c ?? d", "a ? b", "a ? b :", "a ?? ", "a = b = c", "a = b ? c : d", "a = b = ", "a * b / c",
            "a[b ? c : d].e", " a . b [ c ] ? d : e ", "a\n?\nb\n:\nc",
        ];
        Add("valued-operators", valuedOperators);
        Add("valued-operators-eof", valuedOperators);

        Add("valued-prefix",
            "", "a", "-a", "- -a", "!-a", "- !a", "typeof -a * b", "-typeof a", "a * - typeof !b", "-", "- -", "typeof", "a * -",
            "-(- a", "- ( - a ) * - b", " - \n typeof  a ", "typeofa", "typeof typeof a");

        string[] conditionalLast =
        [
            "", "a", "a ? b : c", "a ? b : c = d", "a ? b : c ? d : e", "a || b ? c : d", "a = b ? c : d = e", "a ? b", "a ? b :",
            "a ? b : c =", "a ?", "a ? b : c, d", "a, b ? c : d, e", " a ? ( b , c ) : d ", "a\n?\nb\n:\nc\n=\nd", "a ? b : c || d",
        ];
        Add("conditional-last", conditionalLast);
        Add("conditional-last-eof", conditionalLast);

        Add("sql",
            "SELECT * FROM users",
            "SELECT id, name FROM users WHERE id = 1",
            "SELECT * FROM users WHERE name = 'John'",
            "SELECT * -- comment \n FROM users",
            "SELECT id, name /* multiline\n comment\n */ FROM users",
            "/* some documentation */ SELECT id, name FROM users WHERE id = 1",
            "SELECT * FROM users /* unterminated",
            "SELECT * FROM users WHERE id BETWEEN 1 AND 100 ORDER BY id ASC LIMIT 10",
            "SELECT u.id, o.amount FROM users AS u JOIN orders AS o ON u.id = o.user_id",
            "SELECT category, COUNT(*) FROM products GROUP BY category HAVING COUNT(*) > 10",
            "WITH cte(id, name) AS (SELECT id, name FROM users) SELECT * FROM cte",
            "SELECT * FROM users UNION ALL SELECT * FROM customers",
            "SELECT * FROM users WHERE id >= 1; SELECT 1",
            "SELECT FROM",
            "SELEC * FROM users");

        Add("identifier", "", "a", "abc", " abc def", "1a", "a1_b ");
        string[] codePointIdentifiers = ["", "a", "\\u0061bc d\\u{62}", "\\u{1D4D0}x", "\U0001D49C a\U0001D49Cb", "\\u00", "a\\u00", "a\\u{1D4", "a\\", "a\ud835",
            "a\ud835b", "\udc9c", "a\\u0020", "\u00e9t\u00e9 caf\u00e9", "a\\u{110000}", "a( b\\u c\\u0062(", "a\\u{62}\\u"];
        Add("codepoint-identifiers", codePointIdentifiers);
        Add("codepoint-identifier-calls", codePointIdentifiers);
        Add("integer", "", "1", "123", " 42x", "-5", "+", "123456789012");
        Add("standalone-eof", "", " ", "a");
        Add("integer-standalone-eof", "", "42", "42a", "42 ", " 42");
        Add("integer-eof-error", "", "42", "42a", "42 ", " 42");
        Add("decimal", "", "1", "1.5", "1.", "1.e", "12.5e-3", "1e", "1e+", ".5", "-0.25 ", "1.e5", "1.e+", "1.E-2x");
        Add("double", "", "1", "1.5e10", "1e", "2.", "-3.25e-2x", "2.e3", "2.e");
        Add("underscores", "", "1_000", "1_", "1__", "1__2", "1_x", "1,000_000.0_5e1_0", "1._5", "1e_5", "1e1_", "-1_2 ", "_1");
        Add("underscores-list", "", "1_000;2__0;3", "1_;2", "1;_2", "1_2_3;4_5_6", "9_223_372_036_854_775_807;1");
        Add("text", "", "h", "hel", "hello", "hello!", " hello", "help");
        Add("text-ci", "", "HEL", "HeLLo", "HELP", "hellO world");
        Add("keyword", "", "f", "for", "for ", "fore", "for(", "fo");
        Add("choice", "", "f", "for", "fore", "foreach", "forever", "foreve", "if", "i", "x");
        Add("keywords", "", "for", "fore", "foreach", "foreachx", "if", "ifx", "x");
        Add("prefix-keywords", "", "i", "in", "int", "inte", "inter", "interf", "interface", "interfacex", "intern", "internal", "is", "iz", "intx", " interface ");
        Add("prefix-texts", "", "<", "<<", "<<=", "<=", "<>", "<x", "<<x", " <<= ");
        Add("keyword-part", "", "i", "in", " in ", "in(", "inx", "in1", "in_", "in-", "in\u00e9", "in\u00e8", "inX", "i n");
        Add("keywords-part", "", "i", "in", "in1", "inc", "inc-", "incx", "in.", "x");
        Add("anyof", "", "a", "ab", "abc", "abca", "abx", "x");
        Add("anyof-max", "", "a", "abc", "abcab", "abxa");
        Add("noneof", "", "a", "ab", "ab,c", "a,", ";");
        Add("pattern", "", "1", "12", "1234", "123456", "1a");
        Add("pattern-empty", "", "1", "12a", "a", "123456");
        string[] liquidIdentifiers = ["", "a", "abc", "1", "123", "123a", "12 a", "a-", "a-}", "a-%", "a--}", "1-}", "1-a", "-a", "a_b-c d", "abc-", "  x", "a\n", "a}"];
        Add("scan-identifier", liquidIdentifiers);
        Add("scan-signature", liquidIdentifiers);
        Add("scan-tag", "", "{%", "{% a %}", "{%a-%}", "{% a b- -%}", "{% a 12 %}", "{% a 12-%}", "{% 1 %}", "{% a-b c--%}", "{% a");
        Add("string", "", "'", "'a", "'abc'", "\"a\\\"b\"", "'a\\", "'a\\'", "'\\x'", "x");
        Add("js-string", "", "'\\$'", "'a\\\nb'", "'a\\\r\nb'", "'a\\\rb'", "'a\\\r", "'a\\\r\n", "'a\\\u2028b'", "'\\u{1F600}'", "'\\u{1F6", "'\\u{12g}'",
            "'\\u{110000}'", "'\\101'", "'\\1'", "'\\12", "'\\1234'", "'\\08'", "'\\x41B'", "'\\x4'", "'\\x4", "'\\a\\8'", "'\\'a\\", "'\\'a\\$'", "\"\\\"\\u{41}\"", "'\\u0041'");
        Add("sql-string", "", "'", "''", "'''", "''''", "'it''s'", "'it''", "'a\\'", "'a'b", "'a'''b'");
        Add("python-string", "", "'\\d'", "'\\U0001F600'", "'\\U0001F6'", "'\\U0011FFFF'", "'\\x4'", "'\\101'", "'a\\\r\nb'", "'a\nb'");
        Add("json-string", "", "\"\\/\"", "\"a\tb\"", "\"\\u004", "\"\\u0041\"", "\"\\'\"", "\"abc");
        Add("js-pooled-strings", "", "'a\\\r\nb' 'a\\\nb' 'ab'", "'\\101' 'A' '\\x41'", "'\\u{1F600}' '\\u{1F600}'", "'\\$' '\\", "'unterminated");
        Add("nul-string", "", "\0", "\0abc\0", "\0abc", "\0a\\\0b\0", "x");
        Add("backslash-string", "", "\\", "\\n\\", "\\n\\rest", "\\n", "\\p\\", "\\u12\\", "\\abc\\", "\\\\", "\\abc", "\\a\\\\b\\");
        Add("whitespace", "", " ", "  \n\t x", "x");
        Add("nonwhitespace", "", "a", "abc def", " x");
        Add("textbefore", "", "a", "abc", "abc-", "abc--", "a-b--c", "--");
        Add("textbefore-eof", "", "a", "abc", "abc-", "abc--", "--");
        Add("capture", "", "a", "a=", "a = 12", "a = 12 x", "a=x");
        Add("not", "", "a", "ab", "abc", "ac", "xab");
        Add("peek", "", "ab", "abc", "ab abc a", "  ab  x ab", "a", "1");
        Add("peek-value", "", "12", "  12", "12 3", "x");
        Add("followed", "", "f", "f(", "f (", "f x", "f");
        Add("notfollowed", "", "1", "12", "12.", "12.5", "12 .", "12x");
        Add("thentry", "", "a", "for", "  for  forx", "a for b", " forever for\n for1 1", "fo");
        Add("separated", "", "1", "1,", "1,2", "1 , 2 ,3", "1,,2", "1,x");
        string[] capturedNumbers = ["", "9", " 9", "  9", " 12", "  12", "   12", "123", "  123", "\n\t 9 x", " x", "  ", " 9 "];
        Add("when-capture", capturedNumbers);
        Add("when-capture-else", capturedNumbers);
        Add("thentry-capture", capturedNumbers);
        Add("thentry-capture-offsets", capturedNumbers);
        string[] peekedTexts = ["", "abc", " abc", "  abc", "ab", " ab", " x", "  ", "a", " abcd"];
        Add("when-peek", peekedTexts);
        Add("thentry-peek", peekedTexts);
        string[] peekedWords = ["", "ab", " ab", "  ab x", "abc", " abc", "  abcd ", " 1", "  "];
        Add("when-peek-rejected", peekedWords);
        Add("thentry-peek-rejected", peekedWords);
        Add("when-sequence", "", "a=1", " a=1", "  a = 1", " a = 12", "  ab = 12", "a=", " a =", " a x", "  a = x");
        Add("when-token", "", "9", " 9", "  9 12  7", " 9 123 7", "  123", " x", "   ");
        Add("when-comments", "", "9", " 9", "/**/9", " /* a */ 9", "/* a */\n 12 x", " /* a", "  /* a */ x", " /* long */ 9");
        Add("guarded-optional", "", "a", "a;", "a ;", "a ? b : c;", "a\n ?\n b :\n c ;", "a ? b ;", "a ? b : ;", "a ?? b;", "a  \n ", "a ?");
        Add("guarded-loop", "", "}", " }", "let a; }", "let a;let b;}", "let a; le }", "let a; let }", "let a;\n\n }", "let a; x }", "let");
        Add("guarded-separated", "", "a;", "a , b ;", "a,|b,c;", "a, ;", "a,| ;", "a ,", "a  ;", "a b");
        Add("separated-empty-first", "", ";", ".;", ".a.b;", " . . a . b ;", "a..b;", "a.;", "a.b", ".", "..", "a b;", "+");
        Add("list-eof", "", "a", "a b c", "a b 1", "a b ");
        Add("pooled-strings", "", "a", "abc abc", "'abc' abc \"a\\nb\" 'a\\nb'", "abcdefghijklmnopqr abcdefghijklmnopqr x", "'unterminated");
        Add("text-ci-matched", "", "HELLO hello Hello HELLO", "FOR for hello", "HeLLo FoR fore");
        Add("nested-lists", "", "1", "[", "[]", "()", "{}", "[1]", "(1)", "{1}", "[1,2,3,4,5,6]", "(1 2 3 4 5 6)", "[1,[2,[3,4],5],(6 7 (8 9 10 11 12) 13),{14 15}]",
            "(1 2 [3,4 (5 6)] 7", "[1,2,", "[1,(2 3", "{1 2", "((((((((1 2 3))))))))", "[" + string.Join(",", Enumerable.Range(0, 40)) + ",(" + string.Join(" ", Enumerable.Range(0, 30)) + ")]",
            "(" + string.Join(" ", Enumerable.Range(0, 20).Select(static i => "[" + i + "," + (i + 1) + ",{" + i + "}]")) + ")");
        Add("state", "", "a", "a {b} c", "{!} a", "{ a { b { c } } d }", "{ a {! } b", "{a", "{ {! a", "a { b } } c", "{" + string.Join(" ", Enumerable.Range(0, 40).Select(static i => "x" + i)) + " {!} {y}}");
        Add("elseerror", "", "(", "(1", "(1)", "(1 x", "( 12 )", "x");
        Add("comments", "", "a", "a // b\n c", "a /* b */ c", "a /* b", "a / b", "// only");

        string[] newLines =
        [
            "", "a", "a b", "a;b", "a\nb", "a\r\nb", "a  \n  b", "return", "return a", "return\na", "return\r\n a;", "a++", "a ++ b",
            "a\n++b", "a\n++", "return a\n++b", "a /* x */ b", "a /* \n */ b", "a // c\n b", "return /* x */ a", "return /* \n */ a",
            "return // c\n a", "a++ /* c */\n b", "a /* b", "a ;  ", "a  ", "a\n",
        ];
        Add("newlines", newLines);
        Add("newlines-comments", newLines);
        Add("comments", "a /* b */", "a /**/ b", "a /* b * / c */ d", "a //", "a /", "a /*", "a/*b*//*c*/d", "a // b\r\n c", "a // b\r c", "a\r\n//\r\n\r\nb");
        Add("comments-terminators", "", "a // b\r c", "a // b\u2028c", "a // b\u2029 c", "a // b\n c", "a <!-- b --> c", "a <!-- b -- > c", "a <!- b", "a <", "a //");
        Add("comments-only", "", "a#b\nc", "a(*b*)c", "a (*b*) c", "a#b", "a(*b", "a(b", "#", "(*", "(**)");
        string[] unterminated = ["", "a", "a /* b */ c", "a /* b", "a /*", "a/*b", "/*", "a /**/ b /* c", "a /* b\n */ c /* d\n e", "a * / b", "a /* b */ /* c"];
        Add("comments-not-a-comment", unterminated);
        Add("comments-to-end", unterminated);
        Add("hinted-comments", "", "a", "a.b", "a // b\n.c", "a/* b */(c", "a /* b", "a / b", "a/b", "a*/", "// only", "a.\n/", "a/\n/c", "a //");
        Add("hinted-context", "", "1", "1#2 ##3", "12#", "#", "1 2x", "x", "##12 34#");
        string[] separated = ["", "1", "1 , 2", "1\u00A0, 2", "1 ,\u3000 2", "1\t,\n2\r\n,\v\f3", " \u00A01"];
        Add("ascii-separated", separated);
        Add("ascii-context", separated);
        Add("ascii-choice", "", "for if", "for\u00A0if", "\u00A0 if", "  \u3000", "if\tfor\nif");
        Add("ascii-whitespace", "", " \t\n\r\v\f x", "\u00A0", " \u2000", "x");
        Add("ascii-nonwhitespace", "", "a\u00A0b c", "a\u3000b\tc", "ab\fc", " x");
        Add("ascii-comments", "", "a // b\n c", "a\u00A0b", "a\u00A0// b", "a \u00A0b");
        Add("spacetabcrlf-separated", separated);
        Add("spacetabcrlf-context", separated);
        Add("spacetabcrlf-choice", "", "for if", "for\fif", "\f\r\n if", "  \v", "if\tfor\r\nif");
        Add("spacetabcrlf-whitespace", "", " \t\n\r x", " \t\v", "\f", "x");
        Add("spacetabcrlf-nonwhitespace", "", "a\fb c", "a\vb\rc", "ab\u00A0c", " x");
        Add("position-text", "", "\r", "\r\n", "\r\nx", "\n");
        Add("position-char", "", "\n", "\nx", "\r\n");
        Add("position-noneof", "", "\n", "\r\n", "\r", "x\ny\nz", "x\ny\nz\n", "x\r\ny\rz a", "a");
        Add("position-pattern", "", "ab\rc", "ab\r", "ab\r\n", "ab\n", "a\n\nb c", new string('a', 70) + "\r" + new string('b', 10) + "\n", new string('a', 70) + "\r\n" + new string('b', 70) + " x");
        Add("position-scan", "", "ab\rc", "ab\n", "a\n\nb c", "\n\n\nab c", new string('a', 70) + "\r\n" + new string('b', 70) + " x",
            new string('a', 30) + "\n" + new string('b', 40) + "\n\n" + new string('c', 50) + "\n" + new string('d', 60) + " x");
        string[] lineStart = ["", "-->", "--> c", "x-->y", "x --> y", "x\n-->y", "x\n--> y\nz", "x\r\n--> y", "x\n\n-->", "a--\n-->", "a--\n>", "--", "x\n--", "abc\n-->\nghi-->jkl"];
        Add("scan-line-start", lineStart);
        Add("scan-line-start-literals", lineStart);
        Add("position-textbefore", "", "a\r\nb--c", "a\rb--", "a\n--\n");
        Add("position-elseerror", "", "begin", "begin x = 1", "begin x = y", "begin\n", "begin\n x\n =\n 12", "begin\n\n x\n =\n y", "\n\nbegin\r\n  x =\n\n y",
            "begin\r\n" + new string(' ', 70) + "x\n" + new string(' ', 70) + "= z", "\n begin abc\n\n=\n\n\n", "begin 1");
        Add("non-ascii-choice", "", "if", "if (é) ", "é!", "é!x", "( 中文 ( ℵ )", "€ if", "if #", "ab\u0080 é", "(\uffff");
        Add("longest-numbers", "", "1", "12", "12.5", "1.", " 1 2.5 abc 3", "1.5.5", "12.5e", "a1", "1a");
        Add("longest-statements", "", "a", "a:", "a := 0", "end: total := 12 x", "a :=", "a : = 1", "a:b:c:");
        Add("longest-texts", "", "<", "<<", "<<<", "<<<<", "<<=<=<>", " < <= <<= ", "<<<<<", "<x");
        Add("longest-selector", "", "ab", "abab", "ababab", "abababab", " ab b ab", "aba", "abx");
        Add("position-backtrack", "", "abc", "abc\n =\n (", "abc\n =\n x", "a\r\n=\r\n(", "\n\n abc \n = x", "abc =", "abc\n\n", "1");
        Add("elseerror", "(\n1\n x", "( 12\n )", "\n(\n\n1 \n x", "(1\r\n\r\n");
        Add("integer-eof-error", " 42\n\n x", "\n\n42\n");

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ConclusivePrefixesMatchTheWholeText(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);

        for (var length = 0; length <= input.Length; length++)
        {
            var actual = g.Parse(input.Substring(0, length), isFinal: false);

            if (actual != null)
            {
                Assert.True(expected == actual, $"Prefix '{input.Substring(0, length)}' of '{input}': expected {expected}, actual {actual}");
            }
        }

        // The whole text is conclusive once it is final
        Assert.NotNull(expected);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StreamsMatchTheWholeText(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);
        var random = new Random(input.Length * 31 + grammar.Length);

        (Func<int> Chunk, int BufferSize, bool Yield)[] configurations =
        [
            (() => 1, 1, false),
            (() => 1, 4096, false),
            (() => 3, 2, true),
            (() => 7, 5, false),
            (() => random.Next(1, 6), 1, false),
            (() => int.MaxValue, 1, false),
        ];

        foreach (var (chunk, bufferSize, yield) in configurations)
        {
            var options = new StreamParseOptions { BufferSize = bufferSize, ContextFactory = g.ContextFactory };
            var actual = await g.ParseAsync(() => new ChunkedReader(input, chunk, yield), options);

            Assert.True(expected == actual, $"'{input}' with buffer size {bufferSize}: expected {expected}, actual {actual}");
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task StreamsMatchTheWholeTextOnStringReaders(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);
        var actual = await g.ParseAsync(() => new StringReader(input), new StreamParseOptions { ContextFactory = g.ContextFactory });

        Assert.Equal(expected, actual);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void CompactingStreamsMatchTheWholeText(string grammar, string input)
    {
        var g = Grammars[grammar];
        var expected = g.Parse(input, isFinal: true);
        var random = new Random(input.Length * 17 + grammar.Length);

        (Func<int> Chunk, int BufferSize)[] configurations =
        [
            (() => 1, 1),
            (() => 1, 4096),
            (() => 3, 2),
            (() => 7, 5),
            (() => random.Next(1, 6), 1),
            (() => random.Next(1, 6), 3),
            (() => int.MaxValue, 1),
        ];

        foreach (var (chunk, bufferSize) in configurations)
        {
            var options = new StreamParseOptions { BufferSize = bufferSize, ContextFactory = g.ContextFactory };
            var actual = g.ParseCompacting(() => new ChunkedReader(input, chunk, yield: false), options);

            Assert.True(expected == actual, $"'{input}' with buffer size {bufferSize}: expected {expected}, actual {actual}");
        }

        // Splits the first token at every position
        for (var split = 1; split <= input.Length; split++)
        {
            var options = new StreamParseOptions { BufferSize = split, ContextFactory = g.ContextFactory };
            var actual = g.ParseCompacting(() => new ChunkedReader(input, () => int.MaxValue, yield: false), options);

            Assert.True(expected == actual, $"'{input}' split at {split}: expected {expected}, actual {actual}");
        }
    }

    [Fact]
    public async Task ParseAsyncReadsOnlyWhatIsNeeded()
    {
        var reader = new ChunkedReader("[\"a\"]" + new string(' ', 100_000) + "x", () => 1, yield: false);
        var result = await JsonParser.Json.ParseAsync(reader, new StreamParseOptions { BufferSize = 1 });

        Assert.Equal("[\"a\"]", result!.ToString());
        Assert.True(reader.Position < 64, $"Read {reader.Position} characters");
    }

    [Fact]
    public async Task TryParseAsyncReturnsFalseOnParseException()
    {
        var parser = Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'"));

        var (success, _) = await parser.TryParseAsync(new ChunkedReader("(1 x", () => 1, yield: false));
        Assert.False(success);

        var exception = await Assert.ThrowsAsync<ParseException>(async () => await parser.ParseAsync(new ChunkedReader("(1 x", () => 1, yield: false)));
        Assert.Equal("Expected ')'", exception.Message);
        Assert.Equal(2, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseAsyncLimitsBufferedCharacters()
    {
        var options = new StreamParseOptions { BufferSize = 4, MaxBufferedCharacters = 16 };

        Assert.Equal("[\"abcdefghij\"]", (await JsonParser.Json.ParseAsync(new ChunkedReader("[\"abcdefghij\"]", () => 1, false), options))!.ToString());

        var exception = await Assert.ThrowsAsync<ParseException>(async () => await JsonParser.Json.ParseAsync(new ChunkedReader("[\"abcdefghijklmnopqrstuvwxyz\"]", () => 1, false), options));
        // The start of the token which doesn't fit
        Assert.Equal(1, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseAsyncSupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var reader = new ChunkedReader("[\"a\",\"b\",\"c\"]", () => 1, yield: true) { OnRead = position => { if (position == 3) cts.Cancel(); } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await JsonParser.Json.ParseAsync(reader, new StreamParseOptions { BufferSize = 1 }, cts.Token));
    }

    [Fact]
    public async Task CustomParsersCanMarkHitEnd()
    {
        // Succeeds on "ab" but needs to see what follows the "a" to decide
        var parser = new LookaheadParser().Then(static x => x);

        Assert.True(await TryParseAsync("ab"));
        Assert.True(await TryParseAsync("a"));
        Assert.False(await TryParseAsync("ac"));

        async Task<bool> TryParseAsync(string text)
        {
            var expected = parser.TryParse(text, out _);
            var (actual, _) = await parser.TryParseAsync(new ChunkedReader(text, () => 1, yield: false), new StreamParseOptions { BufferSize = 1 });
            Assert.Equal(expected, actual);
            return actual;
        }
    }

    public static TheoryData<int, int> ChunkSizes() => new() { { 1, 1 }, { 1, 4096 }, { 3, 2 }, { 7, 16 }, { int.MaxValue, 1 } };

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseManyAsyncParsesSuccessiveValues(int chunk, int bufferSize)
    {
        var lines = Enumerable.Range(0, 50).Select(static i => $"{{\"id\":\"{i}\",\"tags\":[\"{new string('x', i % 7)}\"]}}").ToArray();
        var text = string.Join("\n", lines) + "\n";

        var items = await ToListAsync(JsonParser.Json.ParseManyAsync(new ChunkedReader(text, () => chunk, yield: chunk == 3), new StreamParseOptions { BufferSize = bufferSize }));

        Assert.Equal(lines, items.Select(static json => json.ToString()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  \n ")]
    [InlineData("1 2 3")]
    [InlineData(" 1\n22\n 333 \n")]
    public async Task ParseManyAsyncParsesValuesSeparatedByWhiteSpace(string text)
    {
        var values = await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), new StreamParseOptions { BufferSize = 1 }));

        Assert.Equal(text.Split([' ', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(long.Parse), values);
    }

    [Theory]
    [InlineData("1,2,3", 3)]
    [InlineData("1 , 2 ,3 ,", 3)]
    [InlineData("", 0)]
    [InlineData("1", 1)]
    public async Task ParseManyAsyncParsesSeparatedValues(string text, int expected)
    {
        foreach (var bufferSize in new[] { 1, 3, 4096 })
        {
            var values = await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), Terms.Char(','), new StreamParseOptions { BufferSize = bufferSize }));
            Assert.Equal(Enumerable.Range(1, expected).Select(static i => (long)i), values);
        }
    }

    [Theory]
    [InlineData("1 2", 2)]
    [InlineData("1,2 3", 4)]
    [InlineData("1,,2", 2)]
    public async Task ParseManyAsyncRequiresSeparators(string text, int offset)
    {
        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), Terms.Char(','), new StreamParseOptions { BufferSize = 1 })));
        Assert.Equal(offset, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseManyAsyncReportsAbsolutePositions()
    {
        var text = "{\"a\":\"b\"}\n{\"c\":\"d\"}\n {\"e\" \"f\"}\n";
        var items = new List<IJson>();

        foreach (var bufferSize in new[] { 1, 4096 })
        {
            items.Clear();
            var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(JsonParser.Json.ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), new StreamParseOptions { BufferSize = bufferSize }), items));

            Assert.Equal(2, items.Count);
            Assert.Equal(21, exception.Position.Offset);
            Assert.Equal(3, exception.Position.Line);
            Assert.Equal(2, exception.Position.Column);
        }
    }

    [Fact]
    public async Task ParseManyAsyncPropagatesParseExceptionsWithAbsolutePositions()
    {
        var parser = Terms.Char('(').SkipAnd(Terms.Integer()).AndSkip(Terms.Char(')').ElseError("Expected ')'"));

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(parser.ParseManyAsync(new ChunkedReader("(1)\n(2)\n(3 x", () => 1, yield: false), new StreamParseOptions { BufferSize = 1 })));

        Assert.Equal("Expected ')'", exception.Message);
        Assert.Equal(10, exception.Position.Offset);
        Assert.Equal(3, exception.Position.Line);
        Assert.Equal(3, exception.Position.Column);
    }

    [Fact]
    public async Task ParseManyAsyncLeavesParseExceptionsWithoutPositionAsTheyAre()
    {
        // A callback's exception has no position in the window, which is not one to make absolute
        var parser = Terms.Integer().Then<long>(static value => value == 3 ? throw new ParseException("three") : value);

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(parser.ParseManyAsync(new ChunkedReader("1\n2\n3\n4", () => 1, yield: false), new StreamParseOptions { BufferSize = 1 })));

        Assert.Equal("three", exception.Message);
        Assert.Equal(0, exception.Position.Offset);
        Assert.Equal(0, exception.Position.Line);
        Assert.Equal(0, exception.Position.Column);
    }

    [Theory]
    [InlineData("a b\nc\n\n  d\r\n e", false)]
    [InlineData("a\n(b)\n  c (\n d )", false)]
    [InlineData("a\n bb\n\n ccc\r\n (dd", true)]
    [InlineData("\n\n\n a", false)]
    public async Task ParseManyAsyncReportsTheLinesAndColumnsOfTheWholeText(string text, bool fails)
    {
        // Each item reports its position, and a failed one the position of its error. The offsets are relative to the window.
        var item = Terms.Identifier().Then(static (c, _) => (c.Scanner.Cursor.Position.Line, c.Scanner.Cursor.Position.Column))
            .Or(Terms.Char('(').SkipAnd(Terms.Identifier()).AndSkip(Terms.Char(')').ElseError("Expected ')'")).Then(static (c, _) => (-c.Scanner.Cursor.Position.Line, -c.Scanner.Cursor.Position.Column)));

        Assert.Equal(!fails, ZeroOrMany(item).Eof().TryParse(text, out var expected, out var error));

        var expectedError = error == null ? null : $"{error.Message} {error.Position}";

        foreach (var (chunk, bufferSize) in new[] { (1, 1), (1, 4096), (3, 2), (7, 5), (int.MaxValue, 1) })
        {
            var items = new List<(int, int)>();

            try
            {
                await ToListAsync(item.ParseManyAsync(new ChunkedReader(text, () => chunk, yield: false), new StreamParseOptions { BufferSize = bufferSize }), items);
                Assert.Null(expectedError);
                Assert.Equal(expected, items);
            }
            catch (ParseException e)
            {
                Assert.Equal(expectedError, $"{e.Message} {e.Position}");
            }
        }
    }

    [Fact]
    public async Task ParseManyAsyncRejectsEmptyValues()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => ToListAsync(Terms.Integer().Optional().ParseManyAsync(new ChunkedReader("1 x", () => 1, yield: false))));
    }

    [Fact]
    public async Task ParseManyAsyncKeepsMemoryBounded()
    {
        const int Count = 10_000;
        var reader = new GeneratedReader(Count);
        var options = new StreamParseOptions { BufferSize = 64, MaxBufferedCharacters = 256 };
        var count = 0;

        await foreach (var _ in JsonParser.Json.ParseManyAsync(reader, options))
        {
            count++;
        }

        Assert.Equal(Count, count);
    }

    [Fact]
    public async Task ParseManyAsyncLimitsValueSize()
    {
        var text = "[\"a\"]\n[\"" + new string('b', 100) + "\"]";
        var items = new List<IJson>();

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(JsonParser.Json.ParseManyAsync(new ChunkedReader(text, () => 5, false), new StreamParseOptions { BufferSize = 8, MaxBufferedCharacters = 32 }), items));

        Assert.Single(items);
        Assert.Equal(6, exception.Position.Offset);
    }

    [Fact]
    public async Task ParseManyAsyncSupportsCancellation()
    {
        using var cts = new CancellationTokenSource();
        var count = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in Terms.Integer().ParseManyAsync(new ChunkedReader("1 2 3 4 5 6", () => 1, yield: true), new StreamParseOptions { BufferSize = 1 }, cts.Token))
            {
                if (++count == 2)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ParseManyAsyncUsesTheContextFactory()
    {
        var options = new StreamParseOptions
        {
            BufferSize = 1,
            ContextFactory = static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = Capture(ZeroOrMany(Literals.Char('#').Or(Literals.Char(' ')))) },
        };

        var values = await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1#2 ##3", () => 1, false), options));

        Assert.Equal([1L, 2, 3], values);
    }

    [Theory]
    [InlineData("a /* b */ c", false)]
    [InlineData("/* a */ b /* c\n d */ e /**/", false)]
    [InlineData("a /* a comment which is longer than the first windows, whatever their size * / */ b\n/* c */", false)]
    [InlineData("a /* b", false)]
    [InlineData("a\n b /*", false)]
    [InlineData("a /* b */\n /* a comment which is longer than the first windows *", false)]
    [InlineData("/*/ a", false)]
    [InlineData("a /* b */ , /* c */ d /* e */", true)]
    [InlineData("a /* b */ , /* c */ d , /* e */", true)]
    [InlineData("a /* b */ , /* c", true)]
    [InlineData("a /* b", true)]
    public async Task ParseManyAsyncSkipsCommentsSpanningWindows(string text, bool separated)
    {
        // A comment which a window doesn't end is only unterminated when no more text follows. The white space parser
        // throws either way, before the items and the separators
        var comments = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/").Build();
        var item = Terms.Identifier();
        var whole = separated ? Separated(Terms.Char(','), item).AndSkip(Terms.Char(',').Optional()) : ZeroOrMany(item);

        whole.TryParse(new ParseContext(new Scanner(text)) { WhiteSpaceParser = comments }, out var expected, out var error);

        var expectedError = error == null ? null : $"{error.Message} {error.Position.Offset} {error.Position}";

        foreach (var (chunk, bufferSize) in new[] { (1, 1), (1, 4096), (3, 2), (7, 5), (int.MaxValue, 1) })
        {
            var options = new StreamParseOptions
            {
                BufferSize = bufferSize,
                ContextFactory = (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments },
            };

            var reader = new ChunkedReader(text, () => chunk, yield: false);
            var items = new List<TextSpan>();
            string? actualError = null;

            try
            {
                await ToListAsync(separated ? item.ParseManyAsync(reader, Terms.Char(','), options) : item.ParseManyAsync(reader, options), items);
            }
            catch (ParseException e)
            {
                actualError = $"{e.Message} {e.Position.Offset} {e.Position}";
            }

            Assert.Equal(expectedError, actualError);

            if (expectedError == null)
            {
                Assert.Equal(expected!.Select(static s => s.ToString()), items.Select(static s => s.ToString()));
            }
        }
    }

    [Fact]
    public async Task ParseManyAsyncLimitsCommentSize()
    {
        // The white space before a value is buffered with it while it might continue
        var comments = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/").Build();
        var options = new StreamParseOptions
        {
            BufferSize = 8,
            MaxBufferedCharacters = 32,
            ContextFactory = (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments },
        };

        var items = new List<TextSpan>();

        Assert.Equal(["a", "c"], (await ToListAsync(Terms.Identifier().ParseManyAsync(new ChunkedReader("a /*" + new string('b', 20) + "*/ c", () => 5, false), options))).Select(static s => s.ToString()));

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Identifier().ParseManyAsync(new ChunkedReader("a /*" + new string('b', 100) + "*/ c", () => 5, false), options), items));

        Assert.Single(items);
        Assert.Equal("The input requires more than 32 buffered characters to be parsed.", exception.Message);
        Assert.Equal(1, exception.Position.Offset);
    }

    [Theory]
    [InlineData("a\n/* b")]
    [InlineData("a\n\n b /* c\n")]
    [InlineData("a /* b */\n  /*\n")]
    public async Task ParseManyAsyncReportsAbsolutePositionsOfCommentsInDelimitedValues(string text)
    {
        // Each line is parsed on its own, and its white space is skipped before and after its value
        var comments = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/").Build();
        var item = Terms.Identifier();

        Assert.False(ZeroOrMany(item).TryParse(new ParseContext(new Scanner(text)) { WhiteSpaceParser = comments }, out _, out var error));

        foreach (var chunk in new[] { 1, 2, 4096 })
        {
            var options = new StreamParseOptions
            {
                BufferSize = 1,
                ContextFactory = (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments },
            };

            var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(item.ParseManyAsync(new ChunkedReader(text, () => chunk, yield: false), '\n', options)));

            Assert.Equal(error!.Message, exception.Message);
            Assert.Equal(error.Position, exception.Position);
        }
    }

    // The white space parsers of ParseManyAsyncWindowsDontChangeTheResult: every way a comment can depend on the text after a window
    private static readonly Dictionary<string, Parser<TextSpan>> WindowedWhiteSpace = new()
    {
        ["throw"] = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/").Build(),
        ["not-a-comment"] = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/", UnterminatedComment.NotAComment).Build(),
        ["to-end"] = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/", UnterminatedComment.ToEnd).Build(),
        ["terminators"] = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//", "\r\n\u2028").WithMultiLine("<!--", "-->").Build(),
        ["comments-only"] = new CommentsBuilder().WithSingleLine("//").WithMultiLine("/*", "*/").Build(),
        ["literals"] = Capture(OneOrMany(OneOf(Literals.WhiteSpace(includeNewLines: true), Literals.Comments("//"), Literals.Comments("/*", "*/")))),
    };

    public static TheoryData<string, string> WindowedWhiteSpaceCases()
    {
        var data = new TheoryData<string, string>();

        string[] texts =
        [
            "", "a", " a ", "a /* b */ c", "a /* b", "a /*", "a /", "a / b", "a * b", "/*/ a", "/**/", "/* a */ b /* c\n d */ e /**/", "a/*b*//*c*/d", "a /* b * / c */ d",
            "a // b\n c", "a // b\r\n c", "a // b\r c", "a // b", "a //", "// only", "a\r\n//\r\n\r\nb", "a\n b /*", "a /* b */\n /* c *", "a // b\u2028c /* d */ e",
            "a <!-- b --> c", "a <!-- b -- > c", "a <!- b", "a <!--", "a <",
            "a /* b */ , /* c */ d /* e */", "a /* b */ , /* c */ d , /* e */", "a /* b */ , /* c", "a , // b\n , c", "a /* b */ /* c */ , d // e", "a , , b", "a b", "a\n,\n/*\n*/\n,",
            // Comments far longer than the first windows, and their ends cut by a window
            "a /*" + new string('x', 300) + "*/ b //" + new string('y', 200) + "\r\n c /*" + new string('z', 100),
            string.Join(" /* " + new string('-', 40) + " */\n", Enumerable.Range(0, 20).Select(static i => "id" + i)) + " // end",
        ];

        foreach (var whiteSpace in WindowedWhiteSpace.Keys)
        {
            foreach (var text in texts)
            {
                data.Add(whiteSpace, text);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(WindowedWhiteSpaceCases))]
    public async Task ParseManyAsyncWindowsDontChangeTheResult(string whiteSpace, string text)
    {
        // The items and the error of a stream are the ones of a single window holding the whole text, wherever the windows
        // end: in a comment, in one of its delimiters, or before the char which tells that a comment doesn't start
        var comments = WindowedWhiteSpace[whiteSpace];
        var item = OneOf(Terms.Identifier(), Terms.AnyOf("/*<!-", maxSize: 1));
        var random = new Random(text.Length * 31 + whiteSpace.Length);

        foreach (var separated in new[] { false, true })
        {
            var expected = await ParseAsync(() => int.MaxValue, text.Length + 1);

            // The whole text agrees when it parses
            var whole = (separated ? Separated(Terms.Char(','), item).AndSkip(Terms.Char(',').Optional()) : ZeroOrMany(item)).Eof();

            if (TryParseWhole(out var values))
            {
                Assert.Equal(string.Join("|", values!.Select(static s => s.ToString())) + "|", expected);
            }

            (Func<int> Chunk, int BufferSize)[] configurations =
            [
                (() => 1, 1),
                (() => 1, 4096),
                (() => 3, 2),
                (() => 7, 5),
                (() => random.Next(1, 6), 1),
                (() => random.Next(1, 6), 3),
                (() => int.MaxValue, 1),
                (() => int.MaxValue, 64),
            ];

            foreach (var (chunk, bufferSize) in configurations)
            {
                var actual = await ParseAsync(chunk, bufferSize);

                Assert.True(expected == actual, $"'{text}' with buffer size {bufferSize}, separated {separated}: expected {expected}, actual {actual}");
            }

            bool TryParseWhole(out IReadOnlyList<TextSpan>? values)
            {
                try
                {
                    return whole.TryParse(new ParseContext(new Scanner(text)) { WhiteSpaceParser = comments }, out values, out _);
                }
                catch (ParseException)
                {
                    values = null;
                    return false;
                }
            }

            async Task<string> ParseAsync(Func<int> chunk, int bufferSize)
            {
                var options = new StreamParseOptions
                {
                    BufferSize = bufferSize,
                    ContextFactory = (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments },
                };

                var reader = new ChunkedReader(text, chunk, yield: false);
                var items = new List<TextSpan>();
                var error = "";

                try
                {
                    await ToListAsync(separated ? item.ParseManyAsync(reader, Terms.Char(','), options) : item.ParseManyAsync(reader, options), items);
                }
                catch (ParseException e)
                {
                    error = $"{e.Message} {e.Position.Offset} {e.Position}";
                }

                return string.Join("|", items.Select(static s => s.ToString())) + "|" + error;
            }
        }
    }

    [Theory]
    [InlineData(UnterminatedComment.Throw, true)]
    [InlineData(UnterminatedComment.Throw, false)]
    [InlineData(UnterminatedComment.NotAComment, true)]
    [InlineData(UnterminatedComment.NotAComment, false)]
    [InlineData(UnterminatedComment.ToEnd, true)]
    [InlineData(UnterminatedComment.ToEnd, false)]
    public async Task ParseManyAsyncReadsACommentFarLongerThanTheWindowOnce(UnterminatedComment unterminated, bool terminated)
    {
        // The window doubles until it holds the comment: the retries read a bounded multiple of its text, and the reader
        // is read once
        const int Length = 1 << 20;

        var comments = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/", unterminated).Build();
        var text = "a /*" + new string('x', Length) + (terminated ? "*/ b" : "");
        var windows = 0;
        var parsed = 0L;

        var options = new StreamParseOptions
        {
            BufferSize = 16,
            ContextFactory = (scanner, ct) =>
            {
                windows++;
                parsed += scanner.Buffer.Length;
                return new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments };
            },
        };

        var reader = new ChunkedReader(text, () => 4096, yield: false);
        var items = new List<TextSpan>();
        ParseException? exception = null;

        try
        {
            await ToListAsync(Terms.Identifier().ParseManyAsync(reader, options), items);
        }
        catch (ParseException e)
        {
            exception = e;
        }

        Assert.Equal(text.Length, reader.Position);
        Assert.InRange(windows, 1, 24);
        Assert.InRange(parsed, text.Length, 4L * text.Length);

        if (terminated)
        {
            Assert.Null(exception);
            Assert.Equal(["a", "b"], items.Select(static s => s.ToString()));
        }
        else if (unterminated == UnterminatedComment.ToEnd)
        {
            Assert.Null(exception);
            Assert.Equal(["a"], items.Select(static s => s.ToString()));
        }
        else
        {
            // Decided at the end of the stream only, at its position in the stream
            Assert.Equal(["a"], items.Select(static s => s.ToString()));
            Assert.Equal(unterminated == UnterminatedComment.Throw ? "End-of-file found, '*/' expected" : "The input could not be parsed.", exception!.Message);
            Assert.Equal(new TextPosition(unterminated == UnterminatedComment.Throw ? 4 : 2, 1, unterminated == UnterminatedComment.Throw ? 5 : 3), exception.Position);
        }
    }

    [Fact]
    public async Task ParseManyAsyncDropsCommentsWithTheirItems()
    {
        // A window keeps the white space and the comments before the item it retries, not the ones of the items it returned
        var comments = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/").Build();
        var text = string.Join(" /* a comment */ // and another\n", Enumerable.Range(0, 1000).Select(static i => "id" + i)) + " // end";
        var peak = 0;

        var options = new StreamParseOptions
        {
            BufferSize = 2,
            MaxBufferedCharacters = 128,
            ContextFactory = (scanner, ct) =>
            {
                peak = Math.Max(peak, scanner.Buffer.Length);
                return new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments };
            },
        };

        Assert.Equal(1000, (await ToListAsync(Terms.Identifier().ParseManyAsync(new ChunkedReader(text, () => 1, yield: false), options))).Count);
        Assert.InRange(peak, 32, 128);
    }

    [Theory]
    [InlineData(' ', true)]
    [InlineData('\n', true)]
    [InlineData(' ', false)]
    [InlineData('\n', false)]
    public async Task ParseManyAsyncDropsWhiteSpaceAsItIsSkipped(char padding, bool limited)
    {
        // The white space of the context's set is skipped a char at a time, nothing depends on the text after it: a window
        // which ends in it keeps none of it, and a run far longer than the limit is read through windows of the buffer size
        var run = new string(padding, 5000);

        (string Text, bool Separated, long[] Expected)[] cases =
        [
            ("1" + run + "2", false, [1, 2]),
            ("1 2" + run, false, [1, 2]),
            (run + "1 2", false, [1, 2]),
            ("1" + run + "," + run + "2", true, [1, 2]),
            ("1,2" + run, true, [1, 2]),
            ("1,2" + run + ",", true, [1, 2]),
            ("1,2," + run, true, [1, 2]),
            (run + "1" + run + "," + run, true, [1]),

            // Nothing but white space
            (run, false, []),
            (run, true, []),
        ];

        foreach (var (text, separated, expected) in cases)
        {
            foreach (var chunk in new[] { 1, 5, 16, int.MaxValue })
            {
                var peak = 0;
                var parsed = 0L;

                var options = new StreamParseOptions
                {
                    BufferSize = 16,
                    MaxBufferedCharacters = limited ? 64 : int.MaxValue,
                    ContextFactory = (scanner, ct) =>
                    {
                        peak = Math.Max(peak, scanner.Buffer.Length);
                        parsed += scanner.Buffer.Length;
                        return new ParseContext(scanner, cancellationToken: ct);
                    },
                };

                var reader = new ChunkedReader(text, () => chunk, yield: false);
                var values = await ToListAsync(separated ? Terms.Integer().ParseManyAsync(reader, Terms.Char(','), options) : Terms.Integer().ParseManyAsync(reader, options));

                Assert.Equal(expected, values);
                Assert.Equal(text.Length, reader.Position);

                // An item or a separator which ends a window is retried with as much text again
                Assert.InRange(peak, 16, 32);

                // The white space is in one window only: the windows hold the text once, and again the few chars of the
                // items and separators which a window cuts
                Assert.InRange(parsed, text.Length, text.Length + 32);
            }
        }
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \t\r\n")]
    public async Task ParseManyAsyncLimitsAnItemWithoutTheWhiteSpaceBeforeIt(string padding)
    {
        // MaxBufferedCharacters is the limit of an item: the white space before it, however long, neither fails an item
        // which fits nor moves the error of one which doesn't, reported where that item starts in the whole text
        const int Limit = 16;

        foreach (var separated in new[] { false, true })
        {
            foreach (var count in new[] { 0, 1, 3, 4, 5, 15, 16, 17, 64, 65, 1000 })
            {
                if (count == 0 && !separated)
                {
                    continue;
                }

                var run = string.Concat(Enumerable.Repeat(padding, count));
                var prefix = "a" + run + (separated ? "," + run : "");

                foreach (var (chunk, bufferSize) in new[] { (1, 1), (1, 4), (1, 16), (5, 3), (int.MaxValue, 1), (int.MaxValue, 16) })
                {
                    var options = new StreamParseOptions { BufferSize = bufferSize, MaxBufferedCharacters = Limit };

                    // An identifier ends at the char which follows it: one of Limit - 1 chars fits in the limit
                    var fits = new string('b', Limit - 1);

                    Assert.Equal(["a", fits, "c"], await ParseAsync(prefix + fits + run + (separated ? "," : " ") + "c", []));

                    var items = new List<string>();
                    var text = prefix + new string('b', Limit) + run + "c";
                    var exception = await Assert.ThrowsAsync<ParseException>(() => ParseAsync(text, items));

                    Assert.Equal(["a"], items);
                    Assert.Equal($"The input requires more than {Limit} buffered characters to be parsed.", exception.Message);

                    var lastNewLine = text.LastIndexOf('\n', prefix.Length - 1);
                    Assert.Equal(new TextPosition(prefix.Length, 1 + prefix.Count(static c => c == '\n'), prefix.Length - lastNewLine), exception.Position);

                    async Task<List<string>> ParseAsync(string text, List<string> items)
                    {
                        var reader = new ChunkedReader(text, () => chunk, yield: false);
                        var parser = Terms.Identifier().Then(static span => span.ToString());

                        return await ToListAsync(separated ? parser.ParseManyAsync(reader, Terms.Char(','), options) : parser.ParseManyAsync(reader, options), items);
                    }
                }
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseManyAsyncRejectsAnEmptyValueAfterDroppedWhiteSpace(bool separated)
    {
        // The windows which the white space filled are gone when the parser matches nothing at the text which follows it
        var options = new StreamParseOptions { BufferSize = 16, MaxBufferedCharacters = 64 };
        var parser = Terms.Integer().Optional();
        var reader = new ChunkedReader("1" + (separated ? "," : "") + new string(' ', 5000) + "x", () => 5, yield: false);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ToListAsync(separated ? parser.ParseManyAsync(reader, Terms.Char(','), options) : parser.ParseManyAsync(reader, options)));
    }

    [Fact]
    public async Task ParseManyAsyncKeepsTheWhiteSpaceOfAWhiteSpaceParser()
    {
        // What a white space parser reads can depend on the text after the window, a comment for instance, so the white
        // space which ends a window is read again with what follows it, and counts in the limit
        var options = new StreamParseOptions
        {
            BufferSize = 16,
            MaxBufferedCharacters = 64,
            ContextFactory = static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = Literals.WhiteSpace(includeNewLines: true) },
        };

        var items = new List<long>();

        Assert.Equal([1L, 2], await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1" + new string(' ', 40) + "2", () => 5, false), options)));

        // The limit is the one of the white space and the one of the item, not of both: the item is retried from its start
        Assert.Equal([1L, 123456789012345678], await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1" + new string(' ', 60) + "123456789012345678 ", () => 5, false), options)));

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1" + new string(' ', 5000) + "2", () => 5, false), options), items));

        Assert.Single(items);
        Assert.Equal("The input requires more than 64 buffered characters to be parsed.", exception.Message);
        Assert.Equal(new TextPosition(1, 1, 2), exception.Position);
    }

    // The contexts of ParseManyAsyncWhiteSpaceWindowsDontChangeTheResult: every white space which SkipWhiteSpace reads from the
    // context's set. The last one reads comments, with a white space parser which only the items set: what is skipped before
    // an item is then still the white space of the set
    private static readonly Dictionary<string, (Func<Scanner, CancellationToken, ParseContext> Context, Parser<TextSpan>? ItemWhiteSpace)> WindowedWhiteSpaceSets = new()
    {
        ["default"] = (static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct), null),
        ["new-lines"] = (static (scanner, ct) => new ParseContext(scanner, useNewLines: true, cancellationToken: ct), null),
        ["ascii"] = (static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceSet = WhiteSpaceSet.Ascii }, null),
        ["ascii-new-lines"] = (static (scanner, ct) => new ParseContext(scanner, useNewLines: true, cancellationToken: ct) { WhiteSpaceSet = WhiteSpaceSet.Ascii }, null),
        ["space-tab-crlf"] = (static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceSet = WhiteSpaceSet.SpaceTabCrLf }, null),
        ["item-comments"] = (static (scanner, ct) => new ParseContext(scanner, cancellationToken: ct), new CommentsBuilder().WithWhiteSpaceOrNewLine().WithSingleLine("//").WithMultiLine("/*", "*/").Build()),
    };

    public static TheoryData<string, string> WindowedWhiteSpaceSetCases()
    {
        var data = new TheoryData<string, string>();

        string[] texts =
        [
            "", " ", "\r\n", "a", " a ", "a b", "a\nb", "a\r\nb", "a\rb", "a \r\n\r\n b\r\n", "\r\n\r\na", "a\tb\fc\vd", "a\u00A0b", "a \u00A0\u2028\u3000 b\u0085",
            "a , b", "a,\r\n b ,", "a\r\n,\r\n,", "a b , c", "a ! b", "a \r\n !", "a,\n\n !", "a\r", "a,\r",
            // Runs far longer than the windows, new lines which a window cuts, and a failure after them
            "a" + new string(' ', 300) + "b",
            new string(' ', 200) + "a" + new string('\n', 200) + "b" + new string('\t', 200),
            "a" + string.Concat(Enumerable.Repeat("\r\n", 150)) + "b\r\n" + new string(' ', 100) + "!",
            "a" + string.Concat(Enumerable.Repeat(" \r\n", 100)) + "," + string.Concat(Enumerable.Repeat("\n\r", 100)) + "b" + new string(' ', 100) + "," + new string('\n', 100),
            "a" + new string('\u00A0', 200) + "b" + new string('\u2028', 200) + "c d",
            string.Join(new string(' ', 30) + "\r\n", Enumerable.Range(0, 20).Select(static i => "id" + i)) + new string('\n', 50) + "?",
            // Items with white space of their own, which a window keeps, and the exception of one which isn't closed
            "(a b) c", "( a\r\n b )\r\n( c", "a ( b", "(a) , ( b c ) ,", "a , (", "( a ! )", "a\n(\n)\n\n( b !",
            "a" + new string(' ', 300) + "( b\t)" + new string('\n', 300) + "(  c  d  )" + new string(' ', 100) + "," + string.Concat(Enumerable.Repeat("\r\n", 100)) + "( e" + new string(' ', 5) + "?",
            // Comments, for the items which read them: the white space before them is dropped, they are kept with their item
            "a /* b */ c", "a  /* b", "a /", "a // b\r\n c", "a /* b */ , /* c */ d", "a /**/",
            "a" + new string(' ', 300) + "/* b */ c" + string.Concat(Enumerable.Repeat("\r\n", 150)) + "// d\ne" + new string(' ', 100) + ", /*f*/ g" + new string('\n', 100) + "/* h",
        ];

        foreach (var whiteSpace in WindowedWhiteSpaceSets.Keys)
        {
            foreach (var text in texts)
            {
                data.Add(whiteSpace, text);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(WindowedWhiteSpaceSetCases))]
    public async Task ParseManyAsyncWhiteSpaceWindowsDontChangeTheResult(string whiteSpace, string text)
    {
        // The items, their lines and columns, and the error of a stream are the ones of a single window holding the whole
        // text, wherever the windows end and whatever white space they drop
        var (contextFactory, itemWhiteSpace) = WindowedWhiteSpaceSets[whiteSpace];

        // An item in parentheses has white space of its own and throws when it isn't closed
        var group = Capture(Terms.Char('(').And(ZeroOrMany(Terms.Identifier())).And(Terms.Char(')').ElseError("Expected ')'")));

        // New lines are items for the contexts which don't skip them
        var item = OneOf(Terms.Identifier(), Terms.AnyOf("\r\n", maxSize: 1), group).Then(static (context, span) =>
        {
            var position = context.Scanner.Cursor.Position;
            return $"{span.ToString().Replace("\r", "\\r").Replace("\n", "\\n")}@{position.Line}:{position.Column}";
        });

        if (itemWhiteSpace is not null)
        {
            item = item.WithWhiteSpaceParser(itemWhiteSpace);
        }

        var random = new Random(text.Length * 31 + whiteSpace.Length);

        foreach (var separated in new[] { false, true })
        {
            var expected = await ParseAsync(() => int.MaxValue, text.Length + 1, int.MaxValue);

            // The whole text agrees when it parses
            var whole = (separated ? Separated(Terms.Char(','), item).AndSkip(Terms.Char(',').Optional()) : ZeroOrMany(item)).Eof();

            if (TryParseWhole(out var values))
            {
                Assert.Equal(string.Join("|", values!) + "|", expected);
            }

            var configurations = new List<(Func<int> Chunk, int BufferSize, int MaxBufferedCharacters)>
            {
                (() => 1, 1, int.MaxValue),
                (() => 1, 4096, int.MaxValue),
                (() => 3, 2, int.MaxValue),
                (() => 7, 5, int.MaxValue),
                (() => random.Next(1, 6), 1, int.MaxValue),
                (() => random.Next(1, 6), 3, int.MaxValue),
                (() => int.MaxValue, 1, int.MaxValue),
                (() => int.MaxValue, 64, int.MaxValue),

                // No item or separator is longer than the limit, the white space isn't counted
                (() => 1, 1, 16),
                (() => random.Next(1, 6), 4, 16),
                (() => int.MaxValue, 8, 16),
            };

            // Every size of a window: a reader which returns a char at a time fills a window with exactly BufferSize chars
            // while it retains nothing
            for (var size = 2; size <= 24; size++)
            {
                configurations.Add((() => 1, size, int.MaxValue));
                configurations.Add((() => 1, size, 16));
            }

            foreach (var (chunk, bufferSize, maxBufferedCharacters) in configurations)
            {
                var actual = await ParseAsync(chunk, bufferSize, maxBufferedCharacters);

                Assert.True(expected == actual, $"'{text}' with buffer size {bufferSize}, limit {maxBufferedCharacters}, separated {separated}: expected {expected}, actual {actual}");
            }

            bool TryParseWhole(out IReadOnlyList<string>? values)
            {
                try
                {
                    return whole.TryParse(contextFactory(new Scanner(text), CancellationToken.None), out values, out _);
                }
                catch (ParseException)
                {
                    values = null;
                    return false;
                }
            }

            async Task<string> ParseAsync(Func<int> chunk, int bufferSize, int maxBufferedCharacters)
            {
                var options = new StreamParseOptions
                {
                    BufferSize = bufferSize,
                    MaxBufferedCharacters = maxBufferedCharacters,
                    ContextFactory = contextFactory,
                };

                var reader = new ChunkedReader(text, chunk, yield: false);
                var items = new List<string>();
                var error = "";

                try
                {
                    await ToListAsync(separated ? item.ParseManyAsync(reader, Terms.Char(','), options) : item.ParseManyAsync(reader, options), items);
                }
                catch (ParseException e)
                {
                    error = $"{e.Message} {e.Position.Offset} {e.Position}";
                }

                return string.Join("|", items) + "|" + error;
            }
        }
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \t\r\n")]
    public async Task ParseManyAsyncDoesNotBufferEmptyDelimitedValues(string line)
    {
        // The delimited overload frames the text before it parses it: an empty frame is skipped when its delimiter is
        // found and a white space only one once it is parsed, so the blank lines between two values aren't buffered together
        var text = "1\n" + string.Concat(Enumerable.Repeat(line, 2000)) + "2\n" + string.Concat(Enumerable.Repeat(line, 2000));

        foreach (var chunk in new[] { 1, 5, int.MaxValue })
        {
            var peak = 0;

            var options = new StreamParseOptions
            {
                BufferSize = 16,
                MaxBufferedCharacters = 64,
                ContextFactory = (scanner, ct) =>
                {
                    peak = Math.Max(peak, scanner.Buffer.Length);
                    return new ParseContext(scanner, cancellationToken: ct);
                },
            };

            Assert.Equal([1L, 2], await ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => chunk, yield: false), '\n', options)));

            // A frame is a line without its delimiter
            Assert.InRange(peak, 1, line.Length);
        }
    }

    [Fact]
    public async Task ParseManyAsyncCanBeCanceledInAComment()
    {
        using var cts = new CancellationTokenSource();

        var comments = new CommentsBuilder().WithWhiteSpaceOrNewLine().WithMultiLine("/*", "*/").Build();
        var options = new StreamParseOptions
        {
            BufferSize = 4,
            ContextFactory = (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = comments },
        };

        var reader = new ChunkedReader("a /*" + new string('x', 10_000) + "*/ b", () => 16, yield: true) { OnRead = position => { if (position >= 1000) cts.Cancel(); } };
        var items = new List<TextSpan>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => ToListAsync(Terms.Identifier().ParseManyAsync(reader, options, cts.Token), items));

        Assert.Single(items);
        Assert.InRange(reader.Position, 1000, 2100);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParseManyAsyncKeepsThePositionOfAWhiteSpaceParserException(bool positioned)
    {
        // An exception of the white space parser which the end of the window doesn't explain is the one of the whole text:
        // its position is converted when it has one
        var whiteSpace = Capture(OneOrMany(OneOf(
            Literals.WhiteSpace(includeNewLines: true),
            Literals.Char('#').Then<TextSpan>(positioned
                ? static (context, _) => throw new ParseException("No comments", context.Scanner.Cursor.Position)
                : static (context, _) => throw new ParseException("No comments")))));

        foreach (var delimited in new[] { false, true })
        {
            var options = new StreamParseOptions
            {
                BufferSize = 2,
                ContextFactory = (scanner, ct) => new ParseContext(scanner, cancellationToken: ct) { WhiteSpaceParser = whiteSpace },
            };

            var reader = new ChunkedReader("a\nb\nc\n d #x e", () => 1, yield: false);
            var items = new List<TextSpan>();
            var parser = Terms.Identifier();

            var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(delimited ? parser.ParseManyAsync(reader, '\n', options) : parser.ParseManyAsync(reader, options), items));

            Assert.Equal("No comments", exception.Message);
            Assert.Equal(positioned ? new TextPosition(10, 4, 5) : default, exception.Position);

            // The line of the delimited value is parsed to its end before the value is returned
            Assert.Equal(delimited ? ["a", "b", "c"] : ["a", "b", "c", "d"], items.Select(static s => s.ToString()));
        }
    }

    [Fact]
    public async Task TextSpansRemainValid()
    {
        var spans = await ToListAsync(Terms.Identifier().ParseManyAsync(new ChunkedReader("alpha beta gamma delta", () => 1, false), new StreamParseOptions { BufferSize = 1 }));

        Assert.Equal(["alpha", "beta", "gamma", "delta"], spans.Select(static s => s.ToString()));
    }

    [Theory]
    [MemberData(nameof(ChunkSizes))]
    public async Task ParseManyAsyncParsesDelimitedValues(int chunk, int bufferSize)
    {
        var text = "{\"a\": \"1\"}\n\n  \r\n[\"b\", \"c\"]\r\n\"d\"\n \"e\" ";
        var reader = new ChunkedReader(text, () => chunk, yield: chunk == 3);

        var items = await ToListAsync(JsonParser.Json.ParseManyAsync(reader, '\n', new StreamParseOptions { BufferSize = bufferSize }));

        Assert.Equal(["{\"a\":\"1\"}", "[\"b\",\"c\"]", "\"d\"", "\"e\""], items.Select(static json => json.ToString()));
    }

    [Theory]
    [InlineData("1\n2\nx", '\n')]
    [InlineData("1\r\n2\r\n x", '\n')]
    [InlineData("1\n\n\n2\n\n x\n", '\n')]
    [InlineData("1, 2,\n 3,\r\n x", ',')]
    [InlineData("1,\r,\r 2, x", ',')]
    [InlineData("1\r2\r\r x", '\r')]
    [InlineData("1\r\n2\r\n x", '\r')]
    [InlineData("1;2;;x", ';')]
    [InlineData("x", ';')]
    public async Task ParseManyAsyncReportsAbsolutePositionsOfDelimitedValues(string text, char delimiter)
    {
        var expected = new Cursor(text);

        while (expected.Current != 'x')
        {
            expected.Advance();
        }

        foreach (var chunk in new[] { 1, 2, 4096 })
        {
            var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader(text, () => chunk, yield: false), delimiter, new StreamParseOptions { BufferSize = 1 })));

            Assert.Equal(expected.Position, exception.Position);
        }
    }

    [Fact]
    public async Task ParseManyAsyncRequiresDelimitedValuesToBeMatchedEntirely()
    {
        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1\n2 3\n", () => 1, false), '\n')));

        Assert.Equal(4, exception.Position.Offset);
        Assert.Equal(2, exception.Position.Line);
        Assert.Equal(3, exception.Position.Column);

        var options = new StreamParseOptions { SkipWhiteSpace = false };

        Assert.Equal([1L, 2], await ToListAsync(Literals.Integer().ParseManyAsync(new ChunkedReader("1\n\n2", () => 1, false), '\n', options)));

        await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Literals.Integer().ParseManyAsync(new ChunkedReader("1 \n2", () => 1, false), '\n', options)));
    }

    [Fact]
    public async Task ParseManyAsyncLimitsDelimitedValueSize()
    {
        var items = new List<long>();

        var exception = await Assert.ThrowsAsync<ParseException>(() => ToListAsync(Terms.Integer().ParseManyAsync(new ChunkedReader("1\n22\n" + new string('3', 20) + "\n4", () => 3, false), '\n', new StreamParseOptions { BufferSize = 2, MaxBufferedCharacters = 8 }), items));

        Assert.Equal(2, items.Count);
        Assert.Equal(5, exception.Position.Offset);
        Assert.Equal(3, exception.Position.Line);
        Assert.Equal(1, exception.Position.Column);
    }

    [Fact]
    public async Task ParseManyAsyncParsesDelimitedValuesAsSoonAsTheyAreReceived()
    {
        var reader = new InteractiveReader();
        var values = new List<long>();

        reader.Write("1\n2");

        var task = Task.Run(async () =>
        {
            await foreach (var value in Terms.Integer().ParseManyAsync(reader, '\n'))
            {
                values.Add(value);

                // More text is only available once a value is parsed, waiting for a full buffer would never end
                if (value == 1)
                {
                    reader.Write("\n3");
                }
                else if (value == 2)
                {
                    reader.Write("\n");
                }
                else
                {
                    reader.Complete();
                }
            }
        });

        Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))));
        await task;
        Assert.Equal([1L, 2, 3], values);
    }

    [Fact]
    public async Task ParseManyAsyncEnumerationCanStopEarly()
    {
        var reader = new ChunkedReader("1 2 3 4 5", () => 1, false);

        await foreach (var value in Terms.Integer().ParseManyAsync(reader, new StreamParseOptions { BufferSize = 1 }))
        {
            if (value == 2)
            {
                break;
            }
        }

        Assert.True(reader.Position < 9);
    }

    private static async Task<List<TValue>> ToListAsync<TValue>(IAsyncEnumerable<TValue> source, List<TValue>? items = null)
    {
        items ??= [];

        await foreach (var item in source)
        {
            items.Add(item);
        }

        return items;
    }

    private static Parser<string> CreateOperators()
    {
        static Func<string, string, string> Binary(string op) => (a, b) => $"({a}{op}{b})";

        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString()).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix((Terms.Char('!'), static x => $"({x}!)"))
            .Unary((Terms.Char('-'), static x => $"(-{x})"))
            .RightAssociative((Terms.Text("**"), Binary("**")))
            .LeftAssociative((Terms.Text("*"), Binary("*")), (Terms.Text("+"), Binary("+")))
            .LeftAssociative((Terms.Text("<<"), Binary("<<")), (Terms.Text("&"), Binary("&")), (Terms.Text("="), Binary("=")))
            .LeftAssociative((Terms.Text("<"), Binary("<")), (Terms.Text("<="), Binary("<=")))
            .Unary((Terms.Text("not"), static x => $"(not {x})"))
            .LeftAssociative((Terms.Text("&&"), Binary("&&")), (Terms.Text("=="), Binary("==")));

        return expression;
    }

    private static Parser<string> CreateValuedPrefix()
    {
        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString()).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Unary((Terms.Char('!'), static x => $"(!{x})"))
            .Unary(
                (Terms.Char('-').Then(static (_, start, _, _) => start), static (string x, int start) => $"-@{start}({x})"),
                (Terms.Keyword("typeof").Then(static (_, start, _, _) => start), static (string x, int start) => $"typeof@{start}({x})"))
            .LeftAssociative((Terms.Char('*'), static (a, b) => $"({a}*{b})"));

        return expression;
    }

    private static Parser<string> CreateOperatorLevels()
    {
        static Func<string, string, string> Binary(string op) => (a, b) => $"({a}{op}{b})";

        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString()).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary
            .Unary((Terms.Char('-'), static x => $"(-{x})"))
            .RightAssociative((Terms.Text("**"), Binary("**")))
            .LeftAssociative((Terms.Text("*"), Binary("*")), (Terms.Text("+"), Binary("+")))
            .LeftAssociative((Terms.Text("<<"), Binary("<<")), (Terms.Text("&"), Binary("&")), (Terms.Text("="), Binary("=")))
            .LeftAssociative((Terms.Text("<"), Binary("<")), (Terms.Text("<="), Binary("<=")))
            .Unary((Terms.Text("not"), static x => $"(not {x})"))
            .LeftAssociative((Terms.Text("&&"), Binary("&&")), (Terms.Text("=="), Binary("==")));

        return expression;
    }

    private static Parser<string> CreateValuedOperators()
    {
        var expression = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString()).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        expression.Parser = primary.Operators()
            .Postfix(
                (Terms.Char('.').SkipAnd(Terms.Identifier()), static (x, name) => $"{x}.{name}"),
                (Between(Terms.Char('['), expression, Terms.Char(']')), static (x, index) => $"{x}[{index}]"))
            .Unary((Terms.Char('-'), static x => $"(-{x})"))
            .LeftAssociative([Terms.Text("*"), Terms.Text("/")], static (a, b, op) => $"({a}{op}{b})")
            .Ternary(Terms.Char('?'), expression, Terms.Char(':'), static (c, a, b) => $"({c}?{a}:{b})", rightAssociative: false)
            .LeftAssociative((Terms.Text("??"), static (a, b) => $"({a}??{b})"))
            .RightAssociative([Terms.Text("=")], static (a, b, op) => $"({a}{op}{b})");

        return expression;
    }

    private static Parser<string> CreateConditionalWithLast()
    {
        var expression = Deferred<string>();
        var assignment = Deferred<string>();
        var primary = Terms.Identifier().Then(static x => x.ToString()).Or(Between(Terms.Char('('), expression, Terms.Char(')')));

        assignment.Parser = primary.Operators()
            .LeftAssociative((Terms.Text("||"), static (a, b) => $"({a}||{b})"))
            .Ternary(Terms.Char('?'), assignment, Terms.Char(':'), assignment, static (c, a, b) => $"({c}?{a}:{b})")
            .RightAssociative((Terms.Char('='), static (a, b) => $"({a}={b})"));

        expression.Parser = assignment.Operators().LeftAssociative((Terms.Char(','), static (a, b) => $"({a},{b})"));

        return expression;
    }

    private static Parser<StatementList> CreateSqlParser()
    {
        var method = typeof(SqlParser).GetMethod("CreateRuntimeParser", BindingFlags.NonPublic | BindingFlags.Static)!;
        return (Parser<StatementList>)method.Invoke(null, null)!;
    }

    private interface IGrammar
    {
        Func<Scanner, CancellationToken, ParseContext>? ContextFactory { get; }

        /// <summary>
        /// Returns the outcome of the parse, or <see langword="null"/> when it is inconclusive.
        /// </summary>
        string? Parse(string text, bool isFinal);

        Task<string> ParseAsync(Func<TextReader> reader, StreamParseOptions options);

        string ParseCompacting(Func<TextReader> reader, StreamParseOptions options);
    }

    private sealed class Grammar<T> : IGrammar
    {
        private readonly Parser<T> _parser;
        private readonly Func<Scanner, ParseContext>? _contextFactory;

        public Grammar(Parser<T> parser, Func<Scanner, ParseContext>? contextFactory = null)
        {
            _parser = parser;
            _contextFactory = contextFactory;
            ContextFactory = contextFactory == null ? null : (scanner, _) => contextFactory(scanner);
        }

        public Func<Scanner, CancellationToken, ParseContext>? ContextFactory { get; }

        public string? Parse(string text, bool isFinal)
        {
            var scanner = new Scanner(text, isFinal);
            var context = _contextFactory?.Invoke(scanner) ?? new ParseContext(scanner);
            var result = new ParseResult<T>();

            try
            {
                var success = _parser.Parse(context, ref result);

                if (scanner.Cursor.HitEnd)
                {
                    return null;
                }

                return success ? "OK " + Dump(result.Value) : "FAIL";
            }
            catch (ParseException e)
            {
                return scanner.Cursor.HitEnd ? null : Error(e);
            }
        }

        public async Task<string> ParseAsync(Func<TextReader> reader, StreamParseOptions options)
        {
            var (success, value) = await _parser.TryParseAsync(reader(), options);

            if (success)
            {
                return "OK " + Dump(value);
            }

            try
            {
                await _parser.ParseAsync(reader(), options);
                return "FAIL";
            }
            catch (ParseException e)
            {
                return Error(e);
            }
        }

        public string ParseCompacting(Func<TextReader> reader, StreamParseOptions options)
        {
            var success = _parser.TryParse(reader(), out var value, out var error, options);

            if (success)
            {
                return "OK " + Dump(value);
            }

            try
            {
                _parser.Parse(reader(), options);
            }
            catch (ParseException e)
            {
                var message = Error(e);
                Assert.Equal(message, $"ERROR {error!.Message} at {error.Position.Offset}{error.Position}");
                return message;
            }

            Assert.Null(error);
            return "FAIL";
        }

        private static string Error(ParseException e) => $"ERROR {e.Message} at {e.Position.Offset}{e.Position}";
    }

    private static string Dump(object? value, int depth = 0)
    {
        if (value == null)
        {
            return "null";
        }

        if (depth > 64)
        {
            return "...";
        }

        var type = value.GetType();

        switch (value)
        {
            case string s:
                return "\"" + s + "\"";
            case TextSpan span:
                return "'" + span.ToString() + "'";
            case IFormattable formattable when type.IsPrimitive || type.IsEnum || value is decimal:
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            case IEnumerable enumerable:
                return "[" + string.Join(",", enumerable.Cast<object?>().Select(x => Dump(x, depth + 1))) + "]";
        }

        if (type.IsPrimitive)
        {
            return value.ToString()!;
        }

        var members = new List<string>();

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            string dump;

            try
            {
                dump = Dump(property.GetValue(value), depth + 1);
            }
            catch (TargetInvocationException)
            {
                dump = "!";
            }

            members.Add(property.Name + "=" + dump);
        }

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            members.Add(field.Name + "=" + Dump(field.GetValue(value), depth + 1));
        }

        return type.Name + "{" + string.Join(",", members) + "}";
    }

    /// <summary>
    /// Returns the text in chunks, optionally completing asynchronously.
    /// </summary>
    internal sealed class ChunkedReader : TextReader
    {
        private readonly string _text;
        private readonly Func<int> _chunk;
        private readonly bool _yield;

        public ChunkedReader(string text, Func<int> chunk, bool yield)
        {
            _text = text;
            _chunk = chunk;
            _yield = yield;
        }

        public int Position { get; private set; }

        public Action<int>? OnRead { get; set; }

        public override int Peek() => Position < _text.Length ? _text[Position] : -1;

        public override int Read() => Position < _text.Length ? _text[Position++] : -1;

        public override int Read(char[] buffer, int index, int count) => Read(buffer.AsSpan(index, count));

#if NET8_0_OR_GREATER
        public override int Read(Span<char> buffer)
#else
        private int Read(Span<char> buffer)
#endif
        {
            var length = Math.Min(Math.Min(buffer.Length, _chunk()), _text.Length - Position);
            _text.AsSpan(Position, length).CopyTo(buffer);
            Position += length;
            OnRead?.Invoke(Position);
            return length;
        }

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            if (_yield)
            {
                await Task.Yield();
            }

            return Read(buffer, index, count);
        }

#if NET8_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (_yield)
            {
                await Task.Yield();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return Read(buffer.Span);
        }
#endif
    }

    /// <summary>
    /// Generates an endless-looking NDJSON input without retaining it.
    /// </summary>
    private sealed class GeneratedReader : TextReader
    {
        private readonly int _count;
        private int _index;
        private string _current = "";
        private int _position;

        public GeneratedReader(int count)
        {
            _count = count;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (_position == _current.Length)
            {
                if (_index == _count)
                {
                    return 0;
                }

                _current = $"{{\"id\":\"{_index++}\",\"value\":[\"{new string('v', _index % 50)}\"]}}\n";
                _position = 0;
            }

            var length = Math.Min(count, _current.Length - _position);
            _current.CopyTo(_position, buffer, index, length);
            _position += length;
            return length;
        }

        public override Task<int> ReadAsync(char[] buffer, int index, int count) => Task.FromResult(Read(buffer, index, count));

#if NET8_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var chars = new char[buffer.Length];
            var read = Read(chars, 0, chars.Length);
            chars.AsSpan(0, read).CopyTo(buffer.Span);
            return new ValueTask<int>(read);
        }
#endif
    }

    /// <summary>
    /// Returns the text written so far, waiting for more until it is completed.
    /// </summary>
    private sealed class InteractiveReader : TextReader
    {
        private readonly object _lock = new();
        private readonly System.Text.StringBuilder _pending = new();
        private TaskCompletionSource<bool> _available = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _completed;

        public void Write(string text)
        {
            lock (_lock)
            {
                _pending.Append(text);
                _available.TrySetResult(true);
            }
        }

        public void Complete()
        {
            lock (_lock)
            {
                _completed = true;
                _available.TrySetResult(true);
            }
        }

        public override int Read(char[] buffer, int index, int count) => throw new NotSupportedException();

        public override async Task<int> ReadAsync(char[] buffer, int index, int count)
        {
            while (true)
            {
                Task wait;

                lock (_lock)
                {
                    if (_pending.Length > 0)
                    {
                        var length = Math.Min(count, _pending.Length);
                        _pending.CopyTo(0, buffer, index, length);
                        _pending.Remove(0, length);
                        return length;
                    }

                    if (_completed)
                    {
                        return 0;
                    }

                    _available = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    wait = _available.Task;
                }

                await wait.ConfigureAwait(false);
            }
        }

#if NET8_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            var chars = new char[buffer.Length];
            var read = await ReadAsync(chars, 0, chars.Length).ConfigureAwait(false);
            chars.AsSpan(0, read).CopyTo(buffer.Span);
            return read;
        }
#endif
    }

    /// <summary>
    /// Matches 'a' when it is followed by 'b' or ends the text, reading <see cref="Cursor.Span"/> directly.
    /// </summary>
    private sealed class LookaheadParser : Parser<char>
    {
        public override bool Parse(ParseContext context, ref ParseResult<char> result)
        {
            // A token is parsed again on a larger buffer when it reads the end of the buffer
            if (context.IsCompacting)
            {
                return context.ParseToken(this, ref result);
            }

            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;
            var span = cursor.Span;

            if (span.Length > 0 && span[0] == 'a')
            {
                if (span.Length == 1)
                {
                    // The decision depends on the next character
                    cursor.MarkHitEnd();
                }

                if (span.Length == 1 || span[1] == 'b')
                {
                    var start = cursor.Offset;
                    cursor.Advance();
                    result.Set(start, cursor.Offset, 'a');
                    context.ExitParser(this);
                    return true;
                }
            }

            context.ExitParser(this);
            return false;
        }
    }
}
