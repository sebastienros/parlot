using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;
using System.Text.RegularExpressions;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class RegexBenchmarks
    {
        private readonly Regex EmailRegex = new Regex("[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+");
        private readonly Regex EmailRegexCompiled = new Regex("[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+", RegexOptions.Compiled);

        [Benchmark]
        public bool RegexEmail()
        {
            return EmailRegex.Match("sebastien.ros@gmail.com").Success;
        }


        [Benchmark]
        public bool RegexEmailCompiled()
        {
            return EmailRegexCompiled.Match("sebastien.ros@gmail.com").Success;
        }
    }
}
