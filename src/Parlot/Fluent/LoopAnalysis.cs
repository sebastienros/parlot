using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// A parser that tells <see cref="LoopAnalysis"/> which parsers it invokes before it reads anything.
/// </summary>
/// <remarks>
/// Only Parlot's parsers implement it. The analysis treats the parsers of other types as invoking any parser, unless they
/// can't reach one, see <see cref="LoopAnalysis"/>.
/// </remarks>
internal interface ILoopAnalyzable
{
    /// <summary>
    /// Reports with <see cref="LoopAnalysis.Invoke(object)"/> each parser this parser can invoke while the cursor is still
    /// where it was invoked, and returns whether it can succeed without moving the cursor.
    /// </summary>
    /// <remarks>
    /// <see cref="LoopAnalysis.Invoke(object)"/> returns whether the parser it reports can succeed without moving the cursor,
    /// such that a sequence reports an element only when the previous ones can. When unsure, a parser reports a parser and
    /// returns <see langword="true"/>, which can only keep a check that never fires. A parser must not report less when one
    /// of its parsers can succeed without moving the cursor, such that the analysis converges.
    /// </remarks>
    bool AnalyzeStart(LoopAnalysis analysis);
}

/// <summary>
/// The state <see cref="LoopAnalysis"/> keeps on a <see cref="Deferred{T}"/> parser.
/// </summary>
internal interface IDeferredParser
{
    /// <summary>
    /// Gets or sets <see cref="LoopAnalysis.Unknown"/>, <see cref="LoopAnalysis.Required"/> or <see cref="LoopAnalysis.Elided"/>.
    /// </summary>
    int LoopDetection { get; set; }

    /// <summary>
    /// Gets or sets the analyses that read the parser of this one, which no longer apply once it changes.
    /// </summary>
    List<IDeferredParser[]>? LoopGroups { get; set; }
}

/// <summary>
/// Proves that a <see cref="Deferred{T}"/> parser can't be invoked again at the position where it is already active,
/// such that it doesn't need to look for itself in the active parsers of the context, see <see cref="ParseContext.PushParserAtPosition(object)"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the well-formedness check of parsing expression grammars (Ford, 2004). A parser is <em>nullable</em> when it can
/// succeed without moving the cursor. It invokes a parser <em>at its start</em> when the cursor can still be where the parser
/// itself was invoked: every alternative of a choice, the first element of a sequence and the next ones while the previous
/// ones are nullable, the body of a loop or of an option, a lookahead, the parser a conversion or an error wraps... A deferred
/// parser is only invoked again at the position where it is active through a cycle of such invocations. Without one, the
/// loop detection never fires, and skipping it doesn't change what a grammar parses.
/// </para>
/// <para>
/// The nullable parsers are the least fixed point of the rules the parsers declare with <see cref="ILoopAnalyzable"/>, computed
/// over the parsers reachable from a deferred parser through invocations at the start, and the cycles are the strongly connected
/// components of these invocations. A single analysis concludes for every deferred parser it reaches. It runs once, when a
/// deferred parser is first invoked, and again after the <see cref="Deferred{T}.Parser"/> of one of the deferred parsers it read
/// is replaced, as this is the only way a grammar changes once it is built.
/// </para>
/// <para>
/// It relies on two properties of the parsers: one never moves the cursor before the position where it was invoked (the
/// loop detection of generated parsers relies on it too), and the callbacks of the built-in parsers (conversions, predicates,
/// selectors) don't invoke parsers. A parser type the analysis doesn't know can invoke any parser, so a deferred parser that can
/// invoke one at its start keeps the loop detection, unless the type can't reach a parser, see <see cref="IsLeaf(Type)"/>.
/// </para>
/// <para>
/// The parser the context skips white space with is invoked by the parsers that skip white space, wherever the grammar is.
/// It is not part of the analysis: a context whose white space parser can invoke a deferred parser keeps the loop detection
/// of every deferred parser, see <see cref="CanReachDeferred(object)"/>.
/// </para>
/// </remarks>
internal sealed class LoopAnalysis
{
    /// <summary>
    /// The deferred parser hasn't been analyzed since its grammar changed.
    /// </summary>
    public const int Unknown = 0;

