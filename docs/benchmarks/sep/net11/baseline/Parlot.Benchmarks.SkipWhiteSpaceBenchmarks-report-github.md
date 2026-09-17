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
| Method                              | Length | Mean        | Error     | StdDev    | Allocated |
|------------------------------------ |------- |------------:|----------:|----------:|----------:|
| **SkipWhiteSpace_Default**              | **0**      |   **0.8901 ns** | **0.0121 ns** | **0.0019 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 0      |   1.9666 ns | 0.0041 ns | 0.0011 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 0      |   1.9862 ns | 0.0221 ns | 0.0034 ns |         - |
| **SkipWhiteSpace_Default**              | **1**      |   **2.4990 ns** | **0.0515 ns** | **0.0134 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 1      |   3.5023 ns | 0.1036 ns | 0.0160 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 1      |   3.3919 ns | 0.0835 ns | 0.0217 ns |         - |
| **SkipWhiteSpace_Default**              | **2**      |   **2.7858 ns** | **0.1557 ns** | **0.0404 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 2      |   3.9109 ns | 0.2483 ns | 0.0384 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 2      |   3.4066 ns | 0.2419 ns | 0.0628 ns |         - |
| **SkipWhiteSpace_Default**              | **10**     |   **4.4170 ns** | **0.1573 ns** | **0.0409 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 10     |   3.2341 ns | 0.0447 ns | 0.0116 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 10     |   3.9103 ns | 0.3570 ns | 0.0927 ns |         - |
| **SkipWhiteSpace_Default**              | **256**    | **129.1693 ns** | **2.0556 ns** | **0.5338 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 256    |  11.4179 ns | 0.2042 ns | 0.0530 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 256    |  20.7356 ns | 0.1687 ns | 0.0261 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **0**      |   **1.0831 ns** | **0.0477 ns** | **0.0124 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 0      |   2.0154 ns | 0.1610 ns | 0.0418 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **1**      |   **3.8288 ns** | **0.1230 ns** | **0.0320 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 1      |   3.9666 ns | 0.1801 ns | 0.0279 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **2**      |   **4.2982 ns** | **0.2523 ns** | **0.0655 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 2      |   4.9535 ns | 0.2524 ns | 0.0655 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **10**     |  **10.0633 ns** | **0.5450 ns** | **0.1415 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 10     |  10.1633 ns | 0.5121 ns | 0.1330 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **256**    | **281.5836 ns** | **3.5823 ns** | **0.5544 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 256    | 161.3177 ns | 3.4485 ns | 0.8956 ns |         - |
