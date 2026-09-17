# Avoid clearing the runtime decoder scratch buffer

Apply SkipLocalsInit to the runtime decoder, which writes every character before exposing the written slice. Keep the annotation out of embedded generated support so generated consumers do not require unsafe compilation.

Generated decoders intentionally retain initialization. No uninitialized character may enter the returned string; mixed escape and boundary tests exercise the written length.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepDecodeBenchmarks.Current; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.


## Decision and results

Draft / do not keep: the four direct decoder cases show no convincing benefit (means range from 0.8% faster to 4.6% slower), with unchanged allocations. The extra source-generation handling is not justified by these measurements. Generated grammar rows are controls: generated decoders intentionally retain initialization, so their small timing differences are not evidence of this optimization.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `d7bb4a5652fde0c26d87f7cabeed751a10ccaa5b`.

## Validation

Full Release solution build across supported TFMs; 867 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
