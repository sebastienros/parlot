```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 0.16 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.344 μs** | **0.7708 μs** | **0.1193 μs** | **1.5832** |      **-** |  **12.95 KB** |
| JsonGenerated | 8           | False   |  6.612 μs | 0.4308 μs | 0.1119 μs | 1.7237 |      - |  14.31 KB |
| SqlRuntime    | 8           | False   |  2.783 μs | 0.1670 μs | 0.0434 μs | 0.5568 |      - |   4.68 KB |
| SqlGenerated  | 8           | False   |  2.554 μs | 0.1092 μs | 0.0284 μs | 0.2510 |      - |   2.12 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.180 μs** | **0.0220 μs** | **0.0034 μs** | **1.5564** |      **-** |  **12.95 KB** |
| JsonGenerated | 8           | True    |  7.328 μs | 0.1734 μs | 0.0450 μs | 1.6833 |      - |  14.31 KB |
| SqlRuntime    | 8           | True    |  2.812 μs | 0.0951 μs | 0.0147 μs | 0.5526 |      - |   4.68 KB |
| SqlGenerated  | 8           | True    |  2.633 μs | 0.0977 μs | 0.0254 μs | 0.2354 |      - |   2.12 KB |
| **JsonRuntime**   | **256**         | **False**   | **18.576 μs** | **0.1879 μs** | **0.0291 μs** | **1.5060** |      **-** |  **12.95 KB** |
| JsonGenerated | 256         | False   | 18.037 μs | 1.4830 μs | 0.2295 μs | 1.6352 |      - |  14.31 KB |
| SqlRuntime    | 256         | False   |  3.731 μs | 0.2543 μs | 0.0660 μs | 0.7571 |      - |   6.27 KB |
| SqlGenerated  | 256         | False   |  4.026 μs | 0.0756 μs | 0.0196 μs | 0.4371 |      - |   3.71 KB |
| **JsonRuntime**   | **256**         | **True**    | **34.175 μs** | **0.2216 μs** | **0.0575 μs** | **5.4054** | **0.6757** |   **45.7 KB** |
| JsonGenerated | 256         | True    | 33.102 μs | 0.6158 μs | 0.1599 μs | 5.6818 | 0.6684 |  47.06 KB |
| SqlRuntime    | 256         | True    |  5.577 μs | 0.2151 μs | 0.0333 μs | 0.9369 |      - |   7.77 KB |
| SqlGenerated  | 256         | True    |  5.881 μs | 0.3003 μs | 0.0780 μs | 0.5847 |      - |   5.21 KB |