    /// <summary>
    /// The deferred parser can be invoked again at the position where it is active, or the analysis can't tell.
    /// </summary>
    public const int Required = 1;

    /// <summary>
    /// The deferred parser can't be invoked again at the position where it is active.
    /// </summary>
    public const int Elided = 2;

    /// <summary>
    /// Serializes the analyses and the changes of <see cref="Deferred{T}.Parser"/>.
    /// </summary>
    public static readonly object SyncRoot = new();

    private const BindingFlags DeclaredInstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags DeclaredFields = DeclaredInstanceFields | BindingFlags.Static;

    // Whether the parsers of a type the analysis doesn't know can't reach another parser, see IsLeaf(). Written under
    // SyncRoot. The keys are weak, such that the types of an unloaded assembly can be collected.
    private static readonly ConditionalWeakTable<Type, object> s_leafTypes = new();

    // Whether a white space parser can invoke a deferred parser, see CanReachDeferred(). Written under SyncRoot.
    private static readonly ConditionalWeakTable<object, object> s_whiteSpaceParsers = new();

    // The values of the tables
    private static readonly object s_true = true;
    private static readonly object s_false = false;

    private readonly List<Node> _nodes = [];
    private readonly Dictionary<object, Node> _nodesByParser = new(ReferenceComparer.Instance);
    private Node _current = null!;

    // The state of the search of strongly connected components, see FindCycles()
    private readonly Stack<Node> _componentStack = new();
    private int _nextIndex;
    private int _nextComponent;

    private LoopAnalysis()
    {
    }

    /// <summary>
    /// Analyzes the grammar of <paramref name="deferred"/> unless it was analyzed since it last changed, and returns whether
    /// it can't be invoked again at the position where it is active.
    /// </summary>
    public static bool Analyze(IDeferredParser deferred)
    {
        lock (SyncRoot)
        {
            if (deferred.LoopDetection == Unknown)
            {
                var analysis = new LoopAnalysis();

                analysis.Explore(deferred);
                analysis.FindCycles();
                analysis.Publish();
            }

            return deferred.LoopDetection == Elided;
        }
    }

    /// <summary>
    /// Forgets the analyses that read the parser of <paramref name="deferred"/>, which is being replaced. Called under <see cref="SyncRoot"/>.
    /// </summary>
    public static void Invalidate(IDeferredParser deferred)
    {
        deferred.LoopDetection = Unknown;

        var groups = deferred.LoopGroups;

        if (groups is null)
        {
            return;
        }

        deferred.LoopGroups = null;

        foreach (var group in groups)
        {
            foreach (var member in group)
            {
                member.LoopDetection = Unknown;

                if (!ReferenceEquals(member, deferred))
                {
                    member.LoopGroups?.Remove(group);
                }
            }
        }
    }

    /// <summary>
    /// Gets whether a white space parser can invoke a deferred parser, or a parser the analysis doesn't know, before it reads anything.
    /// </summary>
    /// <remarks>
    /// The parsers that skip white space invoke the white space parser where they are invoked, which then invokes these
    /// parsers at the same position, outside of the grammar the deferred parsers were analyzed with. A white space parser that
    /// can't reach a deferred parser doesn't create a cycle through one. The answer doesn't change: only a deferred parser
    /// can change what a parser invokes.
    /// </remarks>
    public static bool CanReachDeferred(object whiteSpaceParser)
    {
        if (!s_whiteSpaceParsers.TryGetValue(whiteSpaceParser, out var answer))
        {
            lock (SyncRoot)
            {
                if (!s_whiteSpaceParsers.TryGetValue(whiteSpaceParser, out answer))
                {
                    var analysis = new LoopAnalysis();

                    analysis.Explore(whiteSpaceParser);

                    answer = s_false;

                    foreach (var node in analysis._nodes)
                    {
                        if (node.Opaque || node.Parser is IDeferredParser)
                        {
                            answer = s_true;
                            break;
                        }
                    }

                    s_whiteSpaceParsers.Add(whiteSpaceParser, answer);
                }
            }
        }

        return ReferenceEquals(answer, s_true);
    }

