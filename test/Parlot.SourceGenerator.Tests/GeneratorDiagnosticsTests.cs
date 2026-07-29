using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class GeneratorDiagnosticsTests
{
    [Fact]
    public void Method_With_Parameters_Is_Not_Generated()
    {
        // [GenerateParser] should only work on parameterless methods
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class ParameterizedGrammar
{
    [GenerateParser]
    public static Parser<string> Foo(string arg) => Terms.Text(arg);
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
            assemblyName: "Parlot.SourceGenerator.Tests.ParameterizedMethod",
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
        var driver = CSharpGeneratorDriver.Create(new[] { sourceGenerator }, parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var generatorDiagnostics);
        
        // The method should generate a PARLOT009 error since it has parameters
        var result = driver.GetRunResult();
        var parserDiagnostics = result.Diagnostics.Where(d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal)).ToList();
        Assert.Single(parserDiagnostics);
        Assert.Equal("PARLOT009", parserDiagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, parserDiagnostics[0].Severity);
        Assert.Contains("Foo", parserDiagnostics[0].GetMessage());
    }

    [Fact]
    public void Class_Must_Be_Partial()
    {
        // [GenerateParser] should only work on partial classes
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static class NonPartialGrammar
{
    [GenerateParser]
    public static Parser<string> Foo() => Terms.Text(""hello"");
}
";

        var (result, _) = RunGenerator(source, "NonPartialClass");
        
        var parserDiagnostics = result.Diagnostics.Where(d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal)).ToList();
        Assert.Single(parserDiagnostics);
        Assert.Equal("PARLOT007", parserDiagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, parserDiagnostics[0].Severity);
        Assert.Contains("NonPartialGrammar", parserDiagnostics[0].GetMessage());
    }

    [Fact]
    public void Method_Must_Be_Static()
    {
        // [GenerateParser] should only work on static methods
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public partial class InstanceMethodGrammar
{
    [GenerateParser]
    public Parser<string> Foo() => Terms.Text(""hello"");
}
";

        var (result, _) = RunGenerator(source, "InstanceMethod");
        
        var parserDiagnostics = result.Diagnostics.Where(d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal)).ToList();
        Assert.Single(parserDiagnostics);
        Assert.Equal("PARLOT008", parserDiagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, parserDiagnostics[0].Severity);
        Assert.Contains("Foo", parserDiagnostics[0].GetMessage());
    }

    [Fact]
    public void Method_Must_Return_Parser()
    {
        // [GenerateParser] should only work on methods returning Parser<T>
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class InvalidReturnGrammar
{
    [GenerateParser]
    public static string Foo() => ""hello"";
}
";

        var (result, _) = RunGenerator(source, "InvalidReturn");
        
        var parserDiagnostics = result.Diagnostics.Where(d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal)).ToList();
        Assert.Single(parserDiagnostics);
        Assert.Equal("PARLOT010", parserDiagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, parserDiagnostics[0].Severity);
        Assert.Contains("Foo", parserDiagnostics[0].GetMessage());
    }

    [Theory]
    [InlineData(6, false)]
    [InlineData(7, false)]
    [InlineData(8, true)]
    [InlineData(10, true)]
    public void Number_Literal_Selects_Fast_Path_By_Target_Framework(int targetFrameworkVersion, bool usesFastPath)
    {
        var context = new global::Parlot.SourceGeneration.SourceGenerationContext(
            targetFramework: new global::Parlot.SourceGeneration.TargetFrameworkInfo(
                global::Parlot.SourceGeneration.TargetFrameworkIdentifier.NetCoreApp,
                new Version(targetFrameworkVersion, 0)));
        var result = new TestLongNumberLiteral().GenerateSource(context);
        var generated = string.Join(Environment.NewLine, result.Body);

        Assert.Equal(usesFastPath, generated.Contains("Numbers.TryParseNumber<", StringComparison.Ordinal));
    }

    [Fact]
    public void Literal_OneOf_Does_Not_Capture_Position()
    {
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class OneOfGrammar
{
    [GenerateParser]
    public static Parser<string> Choice() => OneOf(Literals.Text(""a""), Literals.Text(""b""));
}
";

        var (result, updatedCompilation) = RunGenerator(source, "LiteralOneOf");
        var generated = string.Join(
            Environment.NewLine,
            result.Results.SelectMany(static r => r.GeneratedSources).Select(static s => s.SourceText.ToString()));

        Assert.DoesNotContain("cursor.Position", generated, StringComparison.Ordinal);
        Assert.DoesNotContain(updatedCompilation.GetDiagnostics(), static d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void DesignTimeBuild_Skips_Parlot_Generation_And_Diagnostics()
    {
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class DesignTimeGrammar
{
    [GenerateParser]
    public static Parser<string> Foo(string arg) => Terms.Text(arg);
}
";

        var (result, _) = RunGenerator(
            source,
            "DesignTimeNoOp",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.DesignTimeBuild"] = "true",
                ["build_property.TargetFramework"] = "net10.0",
                ["build_property.TargetFrameworkIdentifier"] = ".NETCoreApp",
                ["build_property.TargetFrameworkVersion"] = "v10.0"
            });

        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal));
        Assert.Empty(result.Results.SelectMany(r => r.GeneratedSources));
    }

    [Fact]
    public void VisualStudio_LiveAnalysis_Context_Skips_Parlot_Generation_And_Diagnostics()
    {
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class VisualStudioGrammar
{
    [GenerateParser]
    public static Parser<string> Foo(string arg) => Terms.Text(arg);
}
";

        var (result, _) = RunGenerator(
            source,
            "VisualStudioNoOp",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["build_property.DesignTimeBuild"] = "false",
                ["build_property.BuildingInsideVisualStudio"] = "true",
                ["build_property.BuildingProject"] = "false",
                ["build_property.TargetFramework"] = "net10.0",
                ["build_property.TargetFrameworkIdentifier"] = ".NETCoreApp",
                ["build_property.TargetFrameworkVersion"] = "v10.0"
            });

        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal));
        Assert.Empty(result.Results.SelectMany(r => r.GeneratedSources));
    }

    private static (GeneratorDriverRunResult result, CSharpCompilation updatedCompilation) RunGenerator(
        string source,
        string assemblyName,
        IReadOnlyDictionary<string, string> globalOptions = null)
    {
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
            assemblyName: $"Parlot.SourceGenerator.Tests.{assemblyName}",
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
        var optionsProvider = globalOptions is null
            ? null
            : new TestAnalyzerConfigOptionsProvider(globalOptions);

        var driver = CSharpGeneratorDriver.Create(new[] { sourceGenerator }, parseOptions: parseOptions, optionsProvider: optionsProvider)
            .RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out var generatorDiagnostics);

        return (driver.GetRunResult(), (CSharpCompilation)updatedCompilation);
    }

    [Fact]
    public void Lambda_With_Closure_Reports_Error()
    {
        // [GenerateParser] should report error when lambda captures variables
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class ClosureGrammar
{
    [GenerateParser]
    public static Parser<string> Foo()
    {
        var prefix = ""hello"";
        return Terms.Identifier().Then(x => prefix + x.ToString());
    }
}
";

        var (result, _) = RunGenerator(source, "ClosureTest");
        
        var parserDiagnostics = result.Diagnostics.Where(d => d.Id.StartsWith("PARLOT", StringComparison.Ordinal)).ToList();
        Assert.Single(parserDiagnostics);
        Assert.Equal("PARLOT015", parserDiagnostics[0].Id);
        Assert.Equal(DiagnosticSeverity.Error, parserDiagnostics[0].Severity);
        Assert.Contains("Foo", parserDiagnostics[0].GetMessage());
        Assert.Contains("prefix", parserDiagnostics[0].GetMessage());
    }

    [Fact]
    public void Static_Lambda_Does_Not_Report_Closure_Error()
    {
        // Static lambdas should work without closure errors
        const string source = @"
using Parlot.SourceGenerator;
using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

public static partial class StaticLambdaGrammar
{
    [GenerateParser]
    public static Parser<string> Foo()
    {
        return Terms.Identifier().Then(static x => x.ToString());
    }
}
";

        var (result, _) = RunGenerator(source, "StaticLambdaTest");
        
        // Should not have PARLOT015 (closure error)
        var closureErrors = result.Diagnostics.Where(d => d.Id == "PARLOT015").ToList();
        Assert.Empty(closureErrors);
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty = new TestAnalyzerConfigOptions(new Dictionary<string, string>());
        private readonly AnalyzerConfigOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
        {
            _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree)
            => Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            => Empty;
    }

    private sealed class TestAnalyzerConfigOptions : AnalyzerConfigOptions
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public TestAnalyzerConfigOptions(IReadOnlyDictionary<string, string> values)
        {
            _values = values;
        }

        public override bool TryGetValue(string key, out string value)
        {
            if (_values.TryGetValue(key, out var found))
            {
                value = found;
                return true;
            }

            value = "";
            return false;
        }
    }

    private sealed class TestLongNumberLiteral : global::Parlot.Fluent.NumberLiteralBase<long>
    {
        public override bool TryParseNumber(
            ReadOnlySpan<char> s,
            System.Globalization.NumberStyles style,
            IFormatProvider provider,
            out long value) =>
            long.TryParse(s, style, provider, out value);
    }
}
