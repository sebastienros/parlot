# Skip per-character cursor updates for long newline-free spans

For advances of at least 64 characters, use the BCL vectorized CR/LF search to detect a span that can update offset and column in bulk. Both endpoints are included because existing CR and LF tracking have different boundary effects.

Modern targets only; spans containing CR/LF fall back to the existing scalar loop. The added branch may affect short tokens.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepCursorBenchmarks; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.


## Decision and results

Draft: strong long-token benefit with a small-token cost. Long JSON improves 31–40% and long SQL 14–19%, with unchanged allocations. Short runtime SQL regresses about 2.9%; isolated one-character Advance(count) is about 24% slower. Keep this candidate for review, but do not treat it as an unconditional improvement.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `f4af0bad360428fb61ebd3b050b6ae4218de0b1a`.

## Validation

Full Release solution build across supported TFMs; 867 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
