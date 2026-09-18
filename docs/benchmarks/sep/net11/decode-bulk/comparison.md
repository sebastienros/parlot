# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7122.27 ± 105.03 | 7105.53 ± 78.02 | -0.2% | 0.998 | 17872 → 17872 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6271.28 ± 101.28 | 6219.07 ± 278.12 | -0.8% | 0.992 | 19264 → 19264 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2600.20 ± 46.06 | 2653.38 ± 99.77 | +2.0% | 1.020 | 5312 → 5312 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2459.86 ± 19.06 | 2600.27 ± 401.06 | +5.7% | 1.057 | 2624 → 2624 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8206.80 ± 703.57 | 8089.04 ± 167.85 | -1.4% | 0.986 | 18384 → 18384 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7125.85 ± 448.06 | 7139.68 ± 168.09 | +0.2% | 1.002 | 19776 → 19776 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2678.28 ± 49.36 | 2730.24 ± 69.93 | +1.9% | 1.019 | 5360 → 5360 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2541.34 ± 84.48 | 2593.49 ± 110.81 | +2.1% | 1.021 | 2672 → 2672 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14112.01 ± 217.36 | 14110.46 ± 712.60 | -0.0% | 1.000 | 33744 → 33744 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 13225.36 ± 1553.50 | 12921.37 ± 146.78 | -2.3% | 0.977 | 35136 → 35136 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3303.40 ± 39.70 | 3365.53 ± 91.29 | +1.9% | 1.019 | 7792 → 7792 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3640.27 ± 75.65 | 3633.82 ± 66.37 | -0.2% | 0.998 | 5104 → 5104 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 33757.46 ± 931.28 | 23769.39 ± 456.53 | -29.6% | 0.704 | 50128 → 50128 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32800.88 ± 546.79 | 22309.66 ± 1523.86 | -32.0% | 0.680 | 51520 → 51520 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5139.47 ± 49.29 | 4245.52 ± 355.61 | -17.4% | 0.826 | 9328 → 9328 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5537.02 ± 222.92 | 4609.74 ± 329.85 | -16.7% | 0.833 | 6640 → 6640 |
| DecodeBenchmarks.Current Length=32, EscapeInterval=4 | 31.32 ± 1.88 | 36.04 ± 0.09 | +15.1% | 1.151 | 88 → 88 |
| DecodeBenchmarks.Current Length=32, EscapeInterval=128 | 27.65 ± 1.90 | 22.73 ± 0.73 | -17.8% | 0.822 | 88 → 88 |
| DecodeBenchmarks.Current Length=1024, EscapeInterval=4 | 845.63 ± 20.68 | 921.07 ± 9.08 | +8.9% | 1.089 | 2072 → 2072 |
| DecodeBenchmarks.Current Length=1024, EscapeInterval=128 | 834.79 ± 11.48 | 236.13 ± 3.38 | -71.7% | 0.283 | 2072 → 2072 |
