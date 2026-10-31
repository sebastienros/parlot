#nullable enable

using System;
using System.Collections.Generic;
using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// Grammars made of literals, whose generated string parsers read the text through a span local and move the cursor
// once (span code). Each has a string and a TextReader entry point, compared with the runtime parser in SpanSourceTests.
[IncludeUsings("System.Collections.Generic")]
public static partial class SpanSourceGrammars
{
    // Choices in loops in a sequence, like the email benchmark
    [GenerateParser(nameof(TryParseEmail))]
    private static Parser<string> BuildEmail()
    {
        var dot = Literals.Char('.');
        var plus = Literals.Char('+');
        var minus = Literals.Char('-');
        var at = Literals.Char('@');
        var wordChar = Literals.Pattern(char.IsLetterOrDigit);
        var wordDotPlusMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), dot, plus, minus));
        var wordDotMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), dot, minus));
        var wordMinus = OneOrMany(OneOf(wordChar.Then(static _ => 'w'), minus));

        return Capture(wordDotPlusMinus.And(at).And(wordMinus).And(dot).And(wordDotMinus))
            .Then(static value => value.ToString());
    }

    // The values of the parts of a sequence
    [GenerateParser(nameof(TryParseValues))]
    private static Parser<string> BuildValues() =>
        Literals.Char('a')
            .And(Literals.Text("bc"))
            .And(Literals.Pattern(static c => c >= '0' && c <= '9'))
            .And(Literals.AnyOf("xyz", maxSize: 2))
            .And(Literals.Text("!"))
            .Then(static t => $"{t.Item1}|{t.Item2}|{t.Item3}|{t.Item4}|{t.Item5}");

    // Skipped parts, optional parts, and constants
    [GenerateParser(nameof(TryParseSkipped))]
    private static Parser<string> BuildSkipped() =>
        Literals.Char('[')
            .SkipAnd(Literals.Pattern(static c => c >= 'a' && c <= 'z', minSize: 2, maxSize: 4))
            .AndSkip(Literals.Char(']'))
            .And(Literals.Char('!').Optional())
            .And(Literals.Text("??").Then(1).Or(Literals.Char('?').Then(2)).ZeroOrOne())
            .AndSkip(Literals.NoneOf(";", minSize: 0))
            .Then(static t => $"{t.Item1}|{(t.Item2.HasValue ? t.Item2.Value : '-')}|{string.Join(",", t.Item3)}");

    // Bounded loops, and a choice which fails after reading some text
    [GenerateParser(nameof(TryParseLoops))]
    private static Parser<string> BuildLoops() =>
        Between(
            Literals.Char('('),
            ZeroOrMany(Literals.Text("ab").Or(Literals.Text("ac")).Or(Literals.Char('b').Then("B")), 3)
                .And(OneOrMany(Literals.AnyOf("0123456789", minSize: 2, maxSize: 3), 2)),
            Literals.Char(')'))
            .Then(static t => $"{string.Join(",", t.Item1)}|{string.Join(",", t.Item2)}");

    // An alternative generated as span code fails after reading some text: the next one reads from the same position
    [GenerateParser(nameof(TryParseRestore))]
    private static Parser<string> BuildRestore() =>
        OneOf(
            Literals.Text("ab").And(Literals.Char('c')).Then(static _ => "abc"),
            Terms.Identifier().Then(static x => "id:" + x.ToString()));

    // The cursor is where the span code ended, and its position is computed from there
    [GenerateParser(nameof(TryParsePosition))]
    private static Parser<(int, int, int)> BuildPosition() =>
        Literals.Text("a\n").And(Literals.Pattern(static c => c != ';', minSize: 0)).AndSkip(Literals.Char(';').Optional())
            .Then(static (context, _) =>
            {
                var position = context.Scanner.Cursor.Position;
                return (position.Offset, position.Line, position.Column);
            });

    // Escape sequences, like NCalc's strings
    [GenerateParser(nameof(TryParseQuoted))]
    private static Parser<string> BuildQuoted()
    {
        var escapedUnicode = Literals.Text("\\u")
            .SkipAnd(Literals.Pattern(Character.IsHexDigit, 4, 4))
            .Then(static value => ((char)Convert.ToInt32(value.ToString(), 16)).ToString());
        var escapedCharacter = Literals.Char('\\')
            .And(Literals.Pattern(static _ => true, 1, 1))
            .Then(static value => value.Item2.ToString());
        var text = Literals.Pattern(static c => c != '\'' && c != '\\')
            .Then(static value => value.ToString());

        return Between(Literals.Char('\''), ZeroOrMany(OneOf(escapedUnicode, escapedCharacter, text)), Literals.Char('\''))
            .Then(static values => string.Concat(values));
    }

    // Numbers, and the white space skipped by the context before each token
    [GenerateParser(nameof(TryParseNumbers))]
    private static Parser<string> BuildNumbers() =>
        Terms.Char('(')
            .SkipAnd(Terms.Integer())
            .AndSkip(Terms.Char(','))
            .And(Terms.Decimal())
            .AndSkip(Terms.Char(')'))
            .And(Terms.Number<double>(NumberOptions.Any).ZeroOrOne())
            .Then(static t => $"{t.Item1}|{t.Item2}|{string.Join(",", t.Item3)}");

    // A custom white space parser skips comments
    [GenerateParser(nameof(TryParseCommented))]
    private static Parser<string> BuildCommented() =>
        Terms.Char('[')
            .SkipAnd(Terms.Identifier())
            .AndSkip(Terms.Char(']'))
            .WithComments(static c => c.WithWhiteSpaceOrNewLine().WithSingleLine("#"))
            .Then(static span => span.ToString());

    // The delimiter is span code inside the code of a token, which is parsed again when it reads the end of the buffer
    [GenerateParser(nameof(TryParseDelimited))]
    private static Parser<string> BuildDelimited() =>
        AnyCharBefore(Literals.Char('-').And(Literals.Char('-')).And(Literals.AnyOf(">!", minSize: 1, maxSize: 1)), consumeDelimiter: true)
            .Then(static span => span.ToString());
}
