```

BenchmarkDotNet v0.16.0-preview.1, macOS Sequoia 15.7.9 (24G830) [Darwin 24.6.0]
Apple M4 Pro, 1 CPU, 14 logical and 14 physical cores
Memory: 48 GB Total, 2.56 GB Available
.NET SDK 11.0.100-rc.1.26413.103
  [Host]   : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a
  ShortRun : .NET 11.0.0 (11.0.0-rc.1.26413.103, 11.0.26.41403), Arm64 RyuJIT armv8.0-a

Job=ShortRun  IterationCount=5  IterationTime=100ms  
LaunchCount=1  WarmupCount=3  

```
| Method        | TokenLength | Escaped | Mean      | Error     | StdDev    | Gen0   | Gen1   | Allocated |
|-------------- |------------ |-------- |----------:|----------:|----------:|-------:|-------:|----------:|
| **JsonRuntime**   | **8**           | **False**   |  **7.106 μs** | **0.0780 μs** | **0.0121 μs** | **2.1115** | **0.0704** |  **17.45 KB** |
| JsonGenerated | 8           | False   |  6.219 μs | 0.2781 μs | 0.0722 μs | 2.2819 | 0.0634 |  18.81 KB |
| SqlRuntime    | 8           | False   |  2.653 μs | 0.0998 μs | 0.0259 μs | 0.6172 |      - |   5.19 KB |
| SqlGenerated  | 8           | False   |  2.600 μs | 0.4011 μs | 0.0621 μs | 0.3028 |      - |   2.56 KB |
| **JsonRuntime**   | **8**           | **True**    |  **8.089 μs** | **0.1678 μs** | **0.0436 μs** | **2.1662** | **0.0802** |  **17.95 KB** |
| JsonGenerated | 8           | True    |  7.140 μs | 0.1681 μs | 0.0260 μs | 2.3571 | 0.0714 |  19.31 KB |
| SqlRuntime    | 8           | True    |  2.730 μs | 0.0699 μs | 0.0182 μs | 0.6277 |      - |   5.23 KB |
| SqlGenerated  | 8           | True    |  2.593 μs | 0.1108 μs | 0.0171 μs | 0.3073 |      - |   2.61 KB |
| **JsonRuntime**   | **256**         | **False**   | **14.110 μs** | **0.7126 μs** | **0.1851 μs** | **3.9150** | **0.2796** |  **32.95 KB** |
| JsonGenerated | 256         | False   | 12.921 μs | 0.1468 μs | 0.0227 μs | 4.1408 | 0.3882 |  34.31 KB |
| SqlRuntime    | 256         | False   |  3.366 μs | 0.0913 μs | 0.0237 μs | 0.9053 |      - |   7.61 KB |
| SqlGenerated  | 256         | False   |  3.634 μs | 0.0664 μs | 0.0103 μs | 0.5774 |      - |   4.98 KB |
| **JsonRuntime**   | **256**         | **True**    | **23.769 μs** | **0.4565 μs** | **0.0706 μs** | **5.9411** | **0.7129** |  **48.95 KB** |
| JsonGenerated | 256         | True    | 22.310 μs | 1.5239 μs | 0.3957 μs | 6.1404 | 0.8772 |  50.31 KB |
| SqlRuntime    | 256         | True    |  4.246 μs | 0.3556 μs | 0.0550 μs | 1.0746 |      - |   9.11 KB |
| SqlGenerated  | 256         | True    |  4.610 μs | 0.3299 μs | 0.0510 μs | 0.7818 |      - |   6.48 KB |
