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
| Method  | Length | EscapeInterval | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------- |------- |--------------- |----------:|----------:|----------:|------:|-------:|----------:|------------:|
| **Current** | **32**     | **4**              |  **31.06 ns** |  **1.808 ns** |  **0.470 ns** |  **1.00** | **0.0104** |      **88 B** |        **1.00** |
|         |        |                |           |           |           |       |        |           |             |
| **Current** | **32**     | **128**            |  **28.65 ns** |  **1.293 ns** |  **0.336 ns** |  **1.00** | **0.0105** |      **88 B** |        **1.00** |
|         |        |                |           |           |           |       |        |           |             |
| **Current** | **1024**   | **4**              | **884.36 ns** | **74.967 ns** | **19.469 ns** |  **1.00** | **0.2443** |    **2072 B** |        **1.00** |
|         |        |                |           |           |           |       |        |           |             |
| **Current** | **1024**   | **128**            | **854.75 ns** | **15.023 ns** |  **2.325 ns** |  **1.00** | **0.2394** |    **2072 B** |        **1.00** |
