# Use SearchValues after a short scalar whitespace prefix

After a prefix of eight whitespace characters, find the first non-whitespace code unit with SearchValues. Preserve Parlot's two exact character sets and existing cursor line tracking.

Modern targets only. Tests compare every UTF-16 code unit and prefix boundaries with existing predicates and scalar cursor positions.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SkipWhiteSpaceBenchmarks default methods; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.


## Decision and results

Draft / do not keep as the default: a 256-space prefix improves 91.0% (38.9% for the newline-aware path), but common one/two-space prefixes regress 12.8–16.5% in the ordinary whitespace method and ten-space prefixes regress 19.0–31.2%. SQL/JSON gains are inconclusive, with several slower means. Allocations remain unchanged. This is useful evidence for a dedicated long-whitespace path, not a general replacement.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `5a808dc3118236a8472b3ca32151d04d7ed379c1`.

## Validation

Full Release solution build across supported TFMs; 869 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
