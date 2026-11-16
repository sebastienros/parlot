#nullable enable

using Parlot;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

// The generated copies of the runtime grammars of FailureReasonParityTests: parsers which reject what they read with a
// reason, a string, an enum value, a combination of flags or a number, which the entry points return with the furthest
// failure.
public static partial class FailureReasonGrammars
{
    private static Parser<string> Word => Terms.Identifier().Then(static value => value.ToString());

    // A word which isn't reserved, followed by a semicolon or not, or "from!"
    [GenerateParser(nameof(TryParseWhen))]
    public static Parser<string> BuildWhen() =>
        OneOf(
            Word.When(static (_, value) => value != "from", "'from' is reserved").AndSkip(Terms.Char(';')),
            Word.When(static (_, value) => value != "from", "not kept: a reason at the same offset"),
            Terms.Text("from!").Then(static _ => "!"))
        .Eof();

    // A number which isn't followed by an identifier
    [GenerateParser(nameof(TryParseNumber))]
    public static Parser<string> BuildNumber() =>
        Terms.Integer().WhenNotFollowedBy(Literals.Identifier(), FailureReason.IdentifierAfterNumber)
            .Then(static value => value.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AndSkip(Terms.Char(';')).Eof();

    // A word which isn't the keyword "let"
    [GenerateParser(nameof(TryParseNot))]
    public static Parser<string> BuildNot() =>
        Not(Terms.Keyword("let"), FailureFlags.A | FailureFlags.B).SkipAnd(Word).AndSkip(Terms.Char(';')).Eof();

    // A word which isn't reserved, converted with a reason, or a number
    [GenerateParser(nameof(TryParseThenTry))]
    public static Parser<string> BuildThenTry() =>
        Word.ThenTry(static value => (value != "from", value), FailureReason.ReservedWord)
            .Or(Terms.Integer().ThenTry(static (ParseContext _, int _, int _, long value, out string result) =>
            {
                result = "#";
                return value < 100;
            }, (short)42))
            .AndSkip(Terms.Char(';')).Eof();

    // An octal number, whose conversion records which digit is wrong
    [GenerateParser(nameof(TryParseOctal))]
    public static Parser<string> BuildOctal() =>
        Terms.Pattern(static c => c >= '0' && c <= '9').ThenTry(static (ParseContext context, int start, int end, TextSpan value, out string result) =>
        {
            result = value.ToString();

            for (var i = 0; i < result.Length; i++)
            {
                if (result[i] > '7')
                {
                    context.Scanner.Cursor.RecordFailure(start + i, FailureReason.InvalidDigit);
                    return false;
                }
            }

            return true;
        }).AndSkip(Terms.Char(';')).Eof();

    // A lookahead whose parser rejects a word further than where it starts
    [GenerateParser(nameof(TryParseLookahead))]
    public static Parser<string> BuildLookahead() =>
        Not(Word.AndSkip(Terms.Identifier().When(static (_, value) => value.ToString() == "x", "inner")))
            .SkipAnd(Word.AndSkip(Terms.Char('!'))).Eof();
}
