using BenchmarkDotNet.Running;

namespace Parlot.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<ExpressionBenchmarks>();
        }
    }
}
