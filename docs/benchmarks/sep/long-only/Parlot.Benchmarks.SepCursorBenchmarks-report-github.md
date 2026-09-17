```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method  | Length | NewLines | Mean         | Error         | StdDev      | Allocated |
|-------- |------- |--------- |-------------:|--------------:|------------:|----------:|
| **Advance** | **1**      | **False**    |     **1.446 ns** |     **0.0248 ns** |   **0.0110 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **1.439 ns** |     **0.0191 ns** |   **0.0085 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **6.127 ns** |     **0.1238 ns** |   **0.0549 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **8.009 ns** |     **0.8509 ns** |   **0.3778 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **21.131 ns** |     **0.0530 ns** |   **0.0277 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **30.433 ns** |     **6.8972 ns** |   **3.6074 ns** |         **-** |
| **Advance** | **256**    | **False**    |    **11.750 ns** |     **0.1693 ns** |   **0.0752 ns** |         **-** |
| **Advance** | **256**    | **True**     |   **302.407 ns** |    **91.9438 ns** |  **48.0884 ns** |         **-** |
| **Advance** | **4096**   | **False**    |   **179.971 ns** |     **0.6765 ns** |   **0.3538 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **5,546.413 ns** | **1,838.5855 ns** | **961.6158 ns** |         **-** |
