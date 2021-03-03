using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class RegexBenchmarks
    {
        private readonly Regex EmailRegex = new Regex("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+");
        private readonly Regex EmailRegexCompiled = new Regex("[\\w\\.+-]+@[\\w-]+\\.[\\w\\.-]+", RegexOptions.Compiled);

        private static readonly Parser<char> Dot = Literals.Char('.');
        private static readonly Parser<char> Plus = Literals.Char('+');
        private static readonly Parser<char> Minus = Literals.Char('-');
        private static readonly Parser<char> At = Literals.Char('@');
        private static readonly Parser<TextSpan> WordChar = Literals.Pattern(char.IsLetterOrDigit);
        private static readonly Parser<List<char>> WordDotPlusMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Plus, Minus));
        private static readonly Parser<List<char>> WordDotMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Dot, Minus));
        private static readonly Parser<List<char>> WordMinus = OneOrMany(OneOf(WordChar.Then(x => 'w'), Minus));
        private static readonly Parser<TextSpan> Email = Capture(WordDotPlusMinus.And(At).And(WordMinus).And(Dot).And(WordDotMinus));

        private static readonly Func<ParseContext, TextSpan> EmailCompiled = Tests.CompileTests.Compile(Email);

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
            return Email.TryParse(_email, out var result);
        }

        private static readonly Scanner scanner = new Scanner(_email);
        private static readonly ParseContext context = new ParseContext(scanner);

        [Benchmark]
        public bool ParlotEmailCompiled()
        {
            scanner.Cursor.ResetPosition(TextPosition.Start);
            return EmailCompiled(context).Length > 0;
        }
    }
}
