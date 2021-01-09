using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ParlotBenchmarks
    {
        private const string _stringWithEscapes = "This is a new line \\n \\t and a tab and some \\xa0";
        private const string _stringWithoutEscapes = "This is a new line \n \t and a tab and some \xa0";

        [Benchmark]
        public TextSpan DecodeStringWithEscapes()
        {
            return Character.DecodeString(_stringWithEscapes);
        }

        [Benchmark]
        public TextSpan DecodeStringWithoutEscapes()
        {
            return Character.DecodeString(_stringWithoutEscapes);
        }

        [Benchmark]
        public string DecodeStringWithEscapesToString()
        {
            return Character.DecodeString(_stringWithEscapes).ToString();
        }

        [Benchmark]
        public string DecodeStringWithoutEscapesToString()
        {
            return Character.DecodeString(_stringWithoutEscapes).ToString();
        }
    }
}
