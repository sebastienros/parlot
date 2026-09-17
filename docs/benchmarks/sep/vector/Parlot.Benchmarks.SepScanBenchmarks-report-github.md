```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method             | Length | Unicode | Mean        | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------- |------- |-------- |------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| **Scalar**             | **8**      | **False**   |   **4.3321 ns** |  **0.3454 ns** |  **0.1534 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| SearchValues       | 8      | False   |   1.6201 ns |  0.0564 ns |  0.0295 ns |  0.37 |    0.01 |         - |          NA |
| VectorMask         | 8      | False   |   0.7531 ns |  0.0137 ns |  0.0061 ns |  0.17 |    0.01 |         - |          NA |
| NarrowedVectorMask | 8      | False   |   4.7381 ns |  0.2093 ns |  0.0929 ns |  1.09 |    0.04 |         - |          NA |
|                    |        |         |             |            |            |       |         |           |             |
| **Scalar**             | **8**      | **True**    |   **3.9375 ns** |  **0.2861 ns** |  **0.1496 ns** |  **1.00** |    **0.05** |         **-** |          **NA** |
| SearchValues       | 8      | True    |   1.7696 ns |  0.2469 ns |  0.1292 ns |  0.45 |    0.04 |         - |          NA |
| VectorMask         | 8      | True    |   0.7353 ns |  0.0281 ns |  0.0125 ns |  0.19 |    0.01 |         - |          NA |
| NarrowedVectorMask | 8      | True    |   4.5326 ns |  0.0707 ns |  0.0370 ns |  1.15 |    0.04 |         - |          NA |
|                    |        |         |             |            |            |       |         |           |             |
| **Scalar**             | **64**     | **False**   |  **24.6434 ns** |  **0.4234 ns** |  **0.2214 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| SearchValues       | 64     | False   |   3.0048 ns |  0.1498 ns |  0.0784 ns |  0.12 |    0.00 |         - |          NA |
| VectorMask         | 64     | False   |   5.1879 ns |  0.1000 ns |  0.0357 ns |  0.21 |    0.00 |         - |          NA |
| NarrowedVectorMask | 64     | False   |   3.4259 ns |  0.1219 ns |  0.0541 ns |  0.14 |    0.00 |         - |          NA |
|                    |        |         |             |            |            |       |         |           |             |
| **Scalar**             | **64**     | **True**    |  **23.0456 ns** |  **1.7125 ns** |  **0.6107 ns** |  **1.00** |    **0.04** |         **-** |          **NA** |
| SearchValues       | 64     | True    |   2.9158 ns |  0.0958 ns |  0.0501 ns |  0.13 |    0.00 |         - |          NA |
| VectorMask         | 64     | True    |   5.1005 ns |  0.1095 ns |  0.0486 ns |  0.22 |    0.01 |         - |          NA |
| NarrowedVectorMask | 64     | True    |   3.4353 ns |  0.2707 ns |  0.1416 ns |  0.15 |    0.01 |         - |          NA |
|                    |        |         |             |            |            |       |         |           |             |
| **Scalar**             | **1024**   | **False**   | **404.6465 ns** | **72.7110 ns** | **38.0293 ns** |  **1.01** |    **0.12** |         **-** |          **NA** |
| SearchValues       | 1024   | False   |  33.9842 ns |  3.7082 ns |  1.9395 ns |  0.08 |    0.01 |         - |          NA |
| VectorMask         | 1024   | False   | 100.2817 ns |  2.1441 ns |  0.7646 ns |  0.25 |    0.02 |         - |          NA |
| NarrowedVectorMask | 1024   | False   |  69.5600 ns |  2.5695 ns |  1.1409 ns |  0.17 |    0.01 |         - |          NA |
|                    |        |         |             |            |            |       |         |           |             |
| **Scalar**             | **1024**   | **True**    | **356.2749 ns** |  **5.3267 ns** |  **2.7860 ns** |  **1.00** |    **0.01** |         **-** |          **NA** |
| SearchValues       | 1024   | True    |  33.3853 ns |  1.6173 ns |  0.7181 ns |  0.09 |    0.00 |         - |          NA |
| VectorMask         | 1024   | True    |  99.7316 ns |  3.8378 ns |  1.7040 ns |  0.28 |    0.00 |         - |          NA |
| NarrowedVectorMask | 1024   | True    |  68.9123 ns |  1.9924 ns |  0.8847 ns |  0.19 |    0.00 |         - |          NA |
