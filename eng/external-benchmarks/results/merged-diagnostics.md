# NCalc's generated parser: locating the slowdown of the long expression

## Along the merges (2 rounds each)

`after-loop` 33da171, `after-lazy` fbc0528, `after-alloc` 3dc7f2f, `after-pratt` 4b5c6ce, `before-genpos` 8dc0037 (after the merge of
perf/pooled-lists).

| Benchmark | main median | main alloc | after-loop time | after-loop alloc | after-lazy time | after-lazy alloc | after-alloc time | after-alloc alloc | after-pratt time | after-pratt alloc | before-genpos time | before-genpos alloc |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 5.85 us | 2,864 B | 1.00 (1.00-1.00) | 2,864 B (1.00) | 0.93 (0.93-0.93) | 2,872 B (1.00) | 0.92 (0.92-0.92) | 2,816 B (0.98) | 0.91 (0.91-0.91) | 2,816 B (0.98) | 0.88 (0.87-0.88) | 2,016 B (0.70) |
| NCalcParse.Generated(Expression=Arithmetic) | 7.53 us | 3,256 B | 1.00 (0.99-1.01) | 3,256 B (1.00) | 0.93 (0.92-0.93) | 3,264 B (1.00) | 0.92 (0.91-0.92) | 3,208 B (0.99) | 0.91 (0.91-0.91) | 3,208 B (0.99) | 0.97 (0.97-0.97) | 2,528 B (0.78) |
| NCalcParse.Generated(Expression=Long) | 62.00 us | 16.4 KB | 1.00 (1.00-1.00) | 16.4 KB (1.00) | 0.93 (0.93-0.93) | 16.4 KB (1.00) | 0.93 (0.92-0.93) | 16.4 KB (1.00) | 0.93 (0.93-0.94) | 16.4 KB (1.00) | 1.11 (1.10-1.11) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 2.17 us | 1,040 B | 1.00 (1.00-1.01) | 1,040 B (1.00) | 0.94 (0.94-0.94) | 1,048 B (1.01) | 0.92 (0.91-0.94) | 992 B (0.95) | 0.90 (0.90-0.91) | 992 B (0.95) | 0.89 (0.89-0.89) | 744 B (0.72) |

## perf/pooled-lists alone, before (lists-1, 3f8aaf6) and after (lists-4, a014020) keeping four items inline

| Benchmark | main median | main alloc | lists-1 time | lists-1 alloc | lists-4 time | lists-4 alloc |
|---|---:|---:|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 5.89 us | 2,864 B | 0.99 (0.99-0.99) | 2,064 B (0.72) | 0.96 (0.95-0.96) | 2,064 B (0.72) |
| NCalcParse.Generated(Expression=Arithmetic) | 7.57 us | 3,256 B | 0.97 (0.97-0.97) | 2,576 B (0.79) | 0.98 (0.97-0.98) | 2,576 B (0.79) |
| NCalcParse.Generated(Expression=Long) | 61.91 us | 16.4 KB | 1.00 (0.99-1.00) | 16.4 KB (1.00) | 1.00 (1.00-1.00) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 2.17 us | 1,040 B | 0.96 (0.95-0.97) | 760 B (0.73) | 0.97 (0.96-0.97) | 760 B (0.73) |

## Without the try/finally blocks of the list loops (noeh, an experiment)

| Benchmark | main median | main alloc | after-pratt time | after-pratt alloc | merged time | merged alloc | noeh time | noeh alloc |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 5.81 us | 2,864 B | 0.94 (0.93-0.95) | 2,816 B (0.98) | 0.86 (0.86-0.86) | 2,016 B (0.70) | 0.85 (0.85-0.85) | 2,016 B (0.70) |
| NCalcParse.Generated(Expression=Arithmetic) | 7.54 us | 3,256 B | 0.92 (0.91-0.93) | 3,208 B (0.99) | 0.94 (0.94-0.94) | 2,528 B (0.78) | 0.93 (0.92-0.93) | 2,528 B (0.78) |
| NCalcParse.Generated(Expression=Long) | 61.91 us | 16.4 KB | 0.93 (0.93-0.93) | 16.4 KB (1.00) | 1.12 (1.11-1.13) | 16.4 KB (1.00) | 1.08 (1.06-1.11) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 2.15 us | 1,040 B | 0.93 (0.92-0.93) | 992 B (0.95) | 0.87 (0.87-0.88) | 744 B (0.72) | 0.88 (0.87-0.88) | 744 B (0.72) |

## With dynamic PGO disabled (DOTNET_TieredPGO=0) and with tiered compilation disabled (DOTNET_TieredCompilation=0)

### DOTNET_TieredPGO=0

| Benchmark | main median | main alloc | merged time | merged alloc |
|---|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 9.41 us | 2,864 B | 0.89 (0.89-0.90) | 2,240 B (0.78) |
| NCalcParse.Generated(Expression=Arithmetic) | 12.48 us | 3,256 B | 0.87 (0.86-0.88) | 2,720 B (0.84) |
| NCalcParse.Generated(Expression=Long) | 91.74 us | 16.4 KB | 0.89 (0.89-0.90) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 3.31 us | 1,040 B | 0.88 (0.87-0.89) | 776 B (0.75) |

### DOTNET_TieredCompilation=0

| Benchmark | main median | main alloc | merged time | merged alloc |
|---|---:|---:|---:|---:|
| NCalcParse.Generated(Expression=Advanced) | 9.52 us | 2,864 B | 0.96 (0.95-0.96) | 2,240 B (0.78) |
| NCalcParse.Generated(Expression=Arithmetic) | 13.29 us | 3,256 B | 0.91 (0.90-0.91) | 2,720 B (0.84) |
| NCalcParse.Generated(Expression=Long) | 99.46 us | 16.4 KB | 0.97 (0.97-0.97) | 16.4 KB (1.00) |
| NCalcParse.Generated(Expression=Simple) | 3.35 us | 1,040 B | 0.89 (0.88-0.89) | 776 B (0.75) |

[exited with code 0]
