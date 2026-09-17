# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7122.27 ± 105.03 | 7042.51 ± 176.95 | -1.1% | 0.989 | 17872 → 17872 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6271.28 ± 101.28 | 6517.78 ± 480.90 | +3.9% | 1.039 | 19264 → 19264 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2600.20 ± 46.06 | 2636.63 ± 62.49 | +1.4% | 1.014 | 5312 → 5312 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2459.86 ± 19.06 | 2500.66 ± 134.15 | +1.7% | 1.017 | 2624 → 2624 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8206.80 ± 703.57 | 8166.67 ± 513.28 | -0.5% | 0.995 | 18384 → 18384 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7125.85 ± 448.06 | 7176.22 ± 190.07 | +0.7% | 1.007 | 19776 → 19776 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2678.28 ± 49.36 | 2734.80 ± 30.74 | +2.1% | 1.021 | 5360 → 5360 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2541.34 ± 84.48 | 2637.71 ± 77.63 | +3.8% | 1.038 | 2672 → 2672 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14112.01 ± 217.36 | 14193.71 ± 477.03 | +0.6% | 1.006 | 33744 → 33744 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 13225.36 ± 1553.50 | 13404.65 ± 1115.03 | +1.4% | 1.014 | 35136 → 35136 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3303.40 ± 39.70 | 3455.23 ± 110.62 | +4.6% | 1.046 | 7792 → 7792 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3640.27 ± 75.65 | 3662.83 ± 74.42 | +0.6% | 1.006 | 5104 → 5104 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 33757.46 ± 931.28 | 34370.52 ± 2774.97 | +1.8% | 1.018 | 50128 → 50128 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32800.88 ± 546.79 | 33051.10 ± 807.50 | +0.8% | 1.008 | 51520 → 51520 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5139.47 ± 49.29 | 5237.82 ± 107.08 | +1.9% | 1.019 | 9328 → 9328 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5537.02 ± 222.92 | 5504.15 ± 291.96 | -0.6% | 0.994 | 6640 → 6640 |
| QuotedStringBenchmarks.Read Length=0, Unicode=False | 5.22 ± 0.07 | 5.26 ± 0.17 | +0.8% | 1.008 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=0, Unicode=True | 5.19 ± 0.06 | 5.19 ± 0.12 | -0.1% | 0.999 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=6, Unicode=False | 9.73 ± 0.11 | 9.70 ± 0.11 | -0.3% | 0.997 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=6, Unicode=True | 9.79 ± 0.36 | 9.77 ± 0.59 | -0.2% | 0.998 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=8, Unicode=False | 11.24 ± 0.33 | 11.08 ± 0.54 | -1.5% | 0.985 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=8, Unicode=True | 11.61 ± 0.22 | 10.88 ± 0.15 | -6.4% | 0.936 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=16, Unicode=False | 16.70 ± 0.09 | 17.22 ± 0.36 | +3.1% | 1.031 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=16, Unicode=True | 16.66 ± 0.39 | 17.34 ± 0.55 | +4.0% | 1.040 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=128, Unicode=False | 97.70 ± 5.98 | 98.93 ± 2.59 | +1.3% | 1.013 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=128, Unicode=True | 97.34 ± 3.98 | 99.37 ± 3.21 | +2.1% | 1.021 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=1024, Unicode=False | 703.68 ± 3.51 | 716.74 ± 21.20 | +1.9% | 1.019 | 0 → 0 |
| QuotedStringBenchmarks.Read Length=1024, Unicode=True | 709.80 ± 12.63 | 716.76 ± 14.00 | +1.0% | 1.010 | 0 → 0 |
