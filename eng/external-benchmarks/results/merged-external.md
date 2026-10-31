# External benchmarks of the merged branch

`merged` is `ccr-2d630000-0z83na` at 9aacf94 (all six merges), `merged-ncalc` the same with NCalc's operator levels ported to
`Operators()` (`patches/ncalc-pratt.patch`). Same settings as the other runs: 2 rounds in alternating order, in-process.

| Benchmark | main median | main alloc | merged time | merged alloc | merged-ncalc time | merged-ncalc alloc |
|---|---:|---:|---:|---:|---:|---:|
| FluidParse.BlogPost_1_6KB | 12.33 us | 13.1 KB | 0.85 (0.85-0.85) | 10.9 KB (0.83) | 0.83 (0.83-0.84) | 10.9 KB (0.83) |
| FluidParse.Large_32KB | 240.40 us | 249.1 KB | 0.86 (0.86-0.86) | 211.0 KB (0.85) | 0.84 (0.83-0.85) | 211.0 KB (0.85) |
| FluidParse.Product_216B | 2.76 us | 3,304 B | 0.89 (0.88-0.89) | 2,664 B (0.81) | 0.88 (0.87-0.88) | 2,664 B (0.81) |
| FluidParse.Snippets_x10 | 1.95 us | 1,928 B | 0.87 (0.86-0.88) | 1,497 B (0.78) | 0.88 (0.87-0.88) | 1,497 B (0.78) |
| NCalcParse.Fluent(Expression=Advanced) | 7.86 us | 2,840 B | 0.94 (0.94-0.94) | 2,048 B (0.72) | 0.65 (0.65-0.65) | 1,752 B (0.62) |
| NCalcParse.Fluent(Expression=Arithmetic) | 10.17 us | 3,232 B | 0.95 (0.94-0.96) | 2,560 B (0.79) | 0.68 (0.68-0.68) | 2,320 B (0.72) |
| NCalcParse.Fluent(Expression=Long) | 79.73 us | 16.4 KB | 0.98 (0.97-0.98) | 16.4 KB (1.00) | 0.79 (0.78-0.79) | 16.4 KB (1.00) |
| NCalcParse.Fluent(Expression=Simple) | 2.76 us | 1,016 B | 0.97 (0.96-0.98) | 712 B (0.70) | 0.66 (0.66-0.67) | 592 B (0.58) |
| NCalcParse.Generated(Expression=Advanced) | 5.87 us | 2,865 B | 0.85 (0.85-0.85) | 2,016 B (0.70) | 0.61 (0.61-0.61) | 1,720 B (0.60) |
| NCalcParse.Generated(Expression=Arithmetic) | 7.68 us | 3,256 B | 0.92 (0.91-0.93) | 2,528 B (0.78) | 0.71 (0.70-0.72) | 2,288 B (0.70) |
| NCalcParse.Generated(Expression=Long) | 62.54 us | 16.4 KB | 1.07 (1.06-1.08) | 16.4 KB (1.00) | 0.98 (0.97-0.98) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 2.18 us | 1,040 B | 0.86 (0.86-0.87) | 744 B (0.72) | 0.63 (0.63-0.64) | 560 B (0.54) |

Time: mean of the per-round ratios of the medians to the baseline's (lower is faster), with their range.
Alloc: bytes allocated per operation, with the ratio to the baseline.
