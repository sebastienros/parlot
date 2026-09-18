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
| Method  | Length | NewLines | Mean         | Error         | StdDev      | Allocated |
|-------- |------- |--------- |-------------:|--------------:|------------:|----------:|
| **Advance** | **1**      | **False**    |     **2.201 ns** |     **0.0769 ns** |   **0.0200 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **2.201 ns** |     **0.1061 ns** |   **0.0276 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **6.906 ns** |     **0.3778 ns** |   **0.0981 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **8.225 ns** |     **0.8380 ns** |   **0.2176 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **22.333 ns** |     **0.2257 ns** |   **0.0586 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **34.704 ns** |    **29.1446 ns** |   **7.5688 ns** |         **-** |
| **Advance** | **256**    | **False**    |   **171.110 ns** |    **11.1916 ns** |   **1.7319 ns** |         **-** |
| **Advance** | **256**    | **True**     |   **298.689 ns** |   **111.7862 ns** |  **29.0305 ns** |         **-** |
| **Advance** | **4096**   | **False**    | **2,583.200 ns** |    **12.6312 ns** |   **3.2803 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **6,185.828 ns** | **3,174.6632 ns** | **824.4500 ns** |         **-** |
