```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 4.49 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.305 μs** | **0.1045 μs** | **0.0271 μs** | **2.1199** | **0.0731** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.150 μs | 0.0838 μs | 0.0218 μs | 2.2889 | 0.0636 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.722 μs | 0.1238 μs | 0.0321 μs | 0.6218 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.496 μs | 0.1700 μs | 0.0441 μs | 0.2942 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.139 μs** | **0.1371 μs** | **0.0212 μs** | **2.1831** | **0.0809** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.307 μs | 0.5042 μs | 0.1309 μs | 2.2936 | 0.0717 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.895 μs | 0.0843 μs | 0.0219 μs | 0.6228 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.645 μs | 0.3213 μs | 0.0834 μs | 0.3046 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **14.067 μs** | **0.2240 μs** | **0.0347 μs** | **3.9238** | **0.2803** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 13.089 μs | 0.0736 μs | 0.0191 μs | 4.1929 | 0.3931 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.335 μs | 0.1147 μs | 0.0298 μs | 0.9225 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.639 μs | 0.0527 μs | 0.0082 μs | 0.5804 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **33.632 μs** | **0.3071 μs** | **0.0797 μs** | **5.6818** | **0.6684** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 33.301 μs | 1.9749 μs | 0.5129 μs | 5.8901 | 0.6545 |  50.31 KB |
| SqlRuntime    | 256         | True    |  5.148 μs | 0.0554 μs | 0.0144 μs | 1.0776 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  5.402 μs | 0.0909 μs | 0.0141 μs | 0.7595 |      - |   6.48 KB |
