```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method        | TokenLength | Mean     | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |---------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **6.677 μs** | **0.0387 μs** | **0.0172 μs** | **2.1368** | **0.1002** |  **17.45 KB** |
| JsonGenerated | 8           | 5.982 μs | 0.0519 μs | 0.0271 μs | 2.2982 | 0.0895 |  18.81 KB |
| SqlRuntime    | 8           | 2.707 μs | 0.0172 μs | 0.0090 μs | 0.6331 |      - |   5.19 KB |
| SqlGenerated  | 8           | 2.457 μs | 0.0201 μs | 0.0105 μs | 0.3088 |      - |   2.56 KB |
| **JsonRuntime**   | **256**         | **8.556 μs** | **0.2276 μs** | **0.1190 μs** | **4.0161** | **0.3347** |  **32.95 KB** |
| JsonGenerated | 256         | 7.618 μs | 0.0618 μs | 0.0275 μs | 4.1918 | 0.3776 |  34.31 KB |
| SqlRuntime    | 256         | 2.918 μs | 0.0843 μs | 0.0441 μs | 0.9189 |      - |   7.61 KB |
| SqlGenerated  | 256         | 3.299 μs | 0.0727 μs | 0.0380 μs | 0.5975 |      - |   4.98 KB |
