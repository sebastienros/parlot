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
| Method  | Length | EscapeInterval | Mean      | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------- |------- |--------------- |----------:|---------:|---------:|------:|-------:|----------:|------------:|
| **Current** | **32**     | **4**              |  **36.04 ns** | **0.092 ns** | **0.014 ns** |  **1.00** | **0.0104** |      **88 B** |        **1.00** |
|         |        |                |           |          |          |       |        |           |             |
| **Current** | **32**     | **128**            |  **22.73 ns** | **0.729 ns** | **0.189 ns** |  **1.00** | **0.0104** |      **88 B** |        **1.00** |
|         |        |                |           |          |          |       |        |           |             |
| **Current** | **1024**   | **4**              | **921.07 ns** | **9.079 ns** | **1.405 ns** |  **1.00** | **0.2399** |    **2072 B** |        **1.00** |
|         |        |                |           |          |          |       |        |           |             |
| **Current** | **1024**   | **128**            | **236.13 ns** | **3.385 ns** | **0.524 ns** |  **1.00** | **0.2454** |    **2072 B** |        **1.00** |
