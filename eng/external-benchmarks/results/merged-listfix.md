# Generated lists read their first item without a builder (c6c6473)

`merged-old` is the merged branch before the change (a780f3f), `merged-new` after it (c6c6473), `merged-new-ncalc` the latter
with NCalc's operator levels ported to `Operators()`. 2 rounds in alternating order.

## NCalc's generated parser

| Benchmark | main median | main alloc | merged-old time | merged-old alloc | merged-new time | merged-new alloc | merged-new-ncalc time | merged-new-ncalc alloc |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 5.83 us | 2,864 B | 0.85 (0.84-0.85) | 2,016 B (0.70) | 0.83 (0.82-0.83) | 2,016 B (0.70) | 0.60 (0.60-0.61) | 1,720 B (0.60) |
| NCalcParse.Generated(Expression=Arithmetic) | 7.56 us | 3,256 B | 0.94 (0.93-0.94) | 2,528 B (0.78) | 0.84 (0.83-0.84) | 2,528 B (0.78) | 0.66 (0.66-0.66) | 2,288 B (0.70) |
| NCalcParse.Generated(Expression=Long) | 62.09 us | 16.4 KB | 1.10 (1.08-1.12) | 16.4 KB (1.00) | 0.87 (0.85-0.89) | 16.4 KB (1.00) | 0.78 (0.77-0.80) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 2.17 us | 1,040 B | 0.86 (0.84-0.89) | 744 B (0.72) | 0.83 (0.81-0.86) | 744 B (0.72) | 0.63 (0.62-0.64) | 560 B (0.54) |

Time: mean of the per-round ratios of the medians to the baseline's (lower is faster), with their range.
Alloc: bytes allocated per operation, with the ratio to the baseline.

## Parlot's generated benchmarks

| Benchmark | main median | main alloc | merged-new time | merged-new alloc | merged-old time | merged-old alloc |
|---|---:|---:|---:|---:|---:|---:|
| ExprBench.ParlotGeneratedBig | 1.32 us | 1,304 B | 0.82 (0.82-0.82) | 1,256 B (0.96) | 0.82 (0.82-0.82) | 1,256 B (0.96) |
| ExprBench.ParlotGeneratedSmall | 256.2 ns | 408 B | 0.82 (0.80-0.83) | 360 B (0.88) | 0.82 (0.81-0.83) | 360 B (0.88) |
| ExprBench.ParlotGeneratedUnary | 337.9 ns | 504 B | 0.87 (0.85-0.88) | 456 B (0.90) | 0.87 (0.85-0.89) | 456 B (0.90) |
| JsonBench.BigJson_ParlotGenerated | 51.32 us | 95.5 KB | 0.82 (0.81-0.83) | 84.2 KB (0.88) | 0.82 (0.82-0.83) | 84.2 KB (0.88) |
| JsonBench.DeepJson_ParlotGenerated | 35.17 us | 104.3 KB | 0.80 (0.79-0.81) | 78.2 KB (0.75) | 0.79 (0.78-0.80) | 78.2 KB (0.75) |
| JsonBench.LongJson_ParlotGenerated | 45.16 us | 124.4 KB | 0.79 (0.79-0.79) | 96.2 KB (0.77) | 0.80 (0.80-0.80) | 96.2 KB (0.77) |
| JsonBench.WideJson_ParlotGenerated | 21.03 us | 40.6 KB | 0.88 (0.83-0.92) | 36.4 KB (0.90) | 0.87 (0.83-0.90) | 36.4 KB (0.90) |
| RegexBenchmarks.ParlotEmailGenerated | 108.7 ns | 176 B | 0.47 (0.45-0.49) | 128 B (0.73) | 0.47 (0.46-0.49) | 128 B (0.73) |
| SqlParserBenchmarks.Generated(Sql=selec(...)foo%' [33]) | 2.80 us | 1,984 B | 0.94 (0.93-0.95) | 1,592 B (0.80) | 0.92 (0.92-0.92) | 1,592 B (0.80) |
Time: mean of the per-round ratios of the medians to the baseline's (lower is faster), with their range.
Alloc: bytes allocated per operation, with the ratio to the baseline.
