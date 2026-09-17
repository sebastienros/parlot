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
| Method          | Distinct | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |--------- |------------:|-----------:|----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Materialize**     | **0**        | **1,126.57 ns** |  **54.562 ns** |  **8.444 ns** |  **1.00** |    **0.00** | **1.4640** |      **-** |   **12288 B** |        **1.00** |
| DocumentPool    | 0        | 7,434.79 ns |  84.096 ns | 13.014 ns |  6.60 |    0.05 | 4.8077 | 0.0801 |   40768 B |        3.32 |
| LastStringCache | 0        | 1,464.14 ns | 129.863 ns | 33.725 ns |  1.30 |    0.03 | 1.4561 |      - |   12288 B |        1.00 |
| KeepTextSpan    | 0        |    66.34 ns |   1.650 ns |  0.428 ns |  0.06 |    0.00 |      - |      - |         - |        0.00 |
|                 |          |             |            |           |       |         |        |        |           |             |
| **Materialize**     | **1**        | **1,121.55 ns** |  **12.573 ns** |  **1.946 ns** |  **1.00** |    **0.00** | **1.4598** |      **-** |   **12288 B** |       **1.000** |
| DocumentPool    | 1        | 1,679.69 ns |  35.339 ns |  5.469 ns |  1.50 |    0.00 | 0.0333 |      - |     288 B |       0.023 |
| LastStringCache | 1        |   354.98 ns |  22.351 ns |  5.805 ns |  0.32 |    0.00 | 0.0035 |      - |      48 B |       0.004 |
| KeepTextSpan    | 1        |    66.48 ns |   5.818 ns |  0.900 ns |  0.06 |    0.00 |      - |      - |         - |       0.000 |
|                 |          |             |            |           |       |         |        |        |           |             |
| **Materialize**     | **4**        | **1,124.30 ns** |   **9.498 ns** |  **2.467 ns** |  **1.00** |    **0.00** | **1.4625** |      **-** |   **12288 B** |        **1.00** |
| DocumentPool    | 4        | 2,096.22 ns | 121.476 ns | 18.799 ns |  1.86 |    0.02 | 0.0633 |      - |     656 B |        0.05 |
| LastStringCache | 4        | 1,353.04 ns |  43.339 ns | 11.255 ns |  1.20 |    0.01 | 1.4582 |      - |   12288 B |        1.00 |
| KeepTextSpan    | 4        |    66.24 ns |   3.406 ns |  0.885 ns |  0.06 |    0.00 |      - |      - |         - |        0.00 |
