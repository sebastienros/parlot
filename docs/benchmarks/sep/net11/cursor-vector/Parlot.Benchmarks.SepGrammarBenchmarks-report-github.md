```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 2.25 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.198 μs** | **0.1076 μs** | **0.0279 μs** | **2.0857** | **0.0719** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.376 μs | 0.1843 μs | 0.0285 μs | 2.2636 | 0.0629 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.676 μs | 0.0548 μs | 0.0142 μs | 0.6351 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.486 μs | 0.1089 μs | 0.0283 μs | 0.2943 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.183 μs** | **0.0909 μs** | **0.0141 μs** | **2.1353** | **0.0821** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.302 μs | 0.1823 μs | 0.0282 μs | 2.3256 | 0.0727 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.756 μs | 0.0271 μs | 0.0042 μs | 0.6324 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.531 μs | 0.0633 μs | 0.0098 μs | 0.3055 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   |  **8.898 μs** | **0.5739 μs** | **0.1490 μs** | **3.9455** | **0.3587** |  **32.95 KB** |
| JsonGenerated | 256         | False   |  7.893 μs | 0.2837 μs | 0.0439 μs | 4.1458 | 0.3911 |  34.31 KB |
| SqlRuntime    | 256         | False   |  2.838 μs | 0.0471 μs | 0.0073 μs | 0.9107 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.128 μs | 0.0503 μs | 0.0131 μs | 0.5935 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **23.221 μs** | **0.3030 μs** | **0.0787 μs** | **5.8302** | **0.6996** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 22.332 μs | 0.5549 μs | 0.1441 μs | 5.9419 | 0.8803 |  50.31 KB |
| SqlRuntime    | 256         | True    |  4.296 μs | 0.1950 μs | 0.0507 μs | 1.1024 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  4.465 μs | 0.0156 μs | 0.0024 μs | 0.7595 |      - |   6.48 KB |
