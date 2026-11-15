using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class TokenBenchmarks
{
    [GenerateParser(nameof(TryParseNames))]
    private static Parser<int> BuildNames() => Names(asToken: false);

    [GenerateParser(nameof(TryParseTokens))]
    private static Parser<int> BuildTokens() => Names(asToken: true);

    private static Parser<int> Names(bool asToken)
    {
        var name = Capture(Separated(Literals.Char('.'), Literals.Identifier()));

        return Separated(Literals.Char('+'), asToken ? AsToken(name) : name).Then(static names => names.Count).Eof();
    }
}
