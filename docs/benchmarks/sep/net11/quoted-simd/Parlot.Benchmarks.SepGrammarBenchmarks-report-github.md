```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 1.14 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.043 μs** | **0.1770 μs** | **0.0460 μs** | **2.1355** | **0.0712** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.518 μs | 0.4809 μs | 0.1249 μs | 2.2413 | 0.0640 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.637 μs | 0.0625 μs | 0.0097 μs | 0.6260 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.501 μs | 0.1342 μs | 0.0208 μs | 0.3018 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.167 μs** | **0.5133 μs** | **0.1333 μs** | **2.1944** | **0.0813** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.176 μs | 0.1901 μs | 0.0294 μs | 2.3625 | 0.0716 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.735 μs | 0.0307 μs | 0.0048 μs | 0.6294 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.638 μs | 0.0776 μs | 0.0202 μs | 0.3120 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **14.194 μs** | **0.4770 μs** | **0.1239 μs** | **4.0046** | **0.2860** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 13.405 μs | 1.1150 μs | 0.2896 μs | 4.1754 | 0.3914 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.455 μs | 0.1106 μs | 0.0287 μs | 0.9093 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.663 μs | 0.0744 μs | 0.0115 μs | 0.6020 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **34.371 μs** | **2.7750 μs** | **0.7206 μs** | **5.9028** | **0.6944** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 33.051 μs | 0.8075 μs | 0.2097 μs | 5.8594 | 0.6510 |  50.31 KB |
| SqlRuntime    | 256         | True    |  5.238 μs | 0.1071 μs | 0.0278 μs | 1.1067 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  5.504 μs | 0.2920 μs | 0.0758 μs | 0.7642 |      - |   6.48 KB |
