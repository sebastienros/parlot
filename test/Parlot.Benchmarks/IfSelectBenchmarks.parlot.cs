using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

public partial class IfSelectBenchmarks
{
    [GenerateParser(nameof(TryParseIf))]
    private static Parser<string> BuildIf(bool condition) =>
        If(() => condition, Literals.Text("42"));

    [GenerateParser(nameof(TryParseSelect))]
    private static Parser<string> BuildSelect(bool condition) =>
        Select(() => condition ? 0 : -1, Literals.Text("42"));

    [GenerateParser(nameof(TryParseIfElse))]
    private static Parser<string> BuildIfElse(bool condition) =>
        If(() => condition, Literals.Text("42"), Literals.Text("17"));

    [GenerateParser(nameof(TryParseSelectElse))]
    private static Parser<string> BuildSelectElse(bool condition) =>
        Select(() => condition ? 0 : 1, Literals.Text("42"), Literals.Text("17"));
}
