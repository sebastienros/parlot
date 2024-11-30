using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public partial class RegexBenchmarks
{
    [GeneratedRegex("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+")]
    private static partial Regex EmailRegexGenerated();

    public static readonly Regex EmailRegex = new("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+");
    public static readonly Regex EmailRegexCompiled = new("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+", RegexOptions.Compiled);

    public static readonly Parser<char> Dot = Literals.Char('.');
    public static readonly Parser<char> Plus = Literals.Char('+');
    public static readonly Parser<char> Minus = Literals.Char('-');
    public static readonly Parser<char> At = Literals.Char('@');
    public static readonly Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit);
    public static readonly Parser<IReadOnlyList<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Plus, Minus));
    public static readonly Parser<IReadOnlyList<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Minus));
    public static readonly Parser<IReadOnlyList<char>> WordMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Minus));
    public static readonly Parser<TextSpan> EmailParser = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

    public static readonly Parser<TextSpan> EmailCompiled = EmailParser.Compile();

    public static readonly string Email = "sebastien.ros@gmail.com";

    [GlobalSetup]
    public void Setup()
    {
        if (RegexEmail() != Email) throw new Exception(nameof(RegexEmail));
        if (RegexEmailCompiled() != Email) throw new Exception(nameof(RegexEmailCompiled));
        if (ParlotEmail() != Email) throw new Exception(nameof(ParlotEmail));
        if (ParlotEmailCompiled() != Email) throw new Exception(nameof(ParlotEmailCompiled));
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
        return EmailParser.Parse(Email);
    }
}
