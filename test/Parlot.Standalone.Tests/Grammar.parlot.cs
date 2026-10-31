using System.Linq;
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class Grammar
{
    [GenerateParser(nameof(TryParseNumber))]
    private static Parser<int> Number() => Terms.Number<int>(NumberOptions.Integer).Eof();

    [GenerateParser(nameof(TryParseEof))]
    private static Parser<object> StandaloneEof() => Eof();

    [GenerateParser(nameof(TryParseNumberWithStandaloneEof))]
    private static Parser<long> NumberWithStandaloneEof() => Terms.Integer().AndSkip(Eof());

    [GenerateParser(nameof(TryParseEofFallback))]
    private static Parser<long> EofFallback() =>
        Terms.Integer().Eof().Or(Terms.Integer().AndSkip(Literals.Char('a')));

    [GenerateParser(nameof(TryParseStandaloneEofFallback))]
    private static Parser<long> StandaloneEofFallback() =>
        Terms.Integer().AndSkip(Eof()).Or(Terms.Integer().AndSkip(Literals.Char('a')));

    [GenerateParser(nameof(TryParsePrefix))]
    private static Parser<string> Prefix() => Literals.Text("hello");

    [GenerateParser(nameof(TryParseMatchedText))]
    private static Parser<string> MatchedText() =>
        Literals.Text("hello", caseInsensitive: true, returnMatchedText: true);

    [GenerateParser(nameof(TryParseSkippedValues))]
    private static Parser<string> SkippedValues() =>
        Literals.Number<byte>(NumberOptions.Integer).SkipAnd(Between(
            Literals.Text("keyword", caseInsensitive: true, returnMatchedText: true),
            Literals.Text("value", caseInsensitive: true, returnMatchedText: true),
            Literals.Text("end", caseInsensitive: true, returnMatchedText: true))).Eof();

    [GenerateParser(nameof(TryParseIdentifier))]
    private static Parser<string> Identifier() =>
        Literals.Identifier().Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseIdentifierWithPredicates))]
    private static Parser<string> IdentifierWithPredicates() =>
        Literals.Identifier(static c => c == '@', static c => c == '-').Then(static value => value.ToString());

    // The text is searched for the chars the delimiter can start with
    [GenerateParser(nameof(TryParseTextBefore))]
    private static Parser<string> TextBefore() =>
        AnyCharBefore(Literals.Text("-->").Or(Literals.Text("==>")), consumeDelimiter: true).Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseTextBeforeChar))]
    private static Parser<string> TextBeforeChar() =>
        AnyCharBefore(Literals.Char(';'), canBeEmpty: true).Then(static value => value.ToString());

    [GenerateParser(nameof(TryParsePattern))]
    private static Parser<string> Pattern() =>
        Literals.Pattern(static c => char.IsDigit(c), minSize: 2, maxSize: 4).Then(static value => value.ToString());

    [GenerateParser(nameof(TryParseAlternative))]
    private static Parser<char> Alternative() =>
        Terms.Char('a').SkipAnd(Literals.Char('b'))
            .Or(Terms.Char('a').SkipAnd(Literals.Char('c'))).Eof();

    [GenerateParser(nameof(TryParseString))]
    private static Parser<string> String() => Terms.String().Then(static value => value.ToString()).Eof();

    [GenerateParser(nameof(TryParseNumbers))]
    private static Parser<System.Collections.Generic.IReadOnlyList<int>> Numbers() =>
        Separated(Terms.Char(','), Terms.Number<int>(NumberOptions.Integer)).Eof();

    [GenerateParser(nameof(TryParseOptional))]
    private static Parser<int> OptionalNumber() =>
        Terms.Number<int>(NumberOptions.Integer).Optional().Then(static value => value.OrSome(-1)).Eof();

    [GenerateParser(nameof(TryParseZeroOrOne))]
    private static Parser<System.Collections.Generic.IReadOnlyList<char>> ZeroOrOneChar() =>
        ZeroOrOne(Literals.Char('a'));

    [GenerateParser(nameof(TryParseZeroOrOneDiscarded))]
    private static Parser<char> ZeroOrOneDiscarded() =>
        ZeroOrOne(Literals.Char('a')).SkipAnd(Literals.Char('b')).Eof();

    [GenerateParser(nameof(TryParseRecursive))]
    private static Parser<int> Recursive()
    {
        var expression = Deferred<int>();
        expression.Parser = Literals.Char('x').Then(0)
            .Or(Between(Literals.Char('('), expression, Literals.Char(')')).Then(static depth => depth + 1));
        return expression.Eof();
    }

    [GenerateParser(nameof(TryParseLeftRecursive))]
    private static Parser<int> LeftRecursive()
    {
        var expression = Deferred<int>();
        expression.Parser = expression.AndSkip(Literals.Char('+')).And(Literals.Number<int>(NumberOptions.Integer)).Then(static x => x.Item1 + x.Item2)
            .Or(Literals.Number<int>(NumberOptions.Integer));
        return expression;
    }

    [GenerateParser(nameof(TryParseCustomWhitespace))]
    private static Parser<string> CustomWhitespace() =>
        Terms.Text("hello").AndSkip(Terms.Text("world")).Eof()
            .WithWhiteSpaceParser(Capture(OneOrMany(Literals.Char('_'))));

    [GenerateParser(nameof(TryParseConfigured))]
    private static Parser<string> Configured(GrammarOptions options) =>
        If(() => { options.Evaluations++; return options.Formal; }, Terms.Text("Hello"), Terms.Text("Hi"))
            .Then(value => options.Prefix + value).Eof();

    [GenerateParser(nameof(TryParseConfiguredWhitespace))]
    private static Parser<string> ConfiguredWhitespace(GrammarOptions options) =>
        Terms.Text("hello").AndSkip(Terms.Text("world")).Then(value => options.Prefix + value).Eof()
            .WithWhiteSpaceParser(Capture(OneOrMany(
                If(() => options.Formal, Literals.Char('_'), Literals.Char('-')))));

    [GenerateParser(nameof(TryParseDigits))]
    private static Parser<string> Digits() =>
        Terms.AnyOf("0123456789", minSize: 2).AndSkip(Literals.NoneOf("0123456789")).Then(static value => value.ToString()).Eof();

    [GenerateParser(nameof(TryParseError))]
    private static Parser<char> ErrorParser() => Literals.Char('x').ElseError("Expected x");

    [GenerateParser(nameof(TryParseThrowingCallback))]
    private static Parser<char> ThrowingCallback() =>
        Literals.Char('x').Then(static char (char value) => throw new System.InvalidOperationException("Callback failed"));

    [GenerateParser(nameof(TryParseExplicitLambda))]
    private static Parser<string> ExplicitLambda() =>
        Literals.Char('x').Then(static string (char value) => value.ToString());

    [GenerateParser(nameof(TryParseCancelableNumber))]
    private static Parser<int> CancelableNumber() => Terms.Number<int>(NumberOptions.Integer).Eof();

    [GenerateParser(nameof(TryParseCancelableRecursive))]
    private static Parser<int> CancelableRecursive() => Recursive();

    [GenerateParser(nameof(TryParseCancelableSequence))]
    private static Parser<int> CancelableSequence(CancellationOptions options) =>
        ZeroOrMany(Literals.Char('x').Then(value =>
        {
            options.Evaluations++;
            options.Source.Cancel();
            return value;
        })).Then(static values => values.Count).Eof();

    [GenerateParser(nameof(TryParseCancelableWhitespace))]
    private static Parser<string> CancelableWhitespace(CancellationOptions options) =>
        Terms.Text("hello").Eof().WithWhiteSpaceParser(Capture(OneOrMany(
            Literals.Char('_').Then(value =>
            {
                options.Evaluations++;
                options.Source.Cancel();
                return value;
            }))));

    [GenerateParser(nameof(TryParseTokenConfiguration))]
    private static Parser<bool> TokenConfiguration(System.Threading.CancellationToken applicationToken) =>
        Literals.Char('x').Then(_ => applicationToken.IsCancellationRequested).Eof();

    [GenerateParser(nameof(TryParseKeyword))]
    private static Parser<string> Keyword() =>
        OneOf(Terms.Keyword("if"), Terms.Keyword("else"), Terms.Keyword("while"), Terms.Keyword("return"),
            Terms.Keyword("int"), Terms.Keyword("interface"), Terms.Keyword("internal"), Terms.Keyword("yield"))
        .Or(OneOf(Terms.Keyword("case"), Terms.Keyword("catch"), Terms.Keyword("const"), Terms.Keyword("class"), Terms.Keyword("if")));

    [GenerateParser(nameof(TryParseKeywordFallback))]
    private static Parser<string> KeywordFallback() =>
        Keyword().AndSkip(Literals.Char('!')).Or(Literals.Text(" \r\nunknown!")).Eof();

    [GenerateParser(nameof(TryParseKeywordCapture))]
    private static Parser<string> KeywordCapture() =>
        Literals.Char('!').SkipAnd(Capture(Keyword())).AndSkip(Literals.Char('1'))
            .Then(static span => span.ToString()).Eof();

    [GenerateParser(nameof(TryParseKeywordCustomWhitespace))]
    private static Parser<string> KeywordCustomWhitespace() =>
        Keyword().AndSkip(Literals.Char('!')).Eof()
            .WithWhiteSpaceParser(Capture(OneOrMany(Literals.Char('_'))));

    [GenerateParser(nameof(TryParseTextChoice))]
    private static Parser<string> TextChoice() =>
        OneOf(Terms.Text("if1"), Terms.Text("if"), Terms.Text("if12"), Terms.Text("Content-Encoding"),
            Terms.Text("Content-Type"), Terms.Text("Content"), Terms.Text("\u00e9\0x"), Terms.Text("\ud800\udc00"))
        .Or(OneOf(Terms.Text("line\r\nend"), Terms.Text("line\r\n"), Terms.Text("if")));

    [GenerateParser(nameof(TryParseTextChoiceFallback))]
    private static Parser<string> TextChoiceFallback() =>
        TextChoice().AndSkip(Literals.Char('!')).Or(Literals.Text(" \r\nunknown!")).Eof();

    [GenerateParser(nameof(TryParseTextChoiceCapture))]
    private static Parser<string> TextChoiceCapture() =>
        Literals.Char('!').SkipAnd(Capture(TextChoice())).AndSkip(Literals.Char('!'))
            .Then(static span => span.ToString()).Eof();

    [GenerateParser(nameof(TryParseTextChoiceCustomWhitespace))]
    private static Parser<string> TextChoiceCustomWhitespace() =>
        TextChoice().AndSkip(Literals.Char('!')).Eof()
            .WithWhiteSpaceParser(Capture(OneOrMany(Literals.Char('_'))));

    [GenerateParser(nameof(TryParseTextChoiceDiscarded))]
    private static Parser<char> TextChoiceDiscarded() => TextChoice().SkipAnd(Literals.Char('!')).Eof();

    [GenerateParser(nameof(TryParseTextChoicePosition))]
    private static Parser<(int Offset, int Line, int Column)> TextChoicePosition() =>
        TextChoice().Then(static (context, _) =>
            (context.Scanner.Cursor.Offset, context.Scanner.Cursor.Position.Line, context.Scanner.Cursor.Position.Column));

    // Wrapped keywords are dispatched on their prefixes. The run of lowercase letters is tried between them, so the
    // keywords after it only match when the input doesn't start with one.
    [GenerateParser(nameof(TryParsePrefixKeywords))]
    private static Parser<(int Value, int Offset)> PrefixKeywords() =>
        OneOf([
            .. PrefixKeywordWords.Take(PrefixKeywordFallback).Select(static (word, index) => Terms.Keyword(word).Then(index)),
            Terms.Pattern(static c => c is >= 'a' and <= 'z').Then(-1),
            .. PrefixKeywordWords.Skip(PrefixKeywordFallback).Select(static (word, index) => Terms.Keyword(word).Then(PrefixKeywordFallback + index)),
        ]).Then(static (context, value) => (value, context.Scanner.Cursor.Offset));

    [GenerateParser(nameof(TryParsePrefixOperators))]
    private static Parser<(int Value, int Offset)> PrefixOperators() =>
        OneOf(PrefixOperatorTexts.Select(static (op, index) => Terms.Text(op).Then(index)).ToArray())
            .Then(static (context, value) => (value, context.Scanner.Cursor.Offset));

    // An operator not followed by a space gives way to the next one
    [GenerateParser(nameof(TryParsePrefixOperatorSequences))]
    private static Parser<(int Value, int Offset)> PrefixOperatorSequences() =>
        OneOf(PrefixOperatorTexts.Select(static (op, index) => Literals.Text(op).AndSkip(Literals.Char(' ')).Then(index)).ToArray())
            .Then(static (context, value) => (value, context.Scanner.Cursor.Offset));

    // Matches up to the first space, new lines included
    [GenerateParser(nameof(TryParseScanPosition))]
    private static Parser<(string Text, int Offset, int Line, int Column)> ScanPosition() =>
        Literals.Scan(static span =>
        {
            var size = 0;

            while (size < span.Length && span[size] != ' ')
            {
                size++;
            }

            return size;
        }).Then(static (context, span) =>
            (span.ToString(), context.Scanner.Cursor.Offset, context.Scanner.Cursor.Position.Line, context.Scanner.Cursor.Position.Column));
}
