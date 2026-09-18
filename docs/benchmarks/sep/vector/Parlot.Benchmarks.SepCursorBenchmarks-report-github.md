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
| **Advance** | **1**      | **False**    |     **1.732 ns** |  **0.0929 ns** |  **0.0486 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **1.791 ns** |  **0.1018 ns** |  **0.0452 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **3.709 ns** |  **0.2859 ns** |  **0.1495 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **4.466 ns** |  **0.2424 ns** |  **0.1268 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **20.844 ns** |  **1.5457 ns** |  **0.8084 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **13.979 ns** |  **0.9140 ns** |  **0.4058 ns** |         **-** |
| **Advance** | **256**    | **False**    |    **19.391 ns** |  **0.2535 ns** |  **0.1326 ns** |         **-** |
| **Advance** | **256**    | **True**     |    **97.954 ns** |  **2.5576 ns** |  **1.3377 ns** |         **-** |
| **Advance** | **4096**   | **False**    |   **197.632 ns** |  **1.6522 ns** |  **0.8641 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **1,799.456 ns** | **23.5729 ns** | **12.3291 ns** |         **-** |
