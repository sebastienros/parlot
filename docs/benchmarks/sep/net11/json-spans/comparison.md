# .NET 11 comparison

Time ratio is candidate / baseline; lower is better. Error is BenchmarkDotNet's 99.9% confidence-interval half-width. Small differences and overlapping intervals are inconclusive in this short screening run.

| Case | Baseline ns ± error | Candidate ns ± error | Time delta | Time ratio | Allocated B (base → candidate) |
|---|---:|---:|---:|---:|---:|
| JsonRetainedTextParserBenchmarks.Parse Distinct=1, Objects=False, Escaped=False, Optimize=True | 10279.66 ± 524.27 | 8897.60 ± 195.44 | -13.4% | 0.866 | 23064 → 12824 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=1, Objects=False, Escaped=True, Optimize=True | 13592.14 ± 1037.02 | 14033.36 ± 714.37 | +3.2% | 1.032 | 23064 → 25112 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=1, Objects=True, Escaped=False, Optimize=True | 32776.95 ± 582.33 | 30704.30 ± 669.36 | -6.3% | 0.937 | 139800 → 127512 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=1, Objects=True, Escaped=True, Optimize=True | 36446.30 ± 1327.02 | 35544.73 ± 786.83 | -2.5% | 0.975 | 139800 → 139800 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=4, Objects=False, Escaped=False, Optimize=True | 10142.50 ± 268.19 | 9200.30 ± 155.34 | -9.3% | 0.907 | 23064 → 12824 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=4, Objects=False, Escaped=True, Optimize=True | 13866.75 ± 666.65 | 14004.42 ± 899.46 | +1.0% | 1.010 | 23064 → 25112 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=4, Objects=True, Escaped=False, Optimize=True | 38007.68 ± 3439.73 | 35067.22 ± 1782.61 | -7.7% | 0.923 | 139800 → 127512 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=4, Objects=True, Escaped=True, Optimize=True | 38013.01 ± 3877.95 | 37367.11 ± 2811.44 | -1.7% | 0.983 | 139800 → 139800 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=256, Objects=False, Escaped=False, Optimize=True | 10385.23 ± 259.63 | 9762.05 ± 2634.55 | -6.0% | 0.940 | 23064 → 12824 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=256, Objects=False, Escaped=True, Optimize=True | 13951.01 ± 618.40 | 15401.43 ± 699.44 | +10.4% | 1.104 | 24312 → 26360 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=256, Objects=True, Escaped=False, Optimize=True | 36172.69 ± 1593.28 | 34181.12 ± 3548.50 | -5.5% | 0.945 | 139800 → 127512 |
| JsonRetainedTextParserBenchmarks.Parse Distinct=256, Objects=True, Escaped=True, Optimize=True | 40373.67 ± 577.83 | 44619.28 ± 11085.17 | +10.5% | 1.105 | 141048 → 141048 |
