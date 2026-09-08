using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Parlot.SourceGenerator.Tests;

public class StandaloneGeneratorTests
{
    private const string Declaration = """
        public static partial class Grammar
        {
            public static partial bool TryParse(string text, out int value);
        }
        """;

    private const string Grammar = """
        using Parlot.Fluent;
        using Parlot.SourceGenerator;
        using static Parlot.Fluent.Parsers;
        public static partial class Grammar
        {
            [GenerateParser(nameof(TryParse))]
            private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer).Eof();
        }
        """;

    [Fact]
    public void Generated_Assembly_Has_No_Parlot_References_Or_Factories()
    {
        var (result, compilation) = Generate(Declaration, Grammar);
        AssertNoErrors(result, compilation);
        using var stream = new MemoryStream();
        var emitted = compilation.Emit(stream);
        Assert.True(emitted.Success, string.Join(Environment.NewLine, emitted.Diagnostics));
        var assembly = Assembly.Load(stream.ToArray());
        Assert.DoesNotContain(assembly.GetReferencedAssemblies(), static name => name.Name.StartsWith("Parlot", StringComparison.Ordinal));
        var grammar = assembly.GetType("Grammar", throwOnError: true);
        Assert.Null(grammar.GetMethod("Build", BindingFlags.NonPublic | BindingFlags.Static));
        var parse = grammar.GetMethod("TryParse");
        object[] args = [" 42", null];
        Assert.True((bool)parse.Invoke(null, args));
        Assert.Equal(42, args[1]);
        args = ["42x", null];
        Assert.False((bool)parse.Invoke(null, args));
        Assert.Equal(0, args[1]);
        Assert.DoesNotContain(result.Results.SelectMany(static r => r.GeneratedSources),
            static source => source.SourceText.ToString().Contains("InterceptsLocation", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_Numeric_Support_Excludes_Runtime_Reflection()
    {
        var (result, compilation) = Generate(Declaration, Grammar);
        AssertNoErrors(result, compilation);
        var sources = result.Results.SelectMany(static result => result.GeneratedSources).ToArray();
        var numbers = Assert.Single(sources, static source => source.HintName == "Parlot.StandaloneRuntime.Numbers.g.cs");
        var source = numbers.SourceText.ToString();
        Assert.Contains("TNumber.TryParse(span, styles, provider, out value)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Reflection", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GetTryParseMethod", source, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseDelegate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ImplementsINumber", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HasTryParseRadixOverload", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Top_Level_Applications_Generate_Without_Executing_Startup()
    {
        var source = """
            #nullable enable
            throw new System.InvalidOperationException("Do not execute startup");
            public sealed class ApplicationState
            {
                public string? Value { get; set; }
            }

            """ + Declaration;
        var (result, compilation) = Generate(source, Grammar, outputKind: OutputKind.ConsoleApplication,
            warningsAsErrors: true);
        AssertNoErrors(result, compilation);
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Generated_Helpers_Use_Jit_Inlining_Heuristics(bool configured)
    {
        var declaration = Declaration;
        var grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<int> Build()
                {
                    var expression = Deferred<int>();
                    var number = Terms.Number<int>(NumberOptions.Integer).Then(System.Math.Abs);
                    var group = Terms.Char('(').SkipAnd(expression).AndSkip(Terms.Char(')'));
                    expression.Parser = number.Or(group).Then(static value => value + 1);
                    return expression.Eof();
                }
            }
            """;
        if (configured)
        {
            declaration = declaration.Replace("string text, out", "string text, int increment, out", StringComparison.Ordinal);
            grammar = grammar.Replace("Build()", "Build(int increment)", StringComparison.Ordinal)
                .Replace("static value => value + 1", "value => value + increment", StringComparison.Ordinal);
        }

        var (result, compilation) = Generate(declaration, grammar);
        AssertNoErrors(result, compilation);
        var sources = result.Results.SelectMany(static result => result.GeneratedSources).ToArray();
        var parserSource = Assert.Single(sources,
            static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal));
        var helpers = parserSource.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(static method => !method.Modifiers.Any(SyntaxKind.InternalKeyword));
        Assert.All(helpers, static method =>
            Assert.DoesNotContain("MethodImplOptions.AggressiveInlining", method.ToString(), StringComparison.Ordinal));
        Assert.Contains(sources, static source =>
            source.HintName.StartsWith("Parlot.StandaloneRuntime.", StringComparison.Ordinal)
            && source.SourceText.ToString().Contains("MethodImplOptions.AggressiveInlining", StringComparison.Ordinal));

        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var assembly = Assembly.Load(stream.ToArray());
        var parse = assembly.GetType("Grammar").GetMethod("TryParse");
        object[] arguments = configured ? ["((2))", 1, null] : ["((2))", null];
        Assert.True((bool)parse.Invoke(null, arguments));
        Assert.Equal(5, arguments[^1]);
        arguments[0] = "((2)";
        Assert.False((bool)parse.Invoke(null, arguments));
        Assert.Equal(0, arguments[^1]);
    }

    [Fact]
    public void Small_Entry_Core_Can_Inline_Into_Public_Wrapper()
    {
        var (result, compilation) = Generate(Declaration, Grammar.Replace(".Eof()", "", StringComparison.Ordinal));
        AssertNoErrors(result, compilation);
        var source = Assert.Single(result.Results.SelectMany(static result => result.GeneratedSources),
            static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal));
        var core = Assert.Single(source.SyntaxTree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>(),
            static method => method.Modifiers.Any(SyntaxKind.InternalKeyword));
        Assert.Contains("MethodImplOptions.AggressiveInlining", core.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Implemented_Partial_Methods_Remain_Available_To_Callbacks()
    {
        var source = Declaration + """

            public static partial class Grammar
            {
                private static partial int Helper();
                private static partial int Helper() => 1;
            }
            """;
        var grammar = Grammar.Replace(".Eof()", ".Then(static value => value + Helper()).Eof()", StringComparison.Ordinal);
        var (result, compilation) = Generate(source, grammar);
        AssertNoErrors(result, compilation);
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var assembly = Assembly.Load(stream.ToArray());
        object[] arguments = ["42", null];
        Assert.True((bool)assembly.GetType("Grammar").GetMethod("TryParse").Invoke(null, arguments));
        Assert.Equal(43, arguments[1]);
    }

    [Theory]
    [InlineData("public static partial int TryParse(string text, out int value);")]
    [InlineData("public static partial bool TryParse(string text, out string value);")]
    [InlineData("public static partial bool TryParse(System.ReadOnlySpan<char> text, out int value);")]
    [InlineData("public static bool TryParse(string text, out int value) { value = 0; return true; }")]
    [InlineData("public static partial bool TryParse(string text, int cancellationToken, out int value);")]
    [InlineData("public static partial bool TryParse(string text, ref System.Threading.CancellationToken cancellationToken, out int value);")]
    [InlineData("public static partial bool TryParse(string text, System.Threading.CancellationToken? cancellationToken, out int value);")]
    [InlineData("public static partial bool TryParse(string text, System.Threading.CancellationToken first, System.Threading.CancellationToken second, out int value);")]
    public void Invalid_Entry_Point_Is_Rejected(string declaration)
    {
        var (result, _) = Generate("public static partial class Grammar { " + declaration + " }", Grammar);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT023");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancellation_Argument_Is_Wired_Without_Name_Collisions(bool configured)
    {
        var declaration = Declaration.Replace("out int value",
            "System.Threading.CancellationToken result, out int value", StringComparison.Ordinal);
        var grammar = Grammar;
        if (configured)
        {
            declaration = declaration.Replace("string text, ", "string text, int context, ", StringComparison.Ordinal);
            grammar = grammar.Replace("Build()", "Build(int __parlotCancellationToken)", StringComparison.Ordinal)
                .Replace(".Eof()", ".Then(value => value + __parlotCancellationToken).Eof()", StringComparison.Ordinal);
        }

        var (result, compilation) = Generate(declaration, grammar);
        AssertNoErrors(result, compilation);
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var assembly = Assembly.Load(stream.ToArray());
        var parse = assembly.GetType("Grammar").GetMethod("TryParse");
        using var source = new CancellationTokenSource();
        object[] arguments = configured ? ["42", 1, source.Token, null] : ["42", source.Token, null];
        Assert.True((bool)parse.Invoke(null, arguments));
        Assert.Equal(configured ? 43 : 42, arguments[^1]);
        source.Cancel();
        var exception = Assert.Throws<TargetInvocationException>(() => parse.Invoke(null, arguments));
        Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(exception.InnerException).CancellationToken);

        var (designTimeResult, designTimeCompilation) = Generate(declaration, grammar, designTime: true);
        AssertNoErrors(designTimeResult, designTimeCompilation);
    }

    [Fact]
    public void Application_And_Engine_Cancellation_Tokens_Are_Independent()
    {
        var declaration = Declaration.Replace("out int value",
            "System.Threading.CancellationToken applicationToken, System.Threading.CancellationToken engineToken, out int value", StringComparison.Ordinal);
        var grammar = Grammar.Replace("Build()", "Build(System.Threading.CancellationToken cancellationToken)", StringComparison.Ordinal)
            .Replace(".Eof()", ".Then(value => cancellationToken.IsCancellationRequested ? value + 1 : value).Eof()", StringComparison.Ordinal);
        var (result, compilation) = Generate(declaration, grammar);
        AssertNoErrors(result, compilation);
        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var parse = Assembly.Load(stream.ToArray()).GetType("Grammar").GetMethod("TryParse");
        using var source = new CancellationTokenSource();
        source.Cancel();
        object[] arguments = ["42", source.Token, CancellationToken.None, null];
        Assert.True((bool)parse.Invoke(null, arguments));
        Assert.Equal(43, arguments[^1]);
        arguments[1] = CancellationToken.None;
        arguments[2] = source.Token;
        var exception = Assert.Throws<TargetInvocationException>(() => parse.Invoke(null, arguments));
        Assert.IsType<OperationCanceledException>(exception.InnerException);
    }

    [Fact]
    public void Standalone_Factory_In_Compile_Is_Rejected()
    {
        var (result, _) = Generate(Grammar, null, referenceParlot: true);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT025");
    }

    [Fact]
    public void Missing_Entry_Point_Name_Is_Rejected()
    {
        var (result, _) = Generate(Declaration, Grammar.Replace("[GenerateParser(nameof(TryParse))]", "[GenerateParser]", StringComparison.Ordinal));
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT023");
    }

    [Fact]
    public void Duplicate_Entry_Point_Is_Rejected()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<int> First() => Terms.Number<int>(NumberOptions.Integer);
                [GenerateParser(nameof(TryParse))]
                private static Parser<int> Second() => Terms.Number<int>(NumberOptions.Integer);
            }
            """;
        var (result, _) = Generate(Declaration, grammar);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT023");
    }

    [Fact]
    public void Factory_Capture_Diagnostics_Are_Reported_For_Additional_Files()
    {
        var declaration = Declaration.Replace("string text, out", "string text, int option, out", StringComparison.Ordinal);
        var grammar = Grammar.Replace("Build()", "Build(int option)", StringComparison.Ordinal)
            .Replace("Terms.Number<int>(NumberOptions.Integer).Eof()", "Literals.Text(option.ToString()).Then(42)", StringComparison.Ordinal);
        var (result, _) = Generate(declaration, grammar);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT021");
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id == "CS8785");
    }

    [Fact]
    public void Factory_Exceptions_Are_Build_Errors()
    {
        var grammar = Grammar.Replace("Terms.Number<int>(NumberOptions.Integer).Eof()", "throw new System.InvalidOperationException(\"Factory failed\")", StringComparison.Ordinal);
        var (result, _) = Generate(Declaration, grammar);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT004");
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Id == "CS8785");
    }

    [Fact]
    public void Unavailable_Runtime_Helper_Is_A_Generation_Error()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                private static int Helper(int value) => value;
                [GenerateParser(nameof(TryParse))]
                private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer).Then(value => Helper(value));
            }
            """;
        var (result, _) = Generate(Declaration, grammar);
        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT024");
    }

    [Fact]
    public void Design_Time_Emits_Only_An_Entry_Point_Stub_Without_Executing_The_Factory()
    {
        var grammar = Grammar.Replace("Terms.Number<int>(NumberOptions.Integer).Eof()", "throw new System.InvalidOperationException(\"Do not execute\")", StringComparison.Ordinal);
        var (result, compilation) = Generate(Declaration, grammar, designTime: true);
        AssertNoErrors(result, compilation);
        var sources = result.Results.SelectMany(static result => result.GeneratedSources).ToArray();
        Assert.Single(sources);
        Assert.Contains("Standalone parsers are generated during build.", sources[0].SourceText.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Multiple_Parsers_Share_One_Runtime()
    {
        var declaration = Declaration + Declaration.Replace("Grammar", "OtherGrammar", StringComparison.Ordinal);
        var grammar = Grammar + "\n" + Grammar.Substring(Grammar.IndexOf("public static partial class", StringComparison.Ordinal))
            .Replace("Grammar", "OtherGrammar", StringComparison.Ordinal);
        var (result, compilation) = Generate(declaration, grammar);
        AssertNoErrors(result, compilation);
        var sources = result.Results.SelectMany(static result => result.GeneratedSources).ToArray();
        Assert.Equal(2, sources.Count(static source => source.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal)));
        Assert.Single(compilation.GetSymbolsWithName("Scanner").OfType<INamedTypeSymbol>(),
            static type => type.ContainingAssembly.Name.StartsWith("StandaloneTest", StringComparison.Ordinal));
    }

    [Fact]
    public void Generated_Support_Types_Are_Not_Shadowed_By_The_Runtime_Parlot_Namespace()
    {
        const string typeDeclaration = "public static partial class Grammar";
        const string namespacedDeclaration = "namespace Parlot.Consumers;\npublic static partial class Grammar";
        var (result, compilation) = Generate(
            Declaration.Replace(typeDeclaration, namespacedDeclaration, StringComparison.Ordinal),
            Grammar.Replace(typeDeclaration, namespacedDeclaration, StringComparison.Ordinal),
            referenceParlot: true);
        AssertNoErrors(result, compilation);
    }

    [Fact]
    public void Namespace_Remapping_Does_Not_Change_String_Literals()
    {
        var (result, compilation) = Generate(
            Declaration.Replace("out int value", "out string value", StringComparison.Ordinal),
            Grammar.Replace("Parser<int>", "Parser<string>", StringComparison.Ordinal)
                .Replace("Terms.Number<int>(NumberOptions.Integer).Eof()", "Literals.Text(\"Parlot.Scanner\")", StringComparison.Ordinal));
        AssertNoErrors(result, compilation);
        var source = Assert.Single(result.Results.SelectMany(static result => result.GeneratedSources),
            static source => source.HintName == "StandaloneParser0.g.cs").SourceText.ToString();
        Assert.Contains("\"Parlot.Scanner\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Parlot.Generated.Scanner\"", source, StringComparison.Ordinal);
    }

    private static void AssertNoErrors(GeneratorDriverRunResult result, CSharpCompilation compilation)
    {
        Assert.DoesNotContain(result.Diagnostics, static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var errors = compilation.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        Assert.Empty(errors);
    }

    private static (GeneratorDriverRunResult Result, CSharpCompilation Compilation) Generate(
        string source, string grammar, bool designTime = false, bool referenceParlot = false,
        OutputKind outputKind = OutputKind.DynamicallyLinkedLibrary, bool warningsAsErrors = false)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.CSharp12,
            preprocessorSymbols: ["NET", "NET8_0_OR_GREATER", "NET9_0_OR_GREATER", "NET10_0_OR_GREATER"]);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Where(path => !Path.GetFileName(path).StartsWith("Parlot", StringComparison.Ordinal))
            .Select(static path => MetadataReference.CreateFromFile(path)).ToList();
        if (referenceParlot)
        {
            references.Add(MetadataReference.CreateFromFile(typeof(Fluent.Parser<>).Assembly.Location));
        }
        var compilation = CSharpCompilation.Create("StandaloneTest" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, parseOptions, "Grammar.cs")], references,
            new CSharpCompilationOptions(outputKind, allowUnsafe: true,
                generalDiagnosticOption: warningsAsErrors ? ReportDiagnostic.Error : ReportDiagnostic.Default));
        var options = new Dictionary<string, string>
        {
            ["build_property.TargetFrameworkIdentifier"] = ".NETCoreApp",
            ["build_property.TargetFrameworkVersion"] = "v10.0",
            ["build_property.DesignTimeBuild"] = designTime.ToString()
        };
        return GeneratorDiagnosticsTests.RunGenerator(compilation, options,
            additionalTexts: grammar is null ? [] : [new GrammarText(grammar)]);
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
