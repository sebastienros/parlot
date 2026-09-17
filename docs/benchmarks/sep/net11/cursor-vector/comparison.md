# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=False | 7122.27 ± 105.03 | 7197.86 ± 107.59 | +1.1% | 1.011 | 17872 → 17872 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=False | 6271.28 ± 101.28 | 6376.04 ± 184.25 | +1.7% | 1.017 | 19264 → 19264 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=False | 2600.20 ± 46.06 | 2675.90 ± 54.77 | +2.9% | 1.029 | 5312 → 5312 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=False | 2459.86 ± 19.06 | 2486.41 ± 108.88 | +1.1% | 1.011 | 2624 → 2624 |
| GrammarBenchmarks.JsonRuntime TokenLength=8, Escaped=True | 8206.80 ± 703.57 | 8182.99 ± 90.88 | -0.3% | 0.997 | 18384 → 18384 |
| GrammarBenchmarks.JsonGenerated TokenLength=8, Escaped=True | 7125.85 ± 448.06 | 7302.19 ± 182.33 | +2.5% | 1.025 | 19776 → 19776 |
| GrammarBenchmarks.SqlRuntime TokenLength=8, Escaped=True | 2678.28 ± 49.36 | 2755.62 ± 27.12 | +2.9% | 1.029 | 5360 → 5360 |
| GrammarBenchmarks.SqlGenerated TokenLength=8, Escaped=True | 2541.34 ± 84.48 | 2530.52 ± 63.25 | -0.4% | 0.996 | 2672 → 2672 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=False | 14112.01 ± 217.36 | 8897.58 ± 573.86 | -37.0% | 0.630 | 33744 → 33744 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=False | 13225.36 ± 1553.50 | 7893.41 ± 283.72 | -40.3% | 0.597 | 35136 → 35136 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=False | 3303.40 ± 39.70 | 2837.79 ± 47.14 | -14.1% | 0.859 | 7792 → 7792 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=False | 3640.27 ± 75.65 | 3127.70 ± 50.31 | -14.1% | 0.859 | 5104 → 5104 |
| GrammarBenchmarks.JsonRuntime TokenLength=256, Escaped=True | 33757.46 ± 931.28 | 23220.60 ± 303.02 | -31.2% | 0.688 | 50128 → 50128 |
| GrammarBenchmarks.JsonGenerated TokenLength=256, Escaped=True | 32800.88 ± 546.79 | 22331.70 ± 554.85 | -31.9% | 0.681 | 51520 → 51520 |
| GrammarBenchmarks.SqlRuntime TokenLength=256, Escaped=True | 5139.47 ± 49.29 | 4295.74 ± 195.04 | -16.4% | 0.836 | 9328 → 9328 |
| GrammarBenchmarks.SqlGenerated TokenLength=256, Escaped=True | 5537.02 ± 222.92 | 4465.11 ± 15.61 | -19.4% | 0.806 | 6640 → 6640 |
| CursorBenchmarks.Advance Length=1, NewLines=False | 2.20 ± 0.08 | 2.74 ± 0.03 | +24.4% | 1.244 | 0 → 0 |
| CursorBenchmarks.Advance Length=1, NewLines=True | 2.20 ± 0.11 | 2.72 ± 0.05 | +23.4% | 1.234 | 0 → 0 |
| CursorBenchmarks.Advance Length=8, NewLines=False | 6.91 ± 0.38 | 7.57 ± 0.16 | +9.6% | 1.096 | 0 → 0 |
| CursorBenchmarks.Advance Length=8, NewLines=True | 8.23 ± 0.84 | 9.07 ± 0.58 | +10.3% | 1.103 | 0 → 0 |
| CursorBenchmarks.Advance Length=32, NewLines=False | 22.33 ± 0.23 | 23.59 ± 0.94 | +5.6% | 1.056 | 0 → 0 |
| CursorBenchmarks.Advance Length=32, NewLines=True | 34.70 ± 29.14 | 33.38 ± 17.27 | -3.8% | 0.962 | 0 → 0 |
| CursorBenchmarks.Advance Length=256, NewLines=False | 171.11 ± 11.19 | 12.83 ± 0.45 | -92.5% | 0.075 | 0 → 0 |
| CursorBenchmarks.Advance Length=256, NewLines=True | 298.69 ± 111.79 | 264.06 ± 96.15 | -11.6% | 0.884 | 0 → 0 |
| CursorBenchmarks.Advance Length=4096, NewLines=False | 2583.20 ± 12.63 | 186.25 ± 5.28 | -92.8% | 0.072 | 0 → 0 |
| CursorBenchmarks.Advance Length=4096, NewLines=True | 6185.83 ± 3174.66 | 4751.02 ± 1616.85 | -23.2% | 0.768 | 0 → 0 |
