using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace Parlot.Explorer;

internal static class Worker
{
    internal static object Execute(Request request)
    {
        var assembly = new ParserLoadContext(request.Path).LoadFromAssemblyPath(request.Path);
        var methods = assembly.GetTypes().Where(static type => !type.ContainsGenericParameters)
            .SelectMany(static type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            .Where(static method => method.GetCustomAttributesData().Any(static a => a.AttributeType.FullName == Protocol.Marker &&
                a.ConstructorArguments.Count == 1 && a.ConstructorArguments[0].Value is 1))
            .Where(static method => method.ReturnType == typeof(bool) && !method.ContainsGenericParameters)
            .ToArray();
        if (request.Parser is null)
            return methods.Select(static method => new ParserInfo(ParserId(method),
                $"{method.DeclaringType!.FullName}.{method.Name}", method.GetParameters()[^1].ParameterType.GetElementType()!.ToString(),
                method.GetParameters()[0].ParameterType == typeof(string) ? "string" : "reader",
                method.GetParameters()[1..^1].Where(p => p.Position != CancellationParameterIndex(method))
                    .Select(static p => $"{p.Name}: {p.ParameterType}").ToArray())).ToArray();

        var selected = methods.SingleOrDefault(method => ParserId(method) == request.Parser)
            ?? throw new InvalidOperationException("This parser is no longer available. Refresh the assembly.");
        var bridge = assembly.GetType("Parlot.Generated.ParserDiagnostics", throwOnError: true)!;
        var events = new List<TraceEvent>();
        var buffers = new List<BufferSnapshot>();
        var bufferIds = new Dictionary<BufferSnapshot, int>();
        var bufferBytes = 0;
        string? lastBuffer = null;
        var lastStart = -1;
        var lastBufferId = -1;
        var truncated = false;
        Action<object[]> sink = row =>
        {
            // Retain a contiguous prefix; one extra event distinguishes an exact fit from truncation.
            if (truncated) return;
            if (events.Count == Protocol.MaxEvents) { truncated = true; return; }
            var text = (string)row[6];
            var start = (int)row[5];
            var bufferId = lastBufferId;
            if (!ReferenceEquals(lastBuffer, text) || lastStart != start)
            {
                var snapshot = new BufferSnapshot(start, text);
                if (!bufferIds.TryGetValue(snapshot, out bufferId))
                {
                    if (bufferBytes + snapshot.Text.Length > 2_000_000) { truncated = true; return; }
                    bufferBytes += snapshot.Text.Length;
                    bufferId = buffers.Count;
                    buffers.Add(snapshot);
                    bufferIds.Add(snapshot, bufferId);
                }
                lastBuffer = text;
                lastStart = start;
                lastBufferId = bufferId;
            }
            events.Add(new TraceEvent((string)row[0], (int)row[1], (int)row[2], (string)row[3], (int)row[4], bufferId, (bool)row[7], (int)row[8]));
        };
        var parameters = selected.GetParameters();
        using var reader = new StringReader(request.Input);
        var arguments = new object?[parameters.Length];
        arguments[0] = parameters[0].ParameterType == typeof(string) ? request.Input : reader;
        for (var i = 1; i < parameters.Length - 1; i++)
        {
            var parameter = parameters[i];
            if (i == CancellationParameterIndex(selected)) { arguments[i] = CancellationToken.None; continue; }
            if (request.Configuration is not { ValueKind: JsonValueKind.Object } config || !config.TryGetProperty(parameter.Name!, out var value))
                throw new InvalidOperationException($"Provide configuration JSON property '{parameter.Name}'.");
            arguments[i] = value.Deserialize(parameter.ParameterType, Protocol.Json);
        }
        bool? success = null;
        string? error = null;
        object? result = null;
        bridge.GetMethod("Begin")!.Invoke(null, [sink, Protocol.MaxEvents + 1]);
        var watch = Stopwatch.StartNew();
        try
        {
            success = (bool)selected.Invoke(null, arguments)!;
            if (success.Value) result = new ResultSnapshot().Read(arguments[^1]);
        }
        catch (Exception exception) { error = (exception is TargetInvocationException invocation ? invocation.InnerException ?? exception : exception).ToString(); }
        finally { bridge.GetMethod("End")!.Invoke(null, null); }
        return new Capture(success, result, error, events, buffers, truncated,
            assembly.ManifestModule.ModuleVersionId.ToString(), watch.Elapsed.TotalMilliseconds);
    }

    private static int CancellationParameterIndex(MethodInfo method)
    {
        var marker = method.GetCustomAttributesData().Single(static attribute => attribute.AttributeType.FullName == Protocol.Marker);
        var argument = marker.NamedArguments.FirstOrDefault(static item => item.MemberName == "CancellationTokenIndex");
        return argument.TypedValue.Value is int index ? index : -1;
    }

    private static string ParserId(MethodInfo method) => method.DeclaringType!.FullName + "::" + method;

    private sealed class ParserLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _directory;
        internal ParserLoadContext(string path) : base(isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(path);
            _directory = System.IO.Path.GetDirectoryName(path)!;
        }
        protected override Assembly? Load(AssemblyName name)
        {
            var resolved = _resolver.ResolveAssemblyToPath(name);
            if (resolved is null)
            {
                var culture = name.CultureName;
                var adjacent = System.IO.Path.Combine(_directory, string.IsNullOrEmpty(culture) ? "" : culture, name.Name + ".dll");
                if (File.Exists(adjacent)) resolved = adjacent;
            }
            return resolved is null ? null : LoadFromAssemblyPath(resolved);
        }
        protected override nint LoadUnmanagedDll(string name)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(name);
            return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
        }
    }

    // Bounded snapshot: no arbitrary ToString(), cycles and throwing getters are visible.
    private sealed class ResultSnapshot
    {
        private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);
        private int _remaining = 2000;
        internal object? Read(object? value, int depth = 0)
        {
            if (value is null) return null;
            if (--_remaining < 0 || depth > 12) return "[limit]";
            if (value is string text) return text.Length <= 4096 ? text : text[..4096] + "…";
            var type = value.GetType();
            if (type.IsPrimitive || value is decimal || value is DateTime || value is Guid || value is DateTimeOffset) return value;
            if (type.IsEnum) return Enum.GetName(type, value);
            if (!_seen.Add(value)) return "[reference already shown]";
            if (value is IEnumerable items)
            {
                var list = new List<object?>();
                foreach (var item in items)
                {
                    if (list.Count >= 200 || _remaining <= 0) { list.Add("[limit]"); break; }
                    list.Add(Read(item, depth + 1));
                }
                return list;
            }
            var members = new Dictionary<string, object?> { ["$type"] = type.FullName };
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Take(100))
            {
                if (!property.CanRead || property.GetIndexParameters().Length != 0) continue;
                try { members[property.Name] = Read(property.GetValue(value), depth + 1); }
                catch { members[property.Name] = "[getter threw]"; }
            }
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance).Take(100))
                members[field.Name] = Read(field.GetValue(value), depth + 1);
            return members;
        }
    }
}
