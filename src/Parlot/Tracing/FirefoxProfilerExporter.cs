using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Parlot.Tracing;

/// <summary>
/// Exports trace data to Firefox Profiler JSON format.
/// </summary>
internal static class FirefoxProfilerExporter
{
    /// <summary>
    /// Exports tracer data to Firefox Profiler JSON format.
    /// </summary>
    public static string Export(FirefoxProfilerTracer tracer, long startTimestamp, double timestampFrequency)
    {
        var events = tracer.Events;
        if (events.Count == 0)
        {
            return CreateEmptyProfile();
        }

        var stringTable = new List<string>();
        var stackTable = new StackTableBuilder();
        var markers = new List<MarkerData>();
        var samples = new SamplesBuilder();

        // Build string table and track stack frames
        int GetStringIndex(string str)
        {
            var index = stringTable.IndexOf(str);
            if (index < 0)
            {
                index = stringTable.Count;
                stringTable.Add(str);
            }
            return index;
        }

        // Pre-populate some common strings
        GetStringIndex("(root)");
        
        // Build stack frames and markers from events
        var stackMap = new Dictionary<string, int>(); // parserName -> stackIndex
        var rootStackIndex = stackTable.AddFrame(null, GetStringIndex("(root)"), GetStringIndex("(root)"));

        // Process events and build markers
        var activeFrames = new Stack<(int stackIndex, long startTime, string name, int startOffset)>();
        
        foreach (var evt in events)
        {
            if (evt.IsEnter)
            {
                // Create or reuse stack frame
                var key = evt.ParserName;
                var parentStack = activeFrames.Count > 0 ? activeFrames.Peek().stackIndex : rootStackIndex;
                
                if (!stackMap.TryGetValue(key, out var stackIndex))
                {
                    var nameIndex = GetStringIndex(evt.ParserName);
                    stackIndex = stackTable.AddFrame(parentStack, nameIndex, nameIndex);
                    stackMap[key] = stackIndex;
                }
                
                activeFrames.Push((stackIndex, evt.Timestamp, evt.ParserName, evt.InputOffset));
            }
            else
            {
                // Exit - create marker for this parser invocation
                if (activeFrames.Count > 0)
                {
                    var frame = activeFrames.Pop();
                    var duration = (evt.Timestamp - frame.startTime) / timestampFrequency;
                    var startTime = (frame.startTime - startTimestamp) / timestampFrequency;
                    
                    var markerName = $"{frame.name} [{(evt.Success ? "✓" : "✗")}] \"{evt.Preview}\"";
                    
                    markers.Add(new MarkerData
                    {
                        Name = GetStringIndex(markerName),
                        StartTime = startTime,
                        EndTime = startTime + duration,
                        Phase = 0,
                        Category = 0,
                        Data = null
                    });
                    
                    // Add a sample at the end of this frame
                    samples.AddSample(startTime + duration, frame.stackIndex);
                }
            }
        }

        // Build the JSON
        var json = BuildProfileJson(stringTable, stackTable, markers, samples);
        return json;
    }

    private static string CreateEmptyProfile()
    {
        var stringTable = new[] { "(root)" };
        var stackTable = new StackTableBuilder();
        stackTable.AddFrame(null, 0, 0);
        
        return BuildProfileJson(stringTable.ToList(), stackTable, new List<MarkerData>(), new SamplesBuilder());
    }

    private static string BuildProfileJson(
        List<string> stringTable,
        StackTableBuilder stackTable,
        List<MarkerData> markers,
        SamplesBuilder samples)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            
            writer.WriteString("meta", "");
            writer.WritePropertyName("meta");
            writer.WriteStartObject();
            writer.WriteNumber("version", 28);
            writer.WriteString("product", "Parlot Parser Tracer");
            writer.WriteEndObject();
            
            writer.WritePropertyName("libs");
            writer.WriteStartArray();
            writer.WriteEndArray();
            
            writer.WritePropertyName("threads");
            writer.WriteStartArray();
            
            // Single thread
            writer.WriteStartObject();
            writer.WriteString("name", "Parser Thread");
            writer.WriteNumber("processType", 0);
            writer.WriteNumber("pid", 0);
            writer.WriteNumber("tid", 0);
            
