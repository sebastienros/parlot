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
| **JsonRuntime**   | **8**           | **7.075 μs** | **0.1854 μs** | **0.0823 μs** | **2.1307** | **0.1015** |  **17.45 KB** |
| JsonGenerated | 8           | 6.631 μs | 0.4013 μs | 0.1782 μs | 2.2918 | 0.0982 |  18.81 KB |
| SqlRuntime    | 8           | 2.829 μs | 0.0896 μs | 0.0398 μs | 0.6343 |      - |   5.19 KB |
| SqlGenerated  | 8           | 2.643 μs | 0.0763 μs | 0.0339 μs | 0.3128 |      - |   2.56 KB |
| **JsonRuntime**   | **256**         | **8.813 μs** | **0.4873 μs** | **0.2164 μs** | **4.0064** | **0.3561** |  **32.95 KB** |
| JsonGenerated | 256         | 8.198 μs | 0.1114 μs | 0.0583 μs | 4.1856 | 0.3657 |  34.31 KB |
| SqlRuntime    | 256         | 3.053 μs | 0.0448 μs | 0.0199 μs | 0.9202 |      - |   7.61 KB |
| SqlGenerated  | 256         | 3.396 μs | 0.0437 μs | 0.0229 μs | 0.6101 |      - |   4.98 KB |
