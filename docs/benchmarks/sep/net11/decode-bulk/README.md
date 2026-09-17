# Copy long ordinary runs while decoding escaped strings

After 16 consecutive ordinary characters, locate the next backslash with IndexOf and copy the intervening span in bulk. Keep the existing escape switch, stack/pool buffer ownership, and returned-string allocation.

Modern targets only. Dense escapes still incur the run counter, and short runs can lose to the original scalar loop.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepDecodeBenchmarks.Current; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.

Results and validation are recorded below after execution.
