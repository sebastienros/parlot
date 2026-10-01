using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Benchmarks.FarkleParsers;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Fluent;
using System;
using System.Text.RegularExpressions;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public partial class RegexBenchmarks
{
#if NET8_0_OR_GREATER
    [GeneratedRegex("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+")]
    private static partial Regex EmailRegexGenerated();
#endif

    public static readonly Regex EmailRegex = new("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+");
    public static readonly Regex EmailRegexCompiled = new("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+", RegexOptions.Compiled);

    public static readonly string Email = "sebastien.ros@gmail.com";

    [GlobalSetup]
    public void Setup()
    {
        if (RegexEmail() != Email) throw new Exception(nameof(RegexEmail));
        if (RegexEmailCompiled() != Email) throw new Exception(nameof(RegexEmailCompiled));
#if NET8_0_OR_GREATER
        if (RegexEmailGenerated() != Email) throw new Exception(nameof(RegexEmailGenerated));
#endif
        if (ParlotEmail() != Email) throw new Exception(nameof(ParlotEmail));
        if (ParlotEmailGenerated() != Email) throw new Exception(nameof(ParlotEmailGenerated));
        if (PidginEmail() != Email) throw new Exception(nameof(PidginEmail));
        if (FarkleEmail() != Email) throw new Exception(nameof(FarkleEmail));
    }

    [Benchmark(Baseline = true)]
    public string RegexEmailCompiled()
    {
        return EmailRegexCompiled.Match(Email).Value;
    }

    [Benchmark]
    public string RegexEmail()
    {
        return EmailRegex.Match(Email).Value;
    }

#if NET8_0_OR_GREATER
    [Benchmark]
    public string RegexEmailGenerated()
    {
        return EmailRegexGenerated().Match(Email).Value;
    }
#endif

    [Benchmark]
    public string ParlotEmail()
    {
        return EmailParser.Parser.Parse(Email).ToString();
    }

    [Benchmark]
    public string ParlotEmailGenerated()
    {
        _ = EmailParser.TryParseGenerated(Email, out var result);
        return result;
    }

    [Benchmark]
    public string PidginEmail()
    {
        return PidginEmailParser.Parse(Email);
    }

    [Benchmark]
    public string FarkleEmail()
    {
        return FarkleEmailParser.Parse(Email);
    }
}
