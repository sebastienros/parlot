using BenchmarkDotNet.Running;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Parlot.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

            //var bench = new ExprBench();

            //for (var i = 0; i < 100; i++)
            //{
            //    bench.ParlotFluentBig();
            //}

            //Console.WriteLine("starting");
            //var sw = Stopwatch.StartNew();
            //for (var i = 0; i < 1000; i++)
            //{
            //    bench.ParlotFluentBig();
            //}
            //Console.WriteLine(sw.ElapsedMilliseconds);
        }

        [MethodImpl(MethodImplOptions.NoOptimization)]
        void MakeUseOf(object o)
        {
        }
    }
}
