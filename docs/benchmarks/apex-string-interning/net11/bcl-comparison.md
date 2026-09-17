# .NET 11 before/after

Time is ns/op; negative deltas are faster. Error is the 99.9% confidence interval half-width. Materialize is normalized per string; grammar rows are per document.

| Case | Baseline ns ± error | Cache ns ± error | Time delta | Allocated B (before → after) |
|---|---:|---:|---:|---:|
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=16, Escaped=False | 5.67 ± 0.23 | 7.72 ± 0.19 | +36.3% | 56 → 0 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=16, Escaped=True | 21.02 ± 3.91 | 22.06 ± 0.24 | +4.9% | 56 → 0 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=256, Escaped=False | 24.83 ± 1.84 | 165.43 ± 6.40 | +566.3% | 536 → 0 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=256, Escaped=True | 229.52 ± 9.41 | 257.96 ± 11.37 | +12.4% | 536 → 536 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=1024, Escaped=False | 80.84 ± 8.16 | 87.67 ± 6.14 | +8.4% | 2072 → 2072 |
| ApexStringCacheBenchmarks.Materialize Distinct=1, Length=1024, Escaped=True | 975.22 ± 32.07 | 979.71 ± 114.00 | +0.5% | 2072 → 2072 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=16, Escaped=False | 5.86 ± 0.20 | 17.36 ± 5.74 | +196.2% | 56 → 48 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=16, Escaped=True | 21.60 ± 0.83 | 31.38 ± 2.41 | +45.3% | 56 → 48 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=256, Escaped=False | 25.52 ± 0.25 | 190.34 ± 19.23 | +645.8% | 536 → 466 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=256, Escaped=True | 232.15 ± 6.02 | 238.87 ± 6.34 | +2.9% | 536 → 536 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=1024, Escaped=False | 90.31 ± 6.56 | 96.25 ± 23.89 | +6.6% | 2072 → 2072 |
| ApexStringCacheBenchmarks.Materialize Distinct=8192, Length=1024, Escaped=True | 890.64 ± 15.95 | 974.10 ± 37.71 | +9.4% | 2072 → 2072 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7069.66 ± 152.51 | 7344.05 ± 770.82 | +3.9% | 17872 → 13264 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6562.22 ± 483.91 | 6611.90 ± 430.79 | +0.8% | 19264 → 14656 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2736.41 ± 145.81 | 2783.45 ± 166.96 | +1.7% | 5312 → 4792 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2549.36 ± 218.02 | 2553.68 ± 109.19 | +0.2% | 2624 → 2168 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8415.33 ± 790.23 | 8180.45 ± 22.05 | -2.8% | 18384 → 13264 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7195.17 ± 128.96 | 7327.53 ± 173.40 | +1.8% | 19776 → 14656 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2763.56 ± 205.38 | 2811.59 ± 95.12 | +1.7% | 5360 → 4792 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2564.74 ± 54.77 | 2633.10 ± 97.72 | +2.7% | 2672 → 2168 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14142.04 ± 565.53 | 18575.96 ± 187.89 | +31.4% | 33744 → 13264 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 12903.50 ± 107.23 | 18036.84 ± 1482.97 | +39.8% | 35136 → 14656 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3443.69 ± 214.36 | 3731.26 ± 254.30 | +8.4% | 7792 → 6424 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3749.92 ± 95.82 | 4025.57 ± 75.59 | +7.4% | 5104 → 3800 |
| SepGrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 34084.73 ± 1362.34 | 34174.87 ± 221.59 | +0.3% | 50128 → 46800 |
| SepGrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32571.03 ± 516.79 | 33102.19 ± 615.82 | +1.6% | 51520 → 48192 |
| SepGrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5177.21 ± 253.38 | 5576.56 ± 215.07 | +7.7% | 9328 → 7960 |
| SepGrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5664.88 ± 144.26 | 5880.99 ± 300.30 | +3.8% | 6640 → 5336 |
