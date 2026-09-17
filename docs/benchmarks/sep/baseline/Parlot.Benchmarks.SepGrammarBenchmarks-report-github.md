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
| **JsonRuntime**   | **8**           |  **6.624 μs** | **0.0468 μs** | **0.0245 μs** | **2.1067** |      **-** |  **17.45 KB** |
| JsonGenerated | 8           |  6.151 μs | 0.0903 μs | 0.0472 μs | 2.2772 | 0.0923 |  18.81 KB |
| SqlRuntime    | 8           |  2.601 μs | 0.0369 μs | 0.0164 μs | 0.6302 |      - |   5.19 KB |
| SqlGenerated  | 8           |  2.417 μs | 0.0311 μs | 0.0163 μs | 0.3137 |      - |   2.56 KB |
| **JsonRuntime**   | **256**         | **13.358 μs** | **0.1376 μs** | **0.0720 μs** | **4.0259** | **0.3300** |  **32.95 KB** |
| JsonGenerated | 256         | 12.707 μs | 0.1964 μs | 0.1027 μs | 4.1454 | 0.3827 |  34.31 KB |
| SqlRuntime    | 256         |  3.316 μs | 0.0686 μs | 0.0305 μs | 0.9235 |      - |   7.61 KB |
| SqlGenerated  | 256         |  3.708 μs | 0.1115 μs | 0.0583 μs | 0.5985 |      - |   4.98 KB |
