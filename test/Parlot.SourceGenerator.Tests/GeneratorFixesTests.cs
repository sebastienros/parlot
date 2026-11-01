#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Parlot.Fluent;
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
        var runtime = Parsers.Terms.Text("ab")
            .Then(static (ParseContext _, int start, int end, string value) => $"{start}:{end}:{value}");
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

    [Fact]
    public void Wide_Numeric_Types_Are_Generated_From_The_Downlevel_Host()
    {
        Assert.True(Grammars.TryParseHalf(" 1.5", out var half));
        Assert.Equal((Half)1.5, half);
        Assert.False(Grammars.TryParseHalf("x", out _));
        Assert.True(Grammars.TryParseInt128Hex("ffffffffffffffffff", out var int128));
        Assert.Equal(Int128.Parse("4722366482869645213695"), int128);
    }

    [Theory]
    [InlineData("System.Half", "Terms.Number<System.Half>(NumberOptions.Float)", " 1.5", "1.5")]
    [InlineData("System.Int128", "Terms.Number<System.Int128>()", "-170141183460469231731687303715884105728", "-170141183460469231731687303715884105728")]
    [InlineData("System.UInt128", "Terms.Number<System.UInt128>()", "340282366920938463463374607431768211455", "340282366920938463463374607431768211455")]
    [InlineData("nint", "Terms.Number<nint>()", "-42", "-42")]
    [InlineData("nuint", "Terms.Number<nuint>()", "42", "42")]
    [InlineData("System.Int128", "Terms.Hexadecimal<System.Int128>()", "ff", "255")]
    [InlineData("System.UInt128", "Terms.Binary<System.UInt128>()", "101", "5")]
    [InlineData("nint", "Terms.Hexadecimal<nint>()", "10", "16")]
    [InlineData("nuint", "Terms.Octal<nuint>()", "17", "15")]
    public void Wide_Numeric_Types_Are_Supported_By_Modern_Consumers(string type, string parser, string input, string expected)
    {
        // The generator host runs the netstandard2.0 Parlot, which can't name these types but must still
        // build the graph and emit the consumer's generic-math path.
        var declaration = Declaration.Replace("out string value", $"out {type} value", StringComparison.Ordinal);
        var grammar = $$"""
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<{{type}}> Build() => {{parser}}.Eof();
            }
            """;
        var (result, compilation) = Generate(declaration, grammar);

        AssertNoErrors(result, compilation);
        var parse = Load(compilation).GetType("Grammar")!.GetMethod("TryParse")!;
        object?[] arguments = [input, null];
        Assert.True((bool)parse.Invoke(null, arguments)!);
        Assert.Equal(expected, Convert.ToString(arguments[1], System.Globalization.CultureInfo.InvariantCulture));
        arguments = ["z", null];
        Assert.False((bool)parse.Invoke(null, arguments)!);
    }

    [Theory]
    [InlineData("Terms.Number<System.Half>()")]
    [InlineData("Terms.Hexadecimal<nint>()")]
    public void Wide_Numeric_Types_Are_Rejected_For_Downlevel_Consumers(string parser)
    {
        var declaration = Declaration.Replace("out string value", "out System.Half value", StringComparison.Ordinal);
        var grammar = $$"""
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<System.Half> Build() => {{parser}}.Then(static _ => default(System.Half));
            }
            """;
        var (result, _) = Generate(declaration, grammar, targetFrameworkIdentifier: ".NETStandard", targetFrameworkVersion: "v2.0");

        Assert.True(result.Diagnostics.Any(static diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("requires .NET 8 or later", StringComparison.Ordinal)),
            string.Join(" | ", result.Diagnostics.Select(static diagnostic => diagnostic.ToString())));
    }

    [Theory]
    [InlineData("10 - 4 - 2", 8.0)]
    [InlineData("10 - 4 + 2", 4.0)]
    [InlineData("10 + 4 - 2", 12.0)]
    [InlineData("10", 10.0)]
    public void Tuple_Right_Associative_Is_Generated(string input, double expected)
    {
        Assert.True(Grammars.TryParseTupleRightAssociative(input, out var value));
        Assert.Equal(expected, value);

        var runtime = Parsers.Terms.Decimal().Then(static value => (double)value).RightAssociative(
            (Parsers.Terms.Char('-'), static (left, right) => left - right),
            (Parsers.Terms.Char('+'), static (left, right) => left + right));
        Assert.Equal(expected, runtime.Parse(input));
    }

    [Theory]
    [InlineData("2 ^ 3 ^ 2", 512.0)]
    [InlineData("2 ^ 3 - 1", 4.0)]
    [InlineData("8 - 3 - 1", 6.0)]
    public void Tuple_Right_Associative_With_Context_Is_Generated(string input, double expected)
    {
        Assert.True(Grammars.TryParseTupleRightAssociativeContext(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Tuple_Right_Associative_Rolls_Back_An_Operator_With_No_Right_Operand()
    {
        Assert.True(Grammars.TryParseTupleRightAssociativeThenPlus("1+2+", out var value));
        Assert.Equal(3.0, value);
        Assert.False(Grammars.TryParseTupleRightAssociativeThenPlus("1+2", out _));
    }

    [Theory]
    [InlineData("1+2", true, 1L)]
    [InlineData(" 7 + 2", true, 7L)]
    [InlineData("x", false, 0L)]
    public void Left_Recursive_Deferred_Fails_At_The_Entry_Position(string input, bool success, long expected)
    {
        // Runtime and generated parsers agree: the recursive alternative fails and the plain integer matches
        var runtime = Parsers.Deferred<long>();
        runtime.Parser = runtime.AndSkip(Parsers.Terms.Char('+')).And(Parsers.Terms.Integer()).Then(static x => x.Item1 + x.Item2)
            .Or(Parsers.Terms.Integer());
        Assert.Equal(success, runtime.TryParse(input, out var runtimeValue));
        Assert.Equal(expected, runtimeValue);

        Assert.Equal(success, Grammars.TryParseLeftRecursive(input, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1+2", true, 1L)]
    [InlineData("(1)", true, 1L)]
    [InlineData("((3))+1", true, 3L)]
    [InlineData("(", false, 0L)]
    public void Indirect_Left_Recursion_Is_Tracked_Per_Deferred_Parser(string input, bool success, long expected)
    {
        Assert.Equal(success, Grammars.TryParseIndirectLeftRecursive(input, out var value));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Deferred_Helpers_Check_The_Position_Without_The_Recursion_Depth_Wrapper()
    {
        var declaration = Declaration.Replace("string text, out string value", "string text, long bias, out long value", StringComparison.Ordinal);
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<long> Build(long bias)
                {
                    var expression = Deferred<long>();
                    expression.Parser = expression.AndSkip(Terms.Char('+')).And(Terms.Integer()).Then(x => x.Item1 + x.Item2 + bias)
                        .Or(Terms.Integer().Then(x => x + bias));
                    return expression;
                }
            }
            """;
        var (result, compilation) = Generate(declaration, grammar);

        AssertNoErrors(result, compilation);
        var source = string.Join("\n", result.Results.SelectMany(static item => item.GeneratedSources)
            .Where(static item => item.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal))
            .Select(static item => item.SourceText.ToString()));
        Assert.DoesNotContain("MaxRecursionDepth", source, StringComparison.Ordinal);
        Assert.DoesNotContain("EnterRecursion", source, StringComparison.Ordinal);
        // Deferred references call the helper directly and don't reset the cursor themselves
        Assert.DoesNotMatch(@"Deferred_0\(context, out [^;]*;\s*if \(![^)]*\)\s*\{\s*cursor\.ResetPosition", source);
        Assert.Contains("= Deferred_0(context, out ", source, StringComparison.Ordinal);
        Assert.Contains("__parlotDeferred0", source, StringComparison.Ordinal);

        // The configured (instance) shape keeps the state on the context subclass
        var parse = Load(compilation).GetType("Grammar")!.GetMethod("TryParse")!;
        object?[] arguments = ["1+2", 10L, null];
        Assert.True((bool)parse.Invoke(null, arguments)!);
        Assert.Equal(11L, arguments[2]);
        arguments = ["x", 10L, null];
        Assert.False((bool)parse.Invoke(null, arguments)!);
    }

    [Fact]
    public void Recursion_Points_Check_The_Stack_And_Count_Only_With_A_Limit()
    {
        static string Source(string attribute)
        {
            var grammar = $$"""
                using Parlot.Fluent;
                using Parlot.SourceGenerator;
                using static Parlot.Fluent.Parsers;
                public static partial class Grammar
                {
                    [GenerateParser({{attribute}})]
                    private static Parser<string> Build() =>
                        Recursive<string>(static list => Between(Literals.Char('['), list, Literals.Char(']')))
                            .Or(Literals.Text("item"))
                            .Unary((Literals.Char('-'), static value => value))
                            .Operators().Unary((Literals.Char('!'), static value => value))
                            .LeftAssociative((Literals.Char('+'), static (left, right) => left + right));
                }
                """;
            var (result, compilation) = Generate(Declaration, grammar);

            AssertNoErrors(result, compilation);
            return string.Join("\n", result.Results.SelectMany(static item => item.GeneratedSources)
                .Where(static item => item.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal))
                .Select(static item => item.SourceText.ToString()));
        }

        // Only the Deferred helper checks the stack: Unary and the prefix operators read their chains in a loop
        var unlimited = Source("nameof(TryParse)");
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(unlimited, @"context\.EnsureSufficientStack\(\);"));
        Assert.DoesNotContain("EnterRecursion", unlimited, StringComparison.Ordinal);

        // The Deferred helper, the Unary parser and the loop of its operators, and the prefix operators (the first one, then the
        // loop of the following ones) count. The first prefix operator leaves its level on both returns of its path without a
        // list, the following ones in the finally block of the list.
        var limited = Source("nameof(TryParse), MaxRecursionDepth = 64");
        Assert.Equal(5, System.Text.RegularExpressions.Regex.Matches(limited, @"context\.EnterRecursion\(\);").Count);
        Assert.Equal(6, System.Text.RegularExpressions.Regex.Matches(limited, @"context\.ExitRecursion\(\);").Count);
        Assert.Contains(": base(__parlotScanner, 64)", limited, StringComparison.Ordinal);
    }

    [Fact]
    public void Negative_Max_Recursion_Depth_Is_A_Generation_Error()
    {
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            public static partial class Grammar
            {
                [GenerateParser(nameof(TryParse), MaxRecursionDepth = -1)]
                private static Parser<string> Build() => Parsers.Literals.Text("item");
            }
            """;
        var (result, _) = Generate(Declaration, grammar);

        Assert.Contains(result.Diagnostics, static diagnostic => diagnostic.Id == "PARLOT024"
            && diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("MaxRecursionDepth", StringComparison.Ordinal));
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

    internal static (GeneratorDriverRunResult Result, CSharpCompilation Compilation) Generate(
        string source, string grammar, string targetFrameworkIdentifier = ".NETCoreApp", string targetFrameworkVersion = "v10.0")
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
            ["build_property.TargetFrameworkIdentifier"] = targetFrameworkIdentifier,
            ["build_property.TargetFrameworkVersion"] = targetFrameworkVersion,
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
