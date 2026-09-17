# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7122.27 ± 105.03 | 7627.32 ± 349.96 | +7.1% | 1.071 | 17872 → 17872 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6271.28 ± 101.28 | 5758.52 ± 116.25 | -8.2% | 0.918 | 19264 → 19264 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2600.20 ± 46.06 | 2725.51 ± 199.63 | +4.8% | 1.048 | 5312 → 5312 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2459.86 ± 19.06 | 2581.47 ± 61.14 | +4.9% | 1.049 | 2624 → 2624 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8206.80 ± 703.57 | 8069.91 ± 369.47 | -1.7% | 0.983 | 18384 → 18384 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7125.85 ± 448.06 | 6826.27 ± 256.79 | -4.2% | 0.958 | 19776 → 19776 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2678.28 ± 49.36 | 2851.22 ± 107.40 | +6.5% | 1.065 | 5360 → 5360 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2541.34 ± 84.48 | 2636.02 ± 93.17 | +3.7% | 1.037 | 2672 → 2672 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14112.01 ± 217.36 | 11878.96 ± 387.14 | -15.8% | 0.842 | 33744 → 33744 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 13225.36 ± 1553.50 | 11111.99 ± 674.60 | -16.0% | 0.840 | 35136 → 35136 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3303.40 ± 39.70 | 3141.55 ± 150.91 | -4.9% | 0.951 | 7792 → 7792 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3640.27 ± 75.65 | 3447.05 ± 31.52 | -5.3% | 0.947 | 5104 → 5104 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 33757.46 ± 931.28 | 29828.31 ± 594.20 | -11.6% | 0.884 | 50128 → 50128 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32800.88 ± 546.79 | 32098.08 ± 12642.30 | -2.1% | 0.979 | 51520 → 51520 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5139.47 ± 49.29 | 4980.72 ± 369.60 | -3.1% | 0.969 | 9328 → 9328 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5537.02 ± 222.92 | 5156.40 ± 307.96 | -6.9% | 0.931 | 6640 → 6640 |
| CursorBenchmarks.Advance Length=1, NewLines=False | 2.20 ± 0.08 | 2.15 ± 0.07 | -2.3% | 0.977 | 0 → 0 |
| CursorBenchmarks.Advance Length=1, NewLines=True | 2.20 ± 0.11 | 2.02 ± 0.05 | -8.2% | 0.918 | 0 → 0 |
| CursorBenchmarks.Advance Length=8, NewLines=False | 6.91 ± 0.38 | 4.01 ± 0.77 | -42.0% | 0.580 | 0 → 0 |
| CursorBenchmarks.Advance Length=8, NewLines=True | 8.23 ± 0.84 | 5.86 ± 1.36 | -28.7% | 0.713 | 0 → 0 |
| CursorBenchmarks.Advance Length=32, NewLines=False | 22.33 ± 0.23 | 13.92 ± 2.48 | -37.7% | 0.623 | 0 → 0 |
| CursorBenchmarks.Advance Length=32, NewLines=True | 34.70 ± 29.14 | 13.01 ± 0.18 | -62.5% | 0.375 | 0 → 0 |
| CursorBenchmarks.Advance Length=256, NewLines=False | 171.11 ± 11.19 | 118.15 ± 17.81 | -31.0% | 0.690 | 0 → 0 |
| CursorBenchmarks.Advance Length=256, NewLines=True | 298.69 ± 111.79 | 116.43 ± 14.69 | -61.0% | 0.390 | 0 → 0 |
| CursorBenchmarks.Advance Length=4096, NewLines=False | 2583.20 ± 12.63 | 1893.87 ± 186.27 | -26.7% | 0.733 | 0 → 0 |
| CursorBenchmarks.Advance Length=4096, NewLines=True | 6185.83 ± 3174.66 | 1804.28 ± 178.32 | -70.8% | 0.292 | 0 → 0 |
