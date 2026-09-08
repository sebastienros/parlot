using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class StandalonePackageTests
{
    [Fact]
    public async Task Packaged_Generator_Produces_A_Dependency_Free_Library_And_Downstream_Application()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
        var directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "Parlot.StandalonePackage.Tests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var feed = Directory.CreateDirectory(Path.Combine(directory, "feed")).FullName;
            var author = Directory.CreateDirectory(Path.Combine(directory, "author")).FullName;
            var consumer = Directory.CreateDirectory(Path.Combine(directory, "consumer")).FullName;
            var version = "0.0.0-standalone." + Guid.NewGuid().ToString("N");
            File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), $"""
                <configuration><packageSources><clear />
                  <add key="local" value="{feed}" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources></configuration>
                """);

            await BuildContextIntegrationTests.RunDotnet(root, "pack", "src/Parlot.SourceGenerator/Parlot.SourceGenerator.csproj",
                "--no-build", "--no-restore", "--configuration", configuration, "--output", feed,
                "-p:PackageVersion=" + version);
            using (var package = ZipFile.OpenRead(Path.Combine(feed, $"Parlot.SourceGenerator.{version}.nupkg")))
            {
                Assert.NotNull(package.GetEntry("analyzers/dotnet/cs/Parlot.SourceGenerator.dll"));
                Assert.NotNull(package.GetEntry("analyzers/dotnet/cs/Parlot.dll"));
                Assert.DoesNotContain(package.Entries, static entry => entry.FullName.StartsWith("lib/", StringComparison.Ordinal));
                AssertNoPackageDependencies(package);
            }

            File.WriteAllText(Path.Combine(author, "Author.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <LangVersion>12</LangVersion>
                    <PackageId>Standalone.Author</PackageId>
                    <Version>{{version}}</Version>
                    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
                  </PropertyGroup>
                  <ItemGroup>
                    <PackageReference Include="Parlot.SourceGenerator" Version="{{version}}" PrivateAssets="all" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(author, "Grammar.cs"), """
                namespace Standalone.Author;
                public static partial class Grammar
                {
                    public static partial bool TryParse(string text, out int value);
                }
                """);
            File.WriteAllText(Path.Combine(author, "Grammar.parlot.cs"), """
                using Parlot.Fluent;
                using Parlot.SourceGenerator;
                using static Parlot.Fluent.Parsers;
                namespace Standalone.Author;
                public static partial class Grammar
                {
                    [GenerateParser(nameof(TryParse))]
                    private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer).Eof();
                }
                """);

            await BuildContextIntegrationTests.RunDotnet(author, "restore", "--packages", Path.Combine(directory, "packages"), "-p:NuGetAudit=false");
            await BuildContextIntegrationTests.RunDotnet(author, "pack", "--no-restore", "--output", feed,
                "--configuration", configuration, "-p:UseSharedCompilation=false");
            using (var package = ZipFile.OpenRead(Path.Combine(feed, $"Standalone.Author.{version}.nupkg")))
            {
                AssertNoPackageDependencies(package);
                Assert.DoesNotContain(package.Entries, static entry => entry.FullName.Contains("Parlot", StringComparison.Ordinal));
                using var assemblyStream = package.GetEntry("lib/net10.0/Author.dll").Open();
                using var memory = new MemoryStream();
                assemblyStream.CopyTo(memory);
                memory.Position = 0;
                using var pe = new PEReader(memory);
                var metadata = pe.GetMetadataReader();
                Assert.DoesNotContain(metadata.AssemblyReferences, handle =>
                    metadata.GetString(metadata.GetAssemblyReference(handle).Name).StartsWith("Parlot", StringComparison.Ordinal));
            }

            File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup>
                  <ItemGroup><PackageReference Include="Standalone.Author" Version="{{version}}" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(consumer, "Program.cs"), """
                if (!Standalone.Author.Grammar.TryParse(" 42", out var value) || value != 42)
                    throw new System.InvalidOperationException("Generated parser failed.");
                if (Standalone.Author.Grammar.TryParse("42x", out _))
                    throw new System.InvalidOperationException("Generated EOF check failed.");
                System.Console.WriteLine(value);
                """);
            await BuildContextIntegrationTests.RunDotnet(consumer, "restore", "--packages",
                Path.Combine(directory, "consumer-packages"), "-p:NuGetAudit=false");
            var output = await BuildContextIntegrationTests.RunDotnet(consumer, "run", "--no-restore",
                "--configuration", configuration, "-p:UseSharedCompilation=false");
            Assert.Equal("42", output.Trim());
            Assert.DoesNotContain(Directory.GetFiles(Path.Combine(consumer, "bin", configuration, "net10.0")),
                static path => Path.GetFileName(path).StartsWith("Parlot", StringComparison.Ordinal));
            Assert.DoesNotContain(Directory.GetDirectories(Path.Combine(directory, "consumer-packages")),
                static path => Path.GetFileName(path).StartsWith("parlot", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertNoPackageDependencies(ZipArchive package)
    {
        using var stream = package.Entries.Single(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        var spec = XDocument.Load(stream);
        Assert.DoesNotContain(spec.Descendants(), static element => element.Name.LocalName == "dependency");
    }
}
