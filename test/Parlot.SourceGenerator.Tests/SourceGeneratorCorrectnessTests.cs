#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Parlot.Fluent;
using Parlot.SourceGeneration;
using Xunit;
using static Parlot.Fluent.Parsers;
using static Parlot.SourceGenerator.Tests.GeneratorDiagnosticsTests;

namespace Parlot.SourceGenerator.Tests;

public class SourceGeneratorCorrectnessTests
{
    [Fact]
    public void Collectible_LoadContext_Does_Not_Leak_Emitted_Assemblies_Into_Default_Context()
    {
        const string declaration = """
            public static partial class CollectibleGrammar
            {
                public static partial bool TryParse(string text, out int value);
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class CollectibleGrammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer).Eof();
            }
            """;

        var beforeNames = AssemblyLoadContext.Default.Assemblies
            .Select(static asm => asm.GetName().Name)
            .Where(static name => name is not null && name.StartsWith("StandaloneDiagnostics", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        // Run several standalone generations in parallel to exercise thread-safe collectible loading
        Parallel.For(0, 4, _ =>
        {
            var (result, compilation) = RunStandalone(declaration, grammar);
            AssertNoErrors(result, compilation);
        });

        var afterLeaked = AssemblyLoadContext.Default.Assemblies
            .Select(static asm => asm.GetName().Name)
            .Where(name => name is not null
                && name.StartsWith("StandaloneDiagnostics", StringComparison.Ordinal)
                && !beforeNames.Contains(name))
            .ToArray();

        Assert.Empty(afterLeaked);
    }

    [Fact]
    public void KeywordLiteral_Uses_SpanSource_And_Matches_Runtime_FurthestOffset_Parity()
    {
        const string declaration = """
            using System.IO;
            public static partial class KeywordParityGrammar
            {
                public static partial bool TryParseLiteral(string text, out string value, out int failureOffset);
                public static partial bool TryParseLiteral(TextReader reader, out string value, out int failureOffset);
                public static partial bool TryParseTerm(string text, out string value, out int failureOffset);
                public static partial bool TryParseTerm(TextReader reader, out string value, out int failureOffset);
                public static partial bool TryParseSequence(string text, out string value, out int failureOffset);
                public static partial bool TryParseSequence(TextReader reader, out string value, out int failureOffset);
                public static partial bool TryParseMatchedCase(string text, out string value, out int failureOffset);
                public static partial bool TryParseMatchedCase(TextReader reader, out string value, out int failureOffset);
                public static partial bool TryParseCustomBoundary(string text, out string value, out int failureOffset);
                public static partial bool TryParseCustomBoundary(TextReader reader, out string value, out int failureOffset);
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class KeywordParityGrammar
            {
                [GenerateParser(nameof(TryParseLiteral))]
                private static Parser<string> BuildLiteral() => Literals.Keyword("for");

                [GenerateParser(nameof(TryParseTerm))]
                private static Parser<string> BuildTerm() => Terms.Keyword("for").Or(Terms.Text("abc"));

                [GenerateParser(nameof(TryParseSequence))]
                private static Parser<string> BuildSequence() => Terms.Keyword("for").AndSkip(Terms.Char(';'));

                [GenerateParser(nameof(TryParseMatchedCase))]
                private static Parser<string> BuildMatchedCase() => Literals.Keyword("for", caseInsensitive: true, returnMatchedText: true).AndSkip(Literals.Char(' '));

                [GenerateParser(nameof(TryParseCustomBoundary))]
                private static Parser<string> BuildCustomBoundary() => Literals.Keyword("in", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_");
            }
            """;

        // Verify KeywordLiteral stays inside SpanSource in sequences and small choices
        var seqSource = ((ISourceable)Terms.Keyword("for").AndSkip(Terms.Char(';'))).GenerateSource(new SourceGenerationContext());
        Assert.Contains("AdvanceWithinBuffer", string.Join("\n", seqSource.Body), StringComparison.Ordinal);

        var ciSeqSource = ((ISourceable)Literals.Keyword("for", caseInsensitive: true, returnMatchedText: true).AndSkip(Literals.Char(' '))).GenerateSource(new SourceGenerationContext());
        Assert.Contains("AdvanceWithinBuffer", string.Join("\n", ciSeqSource.Body), StringComparison.Ordinal);

        var (result, compilation) = RunStandalone(declaration, grammar);
        AssertNoErrors(result, compilation);

        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var type = Assembly.Load(stream.ToArray()).GetType("KeywordParityGrammar")!;

        AssertParity(type, "TryParseLiteral", Literals.Keyword("for"), ["", "fo", "for", "for ", "forEach", "for1"]);
        AssertParity(type, "TryParseTerm", Terms.Keyword("for").Or(Terms.Text("abc")), ["", "  forEach", "  for", "  abc", "  abx"]);
        AssertParity(type, "TryParseSequence", Terms.Keyword("for").AndSkip(Terms.Char(';')), ["", "  forEach;", "  for ;", "  for x", "  fo;"]);
        AssertParity(type, "TryParseMatchedCase", Literals.Keyword("for", caseInsensitive: true, returnMatchedText: true).AndSkip(Literals.Char(' ')), ["", "FOR ", "For ", "for ", "FOREach ", "FORx"]);
        AssertParity(type, "TryParseCustomBoundary", Literals.Keyword("in", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_"), ["", "in", "in ", "in_x", "in1", "inside"]);

        // Explicitly assert boundary mismatch does NOT advance FurthestOffset past 0 (or past leading whitespace)
        var tryParseLiteralStr = type.GetMethod("TryParseLiteral", [typeof(string), typeof(string).MakeByRefType(), typeof(int).MakeByRefType()])!;
        object?[] args = ["forEach", null, 0];
        Assert.False((bool)tryParseLiteralStr.Invoke(null, args)!);
        Assert.Equal(0, args[2]);

        var tryParseTermStr = type.GetMethod("TryParseTerm", [typeof(string), typeof(string).MakeByRefType(), typeof(int).MakeByRefType()])!;
        args = ["  forEach", null, 0];
        Assert.False((bool)tryParseTermStr.Invoke(null, args)!);
        Assert.Equal(2, args[2]);

        // Test buffer boundary split in TextReader (4096-char buffer)
        var tryParseTermReader = type.GetMethod("TryParseTerm", [typeof(TextReader), typeof(string).MakeByRefType(), typeof(int).MakeByRefType()])!;
        var splitPadding = new string(' ', 4096 - 3);
        args = [new StringReader(splitPadding + "forEach"), null, 0];
        Assert.False((bool)tryParseTermReader.Invoke(null, args)!);
        Assert.Equal(splitPadding.Length, args[2]);

        args = [new StringReader(splitPadding + "for "), null, 0];
        Assert.True((bool)tryParseTermReader.Invoke(null, args)!);
        Assert.Equal("for", args[1]);
    }

    private static void AssertParity(Type generatedType, string methodName, Parser<string> runtimeParser, string[] inputs)
    {
        var stringMethod = generatedType.GetMethod(methodName, [typeof(string), typeof(string).MakeByRefType(), typeof(int).MakeByRefType()])!;
        var readerMethod = generatedType.GetMethod(methodName, [typeof(TextReader), typeof(string).MakeByRefType(), typeof(int).MakeByRefType()])!;

        foreach (var input in inputs)
        {
            var rtContext = new ParseContext(new Scanner(input));
            var rtSuccess = runtimeParser.TryParse(rtContext, out var rtValue, out _);
            var rtFailureOffset = rtSuccess ? 0 : rtContext.Scanner.Cursor.FurthestOffset;

            object?[] strArgs = [input, null, 0];
            var genStrSuccess = (bool)stringMethod.Invoke(null, strArgs)!;
            Assert.Equal(rtSuccess, genStrSuccess);
            if (rtSuccess)
            {
                Assert.Equal(rtValue, strArgs[1]);
            }
            else
            {
                Assert.Equal(rtFailureOffset, strArgs[2]);
            }

            object?[] readerArgs = [new StringReader(input), null, 0];
            var genReaderSuccess = (bool)readerMethod.Invoke(null, readerArgs)!;
            Assert.Equal(rtSuccess, genReaderSuccess);
            if (rtSuccess)
            {
                Assert.Equal(rtValue, readerArgs[1]);
            }
            else
            {
                Assert.Equal(rtFailureOffset, readerArgs[2]);
            }
        }
    }

    [Fact]
    public void Early_Return_Root_Emitter_Retains_Declared_Locals()
    {
        const string declaration = """
            public static partial class EarlyReturnLocalsGrammar
            {
                public static partial bool TryParseCustomLocals(string text, out int value);
                public static partial bool TryParseCreateResultEarlyReturn(string text, out int value);
            }
            """;
        const string grammar = """
            using Parlot;
            using Parlot.Fluent;
            using Parlot.SourceGeneration;
            using Parlot.SourceGenerator;
            public static partial class EarlyReturnLocalsGrammar
            {
                private sealed class CustomLocalsEarlyReturnParser : Parser<int>, ISourceable
                {
                    public override bool Parse(ParseContext context, ref ParseResult<int> result) => false;

                    public SourceResult GenerateSource(SourceGenerationContext context)
                    {
                        var result = new SourceResult("success", "value", "int");
                        result.Locals.Add("int multiplier = 7;");
                        result.Body.Add("if (!cursor.Eof && cursor.Current == 'x')");
                        result.Body.Add("{");
                        result.Body.Add("    cursor.Advance();");
                        result.Body.Add("    value = 6 * multiplier;");
                        result.Body.Add("    return true;");
                        result.Body.Add("}");
                        result.Body.Add("return false;");
                        return result;
                    }
                }

                private sealed class CreateResultEarlyReturnParser : Parser<int>, ISourceable
                {
                    public override bool Parse(ParseContext context, ref ParseResult<int> result) => false;

                    public SourceResult GenerateSource(SourceGenerationContext context)
                    {
                        var result = context.CreateResult(typeof(int));
                        result.Locals.Add("int bonus = 10;");
                        result.Body.Add("if (!cursor.Eof && cursor.Current == 'y')");
                        result.Body.Add("{");
                        result.Body.Add("    cursor.Advance();");
                        result.Body.Add($"    {result.ValueVariable} = 32 + bonus;");
                        result.Body.Add("    return true;");
                        result.Body.Add("}");
                        result.Body.Add("return false;");
                        return result;
                    }
                }

                [GenerateParser(nameof(TryParseCustomLocals))]
                private static Parser<int> BuildCustomLocals() => new CustomLocalsEarlyReturnParser();

                [GenerateParser(nameof(TryParseCreateResultEarlyReturn))]
                private static Parser<int> BuildCreateResultEarlyReturn() => new CreateResultEarlyReturnParser();
            }
            """;

        var (result, compilation) = RunStandalone(declaration, grammar);
        AssertNoErrors(result, compilation);

        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var type = Assembly.Load(stream.ToArray()).GetType("EarlyReturnLocalsGrammar")!;

        object?[] args1 = ["x", 0];
        Assert.True((bool)type.GetMethod("TryParseCustomLocals")!.Invoke(null, args1)!);
        Assert.Equal(42, args1[1]);

        object?[] args2 = ["y", 0];
        Assert.True((bool)type.GetMethod("TryParseCreateResultEarlyReturn")!.Invoke(null, args2)!);
        Assert.Equal(42, args2[1]);
    }

    [Fact]
    public void Keyword_Escaped_Namespace_And_Containing_Type_Generate_And_Execute()
    {
        const string declaration = """
            namespace @event.@class
            {
                public static partial class @default
                {
                    public static partial bool TryParse(string text, out int value);
                }
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;

            namespace @event.@class
            {
                public static partial class @default
                {
                    [GenerateParser(nameof(TryParse))]
                    private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer).Eof();
                }
            }
            """;

        var (result, compilation) = RunStandalone(declaration, grammar);
        AssertNoErrors(result, compilation);

        using var stream = new MemoryStream();
        Assert.True(compilation.Emit(stream).Success);
        var type = Assembly.Load(stream.ToArray()).GetType("event.class.default")!;
        Assert.NotNull(type);

        object?[] args = ["123", 0];
        Assert.True((bool)type.GetMethod("TryParse")!.Invoke(null, args)!);
        Assert.Equal(123, args[1]);
    }

    [Theory]
    [InlineData(
        "public static partial class Outer { public static partial class Inner { public static partial bool TryParse(string text, out int value); } }",
        "using Parlot.Fluent; using Parlot.SourceGenerator; using static Parlot.Fluent.Parsers; public static partial class Outer { public static partial class Inner { [GenerateParser(nameof(TryParse))] private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer); } }",
        "nested types are not supported")]
    [InlineData(
        "public partial record Grammar { public static partial bool TryParse(string text, out int value); }",
        "using Parlot.Fluent; using Parlot.SourceGenerator; using static Parlot.Fluent.Parsers; public partial record Grammar { [GenerateParser(nameof(TryParse))] private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer); }",
        "non-record partial class")]
    [InlineData(
        "public static partial class Grammar { }",
        "using Parlot.Fluent; using Parlot.SourceGenerator; using static Parlot.Fluent.Parsers; public static partial class Grammar { [GenerateParser(nameof(Missing))] private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer); private static void Missing() {} }",
        "must be a static partial definition")]
    [InlineData(
        "public static partial class Grammar { public static partial bool TryParse(string text, out int value); public static partial bool TryParse(string text, System.Threading.CancellationToken ct, out int value); }",
        "using Parlot.Fluent; using Parlot.SourceGenerator; using static Parlot.Fluent.Parsers; public static partial class Grammar { [GenerateParser(nameof(TryParse))] private static Parser<int> Build() => Terms.Number<int>(NumberOptions.Integer); }",
        "same input and output kind")]
    public void PARLOT023_Reports_Specific_Reason(string declaration, string grammar, string expectedFragment)
    {
        var (result, _) = RunStandalone(declaration, grammar);
        var diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "PARLOT023");
        Assert.Contains(expectedFragment, diagnostic.GetMessage(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Literals.Char('a').And(custom)")]
    [InlineData("OneOf<int>(Literals.Char('a').Then(1), custom)")]
    [InlineData("ZeroOrMany(custom).Then(static list => list.Count)")]
    [InlineData("custom.Then(static x => x + 1)")]
    [InlineData("Between(Literals.Char('('), custom, Literals.Char(')'))")]
    [InlineData("custom.When(static (_, x) => x > 0)")]
    public void NonSourceable_Child_Parser_Reports_Offending_Type_And_Name_In_PARLOT005(string expression)
    {
        const string declaration = """
            public static partial class UnsupportedChildGrammar
            {
                public static partial bool TryParse(string text, out object value);
            }
            """;
        var grammar = $$"""
            using Parlot;
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;
            public static partial class UnsupportedChildGrammar
            {
                private sealed class RuntimeOnlyChild : Parser<int>
                {
                    public override bool Parse(ParseContext context, ref ParseResult<int> result) => false;
                }

                [GenerateParser(nameof(TryParse))]
                private static Parser<object> Build()
                {
                    var custom = new RuntimeOnlyChild().Named("my_runtime_child");
                    return ({{expression}}).Then(static v => (object)v);
                }
            }
            """;

        var (result, _) = RunStandalone(declaration, grammar);
        var diagnostic = Assert.Single(result.Diagnostics, static d => d.Id == "PARLOT005");
        var message = diagnostic.GetMessage();
        Assert.Contains("RuntimeOnlyChild", message, StringComparison.Ordinal);
        Assert.Contains("my_runtime_child", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Partial_Class_IncludeUsings_Attributes_Are_Merged_Across_Declarations()
    {
        const string declaration = """
            namespace FirstNs { public static class FirstExt { public static string AddFirst(this string s) => s + "-first"; } }
            namespace SecondNs { public static class SecondExt { public static string AddSecond(this string s) => s + "-second"; } }

            public static partial class MergedIncludesGrammar
            {
                public static partial bool TryParse(string text, out string value);
            }
            """;
        const string grammar = """
            using Parlot.Fluent;
            using Parlot.SourceGenerator;
            using static Parlot.Fluent.Parsers;

            [IncludeUsings("FirstNs")]
            public static partial class MergedIncludesGrammar
            {
            }

            [IncludeUsings("SecondNs")]
            public static partial class MergedIncludesGrammar
            {
                [GenerateParser(nameof(TryParse))]
                private static Parser<string> Build()
                    => Literals.Text("ok").Then(static s => FirstNs.FirstExt.AddFirst(s) + "|" + SecondNs.SecondExt.AddSecond(s));
            }
            """;

        var (result, compilation) = RunStandalone(declaration, grammar);
        AssertNoErrors(result, compilation);

        var source = result.Results.SelectMany(static r => r.GeneratedSources)
            .Single(static s => s.HintName.StartsWith("StandaloneParser", StringComparison.Ordinal)).SourceText.ToString();
        Assert.Contains("using FirstNs;", source, StringComparison.Ordinal);
        Assert.Contains("using SecondNs;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LambdaRegistry_Indexes_By_Id_In_Constant_Time_With_Bounds_Checks()
    {
        var registry = new LambdaRegistry();
        Func<int, int> fn1 = static x => x + 1;
        Func<int, int> fn2 = static x => x + 2;

        var id0 = registry.Register(fn1);
        var id1 = registry.Register(fn2);

        Assert.Equal(0, id0);
        Assert.Equal(1, id1);
        Assert.Equal(-1, registry.GetPointer(-1));
        Assert.Equal(-1, registry.GetPointer(2));
        Assert.Equal(-1, registry.GetPointer(id0));
        Assert.Null(registry.GetSourceCode(-1));
        Assert.Null(registry.GetSourceCode(2));
        Assert.Null(registry.GetSourceCode(id0));
    }

    [Fact]
    public void DeferredRegistry_And_ParserHelperRegistry_Use_Reference_Equality()
    {
        var first = new AlwaysEqualParser("first");
        var second = new AlwaysEqualParser("second");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());

        var deferredRegistry = new DeferredRegistry();
        var deferred1 = deferredRegistry.GetOrCreateMethodName(first, "Deferred");
        var deferred2 = deferredRegistry.GetOrCreateMethodName(second, "Deferred");
        var deferred1Again = deferredRegistry.GetOrCreateMethodName(first, "Deferred");

        Assert.NotEqual(deferred1, deferred2);
        Assert.Equal(deferred1, deferred1Again);

        var context = new SourceGenerationContext();
        var helper1 = context.Helpers.GetOrCreate(first, "Helper", "int", () => context.CreateResult(typeof(int))).MethodName;
        var helper2 = context.Helpers.GetOrCreate(second, "Helper", "int", () => context.CreateResult(typeof(int))).MethodName;
        var helper1Again = context.Helpers.GetOrCreate(first, "Helper", "int", () => context.CreateResult(typeof(int))).MethodName;

        Assert.NotEqual(helper1, helper2);
        Assert.Equal(helper1, helper1Again);

        // Token helpers created via GenerateToken still deduplicate per parser instance
        context.IsCompacting = true;
        var token1 = context.GenerateToken(first, typeof(int), () => context.CreateResult(typeof(int)));
        var token1Again = context.GenerateToken(first, typeof(int), () => context.CreateResult(typeof(int)));
        var token2 = context.GenerateToken(second, typeof(int), () => context.CreateResult(typeof(int)));

        static string ExtractTokenHelper(SourceResult res)
        {
            var line = res.Body.First(static l => l.Contains("_Token_", StringComparison.Ordinal));
            var start = line.IndexOf("_Token_", StringComparison.Ordinal);
            var end = line.IndexOf('(', start);
            return line.Substring(start, end - start);
        }

        Assert.Equal(ExtractTokenHelper(token1), ExtractTokenHelper(token1Again));
        Assert.NotEqual(ExtractTokenHelper(token1), ExtractTokenHelper(token2));
    }

    private sealed class AlwaysEqualParser : Parser<int>
    {
        public AlwaysEqualParser(string name)
        {
            Name = name;
        }

        public override bool Parse(ParseContext context, ref ParseResult<int> result) => false;

        public override bool Equals(object? obj) => obj is AlwaysEqualParser;

        public override int GetHashCode() => 42;
    }
}
