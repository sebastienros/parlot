using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Parlot.SourceGeneration;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class TypeNameHelperTests
{
    public static TheoryData<Type, string> TypeNames => new()
    {
        { typeof(int), "global::System.Int32" },
        { typeof(string), "global::System.String" },
        { typeof(int?), "global::System.Nullable<global::System.Int32>" },
        { typeof(List<int>), "global::System.Collections.Generic.List<global::System.Int32>" },
        { typeof(Dictionary<string, List<int>>), "global::System.Collections.Generic.Dictionary<global::System.String, global::System.Collections.Generic.List<global::System.Int32>>" },
        { typeof((int, string)), "global::System.ValueTuple<global::System.Int32, global::System.String>" },
        { typeof(int[]), "global::System.Int32[]" },
        { typeof(int[,]), "global::System.Int32[,]" },
        { typeof(int[,,]), "global::System.Int32[,,]" },
        { typeof(int[][]), "global::System.Int32[][]" },
        { typeof(int[,][]), "global::System.Int32[,][]" },
        { typeof(int[][,]), "global::System.Int32[][,]" },
        { typeof(List<int>[]), "global::System.Collections.Generic.List<global::System.Int32>[]" },
        { typeof(List<int[,]>), "global::System.Collections.Generic.List<global::System.Int32[,]>" },
        { typeof(TypeNameProbes.Container.Nested), "global::Parlot.SourceGenerator.Tests.TypeNameProbes.Container.Nested" },
        { typeof(TypeNameProbes.Outer<int>.Inner<string>), "global::Parlot.SourceGenerator.Tests.TypeNameProbes.Outer<global::System.Int32>.Inner<global::System.String>" },
        { typeof(TypeNameProbes.Outer<int>.Plain), "global::Parlot.SourceGenerator.Tests.TypeNameProbes.Outer<global::System.Int32>.Plain" },
        { typeof(TypeNameProbes.Outer<int>.Plain.Deep<string, long>), "global::Parlot.SourceGenerator.Tests.TypeNameProbes.Outer<global::System.Int32>.Plain.Deep<global::System.String, global::System.Int64>" },
        { typeof(TypeNameProbes.Outer<List<int>>.Inner<int[]>[]), "global::Parlot.SourceGenerator.Tests.TypeNameProbes.Outer<global::System.Collections.Generic.List<global::System.Int32>>.Inner<global::System.Int32[]>[]" },
        { typeof(TypeNameProbes.Container.Generic<byte>), "global::Parlot.SourceGenerator.Tests.TypeNameProbes.Container.Generic<global::System.Byte>" },
    };

    [Theory]
    [MemberData(nameof(TypeNames))]
    public void Emits_Compilable_Fully_Qualified_Names(Type type, string expected)
    {
        var name = TypeNameHelper.GetTypeName(type);

        Assert.Equal(expected, name);

        // The name must resolve back to the same type when compiled, which rules out the
        // ambiguous spellings a flattened nested generic or a jagged rank produce.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(TypeNameHelperTests).Assembly.Location));
        var compilation = CSharpCompilation.Create(
            "TypeNameProbe",
            [CSharpSyntaxTree.ParseText($"static class Probe {{ public static {name} Value; }}")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        Assert.Empty(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));

        var field = compilation.GetTypeByMetadataName("Probe")!.GetMembers("Value").OfType<IFieldSymbol>().Single();
        var roundTrip = field.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var expectedDisplay = ExpectedDisplay(type);

        Assert.Equal(expectedDisplay, roundTrip);
    }

    [Fact]
    public void Rejects_Null()
    {
        Assert.Throws<ArgumentNullException>(() => TypeNameHelper.GetTypeName(null!));
    }

    private static string ExpectedDisplay(Type type)
    {
        // Roslyn prints keywords and the nullable shorthand, which the reflection name does not.
        return TypeNameHelper.GetTypeName(type)
            .Replace("global::System.Nullable<global::System.Int32>", "int?", StringComparison.Ordinal)
            .Replace("global::System.ValueTuple<global::System.Int32, global::System.String>", "(int, string)", StringComparison.Ordinal)
            .Replace("global::System.Int32", "int", StringComparison.Ordinal)
            .Replace("global::System.Int64", "long", StringComparison.Ordinal)
            .Replace("global::System.String", "string", StringComparison.Ordinal)
            .Replace("global::System.Byte", "byte", StringComparison.Ordinal);
    }
}

public static class TypeNameProbes
{
    public static class Container
    {
        public sealed class Nested;

        public sealed class Generic<T>;
    }

    public sealed class Outer<T>
    {
        public sealed class Inner<U>;

        public sealed class Plain
        {
            public sealed class Deep<V, W>;
        }
    }
}
