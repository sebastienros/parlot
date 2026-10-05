using System.Text.Json;

namespace Parlot.Explorer;

internal sealed record Request(string Path, string? Parser = null, string Input = "", JsonElement? Configuration = null);
internal sealed record ParserInfo(string Id, string Name, string ResultType, string InputKind, string[] Configuration);
internal sealed record TraceEvent(string Kind, int Id, int Parent, string Rule, int Offset, int Buffer, bool HitEnd, int Target);
internal sealed record BufferSnapshot(int Start, string Text);
internal sealed record Capture(bool? Success, object? Value, string? Error, List<TraceEvent> Events,
    List<BufferSnapshot> Buffers, bool Truncated, string AssemblyVersion, double ElapsedMs, double ParseElapsedMs);
internal static class Protocol
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = false, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
    internal const string Marker = "Parlot.Generated.ParserDiagnosticsAttribute";
    internal const int MaxEvents = 20000;
}
