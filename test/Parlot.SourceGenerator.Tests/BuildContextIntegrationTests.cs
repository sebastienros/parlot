using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class BuildContextIntegrationTests
{
    [Fact]
    public async Task Package_Exposes_Context_And_Preserves_Build_Edit_Transitions()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        var directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "Parlot.SourceGenerator.Tests", Guid.NewGuid().ToString("N"))).FullName;

        try
        {
            var feed = Directory.CreateDirectory(Path.Combine(directory, "feed")).FullName;
            var packages = Path.Combine(directory, "packages");
            var version = "0.0.0-buildcontext." + Guid.NewGuid().ToString("N");
            var project = Path.Combine(directory, "Consumer.csproj");
            File.Copy(Path.Combine(repositoryRoot, "global.json"), Path.Combine(directory, "global.json"));

            // Pack once from the matching full-build outputs. Never rebuild shared product outputs in this test.
            await RunDotnet(repositoryRoot, "pack", "src/Parlot/Parlot.csproj", "--no-build", "--no-restore",
                "--configuration", configuration, "--output", feed, "-p:PackageVersion=" + version,
                "-p:IncludeSymbols=false", "-p:UseSharedCompilation=false");

            using (var package = ZipFile.OpenRead(Path.Combine(feed, $"Parlot.{version}.nupkg")))
            {
                var entry = package.GetEntry("buildTransitive/Parlot.props");
                Assert.NotNull(entry);
                using var stream = entry.Open();
                var props = XDocument.Load(stream);
                var exposed = props.Descendants("CompilerVisibleProperty").Select(static item => (string)item.Attribute("Include"));
                Assert.Contains("DesignTimeBuild", exposed);
                Assert.Contains("BuildingProject", exposed);
                Assert.Contains("BuildingInsideVisualStudio", exposed);
            }

            var versions = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Packages.props"));
            var polySharpVersion = (string)versions.Descendants("GlobalPackageReference")
                .Single(static item => (string)item.Attribute("Include") == "PolySharp").Attribute("Version");
            File.WriteAllText(project, ConsumerProject(version, polySharpVersion));
            var sourcePath = Path.Combine(directory, "Grammar.cs");
            File.WriteAllText(sourcePath, GrammarSource);

            var cacheOutput = await RunDotnet(repositoryRoot, "nuget", "locals", "global-packages", "--list");
            const string cachePrefix = "global-packages: ";
            Assert.StartsWith(cachePrefix, cacheOutput.Trim(), StringComparison.Ordinal);
            var dependencyCache = cacheOutput.Trim()[cachePrefix.Length..];

            // Keep the unique package isolated, and reuse restored dependencies without network access.
            await RunDotnet(directory, "restore", project, "--packages", packages,
                "--source", feed, "-p:RestoreFallbackFolders=" + dependencyCache, "-p:NuGetAudit=false");

            await CheckContext(isBuild: true, designTime: null, insideVisualStudio: null);
            await CheckContext(isBuild: false, designTime: "true", insideVisualStudio: "true");
            await CheckContext(isBuild: true, designTime: "false", insideVisualStudio: "true");
            await CheckContext(isBuild: false, designTime: "false", insideVisualStudio: "true");
            await CheckContext(isBuild: false, designTime: null, insideVisualStudio: "true");
            await CheckContext(isBuild: true, designTime: null, insideVisualStudio: null);

            async Task CheckContext(bool isBuild, string designTime, string insideVisualStudio)
            {
                var arguments = new List<string>
                {
                    "msbuild", project,
                    isBuild ? "-t:Rebuild;CaptureBuildContext" : "-t:CaptureBuildContext",
                    "-p:UseSharedCompilation=false", "-verbosity:quiet"
                };
                if (!isBuild)
                {
                    arguments.Add("-p:SkipCompilerExecution=true");
                    arguments.Add("-p:ProvideCommandLineArgs=true");
                }
                if (designTime is not null)
                {
                    arguments.Add("-p:DesignTimeBuild=" + designTime);
                }
                if (insideVisualStudio is not null)
                {
                    arguments.Add("-p:BuildingInsideVisualStudio=" + insideVisualStudio);
                }

                await RunDotnet(directory, arguments.ToArray());

                var properties = ReadProperties(Path.Combine(directory, "context.editorconfig"));
                Assert.Equal(designTime ?? "", properties["build_property.DesignTimeBuild"]);
                Assert.Equal(insideVisualStudio ?? "", properties["build_property.BuildingInsideVisualStudio"]);
                Assert.Equal(isBuild ? "true" : "false", properties["build_property.BuildingProject"]);

                var parseOptions = new CSharpParseOptions(LanguageVersion.Preview).WithFeatures(
                    new[] { new KeyValuePair<string, string>("InterceptorsNamespaces", "Consumer") });
                var compilation = CSharpCompilation.Create(
                    "BuildContext" + Guid.NewGuid().ToString("N"),
                    new[] { CSharpSyntaxTree.ParseText(GrammarSource, parseOptions, sourcePath, Encoding.UTF8) },
                    File.ReadAllLines(Path.Combine(directory, "references.txt"))
                        .Select(static path => MetadataReference.CreateFromImage(File.ReadAllBytes(path), filePath: path)),
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

                // The legacy reference set needs sibling generator output, even when Parlot must not run.
                Assert.Null(compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IsExternalInit"));
                Assert.Contains(compilation.GetDiagnostics(), static d => d.Id == "CS0518");
                var analyzers = File.ReadAllLines(Path.Combine(directory, "analyzers.txt"));
                var generatorPath = analyzers.Single(static path => Path.GetFileName(path) == "Parlot.SourceGenerator.dll");
                var polySharpPath = analyzers.Single(static path => Path.GetFileName(path) == "PolySharp.SourceGenerators.dll");
                var polySharp = Assembly.LoadFrom(polySharpPath).GetTypes()
                    .Where(static type => !type.IsAbstract && typeof(IIncrementalGenerator).IsAssignableFrom(type))
                    .Select(static type => ((IIncrementalGenerator)Activator.CreateInstance(type)).AsSourceGenerator())
                    .ToArray();
                Assert.NotEmpty(polySharp);

                var (result, updatedCompilation) = GeneratorDiagnosticsTests.RunGenerator(
                    compilation, properties, generatorPath, polySharp);
                Assert.DoesNotContain(result.Diagnostics, static d => d.Severity == DiagnosticSeverity.Error);
                Assert.DoesNotContain(updatedCompilation.GetDiagnostics(), static d => d.Severity == DiagnosticSeverity.Error);
                Assert.NotNull(updatedCompilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IsExternalInit"));

                if (isBuild)
                {
                    GeneratorDiagnosticsTests.AssertGeneratedParser(result);
                    var generatedFile = Assert.Single(Directory.GetFiles(
                        Path.Combine(directory, "obj", "generated"), "*.Parlot.g.cs", SearchOption.AllDirectories));
                    Assert.Contains("[global::System.Runtime.CompilerServices.InterceptsLocationAttribute(",
                        File.ReadAllText(generatedFile), StringComparison.Ordinal);
                }
                else
                {
                    Assert.Empty(result.Results[0].Diagnostics);
                    Assert.Empty(result.Results[0].GeneratedSources);
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Dictionary<string, string> ReadProperties(string path)
    {
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("build_property.", StringComparison.Ordinal))
            {
                var separator = line.IndexOf('=');
                properties.Add(line[..separator].Trim(), line[(separator + 1)..].Trim());
            }
        }
        return properties;
    }

    private static async Task<string> RunDotnet(string directory, params string[] arguments)
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
        Assert.True(process.ExitCode == 0, $"dotnet {string.Join(" ", arguments)}{Environment.NewLine}{stdout}{stderr}");
        return stdout;
    }

    private static string ConsumerProject(string version, string polySharpVersion) => $$"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>netstandard2.0</TargetFramework>
            <LangVersion>latest</LangVersion>
            <InterceptorsNamespaces>$(InterceptorsNamespaces);Consumer</InterceptorsNamespaces>
            <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
            <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Parlot" Version="{{version}}" />
            <PackageReference Include="PolySharp" Version="{{polySharpVersion}}" PrivateAssets="all" />
          </ItemGroup>
          <Target Name="CaptureBuildContext" DependsOnTargets="Compile">
            <Copy SourceFiles="$(GeneratedMSBuildEditorConfigFile)" DestinationFiles="context.editorconfig" />
            <WriteLinesToFile File="references.txt" Lines="@(ReferencePathWithRefAssemblies)" Overwrite="true" />
            <WriteLinesToFile File="analyzers.txt" Lines="@(Analyzer)" Overwrite="true" />
          </Target>
        </Project>
        """;

    private const string GrammarSource = """
        using Parlot.Fluent;
        using Parlot.SourceGenerator;

        namespace Consumer;

        internal readonly record struct ParserKey(
            bool AllowCharValues,
            bool DisallowSingleEquals,
            FloatingPointNumberType FloatingPointNumberType,
            IntegerNumberType IntegerNumberType,
            ArgumentSeparator ArgumentSeparator);

        internal enum FloatingPointNumberType { Double }
        internal enum IntegerNumberType { Int32 }
        internal enum ArgumentSeparator { Comma }

        internal static partial class Grammar
        {
            [GenerateParser]
            [IncludeUsings("Consumer")]
            [IncludeGenerators("PolySharp")]
            private static Parser<string> CreateParserGrammar(ParserKey key) => Parsers.Terms.Text("ok");

            private static Parser<string> CreateParser() => CreateParserGrammar(new ParserKey());

            public static string Parse(string text) => CreateParser().Parse(text);
        }
        """;
}
