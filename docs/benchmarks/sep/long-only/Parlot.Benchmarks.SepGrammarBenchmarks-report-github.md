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
| **JsonRuntime**   | **8**           | **6.994 μs** | **0.1263 μs** | **0.0661 μs** | **2.1181** | **0.1042** |  **17.45 KB** |
| JsonGenerated | 8           | 6.382 μs | 0.0514 μs | 0.0269 μs | 2.2843 | 0.0952 |  18.81 KB |
| SqlRuntime    | 8           | 2.705 μs | 0.0151 μs | 0.0067 μs | 0.6283 |      - |   5.19 KB |
| SqlGenerated  | 8           | 2.546 μs | 0.0444 μs | 0.0232 μs | 0.3035 |      - |   2.56 KB |
| **JsonRuntime**   | **256**         | **8.370 μs** | **0.0696 μs** | **0.0309 μs** | **4.0173** | **0.3383** |  **32.95 KB** |
| JsonGenerated | 256         | 7.940 μs | 0.0124 μs | 0.0044 μs | 4.1774 | 0.3615 |  34.31 KB |
| SqlRuntime    | 256         | 2.958 μs | 0.0428 μs | 0.0224 μs | 0.9241 |      - |   7.61 KB |
| SqlGenerated  | 256         | 3.168 μs | 0.0171 μs | 0.0076 μs | 0.5984 |      - |   4.98 KB |
