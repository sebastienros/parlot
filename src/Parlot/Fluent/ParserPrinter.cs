using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace Parlot.Fluent;

/// <summary>
/// Prints the text of a parser, its <see cref="object.ToString"/>, writing each parser which invokes others at most once
/// per call: a parser reached again, through another path or a cycle, is written as a reference, its
/// <see cref="Parser{T}.Name"/> or <c>#n</c>, and its text is labelled with it, <c>#n:(...)</c>. Without it, a parser
/// shared by several others is printed once per path to it, which doubles the text with each rule of a grammar where a
/// rule refers to the next one twice.
/// </summary>
/// <remarks>
/// The parsers write their text with <see cref="ParserTextHandler"/>, in one builder for the whole call: a parser which
/// the text of another one invokes appends its text to the builder instead of returning it. Where each text starts and
/// ends, and where each reference is, is recorded, and the labels are inserted once the text is written, when the parsers
/// which are referenced are known.
/// </remarks>
internal static class ParserPrinter
{
    /// <summary>
    /// The length of the text past which <see cref="object.ToString"/> stops, as a last resort for the grammars which
    /// hold many parsers, which a debugger shows.
    /// </summary>
    internal const int MaxLength = 4096;

    /// <summary>
    /// The depth of the parsers past which <see cref="object.ToString"/> writes <see cref="Ellipsis"/>, within the default
    /// stack of a thread.
    /// </summary>
    internal const int MaxDepth = 500;

    internal const string Ellipsis = "...";

    // The innermost call to ToString which writes a text on this thread, null outside of one
    [ThreadStatic]
    private static Session? _current;

    /// <summary>
    /// Starts the text of <paramref name="parser"/>, and returns the session which writes it. <paramref name="entered"/>
    /// is false when the text is not to be written: it was written already by this call, the text is complete, or the
    /// parsers are nested too deeply.
    /// </summary>
    internal static Session Enter(IParserText parser, bool tracked, out bool root, out bool entered)
    {
        var session = _current;

        if (session is not null && ReferenceEquals(session.Expected, parser))
        {
            // A parser invoked by the text of another one, written in the same builder
            session.Expected = null;
            root = false;
            entered = session.Enter(parser, tracked);

            return session;
        }

        // The parser ToString() was called on, or one whose ToString() was called by something other than the text of a
        // parser: a parser which doesn't write its text with ParserTextHandler, in a session which is invoking it, or
        // something else, like a debugger, which starts apart. A session which isn't invoking a parser is also one which
        // an exception left
        var previous = session?.Expected is not null ? session : null;
        session = new Session(previous);
        _current = session;
        root = true;

        entered = session.Enter(parser, tracked);

        return session;
    }

    /// <summary>
    /// Ends the text of a parser, and returns it for the parser ToString() was called on, "" for the others, which were
    /// written in the builder.
    /// </summary>
    internal static string Exit(Session session, bool root, bool entered)
    {
        if (entered)
        {
            session.Exit();
        }

        if (!root)
        {
            return "";
        }

        _current = session.Previous;

        return session.GetText();
    }

    /// <summary>
    /// Forgets a session whose text failed: the exception leaves the ToString() of its parser before it ends the session,
    /// the parser which catches it is in the session before.
    /// </summary>
    internal static void Abandon(Session session)
    {
        session.Expected = null;
        _current = session.Previous;
    }

    private static bool HasSufficientStack(int depth)
    {
        // Checked every 16 parsers: each one uses less than 1 KB of stack, which the margin of the runtime's check
        // (64 KB or more) leaves room for
        if ((depth & 0xF) != 0)
        {
            return true;
        }

#if NETCOREAPP2_0_OR_GREATER || NETSTANDARD2_1_OR_GREATER
        return RuntimeHelpers.TryEnsureSufficientExecutionStack();
#else
        // As in ParseContext.CheckStack
        try
        {
            RuntimeHelpers.EnsureSufficientExecutionStack();
            return true;
        }
        catch (InsufficientExecutionStackException)
        {
            return false;
        }
#endif
    }

    internal sealed class Session
    {
        private readonly StringBuilder _builder = new();

        // The parsers whose text was started, created with the first one
        private HashSet<object>? _printed;

        // In the order of their offsets, which only grow
        private List<Event>? _events;

