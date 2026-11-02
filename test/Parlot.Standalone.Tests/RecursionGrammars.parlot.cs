using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Standalone.Tests;

public static partial class RecursionGrammars
{
    [GenerateParser(nameof(List))]
    public static Parser<int> BuildList() => ListGrammar();

    [GenerateParser(nameof(LimitedList), MaxRecursionDepth = 3)]
    public static Parser<int> BuildLimitedList() => ListGrammar();

    [GenerateParser(nameof(Negation))]
    public static Parser<int> BuildNegation() => NegationGrammar();

    [GenerateParser(nameof(LimitedNegation), MaxRecursionDepth = 3)]
    public static Parser<int> BuildLimitedNegation() => NegationGrammar();

    [GenerateParser(nameof(Prefix))]
    public static Parser<int> BuildPrefix() => PrefixGrammar();

    [GenerateParser(nameof(LimitedPrefix), MaxRecursionDepth = 2)]
    public static Parser<int> BuildLimitedPrefix() => PrefixGrammar();

    // The limit, as callbacks read it from the context
    [GenerateParser(nameof(LimitOfContext), MaxRecursionDepth = 7)]
    public static Parser<int> BuildLimitOfContext() => Literals.Char('x').Then(static (context, _) => context.MaxRecursionDepth);

    // The nesting depth of brackets around "item"
    private static Parser<int> ListGrammar() =>
        Recursive<int>(static list => Between(Literals.Char('['), list, Literals.Char(']')).Then(static depth => depth + 1)
            .Or(Literals.Text("item").Then(0)));

    // The number of '-' before "a"
    private static Parser<int> NegationGrammar() =>
        Literals.Char('a').Then(0).Unary((Literals.Char('-'), static count => count + 1));

    // A table with only prefix levels
    private static Parser<int> PrefixGrammar() =>
        Literals.Char('a').Then(0).Operators()
            .Unary((Literals.Char('-'), static count => count + 1));
}
