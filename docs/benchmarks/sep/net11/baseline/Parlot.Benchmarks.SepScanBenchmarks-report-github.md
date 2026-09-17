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
| Method             | Length | Unicode | Mean       | Error      | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |------- |-------- |-----------:|-----------:|----------:|------:|--------:|----------:|------------:|
| **Scalar**             | **8**      | **False**   |   **3.487 ns** |  **0.9970 ns** | **0.2589 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SearchValues       | 8      | False   |   1.648 ns |  0.0235 ns | 0.0036 ns |  0.47 |    0.03 |         - |          NA |
| VectorMask         | 8      | False   |   1.759 ns |  0.0138 ns | 0.0036 ns |  0.51 |    0.03 |         - |          NA |
| NarrowedVectorMask | 8      | False   |   5.194 ns |  0.5639 ns | 0.0873 ns |  1.50 |    0.10 |         - |          NA |
|                    |        |         |            |            |           |       |         |           |             |
| **Scalar**             | **8**      | **True**    |   **3.596 ns** |  **0.6421 ns** | **0.1667 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SearchValues       | 8      | True    |   1.646 ns |  0.0047 ns | 0.0012 ns |  0.46 |    0.02 |         - |          NA |
| VectorMask         | 8      | True    |   1.767 ns |  0.0350 ns | 0.0054 ns |  0.49 |    0.02 |         - |          NA |
| NarrowedVectorMask | 8      | True    |   5.149 ns |  0.5950 ns | 0.1545 ns |  1.43 |    0.07 |         - |          NA |
|                    |        |         |            |            |           |       |         |           |             |
| **Scalar**             | **64**     | **False**   |  **20.939 ns** |  **0.8559 ns** | **0.1324 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SearchValues       | 64     | False   |   3.305 ns |  0.1540 ns | 0.0400 ns |  0.16 |    0.00 |         - |          NA |
| VectorMask         | 64     | False   |   6.248 ns |  0.1326 ns | 0.0205 ns |  0.30 |    0.00 |         - |          NA |
| NarrowedVectorMask | 64     | False   |   4.559 ns |  0.0983 ns | 0.0255 ns |  0.22 |    0.00 |         - |          NA |
|                    |        |         |            |            |           |       |         |           |             |
| **Scalar**             | **64**     | **True**    |  **21.132 ns** |  **2.2982 ns** | **0.3557 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SearchValues       | 64     | True    |   3.266 ns |  0.0295 ns | 0.0046 ns |  0.15 |    0.00 |         - |          NA |
| VectorMask         | 64     | True    |   6.239 ns |  0.0591 ns | 0.0153 ns |  0.30 |    0.00 |         - |          NA |
| NarrowedVectorMask | 64     | True    |   4.530 ns |  0.0233 ns | 0.0036 ns |  0.21 |    0.00 |         - |          NA |
|                    |        |         |            |            |           |       |         |           |             |
| **Scalar**             | **1024**   | **False**   | **336.970 ns** | **30.0776 ns** | **7.8111 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SearchValues       | 1024   | False   |  29.102 ns |  2.1287 ns | 0.5528 ns |  0.09 |    0.00 |         - |          NA |
| VectorMask         | 1024   | False   |  94.242 ns |  0.9600 ns | 0.1486 ns |  0.28 |    0.01 |         - |          NA |
| NarrowedVectorMask | 1024   | False   |  67.681 ns |  1.2873 ns | 0.3343 ns |  0.20 |    0.00 |         - |          NA |
|                    |        |         |            |            |           |       |         |           |             |
| **Scalar**             | **1024**   | **True**    | **325.664 ns** | **32.0963 ns** | **4.9669 ns** |  **1.00** |    **0.00** |         **-** |          **NA** |
| SearchValues       | 1024   | True    |  28.652 ns |  0.3837 ns | 0.0996 ns |  0.09 |    0.00 |         - |          NA |
| VectorMask         | 1024   | True    |  93.904 ns |  0.9298 ns | 0.2415 ns |  0.29 |    0.00 |         - |          NA |
| NarrowedVectorMask | 1024   | True    |  67.173 ns |  1.4861 ns | 0.2300 ns |  0.21 |    0.00 |         - |          NA |