        // The parsers being written, null for those which aren't tracked
        private readonly Stack<object?> _open = new();

        // The tracked parsers of _open, for the sessions which are in this one, created with the first one
        private HashSet<object>? _openTracked;

        // The parsers which are referenced
        private HashSet<object>? _referenced;

        // The depth of the parsers written by the sessions this one is in
        private readonly int _depth;

        public Session(Session? previous)
        {
            Previous = previous;
            _depth = previous?.Depth ?? 0;
        }

        private int Depth => _depth + _open.Count;

        /// <summary>
        /// Whether <paramref name="parser"/> is being written by this session or by one it is in.
        /// </summary>
        private bool IsOpen(object parser)
        {
            for (var session = this; session is not null; session = session.Previous)
            {
                if (session._openTracked?.Contains(parser) == true)
                {
                    return true;
                }
            }

            return false;
        }

        public Session? Previous { get; }

        /// <summary>
        /// The parser which the text being written invokes, which writes itself in this builder.
        /// </summary>
        public object? Expected { get; set; }

        /// <summary>
        /// Whether the text reached <see cref="MaxLength"/>: nothing is written after.
        /// </summary>
        public bool IsFull { get; private set; }

        public bool Enter(IParserText parser, bool tracked)
        {
            if (IsFull)
            {
                return false;
            }

            if (tracked && _printed?.Contains(parser) == true)
            {
                (_referenced ??= new(LoopAnalysis.ReferenceComparer.Instance)).Add(parser);
                _events!.Add(new Event(_builder.Length, EventKind.Reference, parser, null));
                return false;
            }

            if (tracked && Previous?.IsOpen(parser) == true)
            {
                // A cycle through a parser which doesn't write its text with ParserTextHandler: the parser is being written
                // by a session this one is in, whose text this one can't refer to
                var name = parser.Name;
                Append(string.IsNullOrEmpty(name) ? Ellipsis : name);
                return false;
            }

            // The parsers nested deeper than MaxDepth, or than the stack allows, are cut rather than overflowing the stack
            if (Depth >= MaxDepth || !HasSufficientStack(Depth))
            {
                Append(Ellipsis);
                return false;
            }

            if (tracked)
            {
                (_printed ??= new(LoopAnalysis.ReferenceComparer.Instance)).Add(parser);
                (_openTracked ??= new(LoopAnalysis.ReferenceComparer.Instance)).Add(parser);
                (_events ??= new()).Add(new Event(_builder.Length, EventKind.Start, parser, parser.Name));
            }

            _open.Push(tracked ? parser : null);

            return true;
        }

        public void Exit()
        {
            // Null for a parser which isn't tracked
            if (_open.Pop() is { } parser)
            {
                _openTracked!.Remove(parser);
                _events!.Add(new Event(_builder.Length, EventKind.End, parser, null));
            }
        }

        public void Append(string? text)
        {
            if (IsFull || string.IsNullOrEmpty(text))
            {
                return;
            }

            _builder.Append(text);

            // Past MaxLength, which Truncate then cuts with the ellipsis
            if (_builder.Length > MaxLength)
            {
                IsFull = true;
            }
        }

        /// <summary>
        /// The text, with the labels of the parsers which are referenced and their references.
        /// </summary>
        public string GetText()
        {
            if (_referenced is null)
            {
                return Truncate(_builder.ToString());
            }

            var text = _builder.ToString();
            var labels = new Dictionary<object, string>(LoopAnalysis.ReferenceComparer.Instance);
            var names = new HashSet<string>(StringComparer.Ordinal);
            var count = 0;

            // The labels and the references, at their offsets in the text
            var insertions = new List<(int Offset, string Text)>();
            var total = text.Length;

            foreach (var e in _events!)
            {
                if (e.Kind != EventKind.Reference && !_referenced.Contains(e.Parser))
                {
                    continue;
                }

                var insertion = e.Kind switch
                {
                    EventKind.Start => Label(e.Parser, e.Name) + ":(",
                    EventKind.End => ")",
                    _ => labels[e.Parser],
                };

                insertions.Add((e.Offset, insertion));
                total += insertion.Length;
            }

            // Unless the whole text fits, the insertions are made while they fit before the ellipsis, which then cuts the
            // text and not an insertion
            var available = total <= MaxLength ? int.MaxValue : MaxLength - Ellipsis.Length;
            var result = new StringBuilder(Math.Min(total, MaxLength));
            var position = 0;

            foreach (var (offset, insertion) in insertions)
            {
                if (result.Length + (offset - position) + insertion.Length > available)
                {
                    // The text stops before the insertion
                    result.Append(text, position, CutLength(text, position, Math.Min(offset - position, available - result.Length)));
                    return result.Append(Ellipsis).ToString();
                }

                result.Append(text, position, offset - position);
                result.Append(insertion);
                position = offset;
            }

            result.Append(text, position, text.Length - position);

            return Truncate(result.ToString());

            // The name of the parser, unless another referenced parser has it, or #n, n counting the parsers labelled so
            // far, in the order the texts start, before their references
            string Label(object parser, string? name)
            {
                string label;

                if (!string.IsNullOrEmpty(name) && names.Add(name!))
                {
                    label = name!;
                }
                else
                {
                    do
                    {
                        label = $"{name}#{++count}";
                    }
                    while (!names.Add(label));
                }

                labels[parser] = label;

                return label;
            }
        }

