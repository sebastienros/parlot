```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method  | Length | NewLines | Mean         | Error      | StdDev     | Allocated |
|-------- |------- |--------- |-------------:|-----------:|-----------:|----------:|
| **Advance** | **1**      | **False**    |     **1.250 ns** |  **0.0159 ns** |  **0.0071 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **1.279 ns** |  **0.0577 ns** |  **0.0302 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **3.006 ns** |  **0.2809 ns** |  **0.1469 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **3.435 ns** |  **0.1047 ns** |  **0.0465 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **10.754 ns** |  **0.3000 ns** |  **0.1332 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **11.490 ns** |  **0.3197 ns** |  **0.1672 ns** |         **-** |
| **Advance** | **256**    | **False**    |    **11.683 ns** |  **0.1615 ns** |  **0.0845 ns** |         **-** |
| **Advance** | **256**    | **True**     |   **101.069 ns** |  **9.1236 ns** |  **4.0509 ns** |         **-** |
| **Advance** | **4096**   | **False**    |   **179.426 ns** |  **0.9784 ns** |  **0.5117 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **1,735.426 ns** | **57.8744 ns** | **25.6966 ns** |         **-** |
