# Use SearchValues after a short scalar whitespace prefix

After a prefix of eight whitespace characters, find the first non-whitespace code unit with SearchValues. Preserve Parlot's two exact character sets and existing cursor line tracking.

Modern targets only. Tests compare every UTF-16 code unit and prefix boundaries with existing predicates and scalar cursor positions.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SkipWhiteSpaceBenchmarks default methods; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.

Results and validation are recorded below after execution.
