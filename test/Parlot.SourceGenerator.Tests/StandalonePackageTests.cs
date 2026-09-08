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
    [Theory]
    [InlineData("net472", false)]
    [InlineData("netstandard2.0", false)]
    [InlineData("netstandard2.0", true)]
    [InlineData("net8.0", false)]
    [InlineData("net10.0", false)]
    [InlineData("net472;netstandard2.0;net8.0;net10.0", false)]
    public async Task Packaged_Generator_Produces_A_Dependency_Free_Library_And_Downstream_Application(
        string targetFramework, bool centralPackageManagement)
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
        var frameworks = targetFramework.Split(';');
        var downlevel = frameworks.Any(static framework => framework is "net472" or "netstandard2.0");
        var consumerFramework = frameworks.Length > 1 || targetFramework == "netstandard2.0" ? "net10.0" : targetFramework;
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
                using var manifest = package.Entries.Single(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
                var dependencies = XDocument.Load(manifest).Descendants().Where(static element => element.Name.LocalName == "dependency").ToArray();
                Assert.Equal(2, dependencies.Length);
                Assert.All(dependencies, static dependency => Assert.Equal("System.Memory", dependency.Attribute("id").Value));
                Assert.Equal([".NETFramework4.7.2", ".NETStandard2.0"],
                    dependencies.Select(static dependency => dependency.Parent.Attribute("targetFramework").Value));
            }

            File.WriteAllText(Path.Combine(author, "Author.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>{{targetFramework}}</TargetFrameworks>
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
            if (centralPackageManagement)
            {
                File.WriteAllText(Path.Combine(author, "Directory.Packages.props"), $$"""
                    <Project>
                      <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
                      <ItemGroup><PackageVersion Include="Parlot.SourceGenerator" Version="{{version}}" /></ItemGroup>
                    </Project>
                    """);
                var project = Path.Combine(author, "Author.csproj");
                File.WriteAllText(project, File.ReadAllText(project).Replace(
                    $"Include=\"Parlot.SourceGenerator\" Version=\"{version}\"", "Include=\"Parlot.SourceGenerator\"", StringComparison.Ordinal));
            }
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
            // The analyzer package restores the downlevel BCL support for a fresh application build.
            await BuildContextIntegrationTests.RunDotnet(author, "build", "--no-restore",
                "--configuration", configuration, "-p:UseSharedCompilation=false");
            if (downlevel)
            {
                var error = await Assert.ThrowsAsync<Xunit.Sdk.TrueException>(() =>
                    BuildContextIntegrationTests.RunDotnet(author, "pack", "--no-build", "--no-restore",
                        "--output", feed, "--configuration", configuration));
                Assert.Contains("requires an explicit non-private PackageReference to System.Memory", error.Message, StringComparison.Ordinal);

                // Package build imports are deliberately excluded by NuGet restore. A library author
                // must make its runtime dependency explicit instead of hiding it behind PrivateAssets=all.
                var project = XDocument.Load(Path.Combine(author, "Author.csproj"));
                var reference = new XElement("PackageReference", new XAttribute("Include", "System.Memory"));
                reference.Add(new XAttribute("Condition", "'$(TargetFramework)' == 'net472' or '$(TargetFramework)' == 'netstandard2.0'"));
                if (!centralPackageManagement)
                {
                    reference.Add(new XAttribute("Version", "4.6.3"));
                }
                else
                {
                    var packages = XDocument.Load(Path.Combine(author, "Directory.Packages.props"));
                    packages.Root.Add(new XElement("ItemGroup", new XElement("PackageVersion",
                        new XAttribute("Include", "System.Memory"), new XAttribute("Version", "4.6.3"))));
                    packages.Save(Path.Combine(author, "Directory.Packages.props"));
                }
                project.Root.Add(new XElement("ItemGroup", reference));
                project.Save(Path.Combine(author, "Author.csproj"));
                await BuildContextIntegrationTests.RunDotnet(author, "restore", "--packages",
                    Path.Combine(directory, "packages"), "-p:NuGetAudit=false");
            }
            await BuildContextIntegrationTests.RunDotnet(author, "pack", "--no-restore", "--output", feed,
                "--configuration", configuration, "-p:UseSharedCompilation=false");
            using (var package = ZipFile.OpenRead(Path.Combine(feed, $"Standalone.Author.{version}.nupkg")))
            {
                AssertRuntimePackageDependencies(package, frameworks);
                Assert.DoesNotContain(package.Entries, static entry => entry.FullName.Contains("Parlot", StringComparison.Ordinal));
                foreach (var framework in frameworks)
                {
                    using var assemblyStream = package.GetEntry($"lib/{framework}/Author.dll").Open();
                    using var memory = new MemoryStream();
                    assemblyStream.CopyTo(memory);
                    memory.Position = 0;
                    using var pe = new PEReader(memory);
                    var metadata = pe.GetMetadataReader();
                    Assert.DoesNotContain(metadata.AssemblyReferences, handle =>
                        metadata.GetString(metadata.GetAssemblyReference(handle).Name).StartsWith("Parlot", StringComparison.Ordinal));
                }
            }

            File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>{{consumerFramework}}</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <LangVersion>12</LangVersion>
                  </PropertyGroup>
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
            var canExecute = targetFramework != "net472" || OperatingSystem.IsWindows();
            var output = await BuildContextIntegrationTests.RunDotnet(consumer, canExecute ? "run" : "build", "--no-restore",
                "--configuration", configuration, "-p:UseSharedCompilation=false");
            if (canExecute)
            {
                Assert.Equal("42", output.Trim());
            }
            Assert.DoesNotContain(Directory.GetFiles(Path.Combine(consumer, "bin", configuration, consumerFramework)),
                static path => Path.GetFileName(path).StartsWith("Parlot", StringComparison.Ordinal));
            Assert.DoesNotContain(Directory.GetDirectories(Path.Combine(directory, "consumer-packages")),
                static path => Path.GetFileName(path).StartsWith("parlot", StringComparison.OrdinalIgnoreCase));
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void AssertRuntimePackageDependencies(ZipArchive package, string[] frameworks)
    {
        using var stream = package.Entries.Single(static entry => entry.FullName.EndsWith(".nuspec", StringComparison.Ordinal)).Open();
        var spec = XDocument.Load(stream);
        var groups = spec.Descendants().Where(static element => element.Name.LocalName == "group").ToArray();
        Assert.Equal(frameworks.Length, groups.Length);
        foreach (var framework in frameworks)
        {
            var name = framework switch
            {
                "net472" => ".NETFramework4.7.2",
                "netstandard2.0" => ".NETStandard2.0",
                _ => framework,
            };
            var group = Assert.Single(groups, group => group.Attribute("targetFramework").Value == name);
            var dependencies = group.Elements().ToArray();
            if (framework is "net472" or "netstandard2.0")
            {
                var dependency = Assert.Single(dependencies);
                Assert.Equal("System.Memory", dependency.Attribute("id").Value);
                Assert.Equal("4.6.3", dependency.Attribute("version").Value);
            }
            else
            {
                Assert.Empty(dependencies);
            }
        }
    }

}
