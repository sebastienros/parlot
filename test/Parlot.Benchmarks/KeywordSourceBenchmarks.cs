using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace Parlot.Benchmarks;

[MemoryDiagnoser]
public partial class KeywordSourceBenchmarks
{
    internal static readonly string[] TypeKeywords =
        ["bool", "byte", "sbyte", "short", "ushort", "int", "uint", "long",
            "ulong", "char", "float", "double", "decimal", "string", "object", "var"];

    internal static readonly string[] ModifierKeywords =
        ["public", "private", "protected", "internal", "static", "readonly", "volatile", "const"];

    private string _input;

    [Params(8, 128)]
    public int DeclarationCount { get; set; }

    [Params("Valid", "UnknownType", "InvalidExpression")]
    public string Scenario { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string[] modifiers = ["", "public ", "private static ", "protected readonly ",
            "internal ", "public volatile ", "private const ", "static "];
        int[] modifierCounts = [0, 1, 2, 2, 1, 2, 2, 1];
        var source = new StringBuilder();
        var expected = new List<KeywordSourceDeclaration>();
        for (var i = 0; i < DeclarationCount; i++)
        {
            var type = TypeKeywords[i * 7 % TypeKeywords.Length];
            var name = FormattableString.Invariant($"field{i}");
            var expression = FormattableString.Invariant($"({i + 1} + 2) * 3 - 1");
            var last = i == DeclarationCount - 1;
            if (last && Scenario == "UnknownType")
            {
                type = "integer";
            }
            else if (last && Scenario == "InvalidExpression")
            {
                expression = "(1 + ) * 3";
            }

            source.Append(modifiers[i % modifiers.Length]).Append(type).Append(' ').Append(name)
                .Append(" = ").Append(expression).Append(";\n");
            expected.Add(new KeywordSourceDeclaration(type, name, modifierCounts[i % modifierCounts.Length], (i + 3) * 3 - 1));
        }

        _input = source.ToString();
        var expectedSuccess = Scenario == "Valid";
        if (TryParseBefore(_input, out var before) != expectedSuccess
            || TryParseAfter(_input, out var after) != expectedSuccess)
        {
            throw new InvalidOperationException($"Unexpected source parsing outcome for {Scenario}.");
        }

        if (!expectedSuccess)
        {
            if (before != null || after != null)
            {
                throw new InvalidOperationException("Failed source parsing must not return a partial declaration list.");
            }

            return;
        }

        if (before.Count != expected.Count || after.Count != expected.Count)
        {
            throw new InvalidOperationException("Source parsing returned an incorrect declaration count.");
        }

        for (var i = 0; i < expected.Count; i++)
        {
            if (before[i] != expected[i] || after[i] != expected[i])
            {
                throw new InvalidOperationException($"Source parsing returned an incorrect declaration at index {i}.");
            }
        }
    }

    [Benchmark(Baseline = true)]
    public IReadOnlyList<KeywordSourceDeclaration> Before() =>
        TryParseBefore(_input, out var value) ? value : null;

    [Benchmark]
    public IReadOnlyList<KeywordSourceDeclaration> After() =>
        TryParseAfter(_input, out var value) ? value : null;

    public static partial bool TryParseBefore(string input, out IReadOnlyList<KeywordSourceDeclaration> value);
    public static partial bool TryParseAfter(string input, out IReadOnlyList<KeywordSourceDeclaration> value);
}

public sealed record KeywordSourceDeclaration(string Type, string Name, int ModifierCount, int Value);
