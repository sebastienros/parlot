using Parlot.Fluent;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Parlot.Analysis;

/// <summary>
/// Looks for the patterns of <see cref="GrammarRule.All"/> in a grammar: the ones that make it slower than it could be,
/// and the ones that don't parse what they seem to.
/// </summary>
/// <remarks>
/// The source generator reports what it finds in the grammars it generates, and Parlot Explorer shows it. A grammar built
/// at runtime can be analyzed in a test. The parsers are read through their fields, so without dynamic code (native AOT)
/// some of them may not be reached.
/// </remarks>
public static class GrammarAnalyzer
{
    /// <summary>
    /// Analyzes the grammar <paramref name="parser"/> starts, and returns the patterns it found, in the order of the parsers.
    /// </summary>
    /// <remarks>
    /// The grammar must be complete: every <see cref="Fluent.Deferred{T}"/> has its parser. The analysis doesn't change what the
    /// grammar parses, but it takes time: run it once, not before each parse.
    /// </remarks>
    public static IReadOnlyList<GrammarFinding> Analyze<T>(Parser<T> parser)
    {
        ThrowHelper.ThrowIfNull(parser, nameof(parser));

        return new GrammarAnalysis(parser).Run();
    }
}

/// <summary>
/// The analysis of one grammar, see <see cref="GrammarAnalyzer"/>.
/// </summary>
/// <remarks>
/// It reuses <see cref="LoopAnalysis"/> for what each parser invokes before it reads anything, whether it can succeed without
/// reading anything, and the cycles of these invocations. The rules only report what they can prove from Parlot's parser
/// types, and are silent when a parser of another type could change the conclusion.
/// </remarks>
internal sealed class GrammarAnalysis
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    // How deep the fields of a parser are searched for parsers, through arrays, structs and Parlot's own helpers
    private const int MaxFieldDepth = 4;

    // How many parsers a description lists before it counts the other ones
    private const int MaxListed = 3;

    private static readonly Assembly s_parlot = typeof(GrammarAnalysis).Assembly;

    // Parlot's parsers which succeed only when the first parser they invoke does, at the position where they are invoked
    private static readonly HashSet<string> s_firstMustSucceed = new(StringComparer.Ordinal)
    {
        "Sequence", "SequenceAndSkip", "SequenceSkipAnd", "Then", "Discard", "Capture", "SkipWhiteSpace", "When",
        "WhenFollowedBy", "WhenNotFollowedBy", "ElseError", "Between", "OneOrMany", "Separated", "Deferred", "Commit",
        "Switch", "Seekable", "IParserAdapter", "OperatorIndex", "LeftAssociative", "LeftAssociativeWithContext", "RightAssociative",
        "RightAssociativeWithContext", "AssociativeWithOperator",
    };

    // Parlot's parsers which succeed exactly when the parser they invoke does, at the same position
    private static readonly HashSet<string> s_transparent = new(StringComparer.Ordinal)
    {
        "Then", "Discard", "Capture", "Deferred", "IParserAdapter", "OperatorIndex",
    };

    // Parlot's parsers which never fail
    private static readonly HashSet<string> s_neverFail = new(StringComparer.Ordinal)
    {
        "Optional", "ZeroOrOne", "ZeroOrMany", "Always", "Else",
    };

    // The names Parlot's parsers have when they are built
    private static readonly HashSet<string> s_defaultNames = new(StringComparer.Ordinal)
    {
        "Always", "Fail", "IdentifierLiteral", "Identifier", "NumberLiteral", "NonWhiteSpaceLiteral", "PatternLiteral",
        "StringLiteral", "SpanScanner", "WhiteSpaceLiteral",
    };

    // The parsers that build a choice of their operators
    private static readonly HashSet<string> s_operatorParsers = new(StringComparer.Ordinal)
    {
        "LeftAssociative", "LeftAssociativeWithContext", "RightAssociative", "RightAssociativeWithContext",
        "AssociativeWithOperator", "Unary", "UnaryWithContext", "PrecedenceParser",
    };

    private readonly object _root;
    private readonly List<object> _parsers = [];
    private readonly Dictionary<object, object?> _parents = new(LoopAnalysis.ReferenceComparer.Instance);
    private readonly Dictionary<object, List<object>> _children = new(LoopAnalysis.ReferenceComparer.Instance);
    private readonly Dictionary<Type, bool> _parserTypes = [];
    private readonly Dictionary<Type, PropertyInfo?> _nameProperties = [];
    private readonly List<GrammarFinding> _findings = [];
    private Dictionary<object, LoopAnalysis.Node> _starts = null!;

    private enum Outcome
    {
        CanFail,
        NeverFails,
        ThrowsOnFailure,
    }

    public GrammarAnalysis(object root)
    {
        _root = root;
    }

    public List<GrammarFinding> Run()
    {
        Walk();

        // The lookup tables are built as the first parse builds them, from the hints of the complete grammar
        foreach (var parser in _parsers)
        {
            (parser as IChoiceParser)?.Resolve();
        }

        _starts = LoopAnalysis.ExploreStarts(_parsers);

        var reportedCycles = new HashSet<int>();

        foreach (var parser in _parsers)
        {
            if (parser is IChoiceParser choice)
            {
                AnalyzeChoice(parser, choice.Alternatives);
            }
            else if (parser is IDeferredParser)
            {
                AnalyzeDeferred(parser, reportedCycles);
            }
        }

        return _findings;
    }

    /// <summary>
    /// Lists the parsers of the grammar, depth first, with the parser each one was first reached from.
    /// </summary>
    private void Walk()
    {
        var pending = new Stack<(object Parser, object? Parent)>();

        pending.Push((_root, null));

        while (pending.Count > 0)
        {
            var (parser, parent) = pending.Pop();

            if (_parents.ContainsKey(parser))
            {
                continue;
            }

            _parents[parser] = parent;
            _parsers.Add(parser);

            var children = new List<object>();

            for (var type = parser.GetType(); type is not null && !IsParserBase(type); type = type.BaseType)
            {
                foreach (var field in type.GetFields(InstanceFields))
                {
                    CollectParsers(field.GetValue(parser), children, 0);
                }
            }

            _children.Add(parser, children);

            // The first children are visited first
            for (var i = children.Count - 1; i >= 0; i--)
            {
                if (!_parents.ContainsKey(children[i]))
                {
                    pending.Push((children[i], parser));
                }
            }
        }
    }

    private void CollectParsers(object? value, List<object> parsers, int depth)
    {
        if (value is null or string or Delegate or Type)
        {
            return;
        }

        var type = value.GetType();

        if (IsParser(type))
        {
            if (!ContainsReference(parsers, value))
            {
                parsers.Add(value);
            }

            return;
        }

        if (depth >= MaxFieldDepth || type.IsPrimitive || type.IsEnum || type.IsPointer)
        {
            return;
        }

        if (value is Array array)
        {
            if (!type.GetElementType()!.IsPrimitive)
            {
                foreach (var item in array)
                {
                    CollectParsers(item, parsers, depth + 1);
                }
            }

            return;
        }

        // Tuples and other structs, and Parlot's helpers, like the levels of an operator table
        if (type.IsValueType || type.Assembly == s_parlot)
        {
            for (var current = type; current is not null && current != typeof(object) && current != typeof(ValueType); current = current.BaseType)
            {
                foreach (var field in current.GetFields(InstanceFields))
                {
                    CollectParsers(field.GetValue(value), parsers, depth + 1);
                }
            }
        }
    }

    private void AnalyzeChoice(object choice, IReadOnlyList<object> alternatives)
    {
        if (alternatives.Count < 2)
        {
            return;
        }

        // PARLOT112: the alternatives after one that never fails are never tried
        var reachable = alternatives.Count;

        for (var i = 0; i < alternatives.Count - 1; i++)
        {
            var outcome = GetOutcome(alternatives[i], 0);

            if (outcome == Outcome.CanFail)
            {
                continue;
            }

            reachable = i + 1;

            var skipped = new List<string>();

            for (var j = i + 1; j < alternatives.Count; j++)
            {
                skipped.Add(Describe(alternatives[j]));
            }

            var why = outcome == Outcome.NeverFails
                ? "never fails"
                : "reports an error instead of failing";

            var fix = outcome == Outcome.NeverFails
                ? "Move it last, or make it fail when nothing matches."
                : "Report the error after the choice, with ElseError on the choice itself.";

            Report(GrammarRule.UnreachableAlternative, choice,
                $"In {DescribeChoice(choice, alternatives)}, alternative {i + 1} ({Describe(alternatives[i])}) {why}, so {Plural(skipped.Count, "the alternative", "the alternatives")} after it ({Join(skipped)}) {Plural(skipped.Count, "is", "are")} never tried. {fix}");

            break;
        }

        // PARLOT113: an alternative which can't match because an earlier one matches whenever it would. The other rules
        // only look at the alternatives which can match: fixing a correctness finding changes the choice anyway.
        var candidates = new List<int>();

        for (var j = 0; j < reachable; j++)
        {
            // A left-recursive alternative never matches, PARLOT111 reports it
            if (StartsWithLeftRecursion(alternatives[j]))
            {
                continue;
            }

            var shadowed = false;

            foreach (var i in candidates)
            {
                if (FindShadowing(alternatives[i], alternatives[j]) is { } detail)
                {
                    Report(GrammarRule.ShadowedAlternative, choice,
                        $"In {DescribeChoice(choice, alternatives)}, alternative {j + 1} ({Describe(alternatives[j])}) never matches: {detail}, which alternative {i + 1} ({Describe(alternatives[i])}) matches first. Try the longer alternative first, or remove the one that can't match.");

                    shadowed = true;
                    break;
                }
            }

            if (!shadowed)
            {
                candidates.Add(j);
            }
        }

        // A choice with a single alternative that can match has nothing to choose
        if (candidates.Count < 2)
        {
            return;
        }

        // The lookup tables of OneOf<T>
        if (choice is ISeekable && !ReportMixedWhiteSpace(choice, alternatives, candidates))
        {
            ReportUnseekableAlternatives(choice, alternatives, candidates);
        }

        ReportSharedLeadingParsers(choice, alternatives, candidates);
    }

    // PARLOT101: OneOf<T> builds no lookup table when some of its alternatives skip white space and others don't, or can't tell
    private bool ReportMixedWhiteSpace(object choice, IReadOnlyList<object> alternatives, List<int> candidates)
    {
        var skipping = new List<string>();
        var others = new List<string>();
        var stale = new List<string>();

        foreach (var index in candidates)
        {
            var alternative = alternatives[index];

            if (alternative is ISeekable { SkipWhitespace: true })
            {
                skipping.Add(Describe(alternative));
            }
            else
            {
                others.Add(Describe(alternative));

                if (FindStaleDeferred(alternative) is { } deferred)
                {
                    stale.Add($" {Capitalize(Describe(alternative))} was built before the parser of {Describe(deferred)} was set, and kept the hints it had then.");
                }
            }
        }

        if (skipping.Count == 0 || others.Count == 0)
        {
            return false;
        }

        Report(GrammarRule.MixedWhiteSpaceChoice, choice,
            $"{Capitalize(DescribeChoice(choice, alternatives))} has no lookup table, so it tries each alternative in order whatever the next char is: {Join(skipping)} {Plural(skipping.Count, "skips", "skip")} white space and {Join(others)} {Plural(others.Count, "doesn't", "don't")}, or can't tell.{string.Concat(stale)} Make every alternative skip white space, with Terms parsers, SkipWhiteSpace or Lookup(skipWhiteSpace: true, ...), or none of them.");

        return true;
    }

    // The deferred parser an alternative starts with when the alternative kept the hints the deferred parser had before
    // its parser was set: most parsers copy the hints of the parser they start with when they are built
    private object? FindStaleDeferred(object alternative)
    {
        if (alternative is not ISeekable copied)
        {
            return null;
        }

        foreach (var parser in Chain(alternative))
        {
            if (parser is IDeferredParser)
            {
                return parser is ISeekable current && (current.CanSeek != copied.CanSeek || current.SkipWhitespace != copied.SkipWhitespace) ? parser : null;
            }
        }

        return null;
    }

    private bool StartsWithLeftRecursion(object alternative)
    {
        foreach (var parser in Chain(alternative))
        {
            if (parser is IDeferredParser && _starts.TryGetValue(parser, out var node) && node.Cyclic)
            {
                return true;
            }
        }

        return false;
    }

    // PARLOT102: an alternative which can't tell which chars it can start with is tried for every char
    private void ReportUnseekableAlternatives(object choice, IReadOnlyList<object> alternatives, List<int> candidates)
    {
        foreach (var index in candidates)
        {
            var alternative = alternatives[index];

            if (alternative is ISeekable { CanSeek: true } || FindUnseekableCause(alternative) is not { } cause)
            {
                continue;
            }

            Report(GrammarRule.UnseekableAlternative, choice,
                $"In {DescribeChoice(choice, alternatives)}, alternative {index + 1} ({Describe(alternative)}) is tried whatever the next char is: {cause}. Wrap the alternative in Lookup with the chars it can start with, or use a parser that tells them.");
        }
    }

    /// <summary>
    /// Finds why an alternative can't tell which chars it can start with, or returns <see langword="null"/> when it can
    /// succeed without reading anything, or when another rule explains it.
    /// </summary>
    private string? FindUnseekableCause(object alternative)
    {
        // An alternative that can succeed without reading anything is a fallback by design, or is reported by PARLOT112
        if (IsNullable(alternative) && !ChainHasUnknownParser(alternative))
        {
            return null;
        }

        var parser = alternative;

        for (var depth = 0; depth < 64; depth++)
        {
            if (parser is IChoiceParser)
            {
                // A nested choice reports its own alternatives
                return null;
            }

            if (parser is IDeferredParser && _starts.TryGetValue(parser, out var node) && node.Cyclic)
            {
                // Reported by PARLOT111
                return null;
            }

            if (parser is not ILoopAnalyzable)
            {
                return $"{Describe(parser)} is a parser type that doesn't implement ISeekable";
            }

            if (!ReferenceEquals(parser, alternative) && IsNullable(parser))
            {
                return $"it starts with {Describe(parser)}, which can succeed without reading anything";
            }

            if (FirstInvoked(parser) is not { } first)
            {
                return ParlotTypeName(parser) switch
                {
                    "PatternLiteral" => $"{Describe(parser)} tests the chars with a predicate; AnyOf, NoneOf and Identifier tell the chars they accept",
                    "SpanScannerLiteral" => $"{Describe(parser)} has no start chars; pass the chars a token can start with to Scan",
                    "TextLiteral" => $"{Describe(parser)} uses a culture-sensitive comparison; use an ordinal one",
                    _ => $"{Describe(parser)} doesn't tell which chars it can start with",
                };
            }

            if (first is ISeekable { CanSeek: true })
            {
                return FindStaleDeferred(parser) is { } deferred
                    ? $"{Describe(parser)} was built before the parser of {Describe(deferred)} was set, and kept the hints it had then"
                    : $"{Describe(parser)} doesn't tell which chars {Describe(first)} starts with";
            }

            parser = first;
        }

        return null;
    }

    // PARLOT103: alternatives starting with the same parser parse it again when an earlier one fails after it
    private void ReportSharedLeadingParsers(object choice, IReadOnlyList<object> alternatives, List<int> candidates)
    {
        var chains = new List<List<object>>();

        foreach (var index in candidates)
        {
            chains.Add(Chain(alternatives[index]));
        }

        var shared = new List<(object Parser, List<int> Alternatives)>();

        for (var b = 1; b < candidates.Count; b++)
        {
            for (var a = 0; a < b; a++)
            {
                foreach (var parser in chains[b])
                {
                    if (!ContainsReference(chains[a], parser))
                    {
                        continue;
                    }

                    if (IsCostly(parser))
                    {
                        var entry = shared.Find(x => ReferenceEquals(x.Parser, parser));

                        if (entry.Parser is null)
                        {
                            entry = (parser, []);
                            shared.Add(entry);
                        }

                        AddUnique(entry.Alternatives, candidates[a]);
                        AddUnique(entry.Alternatives, candidates[b]);
                    }

                    // The first parser they share is the one parsed again, the next ones are part of it
                    break;
                }
            }
        }

        foreach (var (parser, indexes) in shared)
        {
            indexes.Sort();

            var numbers = new List<string>();

            foreach (var index in indexes)
            {
                numbers.Add((index + 1).ToString(CultureInfo.InvariantCulture));
            }

            Report(GrammarRule.SharedLeadingParser, choice,
                $"In {DescribeChoice(choice, alternatives)}, alternatives {Join(numbers)} start with {Describe(parser)}, which is parsed again at the same position when an alternative fails after it. Parse it once before choosing what follows it, like a.And(b.Or(c)) instead of a.And(b).Or(a.And(c)), or use LeftAssociative for operators.");
        }
    }

    private void AnalyzeDeferred(object deferred, HashSet<int> reportedCycles)
    {
        if (!_starts.TryGetValue(deferred, out var node))
        {
            return;
        }

        if (node.Cyclic)
        {
            // PARLOT111, once for the parsers of a cycle
            if (reportedCycles.Add(node.Component))
            {
                Report(GrammarRule.LeftRecursion, deferred,
                    $"{Capitalize(Describe(deferred))} can invoke itself before reading anything ({DescribeCycle(node)}). There it fails instead of recursing, so the alternatives that start with it never match. Write the recursion as a loop, with LeftAssociative, ZeroOrMany or Separated.");
            }
        }
        else if (node.ReachesOpaque && FindOpaque(node) is { } opaque)
        {
            // PARLOT104
            Report(GrammarRule.LoopDetection, deferred,
                $"{Capitalize(Describe(deferred))} looks for itself in the active parsers each time it is invoked, because it can invoke {Describe(opaque.Parser)} before reading anything, a parser type whose invocations Parlot can't analyze. Read something before invoking it, or pass disableLoopDetection: true to the ParseContext if the grammar has no left recursion.");
        }
    }

    // The parsers of a cycle of invocations at the start, from a deferred parser back to itself
    private string DescribeCycle(LoopAnalysis.Node start)
    {
        var previous = new Dictionary<LoopAnalysis.Node, LoopAnalysis.Node>();
        var pending = new Queue<LoopAnalysis.Node>();
        LoopAnalysis.Node? last = null;

        pending.Enqueue(start);

        while (pending.Count > 0 && last is null)
        {
            var node = pending.Dequeue();

            foreach (var next in node.Edges)
            {
                if (next.Component != start.Component)
                {
                    continue;
                }

                if (next == start)
                {
                    last = node;
                    break;
                }

                if (!previous.ContainsKey(next))
                {
                    previous[next] = node;
                    pending.Enqueue(next);
                }
            }
        }

        var path = new List<string> { Describe(start.Parser) };

        for (var node = last; node is not null && node != start; node = previous.TryGetValue(node, out var p) ? p : null)
        {
            // Only the parsers with a name tell the reader something
            if (node.Parser is IDeferredParser || GetName(node.Parser) is not null)
            {
                path.Insert(1, Describe(node.Parser));
            }
        }

        path.Add(Describe(start.Parser));

        var description = string.Join(" -> ", path);

        // Deferred parsers without a name all look the same
        return path.Count > 2 && description.Contains("Deferred<") ? description + "; name them with Named() to tell them apart" : description;
    }

    private static LoopAnalysis.Node? FindOpaque(LoopAnalysis.Node start)
    {
        var seen = new HashSet<LoopAnalysis.Node> { start };
        var pending = new Queue<LoopAnalysis.Node>();

        pending.Enqueue(start);

        while (pending.Count > 0)
        {
            var node = pending.Dequeue();

            if (node.Opaque)
            {
                return node;
            }

            foreach (var next in node.Edges)
            {
                if (seen.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Returns why <paramref name="later"/> can't match when <paramref name="earlier"/> is tried first, or <see langword="null"/>.
    /// </summary>
    private string? FindShadowing(object earlier, object later)
    {
        if (ReferenceEquals(earlier, later))
        {
            return "it is the same parser as an earlier alternative";
        }

        // The later alternative must first match a parser which the earlier alternative is, up to conversions
        var equivalents = Equivalents(earlier);

        foreach (var parser in Chain(later))
        {
            if (ContainsReference(equivalents, parser))
            {
                return ReferenceEquals(parser, later)
                    ? "it matches exactly what an earlier alternative matches"
                    : $"it starts with {Describe(parser)}";
            }
        }

        // The later alternative only matches texts that start with the text the earlier one matches
        if (FixedText(earlier) is not { } text || later is not ISeekable { CanSeek: true } seekable || (seekable.SkipWhitespace && !text.SkipsWhiteSpace))
        {
            return null;
        }

        var prefixes = SeekableHints.Prefixes(later);

        if (prefixes is null)
        {
            if (Array.IndexOf(seekable.ExpectedChars, Fluent.OneOf<object>.OtherSeekableChar) >= 0)
            {
                return null;
            }

            prefixes = new string[seekable.ExpectedChars.Length];

            for (var i = 0; i < prefixes.Length; i++)
            {
                prefixes[i] = seekable.ExpectedChars[i].ToString();
            }
        }

        if (prefixes.Length == 0)
        {
            return null;
        }

        foreach (var prefix in prefixes)
        {
            if (!prefix.StartsWith(text.Text, text.Comparison))
            {
                return null;
            }
        }

        return $"it only matches a text that starts with {Quote(text.Text)}";
    }

    /// <summary>
    /// Returns the text a parser matches whenever the input starts with it, with nothing else to test.
    /// </summary>
    private (string Text, StringComparison Comparison, bool SkipsWhiteSpace)? FixedText(object parser)
    {
        var skipsWhiteSpace = false;

        for (var depth = 0; depth < 16; depth++)
        {
            switch (parser)
            {
                case TextLiteral text when text.Text.Length > 0 && text.ComparisonType is StringComparison.Ordinal or StringComparison.OrdinalIgnoreCase:
                    return skipsWhiteSpace && char.IsWhiteSpace(text.Text[0]) ? null : (text.Text, text.ComparisonType, skipsWhiteSpace);
                case CharLiteral literal:
                    return skipsWhiteSpace && char.IsWhiteSpace(literal.Char) ? null : (literal.Char.ToString(), StringComparison.Ordinal, skipsWhiteSpace);
            }

            var name = ParlotTypeName(parser);

            if (name is not null && (s_transparent.Contains(name) || name == "SkipWhiteSpace") && FirstInvoked(parser) is { } inner)
            {
                skipsWhiteSpace |= name == "SkipWhiteSpace";
                parser = inner;
                continue;
            }

            return null;
        }

        return null;
    }

    private Outcome GetOutcome(object parser, int depth)
    {
        if (depth > 32)
        {
            return Outcome.CanFail;
        }

        var name = ParlotTypeName(parser);

        if (name is null)
        {
            return Outcome.CanFail;
        }

        if (s_neverFail.Contains(name))
        {
            return Outcome.NeverFails;
        }

        switch (name)
        {
            case "ElseError":
                return Outcome.ThrowsOnFailure;

            // A deferred parser fails where it is invoked again, so it isn't followed
            case "Then" or "Discard" or "Capture" or "SkipWhiteSpace" or "IParserAdapter" or "OperatorIndex":
                return FirstInvoked(parser) is { } inner ? GetOutcome(inner, depth + 1) : Outcome.CanFail;

            case "Sequence" or "SequenceAndSkip" or "SequenceSkipAnd":
                var outcome = Outcome.NeverFails;

                foreach (var child in _children[parser])
                {
                    switch (GetOutcome(child, depth + 1))
                    {
                        case Outcome.CanFail:
                            return Outcome.CanFail;
                        case Outcome.ThrowsOnFailure:
                            outcome = Outcome.ThrowsOnFailure;
                            break;
                    }
                }

                return outcome;

            default:
                return Outcome.CanFail;
        }
    }

    /// <summary>
    /// Returns the parsers that <paramref name="parser"/> invokes first, where it is invoked, and which must succeed for it
    /// to succeed: itself, the first parser of a sequence, the parser a conversion converts... A choice ends it.
    /// </summary>
    private List<object> Chain(object parser)
    {
        var chain = new List<object> { parser };

        while (parser is not IChoiceParser && ParlotTypeName(parser) is { } name && s_firstMustSucceed.Contains(name) &&
            FirstInvoked(parser) is { } first && !ContainsReference(chain, first))
        {
            chain.Add(first);
            parser = first;
        }

        return chain;
    }

    /// <summary>
    /// Returns the parsers which succeed exactly when <paramref name="parser"/> does: itself, the parser a conversion
    /// converts, the parser of a deferred one...
    /// </summary>
    private List<object> Equivalents(object parser)
    {
        var equivalents = new List<object> { parser };

        while (ParlotTypeName(parser) is { } name && s_transparent.Contains(name) && FirstInvoked(parser) is { } inner && !ContainsReference(equivalents, inner))
        {
            equivalents.Add(inner);
            parser = inner;
        }

        return equivalents;
    }

    // Whether parsing a parser again costs more than reading a fixed text again
    private bool IsCostly(object parser)
    {
        for (var depth = 0; depth < 16; depth++)
        {
            if (parser is TextLiteral or CharLiteral or KeywordLiteral)
            {
                return false;
            }

            if (ParlotTypeName(parser) is "Then" or "Discard" or "Capture" or "SkipWhiteSpace" or "IParserAdapter" or "OperatorIndex" && FirstInvoked(parser) is { } inner)
            {
                parser = inner;
                continue;
            }

            break;
        }

        return parser is not ILoopAnalyzable || !IsNullable(parser);
    }

    private bool ChainHasUnknownParser(object parser)
    {
        foreach (var item in Chain(parser))
        {
            if (item is not ILoopAnalyzable)
            {
                return true;
            }
        }

        return false;
    }

    private object? FirstInvoked(object parser) => _starts.TryGetValue(parser, out var node) && node.Edges.Count > 0 ? node.Edges[0].Parser : null;

    private bool IsNullable(object parser) => _starts.TryGetValue(parser, out var node) && node.Nullable;

    private void Report(GrammarRule rule, object parser, string message) => _findings.Add(new GrammarFinding(rule, parser, message));

    private string DescribeChoice(object choice, IReadOnlyList<object> alternatives)
    {
        if (GetName(choice) is { } name)
        {
            return $"the choice '{name}'";
        }

        var parent = _parents[choice];

        if (parent is not null && ParlotTypeName(parent) is { } parentName && s_operatorParsers.Contains(parentName))
        {
            return $"the operators of {Describe(parent)}{Where(parent)}";
        }

        return $"the choice {Describe(alternatives[0])}.Or({Describe(alternatives[1])}){(alternatives.Count > 2 ? ".Or(...)" : "")}{Where(choice)}";
    }

    // Where a parser is, from the closest parser with a name that leads to it
    private string Where(object parser)
    {
        for (var parent = _parents[parser]; parent is not null; parent = _parents[parent])
        {
            if (GetName(parent) is not null || parent is IDeferredParser)
            {
                return $" in {Describe(parent)}";
            }
        }

        return "";
    }

    private string Describe(object parser) => Describe(parser, 0);

    private string Describe(object parser, int depth)
    {
        if (GetName(parser) is { } name)
        {
            return $"'{name}'";
        }

        if (depth > 8)
        {
            return "...";
        }

        if (Literal(parser) is { } literal)
        {
            return "Literals." + literal;
        }

        var typeName = ParlotTypeName(parser);
        var first = FirstInvoked(parser);

        switch (typeName)
        {
            case "SkipWhiteSpace" when first is not null && Literal(first) is { } term:
                return "Terms." + term;
            case "Then" or "Discard" or "IParserAdapter" or "OperatorIndex" when first is not null:
                return Describe(first, depth + 1);
            case "Sequence" when first is not null:
                return Describe(first, depth + 1) + ".And(...)";
            case "SequenceAndSkip" when first is not null:
                return Describe(first, depth + 1) + ".AndSkip(...)";
            case "SequenceSkipAnd" when first is not null:
                return Describe(first, depth + 1) + ".SkipAnd(...)";
            case "Deferred":
                return $"Deferred<{StripArity(parser.GetType().GetGenericArguments()[0].Name)}>";
            case not null when first is not null && parser is not IChoiceParser:
                return $"{typeName}({Describe(first, depth + 1)})";
        }

        return typeName ?? StripArity(parser.GetType().Name);
    }

    // The call that builds one of Parlot's literals, after Literals. or Terms.
    private static string? Literal(object parser)
    {
        switch (parser)
        {
            case TextLiteral text:
                return $"Text({Quote(text.Text)}{(text.ComparisonType == StringComparison.Ordinal ? "" : ", caseInsensitive: true")})";
            case KeywordLiteral keyword:
                return $"Keyword({Quote(keyword.Text)})";
            case CharLiteral literal:
                return $"Char({QuoteChar(literal.Char)})";
        }

        return ParlotTypeName(parser) switch
        {
            "PatternLiteral" => "Pattern(...)",
            "SpanScannerLiteral" => "Scan(...)",
            "IdentifierLiteral" or "Identifier" => "Identifier(...)",
            "StringLiteral" => "String(...)",
            "WhiteSpaceLiteral" => "WhiteSpace(...)",
            "NonWhiteSpaceLiteral" => "NonWhiteSpace(...)",
            "ListOfCharsLiteral" or "SearchValuesCharLiteral" => "AnyOf(...)",
            "NumberLiteral" or "RadixNumberLiteral" => "Number(...)",
            _ => null,
        };
    }

    private string? GetName(object parser)
    {
        var type = parser.GetType();

        if (!_nameProperties.TryGetValue(type, out var property))
        {
            var current = type;

            while (current is not null && !IsParserBase(current))
            {
                current = current.BaseType;
            }

            property = current?.GetProperty(nameof(Parser<object>.Name));
            _nameProperties.Add(type, property);
        }

        // Some of Parlot's parsers are named after their type when they are built
        return property?.GetValue(parser) is string { Length: > 0 } name && !(ParlotTypeName(parser) is not null && s_defaultNames.Contains(name)) ? name : null;
    }

    private bool IsParser(Type type)
    {
        if (!_parserTypes.TryGetValue(type, out var isParser))
        {
            for (var current = type; current is not null && !isParser; current = current.BaseType)
            {
                isParser = IsParserBase(current);
            }

            _parserTypes.Add(type, isParser);
        }

        return isParser;
    }

    private static bool IsParserBase(Type type) => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Parser<>);

    // The name of a Parlot parser type without its generic arity, or null for the types of other assemblies
    private static string? ParlotTypeName(object parser) => parser.GetType().Assembly == s_parlot ? StripArity(parser.GetType().Name) : null;

    private static string StripArity(string name)
    {
        var index = name.IndexOf('`');

        return index < 0 ? name : name.Substring(0, index);
    }

    private static bool ContainsReference(List<object> list, object item)
    {
        foreach (var element in list)
        {
            if (ReferenceEquals(element, item))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddUnique(List<int> list, int item)
    {
        if (!list.Contains(item))
        {
            list.Add(item);
        }
    }

    private static string Join(List<string> items)
    {
        if (items.Count == 1)
        {
            return items[0];
        }

        var listed = Math.Min(items.Count, MaxListed);
        var builder = new StringBuilder();

        for (var i = 0; i < listed; i++)
        {
            if (i > 0)
            {
                builder.Append(i == listed - 1 && listed == items.Count ? " and " : ", ");
            }

            builder.Append(items[i]);
        }

        if (listed < items.Count)
        {
            builder.Append(" and ").Append(items.Count - listed).Append(" more");
        }

        return builder.ToString();
    }

    private static string Plural(int count, string singular, string plural) => count == 1 ? singular : plural;

    private static string Capitalize(string text) => text.Length > 0 && char.IsLower(text[0]) ? char.ToUpperInvariant(text[0]) + text.Substring(1) : text;

    private static string Quote(string text)
    {
        var builder = new StringBuilder("\"");

        foreach (var c in text)
        {
            builder.Append(Escape(c, '"'));
        }

        return builder.Append('"').ToString();
    }

    private static string QuoteChar(char c) => "'" + Escape(c, '\'') + "'";

    private static string Escape(char c, char quote) => c switch
    {
        '\\' => "\\\\",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        _ when c == quote => "\\" + c,
        _ when char.IsControl(c) => "\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture),
        _ => c.ToString(),
    };
}
