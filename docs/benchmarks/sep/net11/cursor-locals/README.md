# Cache cursor state in locals during bulk advancement

Move repeated field access in Cursor.Advance(int) into local variables and publish the final state once. CR/LF, EOF, reset and overflow behavior remain covered by the shared cursor differential test.

All supported targets use this implementation. Evaluate small advances as carefully as large ones.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepCursorBenchmarks; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.


## Decision and results

Draft: retain as a workload-specific experiment. Long plain JSON improves about 16%, but plain short runtime JSON regresses 7.1% and short generated SQL 4.9%. Allocations are unchanged; this is not a blanket replacement justified by the current measurements.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `b9e2ab2ba8f77eec6d7ad1220eee2e3f5caebd11`.

## Validation

Full Release solution build across supported TFMs; 867 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
