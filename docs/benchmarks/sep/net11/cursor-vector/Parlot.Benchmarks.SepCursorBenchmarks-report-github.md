```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 2.25 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method  | Length | NewLines | Mean         | Error         | StdDev      | Allocated |
|-------- |------- |--------- |-------------:|--------------:|------------:|----------:|
| **Advance** | **1**      | **False**    |     **2.739 ns** |     **0.0314 ns** |   **0.0049 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **2.715 ns** |     **0.0546 ns** |   **0.0142 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **7.569 ns** |     **0.1623 ns** |   **0.0251 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **9.074 ns** |     **0.5773 ns** |   **0.1499 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **23.590 ns** |     **0.9443 ns** |   **0.2452 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **33.376 ns** |    **17.2709 ns** |   **4.4852 ns** |         **-** |
| **Advance** | **256**    | **False**    |    **12.835 ns** |     **0.4549 ns** |   **0.0704 ns** |         **-** |
| **Advance** | **256**    | **True**     |   **264.064 ns** |    **96.1472 ns** |  **14.8789 ns** |         **-** |
| **Advance** | **4096**   | **False**    |   **186.246 ns** |     **5.2849 ns** |   **1.3725 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **4,751.021 ns** | **1,616.8462 ns** | **419.8898 ns** |         **-** |
