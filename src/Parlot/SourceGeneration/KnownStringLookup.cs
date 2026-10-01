#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Parlot.SourceGeneration;

internal static class KnownStringLookup
{
    internal static string Generate(IReadOnlyList<string> words, bool bytes, bool ignoreCase, bool? wide = null,
        Func<int, string>? matchExpression = null, string failureExpression = "-1",
        bool splitByLength = true, string resultType = "int")
    {
        if ((bytes || ignoreCase) && words.Any(static word => word.Any(static c => c > 127)))
        {
            throw new ArgumentException("Byte recognition and case folding require an ASCII vocabulary.", nameof(words));
        }

        var output = new StringBuilder();
        var number = 0;
        var candidates = words.Select((text, index) => new Candidate(text, index))
            .GroupBy(static candidate => candidate.Text, ignoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(static group => group.First()).ToArray();
        var helpers = new StringBuilder();
        var inlineCandidates = 0;
        var lengthGroups = candidates.GroupBy(static candidate => candidate.Text.Length).OrderBy(static group => group.Key).ToArray();
        var useLengthHelpers = splitByLength && candidates.Length > 32 && lengthGroups.Length > 1
            && candidates.Sum(static candidate => candidate.Text.Length) > 1024;
        output.AppendLine("switch (input.Length) {");
        foreach (var group in lengthGroups)
        {
            output.AppendLine(FormattableString.Invariant($"case {group.Key}: {{"));
            if (useLengthHelpers && (group.Count() > 4 || inlineCandidates + group.Count() > 32))
            {
                output.AppendLine(FormattableString.Invariant($"return MatchLength{group.Key}(input);"));
                output.AppendLine("}");
                var dispatch = output;
                output = helpers;
                output.AppendLine(FormattableString.Invariant($"static {resultType} MatchLength{group.Key}(System.ReadOnlySpan<{(bytes ? "byte" : "char")}> input) {{"));
                if (!Emit(group.ToArray(), new bool[group.Key]))
                {
                    output.AppendLine(FormattableString.Invariant($"return {failureExpression};"));
                }

                output.AppendLine("}");
                output = dispatch;
                continue;
            }

            inlineCandidates += group.Count();
            if (!Emit(group.ToArray(), new bool[group.Key]))
            {
                output.AppendLine("break;");
            }

            output.AppendLine("}");
        }

        output.AppendLine(FormattableString.Invariant($"}} return {failureExpression};"));
        output.Append(helpers);
        return output.ToString();

        bool Emit(Candidate[] remaining, bool[] proven)
        {
            if (remaining.Length > 1)
            {
                proven = (bool[])proven.Clone();
                var common = new List<string>();
                for (var width = bytes ? 8 : 4; width >= 1; width /= 2)
                {
                    for (var offset = 0; offset + width <= proven.Length; offset++)
                    {
                        if (Enumerable.Range(offset, width).Any(i => proven[i])
                            || remaining.Select(candidate => (Mask(candidate.Text, offset, width), Value(candidate.Text, offset, width))).Distinct().Count() != 1)
                        {
                            continue;
                        }

                        common.Add(Test(remaining[0], offset, width));
                        Prove(proven, offset, width);
                    }
                }

                if (common.Count > 0)
                {
                    output.AppendLine(FormattableString.Invariant($"if (!({string.Join(" && ", common)})) return {failureExpression};"));
                }
            }

            if (remaining.Length == 1)
            {
                var tests = new List<string>();
                for (var offset = 0; offset < proven.Length;)
                {
                    if (proven[offset])
                    {
                        offset++;
                        continue;
                    }

                    var width = bytes ? 8 : 4;
                    while (offset + width > proven.Length || Enumerable.Range(offset, width).Any(i => proven[i]))
                    {
                        width /= 2;
                    }

                    tests.Add(Test(remaining[0], offset, width));
                    offset += width;
                }

                var match = matchExpression?.Invoke(remaining[0].Index) ?? remaining[0].Index.ToString(CultureInfo.InvariantCulture);
                output.AppendLine(tests.Count == 0
                    ? $"return {match};"
                    : $"if ({string.Join(" && ", tests)}) return {match};");
                return tests.Count == 0;
            }

            var bestOffset = -1;
            var bestWidth = 0;
            var bestLargest = remaining.Length;
            var bestDistinct = 0;
            for (var width = (wide ?? remaining.Length <= 32) ? (bytes ? 8 : 4) : 1; width >= 1; width /= 2)
            {
                for (var offset = 0; offset + width <= proven.Length; offset++)
                {
                    if (Enumerable.Range(offset, width).Any(i => proven[i]))
                    {
                        continue;
                    }

                    // All candidates at a node must use the same mask for a switch.
                    if (remaining.Select(candidate => Mask(candidate.Text, offset, width)).Distinct().Count() != 1)
                    {
                        continue;
                    }

                    var partitions = remaining.GroupBy(candidate => Value(candidate.Text, offset, width)).ToArray();
                    var largest = partitions.Max(static group => group.Count());
                    if (partitions.Length > 1 && (largest < bestLargest
                        || largest == bestLargest && partitions.Length > bestDistinct))
                    {
                        bestOffset = offset;
                        bestWidth = width;
                        bestLargest = largest;
                        bestDistinct = partitions.Length;
                    }
                }
            }

            if (bestOffset < 0)
            {
                foreach (var candidate in remaining)
                {
                    if (Emit([candidate], proven))
                    {
                        return true;
                    }
                }

                return false;
            }

            var local = $"chunk{number++}";
            var mask = Mask(remaining[0].Text, bestOffset, bestWidth);
            output.AppendLine(FormattableString.Invariant($"var {local} = {Load(bestOffset, bestWidth)} & {Constant(mask)};"));
            output.AppendLine(FormattableString.Invariant($"switch ({local}) {{"));
            var nextProven = (bool[])proven.Clone();
            Prove(nextProven, bestOffset, bestWidth);
            foreach (var group in remaining.GroupBy(candidate => Value(candidate.Text, bestOffset, bestWidth)))
            {
                output.AppendLine(FormattableString.Invariant($"case {Constant(group.Key)}: {{"));
                if (!Emit(group.ToArray(), nextProven))
                {
                    output.AppendLine("break;");
                }

                output.AppendLine("}");
            }

            output.AppendLine("}");
            return false;
        }

        string Test(Candidate candidate, int offset, int width) =>
            $"({Load(offset, width)} & {Constant(Mask(candidate.Text, offset, width))}) == {Constant(Value(candidate.Text, offset, width))}";

        string Load(int offset, int width)
        {
            if (width == 1)
            {
                return $"(ulong)input[{offset}]";
            }

            var type = (bytes ? width : width * 2) switch { 2 => "ushort", 4 => "uint", _ => "ulong" };
            var slice = $"input.Slice({offset}, {width})";
            var span = bytes ? slice : $"System.Runtime.InteropServices.MemoryMarshal.AsBytes({slice})";
            var load = $"System.Runtime.InteropServices.MemoryMarshal.Read<{type}>({span})";
            // Constants use little-endian lane order, independently of the compiler host.
            var reverse = type == "ushort" ? $"unchecked((ushort)(({load} >> 8) | ({load} << 8)))"
                : $"System.Buffers.Binary.BinaryPrimitives.ReverseEndianness({load})";
            if (!bytes)
            {
                // Reverse char lanes, not the bytes within each UTF-16 code unit.
                reverse = width == 2
                    ? $"(({load} >> 16) | ({load} << 16))"
                    : $"(({load} >> 48) | (({load} >> 16) & 0xFFFF0000UL) | (({load} << 16) & 0xFFFF00000000UL) | ({load} << 48))";
            }

            return $"(ulong)(System.BitConverter.IsLittleEndian ? {load} : {reverse})";
        }

        ulong Mask(string text, int offset, int width)
        {
            ulong value = 0;
            for (var i = 0; i < width; i++)
            {
                var letter = text[offset + i] is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
                var mask = bytes ? 0xffUL : 0xffffUL;
                if (ignoreCase && letter)
                {
                    mask &= ~0x20UL;
                }

                value |= mask << Shift(i);
            }

            return value;
        }

        ulong Value(string text, int offset, int width)
        {
            ulong value = 0;
            for (var i = 0; i < width; i++)
            {
                value |= (ulong)text[offset + i] << Shift(i);
            }

            return value & Mask(text, offset, width);
        }

        int Shift(int index) => index * (bytes ? 8 : 16);
    }

    internal static string GeneratePrefixes(IReadOnlyList<string> words)
    {
        var helpers = new StringBuilder();
        var number = 0;
        var body = Emit(words.Select((text, index) => new Candidate(text, index)).ToArray(), "null");
        return body + helpers;

        string Emit(Candidate[] candidates, string failure)
        {
            // An earlier complete prefix wins over every longer candidate after it.
            var terminal = Array.FindIndex(candidates, static candidate => candidate.Text.Length == 0);
            if (terminal >= 0)
            {
                failure = LiteralHelper.StringToLiteral(words[candidates[terminal].Index]);
                candidates = candidates.Take(terminal).ToArray();
            }

            if (candidates.Length == 0)
            {
                return $"return {failure};";
            }

            var length = candidates.Min(static candidate => candidate.Text.Length);
            var groups = candidates.GroupBy(candidate => candidate.Text.Substring(0, length), StringComparer.Ordinal).ToArray();
            var matches = new string[groups.Length];
            for (var i = 0; i < groups.Length; i++)
            {
                var suffixes = groups[i].Select(candidate => new Candidate(candidate.Text.Substring(length), candidate.Index)).ToArray();
                if (suffixes[0].Text.Length == 0)
                {
                    matches[i] = LiteralHelper.StringToLiteral(words[suffixes[0].Index]);
                }
                else
                {
                    var name = $"MatchPrefix{number++}";
                    matches[i] = $"{name}(text.Slice({length}))";
                    var helper = Emit(suffixes, failure);
                    helpers.AppendLine(FormattableString.Invariant($"static string {name}(System.ReadOnlySpan<char> text) {{"));
                    helpers.AppendLine(helper);
                    helpers.AppendLine("}");
                }
            }

            return $"if (text.Length < {length}) return {failure};\nvar input = text.Slice(0, {length});\n"
                + Generate(groups.Select(static group => group.Key).ToArray(), bytes: false, ignoreCase: false,
                    matchExpression: index => matches[index], failureExpression: failure, splitByLength: false);
        }
    }

    private static void Prove(bool[] proven, int offset, int width)
    {
        for (var i = offset; i < offset + width; i++)
        {
            proven[i] = true;
        }
    }

    private static string Constant(ulong value) => "0x" + value.ToString("X", CultureInfo.InvariantCulture) + "UL";

    private sealed record Candidate(string Text, int Index);
}
