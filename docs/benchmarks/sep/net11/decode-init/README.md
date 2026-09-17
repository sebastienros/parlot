# Avoid clearing the runtime decoder scratch buffer

Apply SkipLocalsInit to the runtime decoder, which writes every character before exposing the written slice. Keep the annotation out of embedded generated support so generated consumers do not require unsafe compilation.

Generated decoders intentionally retain initialization. No uninitialized character may enter the returned string; mixed escape and boundary tests exercise the written length.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepDecodeBenchmarks.Current; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.

Results and validation are recorded below after execution.
