```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 0.81 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method      | Distinct | Length | Escaped | Mean       | Error      | StdDev    | Gen0   | Allocated |
|------------ |--------- |------- |-------- |-----------:|-----------:|----------:|-------:|----------:|
| **Materialize** | **1**        | **16**     | **False**   |   **5.666 ns** |  **0.2286 ns** | **0.0594 ns** | **0.0066** |      **56 B** |
| **Materialize** | **1**        | **16**     | **True**    |  **21.021 ns** |  **3.9093 ns** | **1.0152 ns** | **0.0066** |      **56 B** |
| **Materialize** | **1**        | **256**    | **False**   |  **24.826 ns** |  **1.8412 ns** | **0.2849 ns** | **0.0639** |     **536 B** |
| **Materialize** | **1**        | **256**    | **True**    | **229.515 ns** |  **9.4133 ns** | **2.4446 ns** | **0.0639** |     **536 B** |
| **Materialize** | **1**        | **1024**   | **False**   |  **80.845 ns** |  **8.1611 ns** | **1.2629 ns** | **0.2473** |    **2072 B** |
| **Materialize** | **1**        | **1024**   | **True**    | **975.225 ns** | **32.0714 ns** | **8.3288 ns** | **0.2441** |    **2072 B** |
| **Materialize** | **8192**     | **16**     | **False**   |   **5.862 ns** |  **0.2031 ns** | **0.0527 ns** | **0.0067** |      **56 B** |
| **Materialize** | **8192**     | **16**     | **True**    |  **21.596 ns** |  **0.8314 ns** | **0.2159 ns** | **0.0065** |      **56 B** |
| **Materialize** | **8192**     | **256**    | **False**   |  **25.522 ns** |  **0.2537 ns** | **0.0393 ns** | **0.0639** |     **536 B** |
| **Materialize** | **8192**     | **256**    | **True**    | **232.145 ns** |  **6.0170 ns** | **0.9311 ns** | **0.0639** |     **536 B** |
| **Materialize** | **8192**     | **1024**   | **False**   |  **90.315 ns** |  **6.5571 ns** | **1.0147 ns** | **0.2468** |    **2072 B** |
| **Materialize** | **8192**     | **1024**   | **True**    | **890.642 ns** | **15.9507 ns** | **2.4684 ns** | **0.2441** |    **2072 B** |
