using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class MinimumSdkCompatibilityTests
{
    [Fact]
    public async Task Packaged_Generator_Runs_In_Minimum_Supported_Compiler_Host()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
        var sdkManifest = Path.Combine(root, "test", "Parlot.SourceGenerator.Tests", "MinimumSdk", "global.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(sdkManifest));
        var sdkVersion = manifest.RootElement.GetProperty("sdk").GetProperty("version").GetString();
        var directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "Parlot.MinimumSdk.Tests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            File.Copy(sdkManifest, Path.Combine(directory, "global.json"));
            Assert.Equal(sdkVersion, (await BuildContextIntegrationTests.RunDotnet(directory, "--version")).Trim());

            var feed = Directory.CreateDirectory(Path.Combine(directory, "feed")).FullName;
            var version = "0.0.0-minimum-sdk." + Guid.NewGuid().ToString("N");
            await BuildContextIntegrationTests.RunDotnet(root, "pack", "src/Parlot.SourceGenerator/Parlot.SourceGenerator.csproj",
                "--no-build", "--no-restore", "--configuration", configuration, "--output", feed,
                "-p:PackageVersion=" + version);
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <configuration><packageSources><clear />
                  <add key="local" value="{feed}" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources></configuration>
                """);
            File.WriteAllText(Path.Combine(directory, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <LangVersion>12</LangVersion>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                    <WarningsAsErrors>$(WarningsAsErrors);CS9057</WarningsAsErrors>
                    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
                    <CompilerGeneratedFilesOutputPath>obj/generated</CompilerGeneratedFilesOutputPath>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Parlot.SourceGenerator" Version="{{version}}" PrivateAssets="all" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(directory, "Grammar.cs"), """
                namespace MinimumSdkConsumer;
                public static partial class Grammar
                {
                    public static partial bool TryParse(string text, out int value);
                }
                """);
            File.WriteAllText(Path.Combine(directory, "Grammar.parlot.cs"), """
                using Parlot.Fluent;
                using Parlot.SourceGenerator;
                namespace MinimumSdkConsumer;
                public static partial class Grammar
                {
                    [GenerateParser(nameof(TryParse))]
                    private static Parser<int> Build() => Parsers.Terms.Number<int>(NumberOptions.Integer).Eof();
                }
                """);
            File.WriteAllText(Path.Combine(directory, "Program.cs"), """
                if (!MinimumSdkConsumer.Grammar.TryParse(" 42", out var value) || value != 42 ||
                    MinimumSdkConsumer.Grammar.TryParse("42x", out _))
                    throw new System.InvalidOperationException("Generated parser failed.");
                System.Console.WriteLine(value);
                """);
            await BuildContextIntegrationTests.RunDotnet(directory, "restore",
                "--packages", Path.Combine(directory, "packages"), "-p:NuGetAudit=false");
            await BuildContextIntegrationTests.RunDotnet(directory, "build", "--no-restore",
                "--configuration", configuration, "-p:UseSharedCompilation=false");
            Assert.Contains(Directory.GetFiles(Path.Combine(directory, "obj", "generated"), "*.cs", SearchOption.AllDirectories),
                static path => Path.GetFileName(path).StartsWith("StandaloneParser", StringComparison.Ordinal));
            Assert.Equal("42", (await BuildContextIntegrationTests.RunDotnet(directory, "run",
                "--no-build", "--configuration", configuration)).Trim());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
