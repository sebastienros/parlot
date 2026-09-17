# .NET 11 before/after

Time is ns/op; negative deltas are faster. Error is the 99.9% confidence interval half-width. Materialize is normalized per string; grammar rows are per document.

| Case | Baseline ns ± error | Cache ns ± error | Time delta | Allocated B (before → after) |
|---|---:|---:|---:|---:|
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=16, Escaped=False | 5.67 ± 0.23 | 5.37 ± 0.06 | -5.2% | 56 → 0 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=16, Escaped=True | 21.02 ± 3.91 | 22.18 ± 1.63 | +5.5% | 56 → 0 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=256, Escaped=False | 24.83 ± 1.84 | 29.97 ± 2.44 | +20.7% | 536 → 0 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=256, Escaped=True | 229.52 ± 9.41 | 233.86 ± 3.92 | +1.9% | 536 → 536 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=1024, Escaped=False | 80.84 ± 8.16 | 80.97 ± 2.96 | +0.2% | 2072 → 2072 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=1024, Escaped=True | 975.22 ± 32.07 | 905.23 ± 10.08 | -7.2% | 2072 → 2072 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=16, Escaped=False | 5.86 ± 0.20 | 10.96 ± 0.14 | +87.0% | 56 → 48 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=16, Escaped=True | 21.60 ± 0.83 | 27.37 ± 1.60 | +26.8% | 56 → 48 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=256, Escaped=False | 25.52 ± 0.25 | 46.84 ± 2.99 | +83.5% | 536 → 464 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=256, Escaped=True | 232.15 ± 6.02 | 233.48 ± 2.14 | +0.6% | 536 → 536 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=1024, Escaped=False | 90.31 ± 6.56 | 92.33 ± 3.88 | +2.2% | 2072 → 2072 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=1024, Escaped=True | 890.64 ± 15.95 | 933.42 ± 71.93 | +4.8% | 2072 → 2072 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7069.66 ± 152.51 | 7374.88 ± 589.72 | +4.3% | 17872 → 13264 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6562.22 ± 483.91 | 6344.72 ± 477.87 | -3.3% | 19264 → 14656 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2736.41 ± 145.81 | 2828.91 ± 144.15 | +3.4% | 5312 → 4792 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2549.36 ± 218.02 | 2684.59 ± 519.83 | +5.3% | 2624 → 2168 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8415.33 ± 790.23 | 8403.28 ± 339.90 | -0.1% | 18384 → 13264 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7195.17 ± 128.96 | 7458.44 ± 390.80 | +3.7% | 19776 → 14656 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2763.56 ± 205.38 | 2811.75 ± 176.94 | +1.7% | 5360 → 4792 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2564.74 ± 54.77 | 2590.61 ± 73.79 | +1.0% | 2672 → 2168 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14142.04 ± 565.53 | 14286.35 ± 274.72 | +1.0% | 33744 → 13264 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 12903.50 ± 107.23 | 13421.86 ± 673.30 | +4.0% | 35136 → 14656 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3443.69 ± 214.36 | 3390.85 ± 73.88 | -1.5% | 7792 → 6424 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3749.92 ± 95.82 | 3722.65 ± 104.11 | -0.7% | 5104 → 3800 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 34084.73 ± 1362.34 | 34279.16 ± 1332.97 | +0.6% | 50128 → 46800 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32571.03 ± 516.79 | 33367.07 ± 1378.71 | +2.4% | 51520 → 48192 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5177.21 ± 253.38 | 5332.13 ± 122.00 | +3.0% | 9328 → 7960 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5664.88 ± 144.26 | 5619.26 ± 141.21 | -0.8% | 6640 → 5336 |
