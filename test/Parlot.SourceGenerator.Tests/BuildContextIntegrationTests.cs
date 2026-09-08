#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class BuildContextIntegrationTests
{
    private const string Declaration = """
        namespace BuildContextConsumer;
        public static partial class Grammar
        {
            public static partial bool TryParse(string text, out string value);
        }
        """;

    private const string Grammar = """
        using Parlot.Fluent;
        using Parlot.SourceGenerator;
        namespace BuildContextConsumer;
        public static partial class Grammar
        {
            [GenerateParser(nameof(TryParse))]
            private static Parser<string> Build() => Parsers.Terms.Text("ok").Eof();
        }
        """;

    public static TheoryData<string?, string?, string?, bool> BuildContexts
    {
        get
        {
            var data = new TheoryData<string?, string?, string?, bool>
            {
                { null, null, null, false },
                { "false", "true", "true", false },
                { "true", "false", "true", true },
                { "false", "false", "true", true },
                { null, "false", "true", true },
                { "false", null, "true", false }
            };
            return data;
        }
    }

    [Theory]
    [MemberData(nameof(BuildContexts))]
    public void Build_Context_Selects_Implementation_Or_Design_Time_Stub(
        string? designTimeBuild,
        string? buildingProject,
        string? buildingInsideVisualStudio,
        bool expectsStub)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["build_property.TargetFramework"] = "net10.0",
            ["build_property.TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["build_property.TargetFrameworkVersion"] = "v10.0",
            ["build_property.MSBuildProjectDirectory"] = Directory.GetCurrentDirectory()
        };
        Add("DesignTimeBuild", designTimeBuild);
        Add("BuildingProject", buildingProject);
        Add("BuildingInsideVisualStudio", buildingInsideVisualStudio);

        var (result, compilation) = GeneratorDiagnosticsTests.RunGenerator(
            CreateCompilation(),
            options,
            additionalTexts: [new GrammarText(Grammar)]);

        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.DoesNotContain(compilation.GetDiagnostics(), static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var sources = result.Results.SelectMany(static item => item.GeneratedSources).ToArray();

        if (expectsStub)
        {
            var source = Assert.Single(sources);
            Assert.Contains("Standalone parsers are generated during build.", source.SourceText.ToString(), StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains(sources, static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal));
            Assert.Contains(sources, static source => source.HintName.StartsWith("Parlot.StandaloneRuntime.", StringComparison.Ordinal));
            Assert.DoesNotContain(sources,
                static source => source.SourceText.ToString().Contains("InterceptsLocation", StringComparison.Ordinal));
        }

        void Add(string name, string? value)
        {
            if (value is not null)
            {
                options["build_property." + name] = value;
            }
        }
    }

    internal static async Task<string> RunDotnet(string directory, params string[] arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = directory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        process.StartInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        process.StartInfo.Environment["DOTNET_NOLOGO"] = "true";
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(2));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync();
            throw;
        }

        var stdout = await output;
        var stderr = await error;
        Assert.True(
            process.ExitCode == 0,
            $"dotnet {string.Join(" ", arguments)}{Environment.NewLine}{stdout}{stderr}");
        return stdout;
    }

    private static CSharpCompilation CreateCompilation()
    {
        var parseOptions = new CSharpParseOptions(
            LanguageVersion.CSharp12,
            preprocessorSymbols: ["NET", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("Parlot", StringComparison.Ordinal))
            .Select(static path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(
            "BuildContext" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(Declaration, parseOptions, "Grammar.cs")],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true));
    }

    private sealed class GrammarText : AdditionalText
    {
        private readonly SourceText _text;

        public GrammarText(string text)
        {
            _text = SourceText.From(text);
        }

        public override string Path => "Grammar.parlot.cs";

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }
}
