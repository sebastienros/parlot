#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// End-to-end checks of the generated parsers built from <c>Grammars.parlot.cs</c>, together with
/// in-memory generations that inspect the emitted source.
/// </summary>
public class GeneratorFixesTests
{
    [Fact]
    public void Error_Messages_Are_Escaped()
    {
        Assert.True(Grammars.TryParseEscapedError("x", out var value));
        Assert.Equal('x', value);
        Assert.False(Grammars.TryParseEscapedError("y", out _));

        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build() => OneOf(
                    Literals.Text("a").ElseError("else \"quoted\"\\\n"),
                    Literals.Text("b").Error("plain \"quoted\"\\\n"),
                    Literals.Text("c").Error<string>("typed \"quoted\"\\\n"));
            }
            """;
        var (result, compilation) = Generate(Declaration, grammar);

        AssertNoErrors(result, compilation);
        var source = string.Join("\n", result.Results.SelectMany(static item => item.GeneratedSources).Select(static item => item.SourceText.ToString()));
        Assert.Contains("\"else \\\"quoted\\\"\\\\\\n\"", source, StringComparison.Ordinal);
        Assert.Contains("\"plain \\\"quoted\\\"\\\\\\n\"", source, StringComparison.Ordinal);
        Assert.Contains("\"typed \\\"quoted\\\"\\\\\\n\"", source, StringComparison.Ordinal);

        var parse = Load(compilation).GetType("Grammar")!.GetMethod("TryParse")!;
        object?[] arguments = ["a", null];
        Assert.True((bool)parse.Invoke(null, arguments)!);
        Assert.Equal("a", arguments[1]);
        arguments = ["b", null];
        Assert.False((bool)parse.Invoke(null, arguments)!);
    }

    internal const string Declaration = """
        public static partial class Grammar
        {
            public static partial bool TryParse(string text, out string value);
        }
        """;

    internal static Assembly Load(CSharpCompilation compilation)
    {
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join("\n", emitted.Diagnostics));
        return Assembly.Load(stream.ToArray());
    }

    internal static void AssertNoErrors(GeneratorDriverRunResult result, CSharpCompilation compilation)
    {
        Assert.Empty(result.Diagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        Assert.Empty(compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
    }

    internal static (GeneratorDriverRunResult Result, CSharpCompilation Compilation) Generate(string source, string grammar)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12,
            preprocessorSymbols: ["NET", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(static path => !Path.GetFileName(path).StartsWith("Parlot", StringComparison.Ordinal))
            .Select(static path => MetadataReference.CreateFromFile(path)).ToList();
        var compilation = CSharpCompilation.Create("GeneratorFixes" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, "Grammar.cs")], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
        var options = new System.Collections.Generic.Dictionary<string, string>
        {
            ["build_property.TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["build_property.TargetFrameworkVersion"] = "v10.0",
            ["build_property.DesignTimeBuild"] = "false"
        };
        return GeneratorDiagnosticsTests.RunGenerator(compilation, options, additionalTexts: [new GrammarText(grammar)]);
    }

    private sealed class GrammarText : AdditionalText
    {
        private readonly Microsoft.CodeAnalysis.Text.SourceText _text;

        public GrammarText(string text)
        {
            _text = Microsoft.CodeAnalysis.Text.SourceText.From(text);
        }

        public override string Path => "Grammar.parlot.cs";

        public override Microsoft.CodeAnalysis.Text.SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => _text;
    }
}
