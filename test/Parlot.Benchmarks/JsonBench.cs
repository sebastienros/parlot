using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Json;
using Parlot.Benchmarks.SpracheParsers;
using Parlot.Benchmarks.SuperpowerParsers;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Fluent;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class JsonBench
    {
#nullable disable
        private string _bigJson;
        private string _longJson;
        private string _wideJson;
        private string _deepJson;
        private Func<ParseContext, IJson> _compiled;
#nullable restore

        [GlobalSetup]
        public void Setup()
        {
            _bigJson = BuildJson(4, 4, 3).ToString()!;
            _longJson = BuildJson(256, 1, 1).ToString()!;
            _wideJson = BuildJson(1, 1, 256).ToString()!;
            _deepJson = BuildJson(1, 256, 1).ToString()!;

            _compiled = Tests.CompileTests.Compile(JsonParser.Json);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Big")]
        public IJson BigJson_Parlot()
        {
            return JsonParser.Parse(_bigJson);
        }

        [Benchmark, BenchmarkCategory("Big")]
        public IJson BigJson_ParlotCompiled()
        {
            var scanner = new Scanner(_bigJson);
            var context = new ParseContext(scanner);
            return _compiled(context);
        }

        [Benchmark, BenchmarkCategory("Big")]
        public IJson BigJson_Pidgin()
        {
            return PidginJsonParser.Parse(_bigJson).Value;
        }

        [Benchmark, BenchmarkCategory("Big")]
        public IJson BigJson_Sprache()
        {
            return SpracheJsonParser.Parse(_bigJson).Value;
        }

        [Benchmark, BenchmarkCategory("Big")]
        public IJson BigJson_Superpower()
        {
            return SuperpowerJsonParser.Parse(_bigJson);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Long")]
        public IJson LongJson_Parlot()
        {
            return JsonParser.Parse(_longJson);
        }

        [Benchmark, BenchmarkCategory("Long")]
        public IJson LongJson_ParlotCompiled()
        {
            var scanner = new Scanner(_longJson);
            var context = new ParseContext(scanner);
            return _compiled(context);
        }

        [Benchmark, BenchmarkCategory("Long")]
        public IJson LongJson_Pidgin()
        {
            return PidginJsonParser.Parse(_longJson).Value;
        }

        [Benchmark, BenchmarkCategory("Long")]
        public IJson LongJson_Sprache()
        {
            return SpracheJsonParser.Parse(_longJson).Value;
        }

        [Benchmark, BenchmarkCategory("Long")]
        public IJson LongJson_Superpower()
        {
            return SuperpowerJsonParser.Parse(_longJson);
        }

        [Benchmark(Baseline = true), BenchmarkCategory("Deep")]
        public IJson DeepJson_Parlot()
        {
            return JsonParser.Parse(_deepJson);
        }

        [Benchmark, BenchmarkCategory("Deep")]
        public IJson DeepJson_ParlotCompiled()
        {
            var scanner = new Scanner(_deepJson);
            var context = new ParseContext(scanner);
            return _compiled(context);
        }

        [Benchmark, BenchmarkCategory("Deep")]
        public IJson DeepJson_Pidgin()
        {
            return PidginJsonParser.Parse(_deepJson).Value;
        }

        [Benchmark, BenchmarkCategory("Deep")]
        public IJson DeepJson_Sprache()
        {
            return SpracheJsonParser.Parse(_deepJson).Value;
        }

        //this one blows the stack
        //[Benchmark, BenchmarkCategory("Deep")]
        //public IJson DeepJson_Superpower()
        //{
        //    return SuperpowerJsonParser.Parse(_deepJson);
        //}

        [Benchmark(Baseline = true), BenchmarkCategory("Wide")]
        public IJson WideJson_Parlot()
        {
            return JsonParser.Parse(_wideJson);
        }

        [Benchmark, BenchmarkCategory("Wide")]
        public IJson WideJson_ParlotCompiled()
        {
            var scanner = new Scanner(_wideJson);
            var context = new ParseContext(scanner);
            return _compiled(context);
        }

        [Benchmark, BenchmarkCategory("Wide")]
        public IJson WideJson_Pidgin()
        {
            return PidginJsonParser.Parse(_wideJson).Value;
        }

        [Benchmark, BenchmarkCategory("Wide")]
        public IJson WideJson_Sprache()
        {
            return SpracheJsonParser.Parse(_wideJson).Value;
        }

        [Benchmark, BenchmarkCategory("Wide")]
        public IJson WideJson_Superpower()
        {
            return SuperpowerJsonParser.Parse(_wideJson);
        }

        private static IJson BuildJson(int length, int depth, int width)
            => new JsonArray(
                Enumerable.Repeat(1, length)
                    .Select(_ => BuildObject(depth, width))
                    .ToImmutableArray()
            );

        private static IJson BuildObject(int depth, int width)
        {
            if (depth == 0)
            {
                return new JsonString(RandomString(6));
            }
            return new JsonObject(
                Enumerable.Repeat(1, width)
                    .Select(_ => new KeyValuePair<string, IJson>(RandomString(5), BuildObject(depth - 1, width)))
                    .ToImmutableDictionary()
            );
        }

        private static readonly Random random = new();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }
}
