#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class GeneratorDiagnosticsTests
{
    private const string Declaration = """
        public static partial class Grammar
        {
            public static partial bool TryParse(string text, out string value);
        }
        """;

    [Fact]
    public void Parameter_Used_To_Build_The_Graph_Is_Rejected()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build(string argument) => Parsers.Terms.Text(argument);
            }
            """;
        var declaration = Declaration.Replace(
            "string text, out",
            "string text, string argument, out",
            StringComparison.Ordinal);

        var (result, _) = RunStandalone(declaration, grammar);

        Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT021");
    }

    [Theory]
    [InlineData("return enabled ? Terms.Text(\"yes\") : Terms.Text(\"no\");")]
    [InlineData("var selected = enabled; return If(() => selected, Terms.Text(\"yes\"));")]
    [InlineData("enabled = true; return If(() => enabled, Terms.Text(\"yes\"));")]
    [InlineData("return If(() => enabled = true, Terms.Text(\"yes\"));")]
    [InlineData("System.Func<bool> predicate = () => enabled; return If(predicate, Terms.Text(\"yes\"));")]
    public void Factory_Parameters_Must_Be_Read_Only_And_Deferred(string body)
    {
        var declaration = Declaration.Replace(
            "string text, out",
            "string text, bool enabled, out",
            StringComparison.Ordinal);
        var grammar = $$"""
            using System;
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build(bool enabled) { {{body}} }
            }
            """;

        var (result, _) = RunStandalone(declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT021");
        Assert.DoesNotContain(result.Results.SelectMany(static item => item.GeneratedSources),
            static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("ref int value")]
    [InlineData("in int value")]
    [InlineData("out int value")]
    [InlineData("System.ReadOnlySpan<char> value")]
    public void Unsupported_Factory_Parameters_Report_A_Diagnostic(string parameter)
    {
        var grammar = $$"""
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build({{parameter}}) => Parsers.Terms.Text("yes");
            }
            """;

        var (result, _) = RunStandalone(Declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT009");
    }

    [Fact]
    public void Deferred_Callbacks_Are_Not_Executed_While_Extracting_Source()
    {
        CallbackProbe.Evaluations = 0;
        const string declaration = """
            public static partial class Grammar
            {
                public static partial bool TryParse(string text, out string value);
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() =>
                    If(
                        global::Parlot.SourceGenerator.Tests.CallbackProbe.Condition,
                        Literals.Text("x"))
                    .Then(global::Parlot.SourceGenerator.Tests.CallbackProbe.Convert);
            }
            """;

        var (result, compilation) = RunStandalone(declaration, grammar);

        AssertNoErrors(result, compilation);
        Assert.Equal(0, CallbackProbe.Evaluations);
    }

    [Fact]
    public void Captured_Local_Reports_A_Diagnostic()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build()
                {
                    var prefix = "hello";
                    return Parsers.Terms.Identifier().Then(value => prefix + value.ToString());
                }
            }
            """;

        var (result, _) = RunStandalone(Declaration, grammar);

        var diagnostic = Assert.Single(result.Diagnostics, static item => item.Id == "PARLOT015");
        Assert.Contains("prefix", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Static_Lambda_Generates_Standalone_Source()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() =>
                    Parsers.Terms.Identifier().Then(static value => value.ToString());
            }
            """;

        var (result, compilation) = RunStandalone(Declaration, grammar);

        AssertNoErrors(result, compilation);
        Assert.Contains(result.Results.SelectMany(static item => item.GeneratedSources),
            static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal));
    }

    [Fact]
    public void Class_Must_Be_Partial()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() => Parsers.Terms.Text("hello");
            }
            """;

        var (result, _) = RunStandalone(Declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT007");
    }

    [Fact]
    public void Method_Must_Be_Static()
    {
        const string declaration = """
            public partial class Grammar
            {
                public static partial bool TryParse(string text, out string value);
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private Parser<string> Build() => Parsers.Terms.Text("hello");
            }
            """;

        var (result, _) = RunStandalone(declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT008");
    }

    [Fact]
    public void Method_Must_Return_Parser()
    {
        const string grammar = """
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static string Build() => "hello";
            }
            """;

        var (result, _) = RunStandalone(Declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT010");
    }

    [Fact]
    public void Factory_In_Compile_Is_Rejected()
    {
        const string source = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                public static partial bool TryParse(string text, out string value);
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() => Parsers.Terms.Text("hello");
            }
            """;
        var compilation = CreateCompilation(source, "CompiledFactory", referenceParlot: true);

        var (result, _) = RunGenerator(compilation, DefaultOptions());

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT025");
    }

    [Theory]
    [InlineData(".NETCoreApp", "v7.0", LanguageVersion.CSharp12)]
    [InlineData(".NETStandard", "v2.0", LanguageVersion.CSharp12)]
    [InlineData(".NETCoreApp", "v10.0", LanguageVersion.CSharp11)]
    public void Unsupported_Target_Reports_A_Diagnostic(
        string identifier,
        string version,
        LanguageVersion languageVersion)
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() => Parsers.Terms.Text("hello");
            }
            """;
        var options = DefaultOptions();
        options["build_property.TargetFrameworkIdentifier"] = identifier;
        options["build_property.TargetFrameworkVersion"] = version;

        var (result, _) = RunStandalone(Declaration, grammar, options, languageVersion);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT026");
    }

    [Fact]
    public void Unavailable_Runtime_Helper_Is_A_Generation_Error()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                private static string Helper(string value) => value;
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() =>
                    Parsers.Terms.Text("hello").Then(Helper);
            }
            """;

        var (result, _) = RunStandalone(Declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT024");
    }

    [Fact]
    public void Design_Time_Emits_A_Stub_Without_Executing_The_Factory()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() =>
                    throw new System.InvalidOperationException("Do not execute");
            }
            """;
        var options = DefaultOptions();
        options["build_property.DesignTimeBuild"] = "true";

        var (result, compilation) = RunStandalone(Declaration, grammar, options);

        AssertNoErrors(result, compilation);
        var source = Assert.Single(result.Results.SelectMany(static item => item.GeneratedSources));
        Assert.Contains("Standalone parsers are generated during build.", source.SourceText.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void IncludeFiles_Rejects_Project_Root_Escape_Without_Disclosing_Path()
    {
        using var project = new TemporaryDirectory();
        const string secretName = "parlot-secret-do-not-disclose.cs";
        var grammar = $$"""
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                [IncludeFiles("../{{secretName}}")]
                private static Parser<string> Build() => Parsers.Terms.Text("hello");
            }
            """;
        var options = DefaultOptions(project.Path);

        var (result, _) = RunStandalone(
            Declaration,
            grammar,
            options,
            grammarPath: Path.Combine(project.Path, "Grammar.parlot.cs"));

        var diagnostic = Assert.Single(result.Diagnostics, static item => item.Id == "PARLOT016");
        Assert.DoesNotContain(secretName, diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(project.Path, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void IncludeFiles_Allows_A_Contained_Build_Only_Helper()
    {
        using var project = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(project.Path, "Included.cs"),
            """
            using Parlot.Fluent;
            internal static class Included
            {
                public static Parser<string> Create() => Parsers.Terms.Text("safe");
            }
            """);
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                [IncludeFiles("Included.cs")]
                private static Parser<string> Build() => Included.Create().Eof();
            }
            """;

        var (result, compilation) = RunStandalone(
            Declaration,
            grammar,
            DefaultOptions(project.Path),
            grammarPath: Path.Combine(project.Path, "Grammar.parlot.cs"));

        AssertNoErrors(result, compilation);
    }

    [Fact]
    public void IncludeFiles_Rejects_Oversized_Source()
    {
        using var project = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(project.Path, "Oversized.cs"), new string(' ', (1024 * 1024) + 1));
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                [IncludeFiles("Oversized.cs")]
                private static Parser<string> Build() => Parsers.Terms.Text("safe");
            }
            """;

        var (result, _) = RunStandalone(
            Declaration,
            grammar,
            DefaultOptions(project.Path),
            grammarPath: Path.Combine(project.Path, "Grammar.parlot.cs"));

        var diagnostic = Assert.Single(result.Diagnostics, static item => item.Id == "PARLOT018");
        Assert.Contains("1024 KiB per-file", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(project.Path, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void IncludeFiles_Rejects_Absolute_Path_Without_Disclosing_It()
    {
        using var project = new TemporaryDirectory();
        var absolutePath = Path.Combine(project.Path, "Secret.cs");
        var literal = Parlot.SourceGeneration.LiteralHelper.StringToLiteral(absolutePath);
        var grammar = $$"""
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                [IncludeFiles({{literal}})]
                private static Parser<string> Build() => Parsers.Terms.Text("safe");
            }
            """;

        var (result, _) = RunStandalone(
            Declaration,
            grammar,
            DefaultOptions(project.Path),
            grammarPath: Path.Combine(project.Path, "Grammar.parlot.cs"));

        var diagnostic = Assert.Single(result.Diagnostics, static item => item.Id == "PARLOT016");
        Assert.Contains("absolute paths are not allowed", diagnostic.GetMessage(), StringComparison.Ordinal);
        Assert.DoesNotContain(absolutePath, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void IncludeFiles_Rejects_Symbolic_Links()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var project = new TemporaryDirectory();
        using var outside = new TemporaryDirectory();
        File.WriteAllText(Path.Combine(outside.Path, "Secret.cs"), "internal static class Secret { }");
        Directory.CreateSymbolicLink(Path.Combine(project.Path, "Linked"), outside.Path);
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                [IncludeFiles("Linked/Secret.cs")]
                private static Parser<string> Build() => Parsers.Terms.Text("safe");
            }
            """;

        var (result, _) = RunStandalone(
            Declaration,
            grammar,
            DefaultOptions(project.Path),
            grammarPath: Path.Combine(project.Path, "Grammar.parlot.cs"));

        Assert.Single(result.Diagnostics, static item => item.Id == "PARLOT017");
    }

    [Fact]
    public void Keyword_And_Unicode_Factory_Identifiers_Generate_Valid_Source()
    {
        const string declaration = """
            namespace GeneratedIdentifierTests;
            public static partial class Grammar
            {
                private const int __Parlot_5_class_Core = 0;
                public static partial bool TryParseKeyword(string text, out string value);
                public static partial bool TryParseUnderscore(string text, out string value);
                public static partial bool TryParseUnicode(string text, out string value);
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            namespace GeneratedIdentifierTests;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParseKeyword))]
                private static Parser<string> @class() => Parsers.Terms.Text("keyword");
                [GenerateParser(nameof(TryParseUnderscore))]
                private static Parser<string> _class() => Parsers.Terms.Text("underscored");
                [GenerateParser(nameof(TryParseUnicode))]
                private static Parser<string> 解析() => Parsers.Terms.Text("unicode");
            }
            """;

        var (result, compilation) = RunStandalone(declaration, grammar);

        AssertNoErrors(result, compilation);
        Assert.Equal(3, result.Results.SelectMany(static item => item.GeneratedSources)
            .Count(static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal)));
    }

    [Fact]
    public void Generated_Line_Directives_Escape_Grammar_Path()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() =>
                    Parsers.Terms.Identifier().Then(static value => value.ToString());
            }
            """;
        var path = Path.Combine(Path.GetTempPath(), "quote\"#line\nGrammar.parlot.cs");

        var (result, compilation) = RunStandalone(Declaration, grammar, grammarPath: path);

        AssertNoErrors(result, compilation);
    }

    [Fact]
    public void Generated_Literals_Escape_Adversarial_Text()
    {
        var values = new[] { "\"quoted\"", "backslash\\", "line\r\nbreak", "#line 1 \"injected.cs\"", "\0" };
        var declarations = string.Join(
            Environment.NewLine,
            values.Select((value, index) =>
                $"internal const string Value{index} = {Parlot.SourceGeneration.LiteralHelper.StringToLiteral(value)};"));
        var tree = CSharpSyntaxTree.ParseText(
            $"internal static class EscapedValues {{{Environment.NewLine}{declarations}{Environment.NewLine}}}",
            new CSharpParseOptions(LanguageVersion.CSharp12));

        Assert.DoesNotContain(tree.GetDiagnostics(), static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    private static (GeneratorDriverRunResult Result, CSharpCompilation Compilation) RunStandalone(
        string declaration,
        string grammar,
        IReadOnlyDictionary<string, string>? globalOptions = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12,
        string grammarPath = "Grammar.parlot.cs")
    {
        var parseOptions = new CSharpParseOptions(
            languageVersion,
            preprocessorSymbols: ["NET", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var compilation = CreateCompilation(
            declaration,
            "StandaloneDiagnostics" + Guid.NewGuid().ToString("N"),
            parseOptions: parseOptions);
        return RunGenerator(
            compilation,
            globalOptions ?? DefaultOptions(),
            additionalTexts: [new StringAdditionalText(grammarPath, grammar)]);
    }

    private static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName,
        bool referenceParlot = false,
        CSharpParseOptions? parseOptions = null)
    {
        parseOptions ??= new CSharpParseOptions(LanguageVersion.CSharp12);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("Parlot", StringComparison.Ordinal))
            .Select(static path => MetadataReference.CreateFromFile(path))
            .ToList();
        if (referenceParlot)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Parlot.Fluent.Parser<>).Assembly.Location));
        }
        references.Add(MetadataReference.CreateFromFile(typeof(GeneratorDiagnosticsTests).Assembly.Location));

        return CSharpCompilation.Create(
            "Parlot.SourceGenerator.Tests." + assemblyName,
            [CSharpSyntaxTree.ParseText(source, parseOptions, "Grammar.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private static Dictionary<string, string> DefaultOptions(string? projectDirectory = null) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["build_property.MSBuildProjectDirectory"] = projectDirectory ?? Directory.GetCurrentDirectory(),
            ["build_property.TargetFramework"] = "net10.0",
            ["build_property.TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["build_property.TargetFrameworkVersion"] = "v10.0",
            ["build_property.DesignTimeBuild"] = "false"
        };

    private static void AssertNoErrors(GeneratorDriverRunResult result, CSharpCompilation compilation)
    {
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(compilation.GetDiagnostics(), static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
    }

    internal static (GeneratorDriverRunResult result, CSharpCompilation updatedCompilation) RunGenerator(
        CSharpCompilation compilation,
        IReadOnlyDictionary<string, string>? globalOptions,
        string? generatorPath = null,
        IEnumerable<ISourceGenerator>? additionalGenerators = null,
        IEnumerable<AdditionalText>? additionalTexts = null)
    {
        var config =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        var isPackagedGenerator = generatorPath is not null;
        generatorPath ??= Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../src/Parlot.SourceGenerator/bin",
            config,
            "netstandard2.0/Parlot.SourceGenerator.dll"));
        Assert.True(File.Exists(generatorPath), $"Generator assembly not found at {generatorPath}");

        var generatorAssembly = isPackagedGenerator
            ? Assembly.Load(File.ReadAllBytes(generatorPath))
            : Assembly.LoadFrom(generatorPath);
        var generatorType = generatorAssembly.GetType(
            "Parlot.SourceGenerator.ParserSourceGenerator",
            throwOnError: true)!;
        var generator = (IIncrementalGenerator)Activator.CreateInstance(generatorType)!;
        var optionsProvider = globalOptions is null
            ? null
            : new TestAnalyzerConfigOptionsProvider(globalOptions);
        var generators = new[] { generator.AsSourceGenerator() }
            .Concat(additionalGenerators ?? Array.Empty<ISourceGenerator>());
        var parseOptions = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var driver = CSharpGeneratorDriver.Create(
                generators,
                additionalTexts: additionalTexts,
                parseOptions: parseOptions,
                optionsProvider: optionsProvider)
            .RunGeneratorsAndUpdateCompilation(
                compilation,
                out var updatedCompilation,
                out _);

        return (driver.GetRunResult(), (CSharpCompilation)updatedCompilation);
    }

    private sealed class StringAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public StringAdditionalText(string path, string text)
        {
            Path = path;
            _text = SourceText.From(text);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    private sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
    {
        private static readonly AnalyzerConfigOptions Empty =
            new TestAnalyzerConfigOptions(new Dictionary<string, string>());
        private readonly AnalyzerConfigOptions _globalOptions;

        public TestAnalyzerConfigOptionsProvider(IReadOnlyDictionary<string, string> globalOptions)
        {
            _globalOptions = new TestAnalyzerConfigOptions(globalOptions);
        }

        public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => Empty;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => Empty;
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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = Directory.CreateDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "Parlot.SourceGenerator.Tests",
                Guid.NewGuid().ToString("N"))).FullName;
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

public static class CallbackProbe
{
    public static int Evaluations { get; set; }

    public static bool Condition()
    {
        Evaluations++;
        return true;
    }

    public static string Convert(string value)
    {
        Evaluations++;
        return value;
    }
}
