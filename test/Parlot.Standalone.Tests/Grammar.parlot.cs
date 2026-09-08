using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class Grammar
{
    [GenerateParser(nameof(TryParseNumber))]
    private static Parser<int> Number() => Terms.Number<int>(NumberOptions.Integer).Eof();

    [GenerateParser(nameof(TryParsePrefix))]
    private static Parser<string> Prefix() => Literals.Text("hello");

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

    [GenerateParser(nameof(TryParseRecursive))]
    private static Parser<int> Recursive()
    {
        var expression = Deferred<int>();
        expression.Parser = Literals.Char('x').Then(0)
            .Or(Between(Literals.Char('('), expression, Literals.Char(')')).Then(static depth => depth + 1));
        return expression.Eof();
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
}
