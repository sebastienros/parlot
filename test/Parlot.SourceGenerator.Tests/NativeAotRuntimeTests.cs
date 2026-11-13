using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class NativeAotRuntimeTests
{
    /// <summary>
    /// A Native AOT application that builds its grammar with the runtime combinators, not the source generator: what the
    /// combinators read by reflection must survive trimming. The grammar has the parsers that guard the parser they may not
    /// invoke with a <c>SeekGuard</c> (Separated, ZeroOrMany, OneOrMany, ZeroOrOne, Optional) over <c>Terms</c>, which skip
    /// white space.
    /// </summary>
    [Fact]
    public async Task Runtime_Combinators_Run_In_A_Native_Aot_Application()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent.Name;
        var directory = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "Parlot.NativeAotRuntime.Tests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            File.Copy(Path.Combine(root, "global.json"), Path.Combine(directory, "global.json"));
            File.WriteAllText(Path.Combine(directory, "NuGet.config"), """
                <configuration><packageSources><clear />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources></configuration>
                """);
            var parlot = Path.Combine(root, "src", "Parlot", "Parlot.csproj");
            File.WriteAllText(Path.Combine(directory, "Consumer.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <OutputType>Exe</OutputType>
                    <Nullable>enable</Nullable>
                    <PublishAot>true</PublishAot>
                  </PropertyGroup>
                  <ItemGroup><ProjectReference Include="{{parlot}}" /></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(directory, "Program.cs"), """
                using Parlot.Fluent;
                using static Parlot.Fluent.Parsers;

                var numbers = Separated(Terms.Char(','), Terms.Integer());
                var grammar = Terms.Char('[').SkipAnd(numbers).AndSkip(Terms.Char(']'))
                    .AndSkip(ZeroOrMany(Terms.Char(';')))
                    .AndSkip(ZeroOrOne(Terms.Text("then")))
                    .AndSkip(OneOrMany(Terms.Text("end")).Optional())
                    .Eof();

                if (!grammar.TryParse(" [ 1, 2 ,3 ] ;; then end end", out var values) || values.Count != 3)
                    throw new System.InvalidOperationException("The grammar failed.");
                if (grammar.TryParse("[1, 2,]", out _))
                    throw new System.InvalidOperationException("The trailing separator was accepted.");
                var sum = 0L;
                foreach (var value in values)
                    sum += value;
                System.Console.WriteLine(sum);
                """);

            var publish = Path.Combine(directory, "native");
            await BuildContextIntegrationTests.RunDotnet(directory, "publish", "--configuration", configuration,
                "--runtime", RuntimeInformation.RuntimeIdentifier, "--output", publish,
                "-p:UseSharedCompilation=false", "-p:NuGetAudit=false");
            var executable = Path.Combine(publish, OperatingSystem.IsWindows() ? "Consumer.exe" : "Consumer");
            Assert.False(File.Exists(Path.Combine(publish, "Consumer.dll")));
            Assert.Equal("6", (await BuildContextIntegrationTests.RunProcess(directory, executable)).Trim());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
