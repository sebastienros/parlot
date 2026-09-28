using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Parlot.SourceGeneration;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, ShortRunJob]
public class KeywordLookupBenchmarks
{
    public delegate int CharMatcher(ReadOnlySpan<char> input);
    public delegate int ByteMatcher(ReadOnlySpan<byte> input);

    private string[] _inputs;
    private byte[][] _bytes;
    private CharMatcher _tree;
    private CharMatcher _chain;
    private CharMatcher _narrow;
    private CharMatcher _hash;
    private ByteMatcher _byteTree;
    private ByteMatcher _byteChain;
    private Dictionary<string, int> _dictionary;
    private FrozenDictionary<string, int> _frozen;

    [Params("Small", "Language", "SharedPrefix", "Headers")]
    public string Vocabulary { get; set; }

    [Params(false, true)]
    public bool IgnoreCase { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var words = Words(Vocabulary);
        var comparer = IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _dictionary = words.Select((word, index) => (word, index)).ToDictionary(static item => item.word, static item => item.index, comparer);
        _frozen = _dictionary.ToFrozenDictionary(comparer);
        (_tree, _chain, _byteTree, _byteChain, _narrow, _hash) = Compile(words, IgnoreCase);
        _inputs = Enumerable.Range(0, 64).Select(index =>
        {
            var word = words[(index * 17 + index / 4) % words.Length];
            if (IgnoreCase && index % 2 == 0)
            {
                word = word.ToUpperInvariant();
            }

            return (index % 4) switch
            {
                0 => word,
                1 => "!" + word[1..],
                2 => word[..^1] + "!",
                _ => word + "x",
            };
        }).ToArray();
        _bytes = _inputs.Select(Encoding.ASCII.GetBytes).ToArray();
        for (var i = 0; i < _inputs.Length; i++)
        {
            var expected = _dictionary.TryGetValue(_inputs[i], out var index) ? index : -1;
            if (_tree(_inputs[i]) != expected || _chain(_inputs[i]) != expected || _narrow(_inputs[i]) != expected || _hash(_inputs[i]) != expected
                || _byteTree(_bytes[i]) != expected || _byteChain(_bytes[i]) != expected)
            {
                throw new InvalidOperationException($"Incorrect recognition of '{_inputs[i]}'.");
            }
        }
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = 64)]
    public int CharSequenceEqual()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _chain(input);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int CharDecisionTree()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _tree(input);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int CharNarrowTree()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _narrow(input);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int ByteSequenceEqual()
    {
        var sum = 0;
        foreach (var input in _bytes)
        {
            sum += _byteChain(input);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int ByteDecisionTree()
    {
        var sum = 0;
        foreach (var input in _bytes)
        {
            sum += _byteTree(input);
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int Dictionary()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _dictionary.TryGetValue(input, out var index) ? index : -1;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int FrozenDictionary()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _frozen.TryGetValue(input, out var index) ? index : -1;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = 64)]
    public int HashDispatch()
    {
        var sum = 0;
        foreach (var input in _inputs)
        {
            sum += _hash(input);
        }

        return sum;
    }

    public static string[] Words(string vocabulary) => vocabulary switch
    {
        "Small" => ["if", "else", "while", "return"],
        "Medium" => Words("Language").Take(16).ToArray(),
        "Language" => ["abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else", "enum",
            "event", "explicit", "extern", "false", "finally", "fixed", "float", "for", "foreach", "goto",
            "if", "implicit", "in", "int", "interface", "internal", "is", "lock", "long", "namespace", "new",
            "null", "object", "operator", "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof", "stackalloc", "static",
            "string", "struct", "switch", "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while"],
        "SharedPrefix" => Enumerable.Range(0, 128).Select(static index =>
            "commonprefix" + (char)('a' + index / 26) + (char)('a' + index % 26)).ToArray(),
        "Headers" => ["Content-Type", "Content-Length", "Content-Encoding", "Content-Language", "Content-Location",
            "Host", "User-Agent", "Accept", "Accept-Encoding", "Accept-Language", "Authorization",
            "Cache-Control", "Connection", "Cookie", "Date", "ETag", "Expires", "Last-Modified", "Location",
            "Origin", "Referer", "Server", "Set-Cookie", "Transfer-Encoding", "Vary", "Via", "Warning"],
        _ => throw new ArgumentOutOfRangeException(nameof(vocabulary)),
    };

    public static (CharMatcher Tree, CharMatcher Chain, ByteMatcher ByteTree, ByteMatcher ByteChain, CharMatcher Narrow, CharMatcher Hash) Compile(
        string[] words, bool ignoreCase, bool simulateBigEndian = false)
    {
        var source = new StringBuilder("using System; public static class Recognizer {");
        source.Append("public static int Tree(ReadOnlySpan<char> input) {")
            .Append(TreeSource(bytes: false)).Append('}');
        source.Append("public static int Chain(ReadOnlySpan<char> input) {")
            .Append(ComparisonChain(words, bytes: false, ignoreCase)).Append('}');
        source.Append("public static int Narrow(ReadOnlySpan<char> input) {")
            .Append(TreeSource(bytes: false, wide: false)).Append('}');
        source.Append("public static int ByteTree(ReadOnlySpan<byte> input) {")
            .Append(TreeSource(bytes: true)).Append('}');
        source.Append("public static int ByteChain(ReadOnlySpan<byte> input) {")
            .Append(ComparisonChain(words, bytes: true, ignoreCase)).Append('}');
        source.Append("public static int Hash(ReadOnlySpan<char> input) {").Append(HashSource(words, ignoreCase)).Append('}');
        source.Append("""
            private static ushort BigByte16(ReadOnlySpan<byte> input) =>
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(input);
            private static uint BigByte32(ReadOnlySpan<byte> input) =>
                System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(input);
            private static ulong BigByte64(ReadOnlySpan<byte> input) =>
                System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(input);
            private static uint BigChar32(ReadOnlySpan<byte> input)
            {
                var chars = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(input);
                return ((uint)chars[0] << 16) | chars[1];
            }
            private static ulong BigChar64(ReadOnlySpan<byte> input)
            {
                var chars = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(input);
                return ((ulong)chars[0] << 48) | ((ulong)chars[1] << 32) | ((ulong)chars[2] << 16) | chars[3];
            }
            }
            """);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
            .Select(static path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("KeywordRecognizer" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source.ToString())], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, checkOverflow: true));
        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        if (!emit.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emit.Diagnostics));
        }

        var type = Assembly.Load(stream.ToArray()).GetType("Recognizer", throwOnError: true);
        return (type.GetMethod("Tree").CreateDelegate<CharMatcher>(),
            type.GetMethod("Chain").CreateDelegate<CharMatcher>(),
            type.GetMethod("ByteTree").CreateDelegate<ByteMatcher>(),
            type.GetMethod("ByteChain").CreateDelegate<ByteMatcher>(),
            type.GetMethod("Narrow").CreateDelegate<CharMatcher>(),
            type.GetMethod("Hash").CreateDelegate<CharMatcher>());

        string TreeSource(bool bytes, bool wide = true)
        {
            var tree = KnownStringLookup.Generate(words, bytes, ignoreCase, wide);
            if (!simulateBigEndian)
            {
                return tree;
            }

            return tree.Replace("System.BitConverter.IsLittleEndian", "false", StringComparison.Ordinal)
                .Replace("System.Runtime.InteropServices.MemoryMarshal.Read<ushort>", "BigByte16", StringComparison.Ordinal)
                .Replace("System.Runtime.InteropServices.MemoryMarshal.Read<uint>", bytes ? "BigByte32" : "BigChar32", StringComparison.Ordinal)
                .Replace("System.Runtime.InteropServices.MemoryMarshal.Read<ulong>", bytes ? "BigByte64" : "BigChar64", StringComparison.Ordinal);
        }
    }

    private static string HashSource(string[] words, bool ignoreCase)
    {
        var output = new StringBuilder("uint hash = 2166136261; foreach (var c in input) { hash = unchecked((hash ^ ");
        output.Append(ignoreCase ? "(uint)(c is >= 'a' and <= 'z' ? c - 32 : c)" : "c");
        output.Append(") * 16777619); } switch (hash) {");
        foreach (var group in words.Select((word, index) => (word, index)).GroupBy(item =>
        {
            uint hash = 2166136261;
            foreach (var c in item.word)
            {
                hash = unchecked((hash ^ (uint)(ignoreCase && c is >= 'a' and <= 'z' ? c - 32 : c)) * 16777619);
            }

            return hash;
        }))
        {
            output.AppendLine($"case {group.Key}U:");
            foreach (var (word, index) in group)
            {
                var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(word, quote: true);
                output.AppendLine(ignoreCase
                    ? $"if (System.Text.Ascii.EqualsIgnoreCase(input, {literal}.AsSpan())) return {index};"
                    : $"if (input.SequenceEqual({literal}.AsSpan())) return {index};");
            }

            output.AppendLine("break;");
        }

        return output.AppendLine("} return -1;").ToString();
    }

    private static string ComparisonChain(string[] words, bool bytes, bool ignoreCase)
    {
        var output = new StringBuilder("switch (input.Length) {");
        foreach (var group in words.Select((text, index) => (text, index)).GroupBy(static item => item.text.Length))
        {
            output.AppendLine($"case {group.Key}:");
            foreach (var (text, index) in group)
            {
                var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true);
                var comparison = bytes
                    ? ignoreCase ? $"System.Text.Ascii.EqualsIgnoreCase(input, {literal}u8)" : $"input.SequenceEqual({literal}u8)"
                    : $"input.Equals({literal}.AsSpan(), StringComparison.{(ignoreCase ? "OrdinalIgnoreCase" : "Ordinal")})";
                output.AppendLine($"if ({comparison}) return {index};");
            }

            output.AppendLine("break;");
        }

        return output.AppendLine("} return -1;").ToString();
    }
}
