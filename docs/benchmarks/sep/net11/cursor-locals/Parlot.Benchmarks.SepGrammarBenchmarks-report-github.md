```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 1.02 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error      | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|-----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.627 μs** |  **0.3500 μs** | **0.0542 μs** | **2.0900** | **0.0995** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  5.759 μs |  0.1163 μs | 0.0180 μs | 2.2684 | 0.0597 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.726 μs |  0.1996 μs | 0.0309 μs | 0.6201 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.581 μs |  0.0611 μs | 0.0159 μs | 0.3027 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.070 μs** |  **0.3695 μs** | **0.0572 μs** | **2.1580** | **0.0830** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  6.826 μs |  0.2568 μs | 0.0667 μs | 2.3123 | 0.0680 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.851 μs |  0.1074 μs | 0.0279 μs | 0.6250 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.636 μs |  0.0932 μs | 0.0144 μs | 0.2966 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **11.879 μs** |  **0.3871 μs** | **0.1005 μs** | **3.9944** | **0.3524** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 11.112 μs |  0.6746 μs | 0.1752 μs | 4.1075 | 0.3330 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.142 μs |  0.1509 μs | 0.0392 μs | 0.9163 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.447 μs |  0.0315 μs | 0.0049 μs | 0.5877 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **29.828 μs** |  **0.5942 μs** | **0.0920 μs** | **5.9242** | **0.8886** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 32.098 μs | 12.6423 μs | 3.2832 μs | 5.8685 | 0.5869 |  50.31 KB |
| SqlRuntime    | 256         | True    |  4.981 μs |  0.3696 μs | 0.0960 μs | 1.1080 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  5.156 μs |  0.3080 μs | 0.0800 μs | 0.7616 |      - |   6.48 KB |
