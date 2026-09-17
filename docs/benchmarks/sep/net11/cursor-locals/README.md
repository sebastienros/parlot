# Cache cursor state in locals during bulk advancement

Move repeated field access in Cursor.Advance(int) into local variables and publish the final state once. CR/LF, EOF, reset and overflow behavior remain covered by the shared cursor differential test.

All supported targets use this implementation. Evaluate small advances as carefully as large ones.

Independent candidate based on the shared benchmark infrastructure PR #345, which depends on the separate decimal test fix #344. No other optimization candidate is included.

## Measurement

Latest installed TFM: net11.0, .NET 11 RC1, ARM64. BenchmarkDotNet runs serially with one launch, three warmups, five measured iterations and 100 ms iteration time. These are screening measurements, not cross-machine guarantees.

Workloads: `SepCursorBenchmarks; SepGrammarBenchmarks`. SQL/JSON compare runtime and generated grammars at token lengths 8/256, with and without escapes. Allocation modes use the same sample grammar and include per-document state creation.

Results and validation are recorded below after execution.