    /// <summary>
    /// Records that the parser being analyzed can invoke <paramref name="parser"/> while the cursor is still where it was
    /// invoked, and returns whether <paramref name="parser"/> can succeed without moving the cursor.
    /// </summary>
    /// <remarks>
    /// The answer is the current estimate, which only goes from <see langword="false"/> to <see langword="true"/> until the
    /// analysis converges.
    /// </remarks>
    public bool Invoke(object parser)
    {
        var node = GetNode(parser);

        _current.Edges.Add(node);

        return node.Nullable;
    }

    private Node GetNode(object parser)
    {
        if (!_nodesByParser.TryGetValue(parser, out var node))
        {
            node = new Node(parser);
            _nodes.Add(node);
            _nodesByParser.Add(parser, node);
        }

        return node;
    }

    /// <summary>
    /// Finds the parsers <paramref name="root"/> can invoke at its start, the ones they can invoke at theirs, and so on, and
    /// whether each of them is nullable.
    /// </summary>
    private void Explore(object root)
    {
        GetNode(root);

        bool changed;

        do
        {
            changed = false;

            // The parsers found while iterating are analyzed in the same pass. Another pass follows a parser that turned out
            // to be nullable, since the parsers that invoke it can then invoke more parsers and be nullable too.
            for (var i = 0; i < _nodes.Count; i++)
            {
                var node = _nodes[i];

                node.Edges.Clear();
                node.Opaque = false;
                _current = node;

                if (Evaluate(node.Parser) && !node.Nullable)
                {
                    node.Nullable = true;
                    changed = true;
                }
            }
        }
        while (changed);
    }

    private bool Evaluate(object parser) => parser is ILoopAnalyzable analyzable ? analyzable.AnalyzeStart(this) : AnalyzeUnknown(parser);

    /// <summary>
    /// Analyzes the parser being analyzed as a parser of a type the analysis doesn't know, which can invoke any parser
    /// unless it can't reach one, and can succeed without moving the cursor.
    /// </summary>
    public bool AnalyzeUnknown(object parser)
    {
        if (!IsLeaf(parser.GetType()))
        {
            _current.Opaque = true;
        }

        // Nothing is known about what it reads
        return true;
    }

    /// <summary>
    /// Finds the deferred parsers which are in a cycle of invocations at the start, or which can invoke a parser the analysis
    /// doesn't know, with Tarjan's algorithm.
    /// </summary>
    /// <remarks>
    /// The search keeps its own stack, since the analysis can run while a parser is already deep in the call stack.
    /// </remarks>
    private void FindCycles()
    {
        var frames = new Stack<(Node Node, int Edge)>();

        foreach (var root in _nodes)
        {
            if (root.Index >= 0)
            {
                continue;
            }

            Visit(root);
            frames.Push((root, 0));

            while (frames.Count > 0)
            {
                var (node, edge) = frames.Pop();

                if (edge < node.Edges.Count)
                {
                    frames.Push((node, edge + 1));

                    var next = node.Edges[edge];

                    if (next.Index < 0)
                    {
                        Visit(next);
                        frames.Push((next, 0));
                    }
                    else if (next.OnStack)
                    {
                        node.LowLink = Math.Min(node.LowLink, next.Index);
                    }

                    continue;
                }

                // Every parser the node invokes has been visited
                if (frames.Count > 0)
                {
                    var parent = frames.Peek().Node;
                    parent.LowLink = Math.Min(parent.LowLink, node.LowLink);
                }

                if (node.LowLink == node.Index)
                {
                    CloseComponent(node);
                }
            }
        }
    }

    private void Visit(Node node)
    {
        node.Index = node.LowLink = _nextIndex++;
        node.OnStack = true;
        _componentStack.Push(node);
    }

    private void CloseComponent(Node root)
    {
        var component = _nextComponent++;
        var members = new List<Node>();

        Node member;

        do
        {
            member = _componentStack.Pop();
            member.OnStack = false;
            member.Component = component;
            members.Add(member);
        }
        while (member != root);

        // The components which the members invoke are closed before this one
        var cyclic = members.Count > 1 || root.Edges.Contains(root);
        var reachesOpaque = false;

        foreach (var node in members)
        {
            reachesOpaque |= node.Opaque;

            foreach (var next in node.Edges)
            {
                reachesOpaque |= next.Component != component && next.ReachesOpaque;
            }
        }

        foreach (var node in members)
        {
            node.Cyclic = cyclic;
            node.ReachesOpaque = reachesOpaque;
        }
    }

