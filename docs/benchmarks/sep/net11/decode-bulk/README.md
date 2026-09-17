# Copy long ordinary runs while decoding escaped strings

After 16 consecutive ordinary characters, locate the next backslash with IndexOf and copy the intervening span in bulk. Keep the existing escape switch, stack/pool buffer ownership, and returned-string allocation.

Modern targets only. Dense escapes still incur the run counter, and short runs can lose to the original scalar loop.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepDecodeBenchmarks.Current; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.


## Decision and results

Draft: retain as a workload-specific candidate. Long escaped JSON improves 29.6–32.0% and long escaped SQL 16.7–17.4%, with unchanged allocations. The 1024-character sparse decoder improves 71.7%, but dense decoding regresses 8.9–15.1%. Do not merge as a general improvement until the dense-input tradeoff is accepted or removed.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `cec9493cb930cb71526398087c40e6a189a5dbc6`.

## Validation

Full Release solution build across supported TFMs; 867 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
