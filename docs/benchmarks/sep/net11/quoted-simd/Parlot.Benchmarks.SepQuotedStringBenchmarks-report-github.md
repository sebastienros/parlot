```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 1.14 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method | Length | Unicode | Mean       | Error      | StdDev    | Allocated |
|------- |------- |-------- |-----------:|-----------:|----------:|----------:|
| **Read**   | **0**      | **False**   |   **5.263 ns** |  **0.1718 ns** | **0.0446 ns** |         **-** |
| **Read**   | **0**      | **True**    |   **5.190 ns** |  **0.1189 ns** | **0.0184 ns** |         **-** |
| **Read**   | **6**      | **False**   |   **9.698 ns** |  **0.1103 ns** | **0.0287 ns** |         **-** |
| **Read**   | **6**      | **True**    |   **9.767 ns** |  **0.5851 ns** | **0.1519 ns** |         **-** |
| **Read**   | **8**      | **False**   |  **11.081 ns** |  **0.5377 ns** | **0.1396 ns** |         **-** |
| **Read**   | **8**      | **True**    |  **10.877 ns** |  **0.1483 ns** | **0.0385 ns** |         **-** |
| **Read**   | **16**     | **False**   |  **17.216 ns** |  **0.3631 ns** | **0.0562 ns** |         **-** |
| **Read**   | **16**     | **True**    |  **17.336 ns** |  **0.5524 ns** | **0.0855 ns** |         **-** |
| **Read**   | **128**    | **False**   |  **98.931 ns** |  **2.5943 ns** | **0.4015 ns** |         **-** |
| **Read**   | **128**    | **True**    |  **99.369 ns** |  **3.2087 ns** | **0.8333 ns** |         **-** |
| **Read**   | **1024**   | **False**   | **716.741 ns** | **21.1989 ns** | **5.5053 ns** |         **-** |
| **Read**   | **1024**   | **True**    | **716.761 ns** | **14.0020 ns** | **3.6363 ns** |         **-** |
