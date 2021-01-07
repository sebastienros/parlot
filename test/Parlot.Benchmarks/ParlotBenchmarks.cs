using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ParlotBenchmarks
    {
        private const string StringWithEscapes = "This is a new line \\n \\t and a tab and some \\xa0";
        private const string StringWithoutEscapes = "This is a new line \n \t and a tab and some \xa0";

        [Benchmark]
        public ReadOnlySpan<char> DecodeStringWithEscapes()
        {
            return Character.DecodeString(StringWithEscapes.AsSpan());
        }

        [Benchmark]
        public ReadOnlySpan<char> DecodeStringWithoutEscapes()
        {
            return Character.DecodeString(StringWithoutEscapes.AsSpan());
        }

        [Benchmark]
        public string DecodeStringWithEscapesToString()
        {
            return Character.DecodeString(StringWithEscapes.AsSpan()).ToString();
        }

        [Benchmark]
        public string DecodeStringWithoutEscapesToString()
        {
            return Character.DecodeString(StringWithoutEscapes.AsSpan()).ToString();
        }
    }
}
