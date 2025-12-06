using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;
using PortableExecutableReference = Microsoft.CodeAnalysis.PortableExecutableReference;
using Parlot;
using Parlot.Fluent;

#nullable enable

namespace Parlot.SourceGenerator.Tests;

public static class ExampleSourceGeneratorTests
{
    [Fact]
    public static void GenerateParserAttribute_Creates_Generated_Parser()
    {
        // User code that defines a descriptor method using the Parlot DSL and marks it with [GenerateParser].
        const string userSource = @"
#nullable enable
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

public static partial class MyGrammar
{
    // Descriptor method: executed at compile-time by the source generator to obtain a Parser<string> instance.
    // The attribute also requests that a public factory MyGrammar.Hello() is generated.
    [GenerateParser(""Hello"")]
    public static global::Parlot.Fluent.Parser<string> HelloDescriptor()
    {
        return Terms.Text(""hello"");
    }

    [GenerateParser()]
    public static global::Parlot.Fluent.Parser<double> ExpressionDescriptor()
    {
        var value = OneOf(
            Terms.Text(""one"").Then(_ => 1.0),
            Terms.Text(""two"").Then(_ => 2.0),
            Terms.Text(""three"").Then(_ => 3.0)
        );

        var tail = ZeroOrMany(Terms.Char('+').SkipAnd(value));

        return value.And(tail).Then(tuple =>
        {
            var total = tuple.Item1;
            foreach (var v in tuple.Item2)
            {
                total += v;
            }
            return total;
        });
    }
}

";

        var syntaxTree = CSharpSyntaxTree.ParseText(
            userSource,
            new CSharpParseOptions(LanguageVersion.Preview),
            cancellationToken: TestContext.Current.CancellationToken);

        // Build a compilation representing the user project that references Parlot and the source generator assembly.
        var parlotAssembly = typeof(ParseContext).Assembly;
        Assert.False(string.IsNullOrEmpty(parlotAssembly.Location), "Parlot assembly location is empty.");

        var references = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string)?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList()
            ?? new List<MetadataReference>();

        void AddReference(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (!references.OfType<PortableExecutableReference>().Any(r =>
                    string.Equals(r.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        AddReference(parlotAssembly.Location);
        AddReference(typeof(global::Parlot.SourceGenerator.GenerateParserAttribute).Assembly.Location);

        var compilation = CSharpCompilation.Create(
            assemblyName: "Parlot.SourceGenerator.Tests.Dynamic",
            syntaxTrees: new[] { syntaxTree },
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                assemblyIdentityComparer: DesktopAssemblyIdentityComparer.Default));

        var referenceAssemblies = compilation.References
            .Select(r => compilation.GetAssemblyOrModuleSymbol(r))
            .OfType<IAssemblySymbol>()
            .Select(a => a.Identity.Name)
            .ToArray();

        Assert.Contains("Parlot", referenceAssemblies);
        Assert.Contains(parlotAssembly.GetExportedTypes(), t => t.FullName == "Parlot.Fluent.Parser`1");
        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var parserIdentifier = syntaxTree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes().OfType<GenericNameSyntax>()
            .FirstOrDefault(id => id.Identifier.ValueText == "Parser");
        Assert.NotNull(parserIdentifier);
        var parserSymbolInfo = semanticModel.GetSymbolInfo(parserIdentifier!, TestContext.Current.CancellationToken);
        Assert.True(
            parserSymbolInfo.Symbol is not null,
            $"Symbol not resolved: reason={parserSymbolInfo.CandidateReason}, candidates={string.Join(", ", parserSymbolInfo.CandidateSymbols.Select(s => s.ToDisplayString()))}, diagnostics={string.Join(Environment.NewLine, semanticModel.GetDiagnostics(cancellationToken: TestContext.Current.CancellationToken))}");
        Assert.NotNull(compilation.GetTypeByMetadataName("Parlot.Fluent.Parser`1"));
        Assert.DoesNotContain(
            compilation.GetDiagnostics(TestContext.Current.CancellationToken),
            d => d.Severity == DiagnosticSeverity.Error);

        // Run the Parlot source generator.
        var generator = new Parlot.SourceGenerator.ParserSourceGenerator();
        CSharpGeneratorDriver
            .Create(new[] { generator.AsSourceGenerator() }, parseOptions: (CSharpParseOptions)syntaxTree.Options)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updatedCompilation,
                out var diagnostics,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var generatedTree = updatedCompilation.SyntaxTrees.FirstOrDefault(st => st.FilePath.Contains(".Parlot.g.cs", StringComparison.Ordinal));
        var generatedSource = generatedTree?.ToString();

        // Emit the updated compilation to an in-memory assembly.
        using var peStream = new System.IO.MemoryStream();
        var emitResult = updatedCompilation.Emit(peStream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(
            emitResult.Success,
            string.Join(Environment.NewLine, emitResult.Diagnostics.Select(d => d.ToString())) +
            (generatedSource is not null ? Environment.NewLine + generatedSource : string.Empty));

        peStream.Position = 0;
        var assembly = Assembly.Load(peStream.ToArray());

        // Invoke the generated parser factory and execute it.
        var grammarType = assembly.GetType("MyGrammar");
        Assert.NotNull(grammarType);

        var helloFactory = grammarType!.GetProperty("Hello", BindingFlags.Public | BindingFlags.Static)!.GetGetMethod();
        Assert.NotNull(helloFactory);

        var parser = Assert.IsAssignableFrom<Parser<string>>(helloFactory!.Invoke(null, null));
        var scanner = new Scanner("   hello");
        var context = new ParseContext(scanner);
        var result = new ParseResult<string>();

        var success = parser.Parse(context, ref result);

        Assert.True(success);
        Assert.Equal("hello", result.Value);

        var exprFactory = grammarType.GetProperty("ExpressionDescriptor_Parser", BindingFlags.Public | BindingFlags.Static)!.GetGetMethod();
        Assert.NotNull(exprFactory);
    }
}
