using System;
using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Parlot.Tests.Json;
using Parlot.Benchmarks.SpracheParsers;
using Parlot.Benchmarks.SuperpowerParsers;
using Parlot.Benchmarks.PidginParsers;
using Parlot.Fluent;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.Json;

namespace Parlot.Benchmarks;

[MemoryDiagnoser, GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory), ShortRunJob]
public class JsonBench
{
#nullable disable
    private string _bigJson;
    private string _longJson;
    private string _wideJson;
    private string _deepJson;
    private Parser<IJson> _compiled;
#nullable restore

    private static readonly JsonSerializerSettings _jsonSerializerSettings = new() { MaxDepth = 1024 };
    private static readonly JsonDocumentOptions _jsonDocumentOptions = new() { MaxDepth = 1024 };
    private static readonly Random _random = new();

    [GlobalSetup]
    public void Setup()
    {
        _bigJson = BuildJson(4, 4, 3).ToString()!;
        _longJson = BuildJson(256, 1, 1).ToString()!;
        _wideJson = BuildJson(1, 1, 256).ToString()!;
        _deepJson = BuildJson(1, 256, 1).ToString()!;

        _compiled = JsonParser.Json.Compile();
    }

    [Benchmark(Baseline = true), BenchmarkCategory("Big")]
    public IJson BigJson_ParlotCompiled()
    {
        return _compiled.Parse(_bigJson);
    }

    [Benchmark, BenchmarkCategory("Big")]
    public IJson BigJson_Parlot()
    {
        return JsonParser.Parse(_bigJson);
    }

    [Benchmark, BenchmarkCategory("Big")]
    public IJson BigJson_Pidgin()
    {
        return PidginJsonParser.Parse(_bigJson).Value;
    }

    [Benchmark, BenchmarkCategory("Big")]
    public JToken BigJson_Newtonsoft()
    {
        return JToken.Parse(_bigJson);
    }

    [Benchmark, BenchmarkCategory("Big")]
    public JsonDocument BigJson_SystemTextJson()
    {
        return JsonDocument.Parse(_bigJson);
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
    public IJson LongJson_ParlotCompiled()
    {
        return _compiled.Parse(_longJson);
    }

    [Benchmark, BenchmarkCategory("Long")]
    public IJson LongJson_Parlot()
    {
        return JsonParser.Parse(_longJson);
    }

    [Benchmark, BenchmarkCategory("Long")]
    public IJson LongJson_Pidgin()
    {
        return PidginJsonParser.Parse(_longJson).Value;
    }

    [Benchmark, BenchmarkCategory("Long")]
    public JToken LongJson_Newtonsoft()
    {
        return JToken.Parse(_longJson);
    }


    [Benchmark, BenchmarkCategory("Long")]
    public JsonDocument LongJson_SystemTextJson()
    {
        return JsonDocument.Parse(_longJson);
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
    public IJson DeepJson_ParlotCompiled()
    {
        return _compiled.Parse(_deepJson);
    }

    [Benchmark, BenchmarkCategory("Deep")]
    public IJson DeepJson_Parlot()
    {
        return JsonParser.Parse(_deepJson);
    }

    [Benchmark, BenchmarkCategory("Deep")]
    public IJson DeepJson_Pidgin()
    {
        return PidginJsonParser.Parse(_deepJson).Value;
    }

    [Benchmark, BenchmarkCategory("Deep")]
    public JToken DeepJson_Newtonsoft()
    {
        return JsonConvert.DeserializeObject<JToken>(_deepJson, _jsonSerializerSettings);
    }

    [Benchmark, BenchmarkCategory("Deep")]
    public JsonDocument DeepJson_SystemTextJson()
    {
        return JsonDocument.Parse(_deepJson, _jsonDocumentOptions);
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
    public IJson WideJson_ParlotCompiled()
    {
        return _compiled.Parse(_wideJson);
    }

    [Benchmark, BenchmarkCategory("Wide")]
    public IJson WideJson_Parlot()
    {
        return JsonParser.Parse(_wideJson);
    }

    [Benchmark, BenchmarkCategory("Wide")]
    public IJson WideJson_Pidgin()
    {
        return PidginJsonParser.Parse(_wideJson).Value;
    }

    [Benchmark, BenchmarkCategory("Wide")]
    public JToken WideJson_Newtonsoft()
    {
        return JToken.Parse(_wideJson);
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
                .ToArray()
        );

    private static IJson BuildObject(int depth, int width)
    {
        if (depth == 0)
        {
            return new JsonString(RandomString(6));
        }
        return new JsonObject(
            new Dictionary<string, IJson>(
                Enumerable.Repeat(1, width)
                .Select(_ => new KeyValuePair<string, IJson>(RandomString(5), BuildObject(depth - 1, width)))
                )
        );
    }

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        return new string(Enumerable.Repeat(chars, length)
        .Select(s => s[_random.Next(s.Length)]).ToArray());
    }
}
