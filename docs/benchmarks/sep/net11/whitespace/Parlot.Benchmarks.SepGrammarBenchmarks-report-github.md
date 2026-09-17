```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 4.28 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.191 μs** | **0.1014 μs** | **0.0263 μs** | **2.1076** | **0.0727** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.298 μs | 0.2627 μs | 0.0682 μs | 2.2583 | 0.0610 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.670 μs | 0.1049 μs | 0.0273 μs | 0.6094 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.495 μs | 0.1038 μs | 0.0270 μs | 0.2982 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.130 μs** | **0.2703 μs** | **0.0418 μs** | **2.1774** | **0.0806** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.071 μs | 0.0973 μs | 0.0253 μs | 2.3148 | 0.0701 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.794 μs | 0.0912 μs | 0.0237 μs | 0.6341 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.575 μs | 0.0324 μs | 0.0084 μs | 0.3080 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **13.974 μs** | **0.3625 μs** | **0.0941 μs** | **3.9503** | **0.2822** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 13.034 μs | 0.7642 μs | 0.1985 μs | 4.1400 | 0.2671 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.387 μs | 0.1157 μs | 0.0301 μs | 0.9053 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.703 μs | 0.1582 μs | 0.0411 μs | 0.5858 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **33.906 μs** | **0.2582 μs** | **0.0670 μs** | **5.7432** | **0.6757** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 32.978 μs | 0.5664 μs | 0.0876 μs | 6.0160 | 0.6684 |  50.31 KB |
| SqlRuntime    | 256         | True    |  5.163 μs | 0.0594 μs | 0.0092 μs | 1.0829 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  5.498 μs | 0.2308 μs | 0.0599 μs | 0.7662 |      - |   6.48 KB |
