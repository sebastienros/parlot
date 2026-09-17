```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 0.81 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.070 μs** | **0.1525 μs** | **0.0396 μs** | **2.1091** | **0.0703** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.562 μs | 0.4839 μs | 0.0749 μs | 2.2750 | 0.0632 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.736 μs | 0.1458 μs | 0.0379 μs | 0.6218 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.549 μs | 0.2180 μs | 0.0566 μs | 0.3044 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.415 μs** | **0.7902 μs** | **0.2052 μs** | **2.1353** | **0.0821** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.195 μs | 0.1290 μs | 0.0335 μs | 2.3202 | 0.0725 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.764 μs | 0.2054 μs | 0.0533 μs | 0.6226 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.565 μs | 0.0548 μs | 0.0142 μs | 0.3065 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **14.142 μs** | **0.5655 μs** | **0.1469 μs** | **3.9238** | **0.2803** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 12.904 μs | 0.1072 μs | 0.0166 μs | 4.1408 | 0.3882 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.444 μs | 0.2144 μs | 0.0332 μs | 0.9107 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.750 μs | 0.0958 μs | 0.0249 μs | 0.5931 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **34.085 μs** | **1.3623 μs** | **0.2108 μs** | **5.7124** | **0.6720** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 32.571 μs | 0.5168 μs | 0.1342 μs | 5.8594 | 0.6510 |  50.31 KB |
| SqlRuntime    | 256         | True    |  5.177 μs | 0.2534 μs | 0.0392 μs | 1.0811 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  5.665 μs | 0.1443 μs | 0.0375 μs | 0.7764 |      - |   6.48 KB |
