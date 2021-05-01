using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Parlot.Fluent.Parsers<Parlot.Fluent.ParseContext>;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class RegexBenchmarks
    {
        public static readonly Regex EmailRegex = new("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+");
        public static readonly Regex EmailRegexCompiled = new("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+", RegexOptions.Compiled);

        public static readonly Parser<char, ParseContext> Dot = Literals.Char('.');
        public static readonly Parser<char, ParseContext> Plus = Literals.Char('+');
        public static readonly Parser<char, ParseContext> Minus = Literals.Char('-');
        public static readonly Parser<char, ParseContext> At = Literals.Char('@');
        public static readonly Parser<TextSpan, ParseContext> WordChar = Literals.Pattern(char.IsLetterOrDigit);
        public static readonly Parser<List<char>, ParseContext> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Plus, Minus));
        public static readonly Parser<List<char>, ParseContext> WordDotMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Minus));
        public static readonly Parser<List<char>, ParseContext> WordMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Minus));
        public static readonly Parser<TextSpan, ParseContext> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

        public static readonly IParser<TextSpan, ParseContext> EmailCompiled = Email.Compile();

        private static readonly string _email = "sebastien.ros@gmail.com";

        public RegexBenchmarks()
        {
            if (!RegexEmail()) throw new Exception(nameof(RegexEmail));
            if (!RegexEmailCompiled()) throw new Exception(nameof(RegexEmailCompiled));
            if (!ParlotEmail()) throw new Exception(nameof(ParlotEmail));
            if (!ParlotEmailCompiled()) throw new Exception(nameof(ParlotEmailCompiled));
        }

        [Benchmark]
        public bool RegexEmail()
        {
            return EmailRegex.Match(_email).Success;
        }

        [Benchmark]
        public bool RegexEmailCompiled()
        {
            return EmailRegexCompiled.Match(_email).Success;
        }

        [Benchmark]
        public bool ParlotEmail()
        {
            return Email.TryParse(_email, out _);
        }

        [Benchmark]
        public bool ParlotEmailCompiled()
        {
            return EmailCompiled.Parse(_email).Length > 0;
        }
    }
}
