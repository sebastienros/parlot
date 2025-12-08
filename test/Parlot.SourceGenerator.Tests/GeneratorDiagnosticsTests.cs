using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class GeneratorDiagnosticsTests
{
    [Fact]
    public void Duplicate_GenerateParser_Names_Report_Diagnostic()
    {
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class DuplicateGrammar
{
    [GenerateParser(""Same"")]
    [GenerateParser(""Same"")]
    public static Parser<string> Foo() => Terms.Text(""x"");
}
";

        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(source, parseOptions);

        var references = new List<MetadataReference>();
        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trusted))
        {
            foreach (var path in trusted!.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        void AddReference(string path)
        {
            if (!references.OfType<PortableExecutableReference>().Any(r => string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        AddReference(typeof(global::Parlot.Fluent.ParseContext).Assembly.Location);
        AddReference(typeof(global::Parlot.SourceGenerator.GenerateParserAttribute).Assembly.Location);

        var compilation = CSharpCompilation.Create(
            assemblyName: "Parlot.SourceGenerator.Tests.DuplicateNames",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var config = 
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        var generatorPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Parlot.SourceGenerator/bin", config, "netstandard2.0/Parlot.SourceGenerator.dll"));
        Assert.True(File.Exists(generatorPath), $"Generator assembly not found at {generatorPath}");

        var generatorAssembly = Assembly.LoadFrom(generatorPath);
        var generatorType = generatorAssembly.GetType("Parlot.SourceGenerator.ParserSourceGenerator", throwOnError: true)!;
        var generator = (IIncrementalGenerator)Activator.CreateInstance(generatorType)!;

        var sourceGenerator = generator.AsSourceGenerator();
        CSharpGeneratorDriver.Create(new[] { sourceGenerator }, parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var diagnostics);

        Assert.Contains(diagnostics, d => d.Id == "PARLOT010");
    }
}
