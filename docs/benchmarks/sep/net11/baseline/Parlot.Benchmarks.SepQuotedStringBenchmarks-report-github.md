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
| Method | Length | Unicode | Mean       | Error      | StdDev    | Allocated |
|------- |------- |-------- |-----------:|-----------:|----------:|----------:|
| **Read**   | **0**      | **False**   |   **5.220 ns** |  **0.0697 ns** | **0.0181 ns** |         **-** |
| **Read**   | **0**      | **True**    |   **5.193 ns** |  **0.0618 ns** | **0.0160 ns** |         **-** |
| **Read**   | **6**      | **False**   |   **9.728 ns** |  **0.1053 ns** | **0.0163 ns** |         **-** |
| **Read**   | **6**      | **True**    |   **9.786 ns** |  **0.3600 ns** | **0.0935 ns** |         **-** |
| **Read**   | **8**      | **False**   |  **11.244 ns** |  **0.3258 ns** | **0.0504 ns** |         **-** |
| **Read**   | **8**      | **True**    |  **11.615 ns** |  **0.2168 ns** | **0.0563 ns** |         **-** |
| **Read**   | **16**     | **False**   |  **16.698 ns** |  **0.0869 ns** | **0.0134 ns** |         **-** |
| **Read**   | **16**     | **True**    |  **16.663 ns** |  **0.3854 ns** | **0.0596 ns** |         **-** |
| **Read**   | **128**    | **False**   |  **97.699 ns** |  **5.9757 ns** | **0.9248 ns** |         **-** |
| **Read**   | **128**    | **True**    |  **97.341 ns** |  **3.9790 ns** | **1.0333 ns** |         **-** |
| **Read**   | **1024**   | **False**   | **703.683 ns** |  **3.5123 ns** | **0.5435 ns** |         **-** |
| **Read**   | **1024**   | **True**    | **709.804 ns** | **12.6331 ns** | **3.2808 ns** |         **-** |
