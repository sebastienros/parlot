using BenchmarkDotNet.Running;
using Parlot.Tests.Calc;
using System;

namespace Parlot.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<ExpressionBenchmarks>();
        }

        //static void Main()
        //{
        //    var benchmarks = new ExpressionBenchmarks();
        //    Expression expression = null;
        //    for (var i = 0; i < 1000; i++)
        //    {
        //        expression = benchmarks.FluentExpression1();
        //    }

        //    Console.WriteLine(expression);
        //}
    }
}
