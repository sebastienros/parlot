```

BenchmarkDotNet v0.15.8, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 10.0.11 (10.0.11, 10.0.1126.37416), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=8  IterationTime=200ms  
LaunchCount=1  WarmupCount=5  

```
| Method                              | Length | Mean        | Error     | StdDev    | Allocated |
|------------------------------------ |------- |------------:|----------:|----------:|----------:|
| **SkipWhiteSpace_Default**              | **0**      |   **0.0000 ns** | **0.0000 ns** | **0.0000 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 0      |   0.7885 ns | 0.0565 ns | 0.0295 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 0      |   1.2245 ns | 0.0232 ns | 0.0121 ns |         - |
| **SkipWhiteSpace_Default**              | **1**      |   **1.0243 ns** | **0.0147 ns** | **0.0065 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 1      |   1.9177 ns | 0.0231 ns | 0.0102 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 1      |   2.0048 ns | 0.0543 ns | 0.0284 ns |         - |
| **SkipWhiteSpace_Default**              | **2**      |   **1.2520 ns** | **0.0306 ns** | **0.0136 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 2      |   2.2278 ns | 0.0137 ns | 0.0071 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 2      |   2.6349 ns | 0.1214 ns | 0.0635 ns |         - |
| **SkipWhiteSpace_Default**              | **10**     |   **3.9984 ns** | **0.1130 ns** | **0.0591 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 10     |   2.0027 ns | 0.0317 ns | 0.0166 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 10     |   2.5004 ns | 0.0383 ns | 0.0201 ns |         - |
| **SkipWhiteSpace_Default**              | **256**    | **133.1368 ns** | **1.2869 ns** | **0.6731 ns** |         **-** |
| SkipWhiteSpace_Vectorized           | 256    |  10.0939 ns | 0.0972 ns | 0.0508 ns |         - |
| SkipWhiteSpace_Vectorized_Optimized | 256    |  19.3659 ns | 0.1159 ns | 0.0514 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **0**      |   **0.0000 ns** | **0.0000 ns** | **0.0000 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 0      |   0.8006 ns | 0.0285 ns | 0.0149 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **1**      |   **1.5752 ns** | **0.0275 ns** | **0.0122 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 1      |   2.3750 ns | 0.0109 ns | 0.0057 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **2**      |   **2.3750 ns** | **0.0357 ns** | **0.0187 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 2      |   3.3416 ns | 0.0112 ns | 0.0050 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **10**     |   **8.5871 ns** | **0.1960 ns** | **0.1025 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 10     |   8.8735 ns | 0.1522 ns | 0.0796 ns |         - |
| **SkipWhiteSpaceOrNewLine_Default**     | **256**    | **296.3170 ns** | **1.2505 ns** | **0.6540 ns** |         **-** |
| SkipWhiteSpaceOrNewLines_Vectorized | 256    | 162.5783 ns | 0.5787 ns | 0.3027 ns |         - |
