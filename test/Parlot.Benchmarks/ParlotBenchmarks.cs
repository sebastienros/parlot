﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Calc;
using Parlot.Tests.Json;

namespace Parlot.Benchmarks
{
    [MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
    public class ParlotBenchmarks
    {
        private const string _stringWithEscapes = "This is a new line \\n \\t and a tab and some \\xa0";
        private const string _stringWithoutEscapes = "This is a new line \n \t and a tab and some \xa0";

        private readonly JsonBench _jsonBench = new();
        private readonly ExprBench _exprBench = new();

        [GlobalSetup]
        public void Setup()
        {
            _jsonBench.Setup();
        }

        [Benchmark, BenchmarkCategory("DecodeString")]
        public BufferSpan<char> DecodeStringWithoutEscapes()
        {
            return Character.DecodeString(_stringWithoutEscapes);
        }

        [Benchmark, BenchmarkCategory("DecodeString")]
        public BufferSpan<char> DecodeStringWithEscapes()
        {
            return Character.DecodeString(_stringWithEscapes);
        }

        [Benchmark, BenchmarkCategory("Expressions - Small")]
        public Expression ExpressionRawSmall()
        {
            return _exprBench.ParlotRawSmall();
        }

        [Benchmark, BenchmarkCategory("Expressions - Small")]
        public Expression ExpressionCompiledSmall()
        {
            return _exprBench.ParlotCompiledSmall();
        }

        [Benchmark, BenchmarkCategory("Expressions - Small")]
        public Expression ExpressionFluentSmall()
        {
            return _exprBench.ParlotFluentSmall();
        }

        [Benchmark, BenchmarkCategory("Expressions - Big")]
        public Expression ExpressionRawBig()
        {
            return _exprBench.ParlotRawBig();
        }

        [Benchmark, BenchmarkCategory("Expressions - Big")]
        public Expression ExpressionCompiledBig()
        {
            return _exprBench.ParlotCompiledBig();
        }

        [Benchmark, BenchmarkCategory("Expressions - Big")]
        public Expression ExpressionFluentBig()
        {
            return _exprBench.ParlotFluentBig();
        }

        [Benchmark, BenchmarkCategory("Json - Big")]
        public IJson BigJson()
        {
            return _jsonBench.BigJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json - Big")]
        public IJson BigJsonCompiled()
        {
            return _jsonBench.BigJson_ParlotCompiled();
        }

        [Benchmark, BenchmarkCategory("Json - Deep")]
        public IJson DeepJson()
        {
            return _jsonBench.DeepJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json - Deep")]
        public IJson DeepJsonCompiled()
        {
            return _jsonBench.DeepJson_ParlotCompiled();
        }

        [Benchmark, BenchmarkCategory("Json - Long")]
        public IJson LongJson()
        {
            return _jsonBench.LongJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json - Long")]
        public IJson LongJsonCompiled()
        {
            return _jsonBench.LongJson_ParlotCompiled();
        }

        [Benchmark, BenchmarkCategory("Json - Wide")]
        public IJson WideJson()
        {
            return _jsonBench.WideJson_Parlot();
        }

        [Benchmark, BenchmarkCategory("Json - Wide")]
        public IJson WideJsonCompiled()
        {
            return _jsonBench.WideJson_ParlotCompiled();
        }
    }
}
