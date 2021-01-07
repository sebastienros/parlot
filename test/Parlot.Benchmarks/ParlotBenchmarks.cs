using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Calc;
using System;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ParlotBenchmarks
    {
        private const string StringWithEscapes = "This is a new line \\n \\t and a tab and some \\xa0";
        private const string StringWithoutEscapes = "This is a new line \n \t and a tab and some \xa0";

        [Benchmark]
        public string DecodeStringWithEscapes()
        {
            return Character.DecodeString(StringWithEscapes.AsSpan());
        }

        [Benchmark]
        public string DecodeStringWithoutEscapes()
        {
            return Character.DecodeString(StringWithoutEscapes.AsSpan());
        }
    }
}