    /// <summary>
    /// Sets the loop detection of each deferred parser that was reached, and keeps the analysis on each of them so that
    /// it is forgotten when one is replaced.
    /// </summary>
    private void Publish()
    {
        var deferreds = new List<IDeferredParser>();

        foreach (var node in _nodes)
        {
            if (node.Parser is IDeferredParser deferred)
            {
                deferreds.Add(deferred);
            }
        }

        var group = deferreds.ToArray();

        foreach (var node in _nodes)
        {
            if (node.Parser is IDeferredParser deferred)
            {
                (deferred.LoopGroups ??= []).Add(group);
                deferred.LoopDetection = node.Cyclic || node.ReachesOpaque ? Required : Elided;
            }
        }
    }

    /// <summary>
    /// Gets whether the parsers of a type the analysis doesn't know can't reach another parser.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type is a leaf when its fields can only hold plain data: primitives, enums, strings, and arrays or structs of
    /// these. These are the instance and static fields it declares, the ones of the types it derives from
    /// <see cref="Parser{T}"/> through, and the static fields of the types it is nested in, such as a grammar. A field
    /// holding a parser, a delegate or any other object makes it opaque.
    /// </para>
    /// <para>
    /// This is a heuristic: a parser that reaches another one without a field, like through the static field of an unrelated
    /// type or through the context, isn't detected. A leaf is still assumed to be nullable. Without dynamic code (native AOT)
    /// the fields of a type may not all be listed, so every unknown type is opaque.
    /// </para>
    /// </remarks>
    private static bool IsLeaf(Type type)
    {
#if NET8_0_OR_GREATER
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return false;
        }
#endif

        if (!s_leafTypes.TryGetValue(type, out var leaf))
        {
            bool holdsPlainData;

            try
            {
                holdsPlainData = HoldsPlainData(type);
            }
            catch
            {
                // A field whose type can't be loaded, for instance, could hold anything
                holdsPlainData = false;
            }

            leaf = holdsPlainData ? s_true : s_false;
            s_leafTypes.Add(type, leaf);
        }

        return ReferenceEquals(leaf, s_true);
    }

    private static bool HoldsPlainData(Type type)
    {
        for (var current = type; current is not null && !IsParserBase(current); current = current.BaseType)
        {
            if (!HasPlainFields(current, DeclaredFields))
            {
                return false;
            }
        }

        for (var current = type.DeclaringType; current is not null; current = current.DeclaringType)
        {
            if (!HasPlainFields(current, DeclaredFields & ~BindingFlags.Instance))
            {
                return false;
            }
        }

        return true;

        static bool IsParserBase(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Parser<>);
    }

    private static bool HasPlainFields(Type type, BindingFlags bindingFlags, int depth = 0)
    {
        foreach (var field in type.GetFields(bindingFlags))
        {
            if (!IsPlainData(field.FieldType, depth))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPlainData(Type type, int depth)
    {
        if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
        {
            return true;
        }

        if (type.IsArray)
        {
            return IsPlainData(type.GetElementType()!, depth);
        }

#if NET8_0_OR_GREATER
        if (type.IsFunctionPointer)
        {
            return false;
        }
#endif

        // A struct of plain data, like a TextSpan or a nullable int. A deep struct is opaque, which also ends a recursion.
        return type.IsValueType && !type.IsPointer && depth < 4 && HasPlainFields(type, DeclaredInstanceFields, depth + 1);
    }

    private sealed class Node
    {
        public Node(object parser)
        {
            Parser = parser;
        }

        public readonly object Parser;

        // The parsers it can invoke at its start
        public readonly List<Node> Edges = [];

        public bool Nullable;

        // A parser the analysis doesn't know, which can invoke any parser
        public bool Opaque;

        // The search of strongly connected components
        public int Index = -1;
        public int LowLink;
        public bool OnStack;
        public int Component = -1;

        // Whether it is in a cycle of invocations at the start
        public bool Cyclic;

        // Whether it can invoke an opaque parser at its start, itself included
        public bool ReachesOpaque;
    }

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceComparer Instance = new();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
