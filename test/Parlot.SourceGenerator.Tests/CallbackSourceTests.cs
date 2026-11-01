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

    private static string GetEntryPointSource(GeneratorDriverRunResult result)
        => result.Results.SelectMany(static item => item.GeneratedSources)
            .Single(static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal)).SourceText.ToString();
}
