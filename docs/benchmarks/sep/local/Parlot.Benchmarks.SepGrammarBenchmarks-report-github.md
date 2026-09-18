```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method        | TokenLength | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           |  **6.787 μs** | **0.1565 μs** | **0.0695 μs** | **2.1152** | **0.1023** |  **17.45 KB** |
| JsonGenerated | 8           |  6.489 μs | 0.0928 μs | 0.0412 μs | 2.2733 | 0.0961 |  18.81 KB |
| SqlRuntime    | 8           |  2.861 μs | 0.0952 μs | 0.0498 μs | 0.6261 |      - |   5.19 KB |
| SqlGenerated  | 8           |  2.672 μs | 0.1088 μs | 0.0569 μs | 0.3017 |      - |   2.56 KB |
| **JsonRuntime**   | **256**         | **12.429 μs** | **0.8862 μs** | **0.4635 μs** | **3.9907** | **0.3218** |  **32.95 KB** |
| JsonGenerated | 256         | 11.524 μs | 0.2150 μs | 0.1124 μs | 4.1893 | 0.3397 |  34.31 KB |
| SqlRuntime    | 256         |  3.384 μs | 0.0782 μs | 0.0347 μs | 0.9181 |      - |   7.61 KB |
| SqlGenerated  | 256         |  3.759 μs | 0.0694 μs | 0.0363 μs | 0.6028 |      - |   4.98 KB |
