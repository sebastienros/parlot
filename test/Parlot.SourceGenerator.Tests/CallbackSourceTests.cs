#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Xunit;
using static Parlot.SourceGenerator.Tests.GeneratorDiagnosticsTests;

namespace Parlot.SourceGenerator.Tests;

/// <summary>
/// Callbacks are copied from the grammar into generated code, which must bind their names as the grammar does.
/// </summary>
public class CallbackSourceTests
{
    // A type of an enclosing namespace has the same name as the model the grammar uses
    private const string Models = """
        namespace App
        {
            public sealed class SqlParameter
            {
                public static SqlParameter Create(string name) => new SqlParameter();
            }

            namespace Models
            {
                public sealed class SqlParameter
                {
                    public string Name { get; private set; } = "";
                    public static SqlParameter Create(string name) => new SqlParameter { Name = name };
                }

                public static class TextExtensions
                {
                    public static string Shout(this string text) => text.ToUpperInvariant();
                }
            }

            namespace Parsing
            {
                public static partial class Grammar
                {
                    public static partial bool TryParse(string text, out App.Models.SqlParameter value);
                }
            }
        }
        """;

    // Usings in the namespace block, as StyleCop places them, take precedence over the members of enclosing namespaces
    private const string Grammar = """
        namespace App.Parsing
        {
            using System.Collections.Generic;
            using App.Models;
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;

            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<SqlParameter> Build()
                    => Terms.NonWhiteSpace().Then(static name =>
                    {
                        var names = new List<string> { name.ToString() };
                        return SqlParameter.Create(names[0].Shout());
                    });
            }
        }
        """;

    [Fact]
    public void Callbacks_Bind_Names_As_In_A_Grammar_With_Namespace_Usings()
    {
        var (result, compilation) = RunStandalone(Models, Grammar);

        AssertNoErrors(result, compilation);
        var source = GetEntryPointSource(result);
        Assert.Contains("global::App.Models.SqlParameter.Create(", source, StringComparison.Ordinal);
        Assert.Contains("new global::System.Collections.Generic.List<string>", source, StringComparison.Ordinal);
        Assert.Contains("using App.Models;", source, StringComparison.Ordinal);

        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var parse = Assembly.Load(stream.ToArray()).GetType("App.Parsing.Grammar")!.GetMethod("TryParse")!;
        object?[] arguments = ["hello", null];
        Assert.True((bool)parse.Invoke(null, arguments)!);
        Assert.Equal("HELLO", arguments[1]!.GetType().GetProperty("Name")!.GetValue(arguments[1]));
    }

    [Fact]
    public void Callbacks_Qualify_Namespaces_And_Nested_Type_Arguments()
    {
        var grammar = Grammar.Replace("SqlParameter.Create(names[0].Shout())",
            "Models.SqlParameter.Create(new Dictionary<string, SqlParameter>().Count + names[0].Shout())", StringComparison.Ordinal);

        var (result, compilation) = RunStandalone(Models, grammar);

        AssertNoErrors(result, compilation);
        var source = GetEntryPointSource(result);
        Assert.Contains("global::App.Models.SqlParameter.Create(", source, StringComparison.Ordinal);
        Assert.Contains("global::System.Collections.Generic.Dictionary<string, global::App.Models.SqlParameter>", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Line_Directives_Use_Absolute_Paths_By_Default()
    {
        var root = Path.Combine(Path.GetTempPath(), "checkout");
        var path = Path.Combine(root, "Parsing", "Grammar.parlot.cs");

        var source = Generate(root, path, lineDirectives: null);

        Assert.Contains("#line 15 \"" + path.Replace("\\", "\\\\") + "\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Relative_Line_Directives_Are_The_Same_In_Every_Checkout()
    {
        var first = Path.Combine(Path.GetTempPath(), "first");
        var second = Path.Combine(Path.GetTempPath(), "second", "nested");

        var source = Generate(first, Path.Combine(first, "Parsing", "Grammar.parlot.cs"), "relative");

        Assert.Contains("#line 15 \"Parsing/Grammar.parlot.cs\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain(first, source, StringComparison.Ordinal);
        Assert.Equal(source, Generate(second, Path.Combine(second, "Parsing", "Grammar.parlot.cs"), "Relative"));
    }

    [Fact]
    public void Line_Directives_Can_Be_Omitted()
    {
        var first = Path.Combine(Path.GetTempPath(), "first");
        var second = Path.Combine(Path.GetTempPath(), "second");

        var source = Generate(first, Path.Combine(first, "Grammar.parlot.cs"), "none");

        Assert.DoesNotContain("#line", source, StringComparison.Ordinal);
        Assert.Equal(source, Generate(second, Path.Combine(second, "Grammar.parlot.cs"), "none"));
    }

    [Fact]
    public void Invalid_Line_Directives_Value_Is_Reported()
    {
        var options = DefaultOptions();
        options["build_property.ParlotLineDirectives"] = "everywhere";

        var (result, compilation) = RunStandalone(Models, Grammar, options);

        AssertNoErrors(result, compilation);
        var diagnostic = Assert.Single(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT027");
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void Relative_Line_Directives_Reach_Grammars_Outside_The_Project()
    {
        var root = Path.Combine(Path.GetTempPath(), "repository");

        var source = Generate(Path.Combine(root, "project"), Path.Combine(root, "shared", "Grammar.parlot.cs"), "relative");

        Assert.Contains("#line 15 \"../shared/Grammar.parlot.cs\"", source, StringComparison.Ordinal);
    }

    private static string Generate(string projectDirectory, string grammarPath, string? lineDirectives)
    {
        var options = DefaultOptions(projectDirectory);
        if (lineDirectives is not null)
        {
            options["build_property.ParlotLineDirectives"] = lineDirectives;
        }

        var (result, compilation) = RunStandalone(Models, Grammar, options, grammarPath: grammarPath);
        AssertNoErrors(result, compilation);
        return GetEntryPointSource(result);
    }

    private static string GetEntryPointSource(GeneratorDriverRunResult result)
        => result.Results.SelectMany(static item => item.GeneratedSources)
            .Single(static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal)).SourceText.ToString();
}
