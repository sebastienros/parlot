```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method  | Length | NewLines | Mean         | Error         | StdDev      | Allocated |
|-------- |------- |--------- |-------------:|--------------:|------------:|----------:|
| **Advance** | **1**      | **False**    |     **1.149 ns** |     **0.0619 ns** |   **0.0221 ns** |         **-** |
| **Advance** | **1**      | **True**     |     **1.213 ns** |     **0.1189 ns** |   **0.0622 ns** |         **-** |
| **Advance** | **8**      | **False**    |     **5.993 ns** |     **0.1699 ns** |   **0.0754 ns** |         **-** |
| **Advance** | **8**      | **True**     |     **7.963 ns** |     **0.5198 ns** |   **0.2719 ns** |         **-** |
| **Advance** | **32**     | **False**    |    **21.023 ns** |     **0.6376 ns** |   **0.2831 ns** |         **-** |
| **Advance** | **32**     | **True**     |    **28.334 ns** |     **4.9356 ns** |   **2.1915 ns** |         **-** |
| **Advance** | **256**    | **False**    |   **167.749 ns** |     **3.0789 ns** |   **1.6103 ns** |         **-** |
| **Advance** | **256**    | **True**     |   **283.932 ns** |    **74.0585 ns** |  **38.7340 ns** |         **-** |
| **Advance** | **4096**   | **False**    | **2,613.499 ns** |    **20.6481 ns** |   **9.1679 ns** |         **-** |
| **Advance** | **4096**   | **True**     | **5,416.112 ns** | **1,444.6703 ns** | **755.5905 ns** |         **-** |
