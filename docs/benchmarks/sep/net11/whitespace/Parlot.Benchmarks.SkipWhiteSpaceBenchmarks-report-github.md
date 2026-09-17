```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 4.28 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method                          | Length | Mean        | Error     | StdDev    | Allocated |
|-------------------------------- |------- |------------:|----------:|----------:|----------:|
| **SkipWhiteSpace_Default**          | **0**      |   **0.8367 ns** | **0.0132 ns** | **0.0034 ns** |         **-** |
| **SkipWhiteSpace_Default**          | **1**      |   **2.9114 ns** | **0.0285 ns** | **0.0074 ns** |         **-** |
| **SkipWhiteSpace_Default**          | **2**      |   **3.1434 ns** | **0.0307 ns** | **0.0080 ns** |         **-** |
| **SkipWhiteSpace_Default**          | **10**     |   **5.7931 ns** | **0.0291 ns** | **0.0076 ns** |         **-** |
| **SkipWhiteSpace_Default**          | **256**    |  **11.6613 ns** | **0.0764 ns** | **0.0198 ns** |         **-** |
| **SkipWhiteSpaceOrNewLine_Default** | **0**      |   **0.8334 ns** | **0.0065 ns** | **0.0010 ns** |         **-** |
| **SkipWhiteSpaceOrNewLine_Default** | **1**      |   **3.9641 ns** | **0.6677 ns** | **0.1734 ns** |         **-** |
| **SkipWhiteSpaceOrNewLine_Default** | **2**      |   **5.4269 ns** | **1.3496 ns** | **0.3505 ns** |         **-** |
| **SkipWhiteSpaceOrNewLine_Default** | **10**     |  **11.9778 ns** | **0.3647 ns** | **0.0947 ns** |         **-** |
| **SkipWhiteSpaceOrNewLine_Default** | **256**    | **171.9199 ns** | **6.2098 ns** | **0.9610 ns** |         **-** |
