using BenchmarkDotNet.Running;
using System.Threading;

namespace Parlot.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            //var benchmarks = new ParlotBenchmarks();
            //benchmarks.Setup();

            //for (int i = 0; i < 1000; i++)
            //{
            //    benchmarks.DeepJson();
            //}

            //Thread.Sleep(3000);

            //for (int i = 0; i < 1000; i++)
            //{
            //    benchmarks.DeepJson();
            //}

        }
    }
}
