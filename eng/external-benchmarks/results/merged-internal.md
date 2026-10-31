# Parlot benchmarks of the merged branch

`merged` is `ccr-2d630000-0z83na` at 9aacf94 (all six merges) against `main` (8a5f6b1). 2 rounds in alternating order, out of
process, 6 warmup and 15 measured iterations. x64 4-core VM (Xeon, AVX-512), .NET 10.0.14, BenchmarkDotNet 0.15.8.

| Benchmark | main median | main alloc | merged time | merged alloc |
|---|---:|---:|---:|---:|
| ExprBench.ParlotFluentBig | 1.55 us | 1,440 B | 0.84 (0.83-0.85) | 1,304 B (0.91) |
| ExprBench.ParlotFluentSmall | 313.3 ns | 544 B | 0.81 (0.80-0.82) | 408 B (0.75) |
| ExprBench.ParlotFluentUnary | 406.0 ns | 640 B | 0.85 (0.84-0.85) | 504 B (0.79) |
| ExprBench.ParlotGeneratedBig | 1.31 us | 1,304 B | 0.83 (0.81-0.84) | 1,256 B (0.96) |
| ExprBench.ParlotGeneratedSmall | 247.3 ns | 408 B | 0.86 (0.86-0.87) | 360 B (0.88) |
| ExprBench.ParlotGeneratedUnary | 345.9 ns | 504 B | 0.85 (0.85-0.85) | 456 B (0.90) |
| JsonBench.BigJson_Parlot | 53.41 us | 88.1 KB | 0.86 (0.84-0.87) | 84.3 KB (0.96) |
| JsonBench.BigJson_ParlotGenerated | 51.67 us | 95.5 KB | 0.82 (0.80-0.83) | 84.2 KB (0.88) |
| JsonBench.DeepJson_Parlot | 36.99 us | 104.6 KB | 0.77 (0.76-0.77) | 78.3 KB (0.75) |
| JsonBench.DeepJson_ParlotGenerated | 34.65 us | 104.3 KB | 0.77 (0.76-0.79) | 78.2 KB (0.75) |
| JsonBench.LongJson_Parlot | 48.60 us | 112.5 KB | 0.81 (0.80-0.81) | 96.2 KB (0.86) |
| JsonBench.LongJson_ParlotGenerated | 45.04 us | 124.4 KB | 0.80 (0.80-0.80) | 96.2 KB (0.77) |
| JsonBench.WideJson_Parlot | 25.50 us | 40.7 KB | 0.82 (0.81-0.83) | 36.4 KB (0.89) |
| JsonBench.WideJson_ParlotGenerated | 20.80 us | 40.6 KB | 0.86 (0.85-0.87) | 36.4 KB (0.90) |
| RegexBenchmarks.ParlotEmail | 220.4 ns | 296 B | 0.91 (0.90-0.92) | 280 B (0.95) |
| RegexBenchmarks.ParlotEmailGenerated | 105.3 ns | 176 B | 0.48 (0.48-0.49) | 128 B (0.73) |
| SqlParserBenchmarks.Generated(Sql=selec(...)foo%' [33]) | 2.75 us | 1,984 B | 0.94 (0.93-0.96) | 1,592 B (0.80) |
| SqlParserBenchmarks.Runtime(Sql=selec(...)foo%' [33]) | 2.97 us | 4,080 B | 0.89 (0.88-0.90) | 2,528 B (0.62) |
Time: mean of the per-round ratios of the medians to the baseline's (lower is faster), with their range.
Alloc: bytes allocated per operation, with the ratio to the baseline.
