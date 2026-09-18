```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method  | Length | NewLines | Mean         | Error       | StdDev     | Allocated |
|-------- |------- |--------- |-------------:|------------:|-----------:|----------:|
| **Advance** | **1**      | **False**    |     **1.069 ns** |   **0.0393 ns** |  **0.0175 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **1.091 ns** |   **0.0285 ns** |  **0.0149 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **3.018 ns** |   **0.2113 ns** |  **0.1105 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **3.732 ns** |   **0.1177 ns** |  **0.0616 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **11.535 ns** |   **0.2704 ns** |  **0.1414 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **12.689 ns** |   **0.5548 ns** |  **0.2463 ns** |         **-** |
| **Advance** | **256**    | **False**    |   **117.558 ns** |   **2.3083 ns** |  **1.2073 ns** |         **-** |
| **Advance** | **256**    | **True**     |    **96.276 ns** |   **1.3592 ns** |  **0.6035 ns** |         **-** |
| **Advance** | **4096**   | **False**    | **1,918.427 ns** | **155.3616 ns** | **68.9815 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **1,810.772 ns** |  **40.0234 ns** | **17.7706 ns** |         **-** |
