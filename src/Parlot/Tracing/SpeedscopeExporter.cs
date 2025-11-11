using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Parlot.Tracing;

/// <summary>
/// Exports trace data to Speedscope JSON format.
/// </summary>
internal static class SpeedscopeExporter
{
    /// <summary>
    /// Exports tracer data to Speedscope JSON format.
    /// </summary>
    public static string Export(FirefoxProfilerTracer tracer, long startTimestamp, double timestampFrequency)
    {
        var events = tracer.Events;
        if (events.Count == 0)
        {
            return CreateEmptyProfile();
        }

        var frames = new List<Frame>();
        var profileEvents = new List<ProfileEvent>();
        var frameIndexMap = new Dictionary<string, int>();

        // Get or create frame index
        int GetFrameIndex(string name, string? preview = null)
        {
            var key = name;
            if (!frameIndexMap.TryGetValue(key, out var index))
            {
                index = frames.Count;
                frames.Add(new Frame
                {
                    Name = name,
                    File = preview
                });
                frameIndexMap[key] = index;
            }
            return index;
        }

        // Convert timestamps to microseconds
        double ToMicroseconds(long timestamp)
        {
            return (timestamp - startTimestamp) / timestampFrequency * 1_000_000;
        }

        // Process events and build frame events
        var activeFrames = new Stack<int>(); // Track frame indices for proper pairing
        
        foreach (var evt in events)
        {
            if (evt.IsEnter)
            {
                var markerName = evt.ParserName;
                var frameIndex = GetFrameIndex(markerName, evt.Preview);
                activeFrames.Push(frameIndex);
                
                profileEvents.Add(new ProfileEvent
                {
                    Type = "O",
                    At = ToMicroseconds(evt.Timestamp),
                    Frame = frameIndex
                });
            }
            else
            {
                // Use the frame from the matching open event
                var frameIndex = activeFrames.Count > 0 ? activeFrames.Pop() : 0;
                
                // Update frame name to include success/failure marker
                if (frameIndex < frames.Count)
                {
                    var frame = frames[frameIndex];
                    frame.Name = $"{frame.Name} [{(evt.Success ? "✓" : "✗")}] \"{evt.Preview}\"";
                }
                
                profileEvents.Add(new ProfileEvent
                {
                    Type = "C",
                    At = ToMicroseconds(evt.Timestamp),
                    Frame = frameIndex
                });
            }
        }

        // Build the JSON
        return BuildSpeedscopeJson(frames, profileEvents);
    }

    private static string CreateEmptyProfile()
    {
        var frames = new List<Frame>
        {
            new Frame { Name = "(empty)" }
        };
        var events = new List<ProfileEvent>();
        return BuildSpeedscopeJson(frames, events);
    }

    private static string BuildSpeedscopeJson(List<Frame> frames, List<ProfileEvent> events)
    {
        using var stream = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();

            // Schema
            writer.WriteString("$schema", "https://www.speedscope.app/file-format-schema.json");

            // Shared data
            writer.WritePropertyName("shared");
            writer.WriteStartObject();
            writer.WritePropertyName("frames");
            writer.WriteStartArray();
            foreach (var frame in frames)
            {
                writer.WriteStartObject();
                writer.WriteString("name", frame.Name);
                if (frame.File != null)
                {
                    writer.WriteString("file", frame.File);
                }
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();

            // Profiles
            writer.WritePropertyName("profiles");
            writer.WriteStartArray();

            // Single evented profile
            writer.WriteStartObject();
            writer.WriteString("type", "evented");
            writer.WriteString("name", "Parser Execution");
            writer.WriteString("unit", "microseconds");
            writer.WriteNumber("startValue", 0);

            // Calculate end value
            double endValue = 0;
            if (events.Count > 0)
            {
                endValue = events[events.Count - 1].At;
            }
            writer.WriteNumber("endValue", endValue);

            // Events
            writer.WritePropertyName("events");
            writer.WriteStartArray();
            foreach (var evt in events)
            {
                writer.WriteStartObject();
                writer.WriteString("type", evt.Type);
                writer.WriteNumber("at", evt.At);
                writer.WriteNumber("frame", evt.Frame);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();

            writer.WriteEndObject(); // profile
            writer.WriteEndArray(); // profiles

            // Exporter info
            writer.WriteString("exporter", "Parlot@1.0.0");
            writer.WriteString("name", "Parlot Parser Trace");

            writer.WriteEndObject(); // root
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private sealed class Frame
    {
        public string Name { get; set; } = string.Empty;
        public string? File { get; set; }
    }

    private sealed class ProfileEvent
    {
        public string Type { get; set; } = string.Empty;
        public double At { get; set; }
        public int Frame { get; set; }
    }
}
