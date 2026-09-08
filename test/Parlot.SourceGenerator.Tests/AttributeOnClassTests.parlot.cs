using Parlot.Fluent;
using Parlot.SourceGenerator;

namespace Parlot.SourceGenerator.Tests;

[IncludeUsings("Parlot.SourceGenerator.Tests", "System.Text")]
public static partial class ClassLevelAttributeGrammars
{
    [GenerateParser(nameof(TryParseValue))]
    private static Parser<SimpleValue> BuildValue() =>
        Parsers.Terms.Identifier().Then(static value => new SimpleValue(value.ToString()));

    [GenerateParser(nameof(TryParseText))]
    [IncludeUsings("System.Linq")]
    private static Parser<string> BuildText() => Parsers.Terms.Text("hello");
}
