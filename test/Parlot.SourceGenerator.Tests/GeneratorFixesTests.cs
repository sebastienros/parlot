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
    [Theory]
    [InlineData("ab", "0:2:ab")]
    [InlineData(" ab", "1:3:ab")]
    [InlineData("\n\t ab", "3:5:ab")]
    public void Then_Position_Callback_Receives_The_Content_Offsets(string input, string expected)
    {
        Assert.True(Grammars.TryParseThenPositions(input, out var value));
        Assert.Equal(expected, value);

        // The same as the runtime parser
        var runtime = Fluent.Parsers.Terms.Text("ab")
            .Then(static (Fluent.ParseContext _, int start, int end, string value) => $"{start}:{end}:{value}");
        Assert.Equal(expected, runtime.Parse(input));
    }

    [Theory]
    [InlineData("ab", "0:2:ab")]
    [InlineData(" ab", null)]
    public void Then_Position_Callback_Receives_The_Literal_Offsets(string input, string? expected)
    {
        Assert.Equal(expected != null, Grammars.TryParseLiteralThenPositions(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(" ab", "1:3:ab")]
    [InlineData(" ac", "0:3: ac")]
    [InlineData("  ac", null)]
    public void Then_Position_Callback_Gives_Back_The_Skipped_White_Space_On_Failure(string input, string? expected)
    {
        Assert.Equal(expected != null, Grammars.TryParseThenPositionsFallback(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Discarded_Lambda_Parameters_Get_Distinct_Names()
    {
        Assert.True(Grammars.TryParseDiscards("abc", out var value));
        Assert.Equal(1 + 2 + 2, value);
        Assert.False(Grammars.TryParseDiscards("abd", out _));
    }

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
