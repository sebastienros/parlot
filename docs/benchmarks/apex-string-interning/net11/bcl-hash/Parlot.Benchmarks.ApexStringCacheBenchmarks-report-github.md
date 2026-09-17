```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 0.16 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method      | Distinct | Length | Escaped | Mean       | Error       | StdDev     | Gen0   | Allocated |
|------------ |--------- |------- |-------- |-----------:|------------:|-----------:|-------:|----------:|
| **Materialize** | **1**        | **16**     | **False**   |   **7.725 ns** |   **0.1888 ns** |  **0.0292 ns** |      **-** |         **-** |
| **Materialize** | **1**        | **16**     | **True**    |  **22.060 ns** |   **0.2429 ns** |  **0.0376 ns** |      **-** |         **-** |
| **Materialize** | **1**        | **256**    | **False**   | **165.427 ns** |   **6.3984 ns** |  **1.6616 ns** |      **-** |         **-** |
| **Materialize** | **1**        | **256**    | **True**    | **257.958 ns** |  **11.3707 ns** |  **2.9529 ns** | **0.0634** |     **536 B** |
| **Materialize** | **1**        | **1024**   | **False**   |  **87.671 ns** |   **6.1365 ns** |  **1.5936 ns** | **0.2476** |    **2072 B** |
| **Materialize** | **1**        | **1024**   | **True**    | **979.710 ns** | **113.9987 ns** | **29.6051 ns** | **0.2441** |    **2072 B** |
| **Materialize** | **8192**     | **16**     | **False**   |  **17.364 ns** |   **5.7435 ns** |  **0.8888 ns** | **0.0057** |      **48 B** |
| **Materialize** | **8192**     | **16**     | **True**    |  **31.376 ns** |   **2.4094 ns** |  **0.6257 ns** | **0.0055** |      **48 B** |
| **Materialize** | **8192**     | **256**    | **False**   | **190.344 ns** |  **19.2269 ns** |  **4.9932 ns** | **0.0543** |     **466 B** |
| **Materialize** | **8192**     | **256**    | **True**    | **238.865 ns** |   **6.3413 ns** |  **1.6468 ns** | **0.0622** |     **536 B** |
| **Materialize** | **8192**     | **1024**   | **False**   |  **96.253 ns** |  **23.8903 ns** |  **3.6971 ns** | **0.2469** |    **2072 B** |
| **Materialize** | **8192**     | **1024**   | **True**    | **974.102 ns** |  **37.7111 ns** |  **9.7934 ns** | **0.2441** |    **2072 B** |
