# Parser Tracing with Speedscope Export

Parlot provides built-in tracing capabilities that allow you to record parser execution and export the data to Speedscope format for visualization.

## Overview

The tracing facility hooks into the parser execution pipeline through the `ParseContext.EnterParser` and `ParseContext.ExitParser` callbacks. It records:

- Parser call hierarchy (stack frames)
- Timing information (using high-resolution timestamps)
- Success/failure status for each parser invocation
- Input text previews at each parse point

The collected trace data can be exported to JSON format compatible with [Speedscope](https://www.speedscope.app/), enabling flame graph visualization and detailed performance analysis.

## Quick Start

```csharp
using Parlot.Fluent;
using Parlot.Tracing;
using static Parlot.Fluent.Parsers;

// Create your parser
var parser = Terms.Integer().And(Terms.Text(",")).And(Terms.Integer());

// Create a parse context
var context = new ParseContext(new Scanner("123,456"));

// Enable tracing for this parse operation
using (var tracing = ParserTracing.Start(context))
{
    // Parse as normal
    var success = parser.TryParse(context, out var result, out var error);
    
    // Export trace data to Speedscope JSON format
    var json = tracing.GetSpeedscopeJson();
    
    // Save to file or analyze programmatically
    File.WriteAllText("trace.json", json);
}
```

## Viewing Traces

1. Save the exported JSON to a file (e.g., `trace.json`)
2. Navigate to https://www.speedscope.app/
3. Drag and drop your trace file onto the page, or click to browse
4. Explore the flame graph visualization showing parser execution hierarchy and timing

### Alternative: Firefox Profiler

The library also supports Firefox Profiler format (marked as obsolete due to compatibility issues):

```csharp
var json = tracing.GetFirefoxProfilerJson(); // Note: Marked as obsolete
File.WriteAllText("trace.json", json);
// Load at https://profiler.firefox.com/
```

## Configuration Options

The `TracingOptions` class allows you to customize tracing behavior:

```csharp
var options = new TracingOptions
{
    // Number of characters to preview from input at each trace point (default: 15)
    PreviewLength = 20,
    
    // Whether to include success/failure markers in traces (default: true)
    IncludeSuccess = true
};

using var tracing = ParserTracing.Start(context, options);
```

### Preview Length

The `PreviewLength` option controls how many characters of input text are captured at each parser entry/exit point. This helps you understand what portion of the input each parser was processing.

```csharp
// Shorter preview for compact traces
var shortOptions = new TracingOptions { PreviewLength = 5 };

// Longer preview for more context
var longOptions = new TracingOptions { PreviewLength = 30 };
```

## Understanding Trace Output

### Parser Names

Parsers appear in traces using either:
- Their explicitly set name (via `.Named("CustomName")`)
- Their type name (e.g., `NumberLiteral`, `TextLiteral`)

Example:
```csharp
var parser = Terms.Integer().Named("MyNumber");
// Will appear as "MyNumber" in the trace
```

### Success/Failure Indicators

Each parser invocation is marked with:
- ✓ (checkmark) for successful parses
- ✗ (x mark) for failed parses

These markers appear alongside the input preview in the trace:
```
MyNumber [✓] "123"     // Successful parse
TextLiteral [✗] "abc"  // Failed parse
```

### Hierarchical Structure

The flame graph shows parser invocations hierarchically:
- Serially executed parsers (e.g., in `And` sequences) appear on the same timeline level
- Nested parser calls appear as sub-frames below their parent

## Performance Considerations

### Low Overhead When Disabled

When no tracer is attached to a `ParseContext`, the overhead is minimal - just a null check on each parser entry/exit. The JIT can inline these checks effectively.

### Tracing Overhead

When tracing is enabled:
- Each parser call records timestamp, position, and preview data
- String operations for preview generation add some cost
- Memory is allocated for event storage

For production use, only enable tracing when needed for debugging or profiling.

## API Reference

### ParserTracing Class

Static helper class for starting tracing sessions.

#### Methods

- `Start(ParseContext context, TracingOptions? options = null)`: Starts tracing on the given context and returns a disposable `TracingScope`.

### TracingScope Class

Represents an active tracing session. Implements `IDisposable`.

#### Methods

- `GetSpeedscopeJson()`: Exports collected trace data as Speedscope JSON.
- `Dispose()`: Stops tracing and removes the tracer from the context.

### TracingOptions Class

Configuration for tracing behavior.

#### Properties

- `PreviewLength` (int): Number of characters to preview from input (default: 15)
- `IncludeSuccess` (bool): Whether to include success/failure markers (default: true)

### IParserTracer Interface

Low-level interface for custom tracer implementations.

#### Methods

- `EnterParser(object parser, ParseContext context)`: Called when a parser is entered
- `ExitParser(object parser, ParseContext context, bool success)`: Called when a parser exits
- `ExitParserLegacy(object parser, ParseContext context)`: Backward compatibility method that infers success

## Advanced Usage

### Custom Tracers

You can implement custom tracing behavior by creating a class that implements `IParserTracer`:

```csharp
public class MyCustomTracer : IParserTracer
{
    public void EnterParser(object parser, ParseContext context)
    {
        // Your custom enter logic
    }
    
    public void ExitParser(object parser, ParseContext context, bool success)
    {
        // Your custom exit logic
    }
    
    public void ExitParserLegacy(object parser, ParseContext context)
    {
        // Infer success from context if needed
        var success = /* your logic */;
        ExitParser(parser, context, success);
    }
}

// Attach to context
var context = new ParseContext(new Scanner("input"));
context.Tracer = new MyCustomTracer();
```

### Analyzing Traces Programmatically

The exported JSON follows the Speedscope schema. You can parse it to extract information:

```csharp
using System.Text.Json;

var json = tracing.GetSpeedscopeJson();
using var doc = JsonDocument.Parse(json);

var thread = doc.RootElement.GetProperty("threads")[0];
var stringTable = thread.GetProperty("stringTable");
var markers = thread.GetProperty("markers");

// Analyze parser names, timings, etc.
```

## Examples

### Debugging Parse Failures

```csharp
var parser = BuildComplexGrammar();
var context = new ParseContext(new Scanner(input));

using (var tracing = ParserTracing.Start(context))
{
    var success = parser.TryParse(context, out var result, out var error);
    
    if (!success)
    {
        // Export trace to see where parsing failed
        var json = tracing.GetSpeedscopeJson();
        File.WriteAllText($"failed-parse-{DateTime.Now:yyyyMMdd-HHmmss}.json", json);
        
        Console.WriteLine($"Parse failed. Trace saved. Error: {error}");
    }
}
```

### Performance Profiling

```csharp
var parser = MyGrammar.Parser;
var inputs = LoadTestInputs();

foreach (var input in inputs)
{
    var context = new ParseContext(new Scanner(input));
    
    using (var tracing = ParserTracing.Start(context))
    {
        parser.TryParse(context, out _, out _);
        
        var json = tracing.GetSpeedscopeJson();
        File.WriteAllText($"trace-{input.Name}.json", json);
    }
}

// Analyze traces to find performance hotspots
```

## Compatibility

- The tracer is compatible with all parser types in Parlot
- Works with compiled and non-compiled parsers
- No changes needed to existing parser code
- Backward compatible with existing `OnEnterParser`/`OnExitParser` callbacks

## Speedscope Schema

The exported JSON conforms to the Speedscope format with the following structure:

- **meta**: Version and product information
- **threads**: Array containing a single thread with parser execution data
  - **stringTable**: All string values referenced by the profile
  - **frameTable**: Function/parser frame definitions
  - **stackTable**: Call stack information
  - **samples**: Time-based samples for flame graph rendering
  - **markers**: Variable-duration events with parser invocation details

For the complete schema specification, see https://profiler.firefox.com/docs/
