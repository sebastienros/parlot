# .NET 11 baseline

115 cases completed on Apple M4 Pro / ARM64, macOS 15.7.9, SDK 11.0.100-rc.1.26413.103 and runtime 11.0.0-rc.1.26413.103. BenchmarkDotNet 0.16.0-preview.1 recognizes the latest TFM. Source revision and full statistics (including original samples) are in [measurements.json](measurements.json).

Reproduce from the shared benchmark branch:

```sh
scripts/benchmark-latest.sh --filter '*Sep*Benchmarks*' '*SkipWhiteSpaceBenchmarks*' --warmupCount 3 --iterationCount 5 --iterationTime 100 --exporters json
```

Runs use one launch, three warmups, five measurements and 100 ms iterations. This is screening evidence on one ARM64 machine and a preview runtime; small or noisy differences require confirmation. BenchmarkDotNet could not raise process priority on this host. All timing runs are serial, with no concurrent builds or tests from this investigation. Normal correctness builds retain the supported framework matrix; only benchmarks opt into net11.0.

The same SQL/JSON corpus is used across candidate worktrees: 8/256-character identifiers and string payloads, plain and escaped strings, runtime and generated grammars. Dedicated tests cover cursor boundaries, Unicode and invalid quoted strings, dense/sparse escapes, whitespace prefix lengths, and document-scoped allocation patterns.

The BCL SearchValues scan beats both full custom SIMD scan kernels across all tested lengths. Two-pass exact-length string creation loses in all four decode cases. Bulk copying wins with sparse escapes but loses with dense escapes. These isolated results motivate the independent guarded implementations and grammar measurements; they do not establish a blanket runtime improvement.

CSV and Markdown reports alongside this file preserve means, errors and allocations for every baseline case. Candidate reports include before/after tables against this exact baseline.
