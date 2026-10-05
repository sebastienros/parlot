using System;
using System.Collections.Generic;

namespace Parlot.Generated;

// Versioned, assembly-local reflection contract. No dependency on the explorer or runtime Parlot.
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ParserDiagnosticsAttribute : Attribute
{
    public ParserDiagnosticsAttribute(int version) { Version = version; }
    public int Version { get; }
    public int CancellationTokenIndex { get; set; } = -1;
}

internal static class ParserDiagnostics
{
    [ThreadStatic] private static Action<object[]>? _sink;
    [ThreadStatic] private static List<int>? _stack;
    [ThreadStatic] private static int _next;
    [ThreadStatic] private static int _remaining;

    public static void Begin(Action<object[]> sink, int limit)
    {
        _sink = sink;
        _stack = new List<int>();
        _next = 0;
        _remaining = limit;
    }

    public static void End() { _sink = null; _stack = null; }

    public static int Enter(string rule, global::Parlot.Cursor cursor)
    {
        if (_sink is null) return -1;
        var id = ++_next;
        var parent = _stack!.Count == 0 ? 0 : _stack[_stack.Count - 1];
        _stack.Add(id);
        Emit("enter", id, parent, rule, cursor, -1);
        return id;
    }

    public static void Exit(int id, bool? success, global::Parlot.Cursor cursor)
    {
        if (id < 0) return;
        Emit(success.HasValue ? (success.Value ? "success" : "failure") : "exception", id, 0, "", cursor, -1);
        _stack!.RemoveAt(_stack.Count - 1);
    }

    public static void Reset(global::Parlot.Cursor cursor, global::Parlot.TextPosition position)
    {
        if (_sink is not null && _stack!.Count > 0)
            Emit("reset", _stack[_stack.Count - 1], 0, "", cursor, position.Offset);
        cursor.ResetPosition(position);
    }

    private static void Emit(string kind, int id, int parent, string rule, global::Parlot.Cursor cursor, int target)
    {
        if (_remaining-- <= 0) return;
        _sink!(new object[] { kind, id, parent, rule, cursor.Offset, cursor.BufferStart, cursor.Buffer, cursor.HitEnd, target });
    }
}
