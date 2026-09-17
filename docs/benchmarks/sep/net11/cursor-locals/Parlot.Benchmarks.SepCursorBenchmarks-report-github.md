```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 1.02 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method  | Length | NewLines | Mean         | Error       | StdDev     | Allocated |
|-------- |------- |--------- |-------------:|------------:|-----------:|----------:|
| **Advance** | **1**      | **False**    |     **2.151 ns** |   **0.0720 ns** |  **0.0187 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **2.021 ns** |   **0.0537 ns** |  **0.0139 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **4.006 ns** |   **0.7696 ns** |  **0.1999 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **5.862 ns** |   **1.3567 ns** |  **0.3523 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **13.925 ns** |   **2.4770 ns** |  **0.6433 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **13.006 ns** |   **0.1829 ns** |  **0.0283 ns** |         **-** |
| **Advance** | **256**    | **False**    |   **118.149 ns** |  **17.8126 ns** |  **4.6259 ns** |         **-** |
| **Advance** | **256**    | **True**     |   **116.426 ns** |  **14.6915 ns** |  **3.8153 ns** |         **-** |
| **Advance** | **4096**   | **False**    | **1,893.867 ns** | **186.2731 ns** | **48.3745 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **1,804.281 ns** | **178.3167 ns** | **46.3083 ns** |         **-** |
