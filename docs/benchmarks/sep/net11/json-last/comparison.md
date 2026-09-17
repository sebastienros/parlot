# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| JsonLastStringParserBenchmarks.Parse Distinct=1, Objects=False, Escaped=False, Optimize=True | 10171.76 ± 407.22 | 9141.65 ± 246.85 | -10.1% | 0.899 | 23072 → 10832 |
| JsonLastStringParserBenchmarks.Parse Distinct=1, Objects=False, Escaped=True, Optimize=True | 13757.94 ± 596.65 | 14142.72 ± 925.60 | +2.8% | 1.028 | 23072 → 23072 |
| JsonLastStringParserBenchmarks.Parse Distinct=1, Objects=True, Escaped=False, Optimize=True | 37139.65 ± 2266.98 | 32557.12 ± 560.20 | -12.3% | 0.877 | 139808 → 139808 |
| JsonLastStringParserBenchmarks.Parse Distinct=1, Objects=True, Escaped=True, Optimize=True | 36482.29 ± 520.89 | 36773.63 ± 1194.92 | +0.8% | 1.008 | 139808 → 139808 |
| JsonLastStringParserBenchmarks.Parse Distinct=4, Objects=False, Escaped=False, Optimize=True | 10259.77 ± 913.15 | 10926.26 ± 826.86 | +6.5% | 1.065 | 23072 → 23072 |
| JsonLastStringParserBenchmarks.Parse Distinct=4, Objects=False, Escaped=True, Optimize=True | 13805.95 ± 937.06 | 14341.93 ± 114.46 | +3.9% | 1.039 | 23072 → 23072 |
| JsonLastStringParserBenchmarks.Parse Distinct=4, Objects=True, Escaped=False, Optimize=True | 33125.48 ± 613.62 | 37968.74 ± 2973.03 | +14.6% | 1.146 | 139808 → 139808 |
| JsonLastStringParserBenchmarks.Parse Distinct=4, Objects=True, Escaped=True, Optimize=True | 37868.35 ± 4625.40 | 38880.77 ± 976.99 | +2.7% | 1.027 | 139808 → 139808 |
| JsonLastStringParserBenchmarks.Parse Distinct=256, Objects=False, Escaped=False, Optimize=True | 10780.20 ± 637.12 | 10970.33 ± 1022.31 | +1.8% | 1.018 | 23072 → 23072 |
| JsonLastStringParserBenchmarks.Parse Distinct=256, Objects=False, Escaped=True, Optimize=True | 14457.40 ± 893.35 | 14250.97 ± 427.20 | -1.4% | 0.986 | 24320 → 24320 |
| JsonLastStringParserBenchmarks.Parse Distinct=256, Objects=True, Escaped=False, Optimize=True | 36314.95 ± 190.65 | 34832.20 ± 5515.43 | -4.1% | 0.959 | 139808 → 139808 |
| JsonLastStringParserBenchmarks.Parse Distinct=256, Objects=True, Escaped=True, Optimize=True | 37068.66 ± 1691.21 | 38838.48 ± 5045.68 | +4.8% | 1.048 | 141056 → 141056 |
