# Probe the first quoted-string vector before the BCL tail search

Compare the first eight UTF-16 code units against the actual quote and backslash using Vector128. Extract the first match from the mask, then delegate the remaining span to IndexOfAny. Loads are bounded and full UTF-16 comparisons preserve non-ASCII quote support.

Modern targets use hardware acceleration when available. Unicode and malformed-input tests cover lengths around the vector boundary. Longer tokens may already favor the BCL implementation.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepQuotedStringBenchmarks; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.

Results and validation are recorded below after execution.
