using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using Parlot.SourceGenerator;
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

    public static readonly Parser<TextSpan> EmailCompiled = EmailParser.Parser.Compile();

    public static readonly string Email = "sebastien.ros@gmail.com";

    [GlobalSetup]
    public void Setup()
    {
        if (RegexEmail() != Email) throw new Exception(nameof(RegexEmail));
        if (RegexEmailCompiled() != Email) throw new Exception(nameof(RegexEmailCompiled));
        if (ParlotEmail() != Email) throw new Exception(nameof(ParlotEmail));
        if (ParlotEmailCompiled() != Email) throw new Exception(nameof(ParlotEmailCompiled));
        if (EmailParser.GeneratedParser().Parse(Email).ToString() != Email) throw new Exception(nameof(ParlotEmailSourceGenerated));
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
    public TextSpan ParlotEmailCompiled()
    {
        return EmailCompiled.Parse(Email);
    }

    [Benchmark]
    public TextSpan ParlotEmail()
    {
        return EmailParser.Parser.Parse(Email);
    }

    [Benchmark]
    public TextSpan ParlotEmailSourceGenerated()
    {
        return EmailParser.GeneratedParser().Parse(Email);
    }
}

