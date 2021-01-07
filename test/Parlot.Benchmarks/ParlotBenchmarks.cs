using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Calc;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ParlotBenchmarks
    {
        private readonly Parser _parser = new();

        private const string StringWithEscapes = "This is a new line \\n \\t and a tab and some \\xD83D";
        private const string StringWithoutEscapes = "This is a new line \n \t and a tab and some \xD83D";

        [Benchmark]
        public Expression DecodeStringWithEscapes()
        {
            return _parser.Parse(StringWithEscapes);
        }

        [Benchmark]
        public Expression DecodeStringWithoutEscapes()
        {
            return _parser.Parse(StringWithoutEscapes);
        }
    }
}
