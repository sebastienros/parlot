```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 0.61 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method      | Distinct | Length | Escaped | Mean       | Error      | StdDev     | Gen0   | Allocated |
|------------ |--------- |------- |-------- |-----------:|-----------:|-----------:|-------:|----------:|
| **Materialize** | **1**        | **16**     | **False**   |   **5.372 ns** |  **0.0611 ns** |  **0.0095 ns** |      **-** |         **-** |
| **Materialize** | **1**        | **16**     | **True**    |  **22.182 ns** |  **1.6348 ns** |  **0.2530 ns** |      **-** |         **-** |
| **Materialize** | **1**        | **256**    | **False**   |  **29.971 ns** |  **2.4444 ns** |  **0.6348 ns** |      **-** |         **-** |
| **Materialize** | **1**        | **256**    | **True**    | **233.864 ns** |  **3.9187 ns** |  **0.6064 ns** | **0.0628** |     **536 B** |
| **Materialize** | **1**        | **1024**   | **False**   |  **80.969 ns** |  **2.9611 ns** |  **0.7690 ns** | **0.2474** |    **2072 B** |
| **Materialize** | **1**        | **1024**   | **True**    | **905.233 ns** | **10.0805 ns** |  **2.6179 ns** | **0.2441** |    **2072 B** |
| **Materialize** | **8192**     | **16**     | **False**   |  **10.961 ns** |  **0.1354 ns** |  **0.0352 ns** | **0.0057** |      **48 B** |
| **Materialize** | **8192**     | **16**     | **True**    |  **27.375 ns** |  **1.5989 ns** |  **0.2474 ns** | **0.0057** |      **48 B** |
| **Materialize** | **8192**     | **256**    | **False**   |  **46.843 ns** |  **2.9854 ns** |  **0.7753 ns** | **0.0553** |     **464 B** |
| **Materialize** | **8192**     | **256**    | **True**    | **233.481 ns** |  **2.1375 ns** |  **0.5551 ns** | **0.0622** |     **536 B** |
| **Materialize** | **8192**     | **1024**   | **False**   |  **92.331 ns** |  **3.8804 ns** |  **1.0077 ns** | **0.2469** |    **2072 B** |
| **Materialize** | **8192**     | **1024**   | **True**    | **933.424 ns** | **71.9306 ns** | **11.1313 ns** | **0.2441** |    **2072 B** |