        private static string Truncate(string text)
        {
            if (text.Length <= MaxLength)
            {
                return text;
            }

            return string.Concat(text.Substring(0, CutLength(text, 0, MaxLength - Ellipsis.Length)), Ellipsis);
        }

        // The length of the text to keep from start, which doesn't split a surrogate pair
        private static int CutLength(string text, int start, int length)
            => length > 0 && char.IsHighSurrogate(text[start + length - 1]) ? length - 1 : length;
    }

    private enum EventKind
    {
        Start,
        End,
        Reference,
    }

    private readonly struct Event
    {
        public Event(int offset, EventKind kind, object parser, string? name)
        {
            Offset = offset;
            Kind = kind;
            Parser = parser;
            Name = name;
        }

        public int Offset { get; }

        public EventKind Kind { get; }

        public object Parser { get; }

        public string? Name { get; }
    }
}

/// <summary>
/// The parsers, or other values, of a list, written with a separator by <see cref="ParserTextHandler"/>, only when the
/// text isn't complete.
/// </summary>
internal readonly struct ParserList
{
    public ParserList(string separator, IEnumerable items)
    {
        Separator = separator;
        Items = items;
    }

    public string Separator { get; }

    public IEnumerable Items { get; }
}

/// <summary>
/// A parser, which <see cref="ParserTextHandler"/> recognizes.
/// </summary>
internal interface IParserText
{
    string? Name { get; }

    /// <summary>
    /// Whether the parser only adds to the text of the parser it invokes, and is written each time it is reached.
    /// </summary>
    bool IsTextWrapper { get; }
}

/// <summary>
/// Writes the text of a parser, in which the parsers it invokes write their own text in the same builder.
/// </summary>
[InterpolatedStringHandler]
internal readonly ref struct ParserTextHandler
{
    private readonly ParserPrinter.Session _session;
    private readonly bool _root;
    private readonly bool _entered;

    public ParserTextHandler(int literalLength, int formattedCount, IParserText parser, out bool shouldAppend)
    {
        _ = literalLength;
        _ = formattedCount;
        _session = ParserPrinter.Enter(parser, !parser.IsTextWrapper, out _root, out _entered);
        shouldAppend = _entered;
    }

    public void AppendLiteral(string value) => _session.Append(value);

    public void AppendFormatted<TValue>(TValue value)
    {
        var session = _session;

        if (session.IsFull)
        {
            return;
        }

        if (value is ParserList list)
        {
            var first = true;

            foreach (var item in list.Items)
            {
                if (!first)
                {
                    session.Append(list.Separator);
                }

                first = false;
                AppendFormatted(item);
            }

            return;
        }

        // A parser writes its text in the builder when it writes it with a ParserTextHandler, and returns it otherwise
        var parser = value is IParserText;

        if (parser)
        {
            session.Expected = value;
        }

        string? text;

        try
        {
            text = value?.ToString();
        }
        catch
        {
            ParserPrinter.Abandon(session);
            throw;
        }

        if (!parser || ReferenceEquals(session.Expected, value))
        {
            session.Expected = null;
            session.Append(text);
        }
    }

    public string ToStringAndClear() => ParserPrinter.Exit(_session, _root, _entered);
}
