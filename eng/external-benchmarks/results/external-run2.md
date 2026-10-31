# External benchmarks, run 2: NCalc generated parser

Every variant built from scratch (12 distinct `NCalc.Parser.dll`), same settings as run 1. Variants that run identical
NCalc code (`main`/`main-offset`, `lazy`/`lazy-offset`, `memo`/`memo-fluid`) agree within 1%.

| Benchmark | main median | main alloc | main-offset time | main-offset alloc | lazy time | lazy alloc | lazy-offset time | lazy-offset alloc | genpos time | genpos alloc | loop time | loop alloc | alloc time | alloc alloc | pratt time | pratt alloc | pratt-ncalc time | pratt-ncalc alloc | lists time | lists alloc | memo time | memo alloc | memo-fluid time | memo-fluid alloc |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 5.82 us | 2,864 B | 1.00 (1.00-1.00) | 2,864 B (1.00) | 0.93 (0.93-0.94) | 2,872 B (1.00) | 0.93 (0.93-0.93) | 2,872 B (1.00) | 0.91 (0.91-0.91) | 2,872 B (1.00) | 1.01 (1.00-1.01) | 2,864 B (1.00) | 0.98 (0.98-0.98) | 2,808 B (0.98) | 0.91 (0.91-0.91) | 2,856 B (1.00) | 0.68 (0.68-0.68) | 1,848 B (0.65) | 1.00 (0.99-1.00) | 2,064 B (0.72) | 1.00 (1.00-1.01) | 2,864 B (1.00) | 1.00 (0.99-1.00) | 2,864 B (1.00) |
| NCalcParse.Generated(Expression=Arithmetic) | 7.56 us | 3,256 B | 0.99 (0.99-1.00) | 3,256 B (1.00) | 0.91 (0.91-0.91) | 3,264 B (1.00) | 0.92 (0.91-0.92) | 3,264 B (1.00) | 0.89 (0.88-0.89) | 3,264 B (1.00) | 1.00 (0.99-1.00) | 3,256 B (1.00) | 0.97 (0.97-0.98) | 3,200 B (0.98) | 0.91 (0.91-0.91) | 3,248 B (1.00) | 0.71 (0.71-0.71) | 2,384 B (0.73) | 0.97 (0.97-0.98) | 2,576 B (0.79) | 1.00 (1.00-1.00) | 3,256 B (1.00) | 0.99 (0.99-0.99) | 3,256 B (1.00) |
| NCalcParse.Generated(Expression=Long) | 62.19 us | 16.4 KB | 0.99 (0.98-0.99) | 16.4 KB (1.00) | 0.93 (0.92-0.94) | 16.4 KB (1.00) | 0.93 (0.93-0.93) | 16.4 KB (1.00) | 0.90 (0.90-0.90) | 16.4 KB (1.00) | 1.00 (0.99-1.01) | 16.4 KB (1.00) | 0.98 (0.97-0.99) | 16.4 KB (1.00) | 0.93 (0.93-0.94) | 16.4 KB (1.00) | 0.87 (0.86-0.87) | 16.4 KB (1.00) | 0.99 (0.99-0.99) | 16.4 KB (1.00) | 1.00 (0.99-1.01) | 16.4 KB (1.00) | 0.99 (0.99-1.00) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 2.16 us | 1,040 B | 0.99 (0.99-0.99) | 1,040 B (1.00) | 0.93 (0.92-0.94) | 1,048 B (1.01) | 0.93 (0.93-0.93) | 1,048 B (1.01) | 0.91 (0.91-0.91) | 1,048 B (1.01) | 1.00 (0.99-1.01) | 1,040 B (1.00) | 0.97 (0.97-0.98) | 984 B (0.95) | 0.93 (0.93-0.93) | 1,032 B (0.99) | 0.70 (0.70-0.70) | 600 B (0.58) | 0.96 (0.95-0.97) | 760 B (0.73) | 1.00 (1.00-1.01) | 1,040 B (1.00) | 0.99 (0.98-1.00) | 1,040 B (1.00) |

Time: mean of the per-round ratios of the medians to the baseline's (lower is faster), with their range.
Alloc: bytes allocated per operation, with the ratio to the baseline.

