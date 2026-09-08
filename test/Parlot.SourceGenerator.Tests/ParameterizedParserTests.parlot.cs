#nullable disable

using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

[IncludeUsings("Parlot.SourceGenerator.Tests")]
public static partial class ParameterizedGrammars
{
    [GenerateParser(nameof(TryParseChoice))]
    private static Parser<string> BuildChoice(ConditionalOptions options) =>
        If(() => options.Enabled, Terms.Text("yes"), Terms.Text("no"));

    [GenerateParser(nameof(TryParseBacktracking))]
    private static Parser<string> BuildBacktracking(bool enabled) =>
        If(
            () => enabled,
            Terms.Text("yes").AndSkip(Literals.Char('!')),
            Terms.Text("no").AndSkip(Literals.Char('!')))
        .Eof();

    [GenerateParser(nameof(TryParseMultiple))]
    private static Parser<string> BuildMultiple(int first, int second, string prefix) =>
        Literals.Char('x').Then(_ =>
            prefix + (first + second).ToString(System.Globalization.CultureInfo.InvariantCulture));

    [GenerateParser(nameof(TryParseSelected))]
    private static Parser<string> BuildSelected(int index, string expected) =>
        Select(() => index, Literals.Text("yes"), Literals.Text("no"))
            .When((_, value) => value == expected);

    [GenerateParser(nameof(TryParseSwitched))]
    private static Parser<string> BuildSwitched(int index) =>
        Literals.Char('x').Switch((parseContext, parsedValue) => index, Literals.Text("yes"), Literals.Text("no"));

    [GenerateParser(nameof(TryParseFallback))]
    private static Parser<string> BuildFallback(string prefix) =>
        Literals.Text("x").Then(value => prefix + value)
            .Else(context => prefix + context.Scanner.Cursor.Offset.ToString(
                System.Globalization.CultureInfo.InvariantCulture));

    [GenerateParser(nameof(TryParseRecursiveChoice))]
    private static Parser<string> BuildRecursiveChoice(bool enabled) =>
        Recursive<string>(self => OneOf(
            If(() => enabled, Between(Literals.Char('('), self, Literals.Char(')'))),
            Literals.Text("x")))
        .Eof();

    [GenerateParser(nameof(TryParseValues))]
    private static Parser<int> BuildValues(
        (int First, int Second) pair,
        int[,] values,
        int? optional) =>
        Literals.Char('x').Then(_ => pair.First + pair.Second + values[0, 0] + (optional ?? 0));

    [GenerateParser(nameof(TryParseNullable))]
    private static Parser<string> BuildNullable(string configuredValue) =>
        Literals.Char('x').Then(_ => configuredValue);

    [GenerateParser(nameof(TryParseEnclosingMembers))]
    private static Parser<string> BuildEnclosingMembers(string prefix) =>
        Literals.Char('x').Then(value => prefix + Name + ":" + value);

    [GenerateParser(nameof(TryParseCallback))]
    private static Parser<string> BuildCallback(CallbackOptions options) =>
        Literals.Char('x').Then(value => ParameterizedCallbacks.Transform(options, value));

    [GenerateParser(nameof(TryParseRequiredBang))]
    private static Parser<string> BuildRequiredBang() =>
        Literals.Text("x").AndSkip(Literals.Char('!').ElseError("Expected '!'.")).Eof();

    [GenerateParser(nameof(TryParseOverloadedDefault))]
    private static Parser<string> BuildOverloaded() => Literals.Text("default").Eof();

    [GenerateParser(nameof(TryParseOverloadedBoolean))]
    private static Parser<string> BuildOverloaded(bool enabled) =>
        If(() => enabled, Literals.Text("yes"), Literals.Text("no")).Eof();

    [GenerateParser(nameof(TryParseOverloadedInteger))]
    private static Parser<string> BuildOverloaded(int number) =>
        Literals.Char('x').Then(_ => number.ToString(System.Globalization.CultureInfo.InvariantCulture)).Eof();

    [GenerateParser(nameof(TryParseCountedPair))]
    private static Parser<int> BuildCountedPair(ConditionalCounter counter) =>
        Literals.Char('x').And(Literals.Char('x'))
            .Then(_ => (counter.Next() * 10) + counter.Next())
            .Eof();

    [GenerateParser(nameof(TryParseSymbols))]
    private static Parser<string> BuildSymbols(string context, int @class) =>
        Literals.Char('x').Then((parseContext, parsedValue) =>
            context
            + ":"
            + @class.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ":"
            + nameof(context))
        .Eof();

    [GenerateParser(nameof(TryParseInferredNames))]
    private static Parser<string> BuildInferredNames(string configuredValue) =>
        Literals.Char('x').Then(_ =>
        {
            var item = new { configuredValue };
            var tuple = (configuredValue, 1);
            return item.configuredValue + tuple.configuredValue;
        }).Eof();

    [GenerateParser(nameof(TryParseMethodGroup))]
    private static Parser<string> BuildMethodGroup() =>
        Literals.Char('x').Then(ParameterizedCallbacks.Format).Eof();
}
