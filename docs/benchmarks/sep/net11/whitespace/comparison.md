# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default Length=0 | 0.89 ± 0.01 | 0.84 ± 0.01 | -6.0% | 0.940 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default Length=1 | 2.50 ± 0.05 | 2.91 ± 0.03 | +16.5% | 1.165 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default Length=2 | 2.79 ± 0.16 | 3.14 ± 0.03 | +12.8% | 1.128 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default Length=10 | 4.42 ± 0.16 | 5.79 ± 0.03 | +31.2% | 1.312 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpace_Default Length=256 | 129.17 ± 2.06 | 11.66 ± 0.08 | -91.0% | 0.090 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default Length=0 | 1.08 ± 0.05 | 0.83 ± 0.01 | -23.1% | 0.769 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default Length=1 | 3.83 ± 0.12 | 3.96 ± 0.67 | +3.5% | 1.035 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default Length=2 | 4.30 ± 0.25 | 5.43 ± 1.35 | +26.3% | 1.263 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default Length=10 | 10.06 ± 0.54 | 11.98 ± 0.36 | +19.0% | 1.190 | 0 → 0 |
| SkipWhiteSpaceBenchmarks.SkipWhiteSpaceOrNewLine_Default Length=256 | 281.58 ± 3.58 | 171.92 ± 6.21 | -38.9% | 0.611 | 0 → 0 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7122.27 ± 105.03 | 7191.24 ± 101.42 | +1.0% | 1.010 | 17872 → 17872 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6271.28 ± 101.28 | 6298.26 ± 262.66 | +0.4% | 1.004 | 19264 → 19264 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2600.20 ± 46.06 | 2670.45 ± 104.94 | +2.7% | 1.027 | 5312 → 5312 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2459.86 ± 19.06 | 2494.79 ± 103.83 | +1.4% | 1.014 | 2624 → 2624 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8206.80 ± 703.57 | 8130.37 ± 270.30 | -0.9% | 0.991 | 18384 → 18384 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7125.85 ± 448.06 | 7071.04 ± 97.32 | -0.8% | 0.992 | 19776 → 19776 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2678.28 ± 49.36 | 2794.15 ± 91.25 | +4.3% | 1.043 | 5360 → 5360 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2541.34 ± 84.48 | 2575.15 ± 32.35 | +1.3% | 1.013 | 2672 → 2672 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14112.01 ± 217.36 | 13974.31 ± 362.53 | -1.0% | 0.990 | 33744 → 33744 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 13225.36 ± 1553.50 | 13033.97 ± 764.18 | -1.4% | 0.986 | 35136 → 35136 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3303.40 ± 39.70 | 3387.04 ± 115.74 | +2.5% | 1.025 | 7792 → 7792 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3640.27 ± 75.65 | 3703.13 ± 158.17 | +1.7% | 1.017 | 5104 → 5104 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 33757.46 ± 931.28 | 33905.84 ± 258.16 | +0.4% | 1.004 | 50128 → 50128 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32800.88 ± 546.79 | 32977.61 ± 566.36 | +0.5% | 1.005 | 51520 → 51520 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5139.47 ± 49.29 | 5163.49 ± 59.44 | +0.5% | 1.005 | 9328 → 9328 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5537.02 ± 222.92 | 5497.73 ± 230.81 | -0.7% | 0.993 | 6640 → 6640 |
