using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

/// <summary>
/// OneOf the inner choices when all parsers return the same type.
/// We then return the actual result of each parser.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class OneOf<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable /**/, ISpanSourceable /**/
{
    // Used as a lookup for OneOf<T> to find other OneOf<T> parsers that could
    // be invoked when there is no match.

    public const char OtherSeekableChar = '\0';
    // The lookup groups are arrays, iterated without the bounds and version checks of List<T>.
    // They are rebuilt by EnsureResolved() when an alternative declared its hints late.
    internal CharMap<Parser<T>[]>? _map;
    internal Parser<T>[]? _otherParsers;

    // The lookup table of Parse(): _map itself, or a copy where the chars whose alternatives tell longer prefixes have an
    // empty group, and the trie of _prefixMap selects their alternatives on the rest of the input. No other char has an
    // empty group, so Parse() only tests the length it reads anyway.
    private CharMap<Parser<T>[]>? _parseMap;
    private CharMap<PrefixNode>? _prefixMap;

    // The prefixes of a resolved table, see ISeekablePrefixes.ExpectedPrefixes
    private string[]? _expectedPrefixes;
    private bool _expectedPrefixesComputed;

    // Parsers as an array, to iterate over it without the interface calls of IReadOnlyList<T>
    private Parser<T>[] _parsers;
    private readonly Parser<T>[] _originalParsers;

    // Whether the lookup table reflects the hints of every alternative, see EnsureResolved()
    private bool _resolved;
    private bool _resolving;

    // For compilation, ignored for now
    //private readonly CharMap<Func<ParseContext, ValueTuple<bool, T>>> _lambdaMap = new();
    //private Func<ParseContext, ValueTuple<bool, T>>? _lambdaOtherParsers;

    public OneOf(Parser<T>[] parsers)
    {
        _originalParsers = _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        OriginalParsers = Parsers = parsers;

        Build();

        // An alternative that can't tell what it starts with may be a Deferred whose parser is set later, or
        // a choice holding one, so the table is built again from the hints they declare once parsing starts
        _resolved = _originalParsers.Length <= 1 || !_originalParsers.Any(static x => x is ISeekable seekable && !IsResolved(seekable));

        static bool IsResolved(ISeekable seekable) => seekable.CanSeek && Array.IndexOf(seekable.ExpectedChars, OtherSeekableChar) < 0;
    }

    /// <summary>
    /// Builds the lookup table from the hints the alternatives declare once they are all known.
    /// </summary>
    /// <remarks>
    /// It is called before the first parse, and by the source generator, when an alternative couldn't declare
    /// its hints when this parser was built.
    /// </remarks>
    internal void EnsureResolved()
    {
        if (Volatile.Read(ref _resolved))
        {
            return;
        }

        // A single lock for every choice and deferred parser, since a table can only be built once the
        // nested ones are, and two threads resolving a cycle from different ends would otherwise wait for each other
        lock (SeekableResolution.SyncRoot)
        {
            // A choice reached again through a deferred parser while it is resolved keeps its current hints
            if (_resolved || _resolving)
            {
                return;
            }

            _resolving = true;

            try
            {
                // Nested choices are flattened into this table, so they declare their hints first
                foreach (var parser in _originalParsers)
                {
                    if (parser is OneOf<T> oneOf)
                    {
                        oneOf.EnsureResolved();
                    }
                }

                Build();

                Volatile.Write(ref _resolved, true);
            }
            finally
            {
                _resolving = false;
            }
        }
    }

    private void Build()
    {
        var parsers = _originalParsers;

        Parsers = _parsers = parsers;
        _map = null;
        _otherParsers = null;
        _parseMap = null;
        _prefixMap = null;
        CanSeek = false;
        ExpectedChars = [];
        SkipWhitespace = false;

        static void AddUniqueRange(List<Parser<T>> target, IReadOnlyList<Parser<T>> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                var exists = false;
                for (var j = 0; j < target.Count; j++)
                {
                    if (ReferenceEquals(target[j], item))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    target.Add(item);
                }
            }
        }

        static void AddUniqueSingle(List<Parser<T>> target, Parser<T> item)
        {
            for (var j = 0; j < target.Count; j++)
            {
                if (ReferenceEquals(target[j], item))
                {
                    return;
                }
            }

            target.Add(item);
        }

        static void RemoveSkipWhiteSpace(List<Parser<T>> target)
        {
            for (var j = 0; j < target.Count; j++)
            {
                if (target[j] is SkipWhiteSpace<T> skip)
                {
                    target[j] = skip.Parser;
                }
            }
        }

        // We can't build a lookup table if there is only one parser
        if (parsers.Length <= 1)
        {
            return;
        }

        // Technically we shouldn't be able to build a lookup table if not all parsers are seekable.
        // For instance, "or" | identifier is not seekable since 'identifier' is not seekable.
        // Also the order of the parsers need to be conserved in the lookup table results.
        // The solution is to add the parsers that are not seekable in all the groups, and
        // keep the relative order of each. So if the parser p1, p2 ('a'), p3, p4 ('b') are available,
        // the group 'a' will have p1, p2, p3 and the group 'b' will have p1, p3, p4.
        // And then when we parse, we need to try the non-seekable parsers when the _map doesn't have the character to lookup.

        // NB:We could extract the lookup logic as a separate parser and then have a each lookup group as a OneOf one.

        if (parsers.Any(x => x is ISeekable seekable))
        {
            // The groups are built as lists and converted to arrays once, at the end
            List<Parser<T>>? otherParsers = null;

            var lookupTable = parsers
                            .Where(p => p is ISeekable seekable && seekable.CanSeek)
                            .Cast<ISeekable>()
                            .SelectMany(s => s.ExpectedChars.Select(x => (Key: x, Parser: (Parser<T>)s)))
                            .GroupBy(s => s.Key)
                            .ToDictionary(group => group.Key, group => new List<Parser<T>>());

            foreach (var parser in parsers)
            {
                if (parser is ISeekable seekable && seekable.CanSeek)
                {
                    foreach (var c in seekable.ExpectedChars)
                    {
                        IReadOnlyList<Parser<T>> subParsers = parser is OneOf<T> oneof ? oneof._map?[c] ?? [parser] : [parser];

                        if (c != OtherSeekableChar)
                        {
                            AddUniqueRange(lookupTable[c], subParsers);
                        }
                        else
                        {
                            otherParsers ??= [];
                            AddUniqueRange(otherParsers, subParsers);

                            foreach (var entry in lookupTable)
                            {
                                AddUniqueRange(entry.Value, subParsers);
                            }
                        }
                    }
                }
                else
                {
                    otherParsers ??= [];
                    AddUniqueSingle(otherParsers, parser);

                    foreach (var entry in lookupTable)
                    {
                        AddUniqueSingle(entry.Value, parser);
                    }
                }
            }

            // If only some parser use SkipWhiteSpace, we can't use a lookup table
            // Meaning, All/None is fine, but not Any
            if (parsers.All(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                SkipWhitespace = true;

                // Remove the SkipWhiteSpace parser if we can
                Parsers = _parsers = parsers.Select(x => x is SkipWhiteSpace<T> skip ? skip.Parser : x).ToArray();

                // The lookup groups are the ones invoked when there is a map, and they were built from the original parsers
                foreach (var entry in lookupTable)
                {
                    RemoveSkipWhiteSpace(entry.Value);
                }

                if (otherParsers != null)
                {
                    RemoveSkipWhiteSpace(otherParsers);
                }
            }
            else if (parsers.Any(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                // There is a mix of parsers that can skip whitespaces.
                // If the ones that can skip don't have a custom WS parser then
                // we can group them in a special OneOf and add them to a lookup
                // with space chars.

                // But there are still cases where this can't be done, like if the
                // parsers have a space in their lookup table, or if non-seekable
                // accept a space. So we can't always redirect spaces automatically.

                lookupTable = null;
            }

            lookupTable?.Remove(OtherSeekableChar);

            _otherParsers = otherParsers?.ToArray();

            if (lookupTable?.Count > 0)
            {
                _map = new CharMap<Parser<T>[]>(lookupTable.Select(static entry => new KeyValuePair<char, Parser<T>[]>(entry.Key, entry.Value.ToArray())));

                // This parser is only seekable if there isn't a parser
                // that can't be reached without a lookup.
                // However we use a trick to match other parsers
                // by assigning them to `OtherSeekableChar` such that
                // we can pass on this collection through parsers
                // that forward the ISeekable implementation (e.g. Error, Then)
                // This way we make more OneOf parsers seekable.

                if (_otherParsers == null)
                {
                    CanSeek = true;
                    ExpectedChars = _map.ExpectedChars;
                }
                else
                {
                    CanSeek = true;
                    ExpectedChars = [.. _map.ExpectedChars, OtherSeekableChar];
                    _map.Set(OtherSeekableChar, _otherParsers);
                }

                BuildPrefixMaps(_map);
            }
        }
    }

    private void BuildPrefixMaps(CharMap<Parser<T>[]> map)
    {
        _parseMap = map;

        List<KeyValuePair<char, PrefixNode>>? nodes = null;

        foreach (var c in map.ExpectedChars)
        {
            // Every choice is built through here, and most chars have fewer alternatives than a dispatch needs
            if (c != OtherSeekableChar && map[c] is { Length: >= PrefixDispatch.MinimumAlternatives } parsers && PrefixDispatch.TryCreate(c, parsers) is { } dispatch)
            {
                nodes ??= [];
                nodes.Add(new KeyValuePair<char, PrefixNode>(c, dispatch.CreateNode()));
            }
        }

        if (nodes != null)
        {
            _prefixMap = new CharMap<PrefixNode>(nodes);
            _parseMap = WithoutAlternatives(map, nodes);
        }
    }

    // A method of its own, so that the closures are only allocated for the choices which have a prefix map
    private static CharMap<Parser<T>[]> WithoutAlternatives(CharMap<Parser<T>[]> map, List<KeyValuePair<char, PrefixNode>> nodes)
    {
        return new CharMap<Parser<T>[]>(map.ExpectedChars.Select(c => new KeyValuePair<char, Parser<T>[]>(c, nodes.Exists(node => node.Key == c) ? [] : map[c]!)));
    }

    public bool CanSeek { get; private set; }

    public char[] ExpectedChars { get; private set; } = [];

    public bool SkipWhitespace { get; private set; }

    // The prefixes of the alternatives each char of the lookup table selects
    string[]? ISeekablePrefixes.ExpectedPrefixes
    {
        get
        {
            // A table which isn't resolved yet tells no prefixes rather than being resolved here: when they are read
            // through a deferred parser, which can't tell its hints meanwhile, the table would treat it as unseekable
            if (!Volatile.Read(ref _resolved))
            {
                return null;
            }

            // A resolved table doesn't change, and the prefixes of nested choices are read by each choice that holds them
            if (!Volatile.Read(ref _expectedPrefixesComputed))
            {
                _expectedPrefixes = ComputeExpectedPrefixes();
                Volatile.Write(ref _expectedPrefixesComputed, true);
            }

            return _expectedPrefixes;
        }
    }

    private string[]? ComputeExpectedPrefixes()
    {
        var map = _map;

        // The choice can start with any text when one of its alternatives can
        if (!CanSeek || map == null || _otherParsers != null)
        {
            return null;
        }

        var prefixes = new List<string>();

        foreach (var c in map.ExpectedChars)
        {
            foreach (var parser in map[c]!)
            {
                foreach (var prefix in SeekableHints.Prefixes(parser, c))
                {
                    if (!prefixes.Contains(prefix))
                    {
                        prefixes.Add(prefix);
                    }
                }
            }
        }

        return [.. prefixes];
    }

    /// <summary>
    /// Gets the parsers before they get SkipWhitespace removed.
    /// </summary>
    public IReadOnlyList<Parser<T>> OriginalParsers { get; }

    public IReadOnlyList<Parser<T>> Parsers { get; private set; }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        // The lookup tables only hold these parsers, or the ones they invoke at their start
        var nullable = false;

        foreach (var parser in _originalParsers)
        {
            nullable |= analysis.Invoke(parser);
        }

        return nullable;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (!Volatile.Read(ref _resolved))
        {
            EnsureResolved();
        }

        if (context.IsCompacting)
        {
            return ParseCompacting(context, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        // The position is only needed to revert the whitespaces this parser skipped, the sub-parsers
        // revert their own state.
        var start = 0;

        // If all sub-parsers skip whitespaces, do it once here
        if (SkipWhitespace)
        {
            start = cursor.Offset;
            context.SkipWhiteSpace();
        }

        var map = _parseMap;

        if (map != null)
        {
            // Each lookup entry also contains the non-seekable parsers
            var seekableParsers = map[cursor.Current] ?? _otherParsers;

            if (seekableParsers != null)
            {
                if (seekableParsers.Length == 0)
                {
                    seekableParsers = SelectByPrefix(cursor);
                }

                // The length is read after the selection, so that the loop doesn't check the bounds of the array
                var length = seekableParsers.Length;

                for (var i = 0; i < length; i++)
                {
                    if (seekableParsers[i].Parse(context, ref result))
                    {
                        context.ExitParser(this);
                        return true;
                    }
                }
            }
        }
        else
        {
            var parsers = _parsers;

            for (var i = 0; i < parsers.Length; i++)
            {
                if (parsers[i].Parse(context, ref result))
                {
                    context.ExitParser(this);
                    return true;
                }
            }
        }

        // We only need to reset the position if we are skipping whitespaces
        // as the parsers would have reverted their own state

        if (SkipWhitespace)
        {
            cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    // The alternatives of the char are selected again by the prefix the input starts with. Inlined, the lookup would grow
    // the frame of Parse(), which every choice pays for.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private Parser<T>[] SelectByPrefix(Cursor cursor) => _prefixMap![cursor.Current]!.Select(cursor);

    private bool ParseCompacting(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = 0;

        if (SkipWhitespace)
        {
            start = cursor.Offset;
            context.SkipWhiteSpace();
        }

        // The lookup map releases the text as soon as a single alternative remains. The window may not hold the text
        // after the current char, so the alternatives aren't selected again by their prefixes.
        var parsers = _map != null ? _map[cursor.Current] ?? _otherParsers : _parsers;

        if (parsers != null)
        {
            var length = parsers.Length;

            // Each alternative is read from the same position
            var pin = length > 1 ? context.Pin() : -1;

            for (var i = 0; i < length; i++)
            {
                if (i == length - 1)
                {
                    context.Unpin(pin);
                }

                if (parsers[i].Parse(context, ref result))
                {
                    context.Unpin(pin);
                    context.ExitParser(this);
                    return true;
                }
            }
        }

        if (SkipWhitespace)
        {
            cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public override string ToString() => $"{string.Join(" | ", Parsers)} on [{string.Join(" ", ExpectedChars)}]";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // The factory has run to completion, so every alternative can declare its hints
        EnsureResolved();

        if (SpanSource.TryGenerate(this, typeof(T), context) is { } spanResult)
        {
            return spanResult;
        }

        var choiceResult = KeywordChoiceSource.TryGenerate(Parsers, SkipWhitespace, context)
            ?? TextChoiceSource.TryGenerate(Parsers, SkipWhitespace, context);

        if (choiceResult != null)
        {
            // These read the cursor directly
            return context.IsCompacting
                ? context.GenerateToken(this, typeof(T), () => GenerateSource(context))
                : choiceResult;
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;

        var valueTypeName = result.ValueTypeName ?? SourceGenerationContext.GetTypeName(typeof(T));

        string GetHelper(Parser<T> parser)
        {
            if (parser is not ISourceable sourceable)
            {
                throw new NotSupportedException("OneOf requires all parsers to be source-generatable.");
            }

            var (methodName, _, _, _) = context.Helpers.GetOrCreate(
                parser,
                $"{context.MethodNamePrefix}_OneOf",
                valueTypeName,
                () => sourceable.GenerateSource(context));

            return methodName;
        }

        string? startName = null;

        // The dispatch methods of PrefixDispatch, as local functions after the statements
        var localFunctions = new List<string>();

        if (SkipWhitespace)
        {
            startName = $"start{context.NextNumber()}";
            result.Body.Add($"var {startName} = {cursorName}.Offset;");
            result.Body.Add($"{ctx}.SkipWhiteSpace();");
        }

        // If a lookup map was built, emit a switch-based dispatch that only tries
        // the relevant sub-parsers for the current char (mirroring the runtime path).
        if (_map != null && _map.ExpectedChars.Length > 0)
        {
            var currentName = $"current{context.NextNumber()}";
            result.Body.Add($"var {currentName} = {cursorName}.Current;");

            // Group characters that share the same parser list (by contents) to avoid duplicating code.
            var groups = new Dictionary<ParserListKey, ParserGroup>(ParserListKeyComparer.Instance);

            // The chars whose alternatives tell longer prefixes are dispatched again on these
            var dispatches = new List<(char Char, PrefixDispatch Dispatch)>();

            foreach (var ch in _map.ExpectedChars)
            {
                // Skip '\0' as it represents "no match" / default case
                if (ch == '\0')
                {
                    continue;
                }

                var parsersForChar = _map[ch];
                if (parsersForChar is null)
                {
                    continue;
                }

                // A dispatch reads past the current char, which a compacting buffer may not have read yet
                if (!context.IsCompacting && PrefixDispatch.TryCreate(ch, parsersForChar) is { } dispatch)
                {
                    dispatches.Add((ch, dispatch));
                    continue;
                }

                var key = new ParserListKey(parsersForChar);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ParserGroup(parsersForChar);
                    groups.Add(key, group);
                }

                group.Chars.Add(ch);
            }

            ParserGroup? defaultGroup = null;
            if (_otherParsers != null)
            {
                var key = new ParserListKey(_otherParsers);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ParserGroup(_otherParsers);
                    groups.Add(key, group);
                }

                group.IsDefault = true;
                defaultGroup = group;
            }

            result.Body.Add($"switch ({currentName})");
            result.Body.Add("{");

            foreach (var group in groups.Values)
            {
                if (group.Chars.Count == 0)
                {
                    continue;
                }

                if (context.SupportsCSharp9SwitchPatterns)
                {
                    // Use pattern matching to reduce the number of case labels, including range detection.
                    // Example: case '.' or >= '0' and <= '9' or 'E' or 'e':
                    var pattern = BuildCharPattern(group.Chars, rangeThreshold: 4);
                    result.Body.Add($"    case {pattern}:");
                }
                else
                {
                    foreach (var ch in group.Chars)
                    {
                        result.Body.Add($"    case {ToCharLiteral(ch)}:");
                    }
                }

                result.Body.Add("    {");
                EmitParsers(group.Parsers, result, ctx, indent: "        ", getHelper: GetHelper, context);
                result.Body.Add("        break;");
                result.Body.Add("    }");
            }

            foreach (var (ch, dispatch) in dispatches)
            {
                // The rest of the input selects the alternatives whose prefix it starts with
                var dispatchName = $"DispatchPrefix{context.NextNumber()}";

                result.Body.Add($"    case {ToCharLiteral(ch)}:");
                result.Body.Add("    {");
                result.Body.Add($"        switch ({dispatchName}({cursorName}.Span.Slice(1)))");
                result.Body.Add("        {");

                for (var i = 0; i < dispatch.Groups.Count; i++)
                {
                    result.Body.Add($"            case {i}:");
                    result.Body.Add("            {");
                    EmitParsers(dispatch.Groups[i], result, ctx, indent: "                ", getHelper: GetHelper, context);
                    result.Body.Add("                break;");
                    result.Body.Add("            }");
                }

                result.Body.Add("        }");
                result.Body.Add("        break;");
                result.Body.Add("    }");

                localFunctions.Add($"static int {dispatchName}(System.ReadOnlySpan<char> text)");
                localFunctions.Add("{");
                localFunctions.Add(KnownStringLookup.GeneratePrefixes(dispatch.Suffixes,
                    index => dispatch.GroupIndexes[index].ToString(CultureInfo.InvariantCulture),
                    failureExpression: "-1", resultType: "int"));
                localFunctions.Add("}");
            }

            if (defaultGroup is not null)
            {
                result.Body.Add("    default:");
                result.Body.Add("    {");
                EmitParsers(defaultGroup.Parsers, result, ctx, indent: "        ", getHelper: GetHelper, context);
                result.Body.Add("        break;");
                result.Body.Add("    }");
            }

            result.Body.Add("}");
        }
        else
        {
            EmitParsers(Parsers, result, ctx, indent: string.Empty, getHelper: GetHelper, context);
        }

        if (startName is not null)
        {
            result.Body.Add($"if (!{result.SuccessVariable})");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        foreach (var line in localFunctions)
        {
            result.Body.Add(line);
        }

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context)
    {
        EnsureResolved();

        // The alternatives are tried in order. A choice of 8 texts or keywords or more is generated as a lookup instead.
        if (Parsers.Count == 0 || CountAlternatives(this) >= 8)
        {
            return -1;
        }

        return SpanSource.GetWeight(context, Parsers.ToArray<object>());

        // The choices of texts and keywords are flattened like they are
        static int CountAlternatives(OneOf<T> oneOf)
        {
            var count = 0;

            foreach (var parser in oneOf.Parsers)
            {
                count += parser is OneOf<T> nested && nested.SkipWhitespace == oneOf.SkipWhitespace ? CountAlternatives(nested) : 1;
            }

            return count;
        }
    }

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var parsers = Parsers;
        var pos = writer.Pos;
        var done = writer.NewLabel("choiceDone");
        var start = parsers.Count > 1 ? writer.DeclareLocal("int", "choiceStart", "0") : null;

        // The white space is skipped once for all the alternatives, like at runtime
        if (SkipWhitespace)
        {
            writer.SkipWhiteSpace();
        }

        if (start != null)
        {
            writer.Add($"{start} = {pos};");
        }

        for (var i = 0; i < parsers.Count - 1; i++)
        {
            var next = writer.NewLabel("alternative");

            writer.Generate(parsers[i], valueVariable, next);

            if (!next.IsUsed)
            {
                // The alternative always matches, the next ones are never tried
                writer.PlaceLabel(done);
                return;
            }

            writer.Add(done.Goto());
            writer.PlaceLabel(next, $"{pos} = {start};");
        }

        // The failure of the last alternative is the failure of the choice, whose owner moves the position back
        writer.Generate(parsers[parsers.Count - 1], valueVariable, fail);
        writer.PlaceLabel(done);
    }

    private static void EmitParsers(
        IReadOnlyList<Parser<T>> parsers,
        SourceResult outerResult,
        string contextVariableName,
        string indent,
        Func<Parser<T>, string> getHelper,
        SourceGenerationContext context)
    {
        if (parsers.Count == 0)
        {
            return;
        }

        var successVar = outerResult.SuccessVariable;
        var valueVar = outerResult.ValueVariable;

        var outTarget = context.DiscardResult ? "_" : valueVar;

        // Build a short-circuiting OR-chain:
        // success = Helper1(ctx, out value) || Helper2(ctx, out value) || ...;
        // (value may be assigned by failed helpers; callers only read it when success==true)
        var firstHelper = getHelper(parsers[0]);

        if (parsers.Count == 1)
        {
            outerResult.Body.Add($"{indent}{successVar} = {firstHelper}({contextVariableName}, out {outTarget});");
            return;
        }

        if (context.IsCompacting)
        {
            // Each alternative is read from the same position, the text is released before the last one
            var pinName = $"pin{context.NextNumber()}";
            outerResult.Body.Add($"{indent}var {pinName} = {contextVariableName}.Pin();");
            outerResult.Body.Add($"{indent}{successVar} = {firstHelper}({contextVariableName}, out {outTarget})");

            for (var i = 1; i < parsers.Count - 1; i++)
            {
                outerResult.Body.Add($"{indent}    || {getHelper(parsers[i])}({contextVariableName}, out {outTarget})");
            }

            outerResult.Body[outerResult.Body.Count - 1] += ";";
            outerResult.Body.Add($"{indent}{contextVariableName}.Unpin({pinName});");
            outerResult.Body.Add($"{indent}if (!{successVar})");
            outerResult.Body.Add($"{indent}{{");
            outerResult.Body.Add($"{indent}    {successVar} = {getHelper(parsers[parsers.Count - 1])}({contextVariableName}, out {outTarget});");
            outerResult.Body.Add($"{indent}}}");
            return;
        }

        outerResult.Body.Add($"{indent}{successVar} = {firstHelper}({contextVariableName}, out {outTarget})");

        for (var i = 1; i < parsers.Count; i++)
        {
            var helperName = getHelper(parsers[i]);
            var terminator = i == parsers.Count - 1 ? ";" : string.Empty;
            outerResult.Body.Add($"{indent}    || {helperName}({contextVariableName}, out {outTarget}){terminator}");
        }
    }

    private static string ToCharLiteral(char c)
    {
        return "'" + (c switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\"' => "\\\"",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            _ when char.IsControl(c) || c > 0x7e => $"\\u{(int)c:X4}",
            _ => c.ToString()
        }) + "'";
    }

    private static string BuildCharPattern(IReadOnlyList<char> chars, int rangeThreshold)
    {
        // Sort and de-dup.
        var list = new List<char>(chars);
        list.Sort();

        var parts = new List<string>();

        for (var i = 0; i < list.Count; i++)
        {
            var start = list[i];
            if (i > 0 && start == list[i - 1])
            {
                continue;
            }

            var end = start;
            var runLength = 1;

            while (i + 1 < list.Count)
            {
                var next = list[i + 1];
                if (next == end)
                {
                    i++;
                    continue;
                }

                if (next == (char)(end + 1))
                {
                    end = next;
                    runLength++;
                    i++;
                    continue;
                }

                break;
            }

            if (runLength >= rangeThreshold)
            {
                parts.Add($">= {ToCharLiteral(start)} and <= {ToCharLiteral(end)}");
            }
            else
            {
                // Small runs are emitted as individual char patterns.
                var c = start;
                for (var r = 0; r < runLength; r++)
                {
                    parts.Add(ToCharLiteral((char)(c + r)));
                }
            }
        }

        return string.Join(" or ", parts);
    }

    private readonly record struct ParserListKey(IReadOnlyList<Parser<T>> Parsers);

    /// <summary>
    /// The alternatives of a char of the lookup table, selected again by the prefixes they tell (see
    /// <see cref="ISeekablePrefixes"/>), when some of them can be skipped for the rest of the input. The generated code
    /// emits it as a lookup, the runtime walks it as a <see cref="PrefixNode"/>.
    /// </summary>
    /// <remarks>
    /// The input selects the alternatives of the longest prefix it starts with: these are the alternatives with a prefix
    /// which this one starts with, which are all the ones with a prefix the input starts with. A prefix of a single char
    /// is the char of the lookup table, so an input that starts with no longer prefix selects its alternatives.
    /// </remarks>
    private sealed class PrefixDispatch
    {
        // Fewer alternatives cost less to invoke than a dispatch
        public const int MinimumAlternatives = 3;

        // The bounds of the generated text choices, see TextChoiceSource. A prefix is still one when it is shortened.
        private const int MaximumPrefixLength = 64;
        private const int MaximumPrefixes = 256;
        private const int MaximumLength = 4096;

        private PrefixDispatch()
        {
        }

        /// <summary>
        /// The prefixes without the char of the lookup table, longest first, such that the first one the input starts
        /// with is the longest.
        /// </summary>
        public List<string> Suffixes { get; } = [];

        /// <summary>
        /// The index in <see cref="Groups"/> of the alternatives each one of <see cref="Suffixes"/> selects.
        /// </summary>
        public List<int> GroupIndexes { get; } = [];

        public List<Parser<T>[]> Groups { get; } = [];

        // Every choice calls it for each char of its lookup table when it is built, so the closures of Create are only
        // allocated for the chars that may be dispatched
        public static PrefixDispatch? TryCreate(char c, Parser<T>[] parsers) => parsers.Length < MinimumAlternatives ? null : Create(c, parsers);

        private static PrefixDispatch? Create(char c, Parser<T>[] parsers)
        {
            var alternativePrefixes = new List<string>[parsers.Length];
            var prefixes = new List<string>();

            for (var i = 0; i < parsers.Length; i++)
            {
                alternativePrefixes[i] = [];

                foreach (var prefix in SeekableHints.Prefixes(parsers[i], c))
                {
                    var bounded = prefix.Length > MaximumPrefixLength ? prefix.Substring(0, MaximumPrefixLength) : prefix;

                    if (!alternativePrefixes[i].Contains(bounded))
                    {
                        alternativePrefixes[i].Add(bounded);
                    }

                    if (!prefixes.Contains(bounded))
                    {
                        prefixes.Add(bounded);
                    }
                }
            }

            if (prefixes.Count > MaximumPrefixes || prefixes.Sum(static prefix => prefix.Length) > MaximumLength)
            {
                return null;
            }

            var dispatch = new PrefixDispatch();
            var indexes = new Dictionary<ParserListKey, int>(ParserListKeyComparer.Instance);
            var narrowed = false;

            foreach (var prefix in prefixes.OrderByDescending(static prefix => prefix.Length))
            {
                var group = parsers.Where((_, i) => alternativePrefixes[i].Exists(other => prefix.StartsWith(other, StringComparison.Ordinal))).ToArray();
                narrowed |= group.Length < parsers.Length;

                var key = new ParserListKey(group);

                if (!indexes.TryGetValue(key, out var index))
                {
                    index = dispatch.Groups.Count;
                    indexes.Add(key, index);
                    dispatch.Groups.Add(group);
                }

                dispatch.Suffixes.Add(prefix.Substring(1));
                dispatch.GroupIndexes.Add(index);
            }

            // Nothing to skip when every prefix selects all the alternatives
            return narrowed ? dispatch : null;
        }

        /// <summary>
        /// Creates the trie of the suffixes, which selects the same alternatives as the generated lookup.
        /// </summary>
        public PrefixNode CreateNode()
        {
            var root = new SuffixNode();

            for (var i = 0; i < Suffixes.Count; i++)
            {
                var node = root;

                foreach (var c in Suffixes[i])
                {
                    if (!node.Children.TryGetValue(c, out var child))
                    {
                        child = new SuffixNode();
                        node.Children.Add(c, child);
                    }

                    node = child;
                }

                node.Group = GroupIndexes[i];
            }

            // An input that starts with no prefix, not even the char of the lookup table, selects no alternative
            return Convert(root, string.Empty, []);

            PrefixNode Convert(SuffixNode node, string segment, Parser<T>[] inherited)
            {
                // A node on the way to longer prefixes selects the alternatives of the longest one before it
                var parsers = node.Group >= 0 ? Groups[node.Group] : inherited;
                var edges = new char[node.Children.Count];
                var children = new PrefixNode[node.Children.Count];
                var index = 0;

                foreach (var entry in node.Children)
                {
                    var chain = new List<char>();
                    var child = entry.Value;

                    // The chars that only lead to a single longer prefix are compared at once
                    while (child.Group < 0 && child.Children.Count == 1)
                    {
                        var next = child.Children.First();
                        chain.Add(next.Key);
                        child = next.Value;
                    }

                    edges[index] = entry.Key;
                    children[index] = Convert(child, new string(chain.ToArray()), parsers);
                    index++;
                }

                return new PrefixNode(segment, new string(edges), children, parsers);
            }
        }

        private sealed class SuffixNode
        {
            public Dictionary<char, SuffixNode> Children { get; } = [];

            // The index in Groups of the alternatives the suffix ending here selects, or -1 when none ends here
            public int Group { get; set; } = -1;
        }
    }

    /// <summary>
    /// A node of the trie that selects the alternatives of a char of the lookup table from the rest of the input, see
    /// <see cref="PrefixDispatch"/>.
    /// </summary>
    private sealed class PrefixNode
    {
        // The chars after the one of the edge from the parent node
        private readonly string _segment;

        // The char of the edge to each child
        private readonly string _edges;
        private readonly PrefixNode[] _children;

        // The alternatives of an input which only starts with the prefix of this node
        private readonly Parser<T>[] _parsers;

        public PrefixNode(string segment, string edges, PrefixNode[] children, Parser<T>[] parsers)
        {
            _segment = segment;
            _edges = edges;
            _children = children;
            _parsers = parsers;
        }

        /// <summary>
        /// Returns the alternatives of the longest prefix at the cursor, whose current char is the one of this root node.
        /// </summary>
        public Parser<T>[] Select(Cursor cursor)
        {
            var text = cursor.Span;
            var node = this;
            var offset = 1;

            while (node._edges.Length > 0)
            {
                if (offset >= text.Length)
                {
                    // A longer prefix may follow the end of a streamed window
                    cursor.MarkHitEnd();
                    break;
                }

                var c = text[offset];
                var edges = node._edges;
                var index = 0;

                while (index < edges.Length && edges[index] != c)
                {
                    index++;
                }

                if (index == edges.Length)
                {
                    break;
                }

                var child = node._children[index];
                var segment = child._segment;
                offset++;

                if (segment.Length > 0)
                {
                    var rest = text.Slice(offset);

                    if (!rest.StartsWith(segment.AsSpan()))
                    {
                        if (rest.Length < segment.Length && segment.AsSpan().StartsWith(rest))
                        {
                            cursor.MarkHitEnd();
                        }

                        break;
                    }

                    offset += segment.Length;
                }

                node = child;
            }

            return node._parsers;
        }
    }

    private sealed class ParserReferenceComparer : IEqualityComparer<Parser<T>>
    {
        public static readonly ParserReferenceComparer Instance = new();

        public bool Equals(Parser<T>? x, Parser<T>? y) => ReferenceEquals(x, y);

        public int GetHashCode(Parser<T> obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class ParserListKeyComparer : IEqualityComparer<ParserListKey>
    {
        public static readonly ParserListKeyComparer Instance = new();

        public bool Equals(ParserListKey x, ParserListKey y)
        {
            if (ReferenceEquals(x.Parsers, y.Parsers))
            {
                return true;
            }

            var xCount = x.Parsers.Count;
            var yCount = y.Parsers.Count;
            if (xCount != yCount)
            {
                return false;
            }

            for (var i = 0; i < xCount; i++)
            {
                if (!ReferenceEquals(x.Parsers[i], y.Parsers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(ParserListKey key)
        {
            unchecked
            {
                var hash = key.Parsers.Count;
                for (var i = 0; i < key.Parsers.Count; i++)
                {
                    hash = (hash * 31) + RuntimeHelpers.GetHashCode(key.Parsers[i]!);
                }

                return hash;
            }
        }
    }

    private sealed class ParserGroup
    {
        public ParserGroup(IReadOnlyList<Parser<T>> parsers)
        {
            Parsers = parsers;
            Chars = [];
        }

        public IReadOnlyList<Parser<T>> Parsers { get; }
        public List<char> Chars { get; }
        public bool IsDefault { get; set; }
    }

}
