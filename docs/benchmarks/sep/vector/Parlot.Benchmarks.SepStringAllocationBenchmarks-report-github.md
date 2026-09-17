```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method          | Distinct | Mean        | Error        | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------- |--------- |------------:|-------------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| **Materialize**     | **0**        | **1,130.87 ns** |    **87.345 ns** |  **38.782 ns** |  **1.00** |    **0.04** | **1.4660** |      **-** |   **12288 B** |        **1.00** |
| DocumentPool    | 0        | 7,249.73 ns | 1,080.238 ns | 564.985 ns |  6.42 |    0.51 | 4.8577 | 0.2053 |   40768 B |        3.32 |
| LastStringCache | 0        | 1,511.25 ns |   131.115 ns |  68.575 ns |  1.34 |    0.07 | 1.4659 |      - |   12288 B |        1.00 |
| KeepTextSpan    | 0        |    73.00 ns |     3.645 ns |   1.618 ns |  0.06 |    0.00 |      - |      - |         - |        0.00 |
|                 |          |             |              |            |       |         |        |        |           |             |
| **Materialize**     | **1**        | **1,124.10 ns** |    **80.964 ns** |  **35.948 ns** |  **1.00** |    **0.04** | **1.4651** |      **-** |   **12288 B** |       **1.000** |
| DocumentPool    | 1        | 1,787.35 ns |   111.922 ns |  49.694 ns |  1.59 |    0.06 | 0.0265 |      - |     288 B |       0.023 |
| LastStringCache | 1        |   387.96 ns |     5.305 ns |   2.775 ns |  0.35 |    0.01 | 0.0041 |      - |      48 B |       0.004 |
| KeepTextSpan    | 1        |    71.42 ns |     1.271 ns |   0.564 ns |  0.06 |    0.00 |      - |      - |         - |       0.000 |
|                 |          |             |              |            |       |         |        |        |           |             |
| **Materialize**     | **4**        | **1,053.76 ns** |    **10.593 ns** |   **5.540 ns** |  **1.00** |    **0.01** | **1.4689** |      **-** |   **12288 B** |        **1.00** |
| DocumentPool    | 4        | 2,309.77 ns |    99.410 ns |  51.994 ns |  2.19 |    0.05 | 0.0676 |      - |     656 B |        0.05 |
| LastStringCache | 4        | 1,556.86 ns |    98.335 ns |  51.431 ns |  1.48 |    0.05 | 1.4650 |      - |   12288 B |        1.00 |
| KeepTextSpan    | 4        |    74.19 ns |     1.718 ns |   0.763 ns |  0.07 |    0.00 |      - |      - |         - |        0.00 |
