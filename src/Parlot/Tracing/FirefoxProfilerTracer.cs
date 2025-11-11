using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Parlot.Tracing;

/// <summary>
/// Parser tracer that collects execution traces compatible with Firefox Profiler format.
/// </summary>
public sealed class FirefoxProfilerTracer : IParserTracer
{
    private readonly TracingOptions _options;
    private readonly List<TraceEvent> _events = new();
    private readonly Stack<StackFrame> _stack = new();
    private readonly long _startTimestamp;
    private readonly double _timestampFrequency;

    internal sealed class TraceEvent
    {
        public long Timestamp { get; set; }
        public string ParserName { get; set; } = string.Empty;
        public int Depth { get; set; }
        public bool IsEnter { get; set; }
        public bool Success { get; set; }
        public string Preview { get; set; } = string.Empty;
        public int InputOffset { get; set; }
    }

    internal sealed class StackFrame
    {
        public string ParserName { get; set; } = string.Empty;
        public long StartTimestamp { get; set; }
        public int StartOffset { get; set; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FirefoxProfilerTracer"/> class.
    /// </summary>
    /// <param name="options">Tracing options.</param>
    public FirefoxProfilerTracer(TracingOptions? options = null)
    {
        _options = options ?? new TracingOptions();
        _startTimestamp = Stopwatch.GetTimestamp();
        _timestampFrequency = Stopwatch.Frequency / 1000.0; // Convert to milliseconds
    }

    /// <summary>
    /// Gets the collected trace events.
    /// </summary>
    internal IReadOnlyList<TraceEvent> Events => _events;

    /// <inheritdoc/>
    public void EnterParser(object parser, Fluent.ParseContext context)
    {
        var parserName = GetParserName(parser);
        var timestamp = Stopwatch.GetTimestamp();
        var offset = context.Scanner.Cursor.Position.Offset;
        var preview = GetInputPreview(context, offset);

        var evt = new TraceEvent
        {
            Timestamp = timestamp,
            ParserName = parserName,
            Depth = _stack.Count,
            IsEnter = true,
            Preview = preview,
            InputOffset = offset
        };

        _events.Add(evt);

        _stack.Push(new StackFrame
        {
            ParserName = parserName,
            StartTimestamp = timestamp,
            StartOffset = offset
        });
    }

    /// <inheritdoc/>
    public void ExitParser(object parser, Fluent.ParseContext context, bool success)
    {
        if (_stack.Count == 0)
        {
            // Defensive: shouldn't happen but guard against mismatched calls
            return;
        }

        var frame = _stack.Pop();
        var timestamp = Stopwatch.GetTimestamp();
        var offset = context.Scanner.Cursor.Position.Offset;
        var preview = GetInputPreview(context, frame.StartOffset);

        var evt = new TraceEvent
        {
            Timestamp = timestamp,
            ParserName = frame.ParserName,
            Depth = _stack.Count,
            IsEnter = false,
            Success = success,
            Preview = preview,
            InputOffset = offset
        };

        _events.Add(evt);
    }

    /// <summary>
    /// Called when a parser exits (backward compatibility - infers success from position change).
    /// </summary>
    public void ExitParserLegacy(object parser, Fluent.ParseContext context)
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var frame = _stack.Peek();
        var currentOffset = context.Scanner.Cursor.Position.Offset;
        
        // Infer success: if the cursor moved forward, likely successful
        // This is a heuristic since some parsers may succeed without advancing
        var success = currentOffset > frame.StartOffset;
        
        ExitParser(parser, context, success);
    }

    private static string GetParserName(object parser)
    {
        // Try to get a useful name from the parser using reflection
        // The Name property is on Parser<T> but we don't know T at runtime
        var nameProperty = parser.GetType().GetProperty("Name");
        if (nameProperty != null)
        {
            var name = nameProperty.GetValue(parser) as string;
            if (!string.IsNullOrEmpty(name))
            {
                return name!;
            }
        }

        var type = parser.GetType();
        var typeName = type.Name;

        // Clean up generic type names
        var backtickIndex = typeName.IndexOf('`');
        if (backtickIndex >= 0)
        {
            typeName = typeName.Substring(0, backtickIndex);
        }

        return typeName ?? "Unknown";
    }

    private string GetInputPreview(Fluent.ParseContext context, int offset)
    {
        var scanner = context.Scanner;
        var currentOffset = scanner.Cursor.Position.Offset;
        
        // Save current position
        var savedPosition = scanner.Cursor.Position;
        
        try
        {
            // Move to the desired offset
            scanner.Cursor.ResetPosition(new TextPosition(offset, 0, 0));
            
            var remaining = scanner.Cursor.Buffer.Length - offset;
            var length = Math.Min(_options.PreviewLength, remaining);
            
            if (length <= 0)
            {
                return string.Empty;
            }

            var chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = scanner.Cursor.Current;
                scanner.Cursor.Advance();
            }

            // Escape special characters for readability
            var preview = new string(chars);
            preview = preview.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
            
            return preview;
        }
        finally
        {
            // Restore position
            scanner.Cursor.ResetPosition(savedPosition);
        }
    }

    /// <summary>
    /// Exports the trace data in Firefox Profiler JSON format.
    /// </summary>
    /// <returns>JSON string compatible with Firefox Profiler.</returns>
    public string ExportFirefoxProfiler()
    {
        return FirefoxProfilerExporter.Export(this, _startTimestamp, _timestampFrequency);
    }
}
