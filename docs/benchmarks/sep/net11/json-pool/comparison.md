# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| JsonDocumentPoolParserBenchmarks.Parse Distinct=1, Objects=False, Escaped=False, Optimize=True | 9916.02 ± 151.10 | 11228.11 ± 275.06 | +13.2% | 1.132 | 23072 → 11072 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=1, Objects=False, Escaped=True, Optimize=True | 15837.01 ± 10875.52 | 16424.61 ± 75.95 | +3.7% | 1.037 | 23072 → 23312 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=1, Objects=True, Escaped=False, Optimize=True | 36281.75 ± 1970.95 | 41844.95 ± 2297.08 | +15.3% | 1.153 | 139808 → 119648 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=1, Objects=True, Escaped=True, Optimize=True | 37428.92 ± 7827.05 | 42777.54 ± 1225.19 | +14.3% | 1.143 | 139808 → 131888 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=4, Objects=False, Escaped=False, Optimize=True | 10034.35 ± 245.79 | 12500.97 ± 204.70 | +24.6% | 1.246 | 23072 → 11520 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=4, Objects=False, Escaped=True, Optimize=True | 13280.58 ± 115.80 | 17534.05 ± 570.12 | +32.0% | 1.320 | 23072 → 23616 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=4, Objects=True, Escaped=False, Optimize=True | 32359.03 ± 1124.16 | 39424.66 ± 394.18 | +21.8% | 1.218 | 139808 → 120096 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=4, Objects=True, Escaped=True, Optimize=True | 37458.32 ± 3478.87 | 43719.48 ± 2873.51 | +16.7% | 1.167 | 139808 → 132192 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=256, Objects=False, Escaped=False, Optimize=True | 10322.00 ± 602.06 | 19516.77 ± 396.80 | +89.1% | 1.891 | 23072 → 51632 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=256, Objects=False, Escaped=True, Optimize=True | 14333.53 ± 835.20 | 24611.82 ± 1540.59 | +71.7% | 1.717 | 24320 → 52880 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=256, Objects=True, Escaped=False, Optimize=True | 37183.84 ± 1369.72 | 49941.33 ± 5873.34 | +34.3% | 1.343 | 139808 → 160208 |
| JsonDocumentPoolParserBenchmarks.Parse Distinct=256, Objects=True, Escaped=True, Optimize=True | 37270.16 ± 855.29 | 52403.95 ± 3321.86 | +40.6% | 1.406 | 141056 → 161456 |