            // String table
            writer.WritePropertyName("stringTable");
            writer.WriteStartArray();
            foreach (var str in stringTable)
            {
                writer.WriteStringValue(str);
            }
            writer.WriteEndArray();
            
            // Frame table
            writer.WritePropertyName("frameTable");
            stackTable.WriteJson(writer);
            
            // Stack table
            writer.WritePropertyName("stackTable");
            stackTable.WriteStackTableJson(writer);
            
            // Markers
            writer.WritePropertyName("markers");
            writer.WriteStartObject();
            writer.WritePropertyName("data");
            writer.WriteStartArray();
            foreach (var marker in markers)
            {
                writer.WriteStartArray();
                writer.WriteNumberValue(marker.Name);
                writer.WriteNumberValue(marker.StartTime);
                writer.WriteNumberValue(marker.EndTime);
                writer.WriteNumberValue(marker.Phase);
                writer.WriteNumberValue(marker.Category);
                writer.WriteNullValue(); // data
                writer.WriteEndArray();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("name");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("startTime");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("endTime");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("phase");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("category");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("data");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
            
            // Samples
            writer.WritePropertyName("samples");
            samples.WriteJson(writer);
            
            writer.WriteEndObject(); // thread
            writer.WriteEndArray(); // threads
            
            writer.WriteEndObject(); // root
        }
        
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class StackTableBuilder
    {
        private readonly List<(int? prefix, int frame, int category)> _stacks = new();
        private readonly List<(int? prefix, int func, int category)> _frames = new();

        public int AddFrame(int? parentStack, int funcIndex, int categoryIndex)
        {
            var frameIndex = _frames.Count;
            _frames.Add((null, funcIndex, categoryIndex));
            
            var stackIndex = _stacks.Count;
            _stacks.Add((parentStack, frameIndex, categoryIndex));
            
            return stackIndex;
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            
            writer.WritePropertyName("prefix");
            writer.WriteStartArray();
            foreach (var frame in _frames)
            {
                if (frame.prefix.HasValue)
                    writer.WriteNumberValue(frame.prefix.Value);
                else
                    writer.WriteNullValue();
            }
            writer.WriteEndArray();
            
            writer.WritePropertyName("func");
            writer.WriteStartArray();
            foreach (var frame in _frames)
            {
                writer.WriteNumberValue(frame.func);
            }
            writer.WriteEndArray();
            
            writer.WritePropertyName("category");
            writer.WriteStartArray();
            foreach (var frame in _frames)
            {
                writer.WriteNumberValue(frame.category);
            }
            writer.WriteEndArray();
            
            writer.WriteEndObject();
        }

        public void WriteStackTableJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            
            writer.WritePropertyName("prefix");
            writer.WriteStartArray();
            foreach (var stack in _stacks)
            {
                if (stack.prefix.HasValue)
                    writer.WriteNumberValue(stack.prefix.Value);
                else
                    writer.WriteNullValue();
            }
            writer.WriteEndArray();
            
            writer.WritePropertyName("frame");
            writer.WriteStartArray();
            foreach (var stack in _stacks)
            {
                writer.WriteNumberValue(stack.frame);
            }
            writer.WriteEndArray();
            
            writer.WritePropertyName("category");
            writer.WriteStartArray();
            foreach (var stack in _stacks)
            {
                writer.WriteNumberValue(stack.category);
            }
            writer.WriteEndArray();
            
            writer.WriteEndObject();
        }
    }

    private sealed class SamplesBuilder
    {
        private readonly List<int> _stacks = new();
        private readonly List<double> _times = new();

        public void AddSample(double time, int stackIndex)
        {
            _times.Add(time);
            _stacks.Add(stackIndex);
        }

        public void WriteJson(Utf8JsonWriter writer)
        {
            writer.WriteStartObject();
            
            writer.WritePropertyName("stack");
            writer.WriteStartArray();
            foreach (var stack in _stacks)
            {
                writer.WriteNumberValue(stack);
            }
            writer.WriteEndArray();
            
            writer.WritePropertyName("time");
            writer.WriteStartArray();
            foreach (var time in _times)
            {
                writer.WriteNumberValue(time);
            }
            writer.WriteEndArray();
            
            writer.WriteEndObject();
        }
    }

    private sealed class MarkerData
    {
        public int Name { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public int Phase { get; set; }
        public int Category { get; set; }
        public object? Data { get; set; }
    }
}
