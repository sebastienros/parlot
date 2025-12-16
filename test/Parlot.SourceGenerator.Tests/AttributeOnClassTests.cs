using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Tests for attributes applied at the class level that should affect all parsers in the class.
/// </summary>
[IncludeFiles("TestAst.cs")]
[IncludeUsings("System.Collections.Generic", "System.Text")]
public static partial class ClassLevelAttributeGrammars
{
    // This parser inherits [IncludeFiles] and [IncludeUsings] from the class
    [GenerateParser]
    public static Parser<SimpleValue> InheritedAttributesParser()
    {
        return Terms.Identifier().Then(static x => new SimpleValue(x.ToString()!));
    }

    // This parser has its own [IncludeFiles] which adds to the class-level one
    [GenerateParser]
    [IncludeFiles("TestAst.cs")] // Can still specify at method level (will be combined)
    public static Parser<SimpleNumber> CombinedAttributesParser()
    {
        return Terms.Decimal().Then(static x => new SimpleNumber(x));
    }

    // This parser uses additional usings from the class level
    [GenerateParser]
    [IncludeUsings("System.Linq")]
    public static Parser<string> AdditionalUsingsParser()
    {
        return Terms.Text("hello").Then(static x => x.ToString()!);
    }
}
