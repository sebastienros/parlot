using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System;

namespace Parlot.Benchmarks
{
    /// <summary>
    /// Shows that ROS.IndexOf(string/ROS) is always the fastest option when Ordinal comparison should
    /// be used. Using the char overload is preferred too.
    /// 
    /// BenchmarkDotNet v0.14.0, Windows 11 (10.0.22631.4037/23H2/2023Update/SunValley3)
    /// 12th Gen Intel Core i7-1260P, 1 CPU, 16 logical and 12 physical cores
    /// .NET SDK 9.0.100-preview.7.24407.12
    ///   [Host]   : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
    ///   ShortRun : .NET 8.0.8 (8.0.824.36612), X64 RyuJIT AVX2
    /// 
    /// Job=ShortRun  IterationCount=3  LaunchCount=1
    /// WarmupCount=3
    /// 
    /// | Method                     | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
    /// |--------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
    /// | RosIndexOfChar             |  1.427 ns | 0.2525 ns | 0.0138 ns |  1.00 |    0.01 |         - |          NA |
    /// | RosIndexOfString           |  2.720 ns | 0.7143 ns | 0.0392 ns |  1.91 |    0.03 |         - |          NA |
    /// | RosIndexOfStringOrdinal    |  2.860 ns | 1.3014 ns | 0.0713 ns |  2.00 |    0.05 |         - |          NA |
    /// |                            |           |           |           |       |         |           |             |
    /// | StringIndexOfChar          |  1.500 ns | 1.4386 ns | 0.0789 ns |  1.00 |    0.06 |         - |          NA |
    /// | StringIndexOfString        | 20.854 ns | 6.4076 ns | 0.3512 ns | 13.92 |    0.65 |         - |          NA |
    /// | StringIndexOfStringOrdinal |  3.890 ns | 1.6617 ns | 0.0911 ns |  2.60 |    0.13 |         - |          NA |
    /// 
    /// </summary>
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    public class IndexOfBenchmarks
    {
        private const string _helloWorldNoEscape = "Hello World!!!/n";
        private const string _helloWorldEscape = "Hello World!!!\\n";
        
        [Benchmark(Baseline = true), BenchmarkCategory("ROS.IndexOf")]
        public int RosIndexOfChar()
        {
            return _helloWorldNoEscape.AsSpan().IndexOf('\\');
        }

        [Benchmark, BenchmarkCategory("ROS.IndexOf")]
        public int RosIndexOfString()
        {
            return _helloWorldNoEscape.AsSpan().IndexOf("\\");
        }

        [Benchmark, BenchmarkCategory("ROS.IndexOf")]
        public int RosIndexOfStringOrdinal()
        {
            return _helloWorldNoEscape.AsSpan().IndexOf("\\", StringComparison.Ordinal);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("String.IndexOf")]
        public int StringIndexOfChar()
        {
            return _helloWorldNoEscape.IndexOf('\\');
        }

        [Benchmark, BenchmarkCategory("String.IndexOf")]
        public int StringIndexOfString()
        {
            return _helloWorldNoEscape.IndexOf("\\");
        }

        [Benchmark, BenchmarkCategory("String.IndexOf")]
        public int StringIndexOfStringOrdinal()
        {
            return _helloWorldNoEscape.IndexOf("\\", StringComparison.Ordinal);
        }
    }
}
