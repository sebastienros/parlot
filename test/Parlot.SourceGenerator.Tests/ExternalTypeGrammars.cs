using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Parsers that demonstrate the [IncludeFiles] attribute for including
/// types defined in separate files during source generation.
/// 
/// These are in a separate file because the minimal compilation created
/// by the source generator only includes the file containing the parser method,
/// plus any files specified by [IncludeFiles].
/// </summary>
public static partial class ExternalTypeGrammars
{
    // Test for [IncludeFiles] attribute - parser returns a type defined in TestAst.cs
    [GenerateParser]
    [IncludeFiles("TestAst.cs")]
    public static Parser<SimpleValue> SimpleValueParser()
    {
        return Terms.Identifier().Then(static x => new SimpleValue(x.ToString()!));
    }

    // Test for [IncludeFiles] with decimal numbers
    [GenerateParser]
    [IncludeFiles("TestAst.cs")]
    public static Parser<SimpleNumber> SimpleNumberParser()
    {
        return Terms.Decimal().Then(static x => new SimpleNumber(x));
    }
}
