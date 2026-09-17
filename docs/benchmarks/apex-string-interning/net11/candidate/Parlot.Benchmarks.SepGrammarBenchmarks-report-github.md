```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 0.61 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.375 μs** | **0.5897 μs** | **0.1531 μs** | **1.5115** |      **-** |  **12.95 KB** |
| JsonGenerated | 8           | False   |  6.345 μs | 0.4779 μs | 0.0740 μs | 1.7413 |      - |  14.31 KB |
| SqlRuntime    | 8           | False   |  2.829 μs | 0.1441 μs | 0.0374 μs | 0.5497 |      - |   4.68 KB |
| SqlGenerated  | 8           | False   |  2.685 μs | 0.5198 μs | 0.1350 μs | 0.2376 |      - |   2.12 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.403 μs** | **0.3399 μs** | **0.0883 μs** | **1.5060** |      **-** |  **12.95 KB** |
| JsonGenerated | 8           | True    |  7.458 μs | 0.3908 μs | 0.0605 μs | 1.6932 |      - |  14.31 KB |
| SqlRuntime    | 8           | True    |  2.812 μs | 0.1769 μs | 0.0274 μs | 0.5509 |      - |   4.68 KB |
| SqlGenerated  | 8           | True    |  2.591 μs | 0.0738 μs | 0.0114 μs | 0.2346 |      - |   2.12 KB |
| **JsonRuntime**   | **256**         | **False**   | **14.286 μs** | **0.2747 μs** | **0.0425 μs** | **1.5696** |      **-** |  **12.95 KB** |
| JsonGenerated | 256         | False   | 13.422 μs | 0.6733 μs | 0.1749 μs | 1.7141 |      - |  14.31 KB |
| SqlRuntime    | 256         | False   |  3.391 μs | 0.0739 μs | 0.0192 μs | 0.7461 |      - |   6.27 KB |
| SqlGenerated  | 256         | False   |  3.723 μs | 0.1041 μs | 0.0270 μs | 0.4483 |      - |   3.71 KB |
| **JsonRuntime**   | **256**         | **True**    | **34.279 μs** | **1.3330 μs** | **0.3462 μs** | **5.5866** | **0.6983** |   **45.7 KB** |
| JsonGenerated | 256         | True    | 33.367 μs | 1.3787 μs | 0.3580 μs | 5.5628 | 0.6545 |  47.06 KB |
| SqlRuntime    | 256         | True    |  5.332 μs | 0.1220 μs | 0.0189 μs | 0.9081 |      - |   7.77 KB |
| SqlGenerated  | 256         | True    |  5.619 μs | 0.1412 μs | 0.0367 μs | 0.6127 |      - |   5.21 KB |
