# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7122.27 ± 105.03 | 7304.59 ± 104.50 | +2.6% | 1.026 | 17872 → 17872 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6271.28 ± 101.28 | 6150.47 ± 83.83 | -1.9% | 0.981 | 19264 → 19264 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2600.20 ± 46.06 | 2721.69 ± 123.78 | +4.7% | 1.047 | 5312 → 5312 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2459.86 ± 19.06 | 2495.50 ± 169.97 | +1.4% | 1.014 | 2624 → 2624 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8206.80 ± 703.57 | 8139.16 ± 137.10 | -0.8% | 0.992 | 18384 → 18384 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7125.85 ± 448.06 | 7306.76 ± 504.19 | +2.5% | 1.025 | 19776 → 19776 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2678.28 ± 49.36 | 2894.97 ± 84.33 | +8.1% | 1.081 | 5360 → 5360 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2541.34 ± 84.48 | 2644.51 ± 321.29 | +4.1% | 1.041 | 2672 → 2672 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14112.01 ± 217.36 | 14067.33 ± 223.97 | -0.3% | 0.997 | 33744 → 33744 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 13225.36 ± 1553.50 | 13089.21 ± 73.56 | -1.0% | 0.990 | 35136 → 35136 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3303.40 ± 39.70 | 3335.12 ± 114.75 | +1.0% | 1.010 | 7792 → 7792 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3640.27 ± 75.65 | 3639.37 ± 52.70 | -0.0% | 1.000 | 5104 → 5104 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 33757.46 ± 931.28 | 33631.73 ± 307.05 | -0.4% | 0.996 | 50128 → 50128 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32800.88 ± 546.79 | 33300.97 ± 1974.86 | +1.5% | 1.015 | 51520 → 51520 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5139.47 ± 49.29 | 5147.97 ± 55.38 | +0.2% | 1.002 | 9328 → 9328 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5537.02 ± 222.92 | 5401.62 ± 90.93 | -2.4% | 0.976 | 6640 → 6640 |
| DecodeBenchmarks.Current Length=32, EscapeInterval=4 | 31.32 ± 1.88 | 31.06 ± 1.81 | -0.8% | 0.992 | 88 → 88 |
| DecodeBenchmarks.Current Length=32, EscapeInterval=128 | 27.65 ± 1.90 | 28.65 ± 1.29 | +3.6% | 1.036 | 88 → 88 |
| DecodeBenchmarks.Current Length=1024, EscapeInterval=4 | 845.63 ± 20.68 | 884.36 ± 74.97 | +4.6% | 1.046 | 2072 → 2072 |
| DecodeBenchmarks.Current Length=1024, EscapeInterval=128 | 834.79 ± 11.48 | 854.75 ± 15.02 | +2.4% | 1.024 | 2072 → 2072 |
