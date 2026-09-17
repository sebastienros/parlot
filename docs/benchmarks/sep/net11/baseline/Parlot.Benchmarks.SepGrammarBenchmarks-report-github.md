```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 1.78 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.122 μs** | **0.1050 μs** | **0.0273 μs** | **2.1283** | **0.0709** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.271 μs | 0.1013 μs | 0.0263 μs | 2.2545 | 0.0626 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.600 μs | 0.0461 μs | 0.0120 μs | 0.6206 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.460 μs | 0.0191 μs | 0.0049 μs | 0.2929 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.207 μs** | **0.7036 μs** | **0.1827 μs** | **2.1718** | **0.0804** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.126 μs | 0.4481 μs | 0.0693 μs | 2.3491 | 0.0712 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.678 μs | 0.0494 μs | 0.0128 μs | 0.6405 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.541 μs | 0.0845 μs | 0.0219 μs | 0.3012 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **14.112 μs** | **0.2174 μs** | **0.0336 μs** | **3.9863** | **0.2847** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 13.225 μs | 1.5535 μs | 0.4034 μs | 4.1237 | 0.3866 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.303 μs | 0.0397 μs | 0.0103 μs | 0.9309 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.640 μs | 0.0756 μs | 0.0196 μs | 0.5838 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **33.757 μs** | **0.9313 μs** | **0.2419 μs** | **5.6818** | **0.6684** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 32.801 μs | 0.5468 μs | 0.1420 μs | 5.8594 | 0.6510 |  50.31 KB |
| SqlRuntime    | 256         | True    |  5.139 μs | 0.0493 μs | 0.0076 μs | 1.0947 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  5.537 μs | 0.2229 μs | 0.0345 μs | 0.7792 |      - |   6.48 KB |
