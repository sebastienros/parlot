using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Parlot.Fluent;

/// <summary>
/// A choice which invokes each of its alternatives at the same position, and continues after the one which reads the
/// furthest: the longest match, the first alternative winning when several end at the same offset. A selector can keep
/// another match instead.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="OneOf{T}"/>, whose first alternative that matches wins, the result doesn't depend on the order of the
/// alternatives, except for the matches of the same length. It is the maximal munch of lexers and the choice of the
/// grammars written for LR parsers.
/// </para>
/// <para>
/// Like <see cref="OneOf{T}"/>, the alternatives which can't start with the next char aren't invoked, and the white space is
/// skipped once when they all skip it.
/// </para>
/// </remarks>
/// <typeparam name="T">The type of the value of the alternatives.</typeparam>
public sealed class Longest<T> : Parser<T>, ISeekable, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Parser<T>[] _parsers;
    private readonly Func<LongestMatches<T>, int>? _selector;
    private readonly Func<ParseContext, LongestMatches<T>, int>? _contextSelector;

    // Whether the matches are kept for a selector, rather than only the longest one
    private readonly bool _selecting;

    // Whether an alternative can be invoked without its value, such that this one is when its value isn't used
    private readonly bool _withoutValue;

    // The hints of the alternatives, read when they are asked for since they can be Deferred parsers set later
    private ForwardedHints? _hints;

    // The alternatives each char selects, built from their hints before the first parse, once the grammar is complete
    private Table? _table;

    // The parsers of the table, read by Parse without the indirections of the table, like the fields of OneOf. They are
    // set before the table, which is read first.
    private CharMap<Parser<T>[]>? _parseMap;
    private Parser<T>[]? _otherParsers;
    private bool _skipWhiteSpace;

    /// <summary>
    /// Creates a choice which continues after the longest match of <paramref name="parsers"/>.
    /// </summary>
    /// <param name="parsers">The alternatives. The first one wins when several matches end at the same offset.</param>
    public Longest(Parser<T>[] parsers)
    {
        ThrowHelper.ThrowIfNull(parsers, nameof(parsers));

        for (var i = 0; i < parsers.Length; i++)
        {
            if (parsers[i] is null)
            {
                throw new ArgumentException("The alternatives can't be null.", nameof(parsers));
            }

            _withoutValue |= parsers[i].CanParseWithoutValue;
        }

        _parsers = parsers;
    }

    /// <summary>
    /// Creates a choice which continues after the match of <paramref name="parsers"/> that <paramref name="selector"/>
    /// keeps.
    /// </summary>
    /// <param name="selector">
    /// Receives the alternatives which matched, in their order, and returns the index of the match to keep, or a negative
    /// value to fail. It is only invoked when an alternative matches.
    /// </param>
    /// <param name="parsers">The alternatives.</param>
    public Longest(Func<LongestMatches<T>, int> selector, Parser<T>[] parsers) : this(parsers)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _selecting = true;
        _withoutValue = false;
    }

    /// <summary>
    /// Creates a choice which continues after the match of <paramref name="parsers"/> that <paramref name="selector"/>
    /// keeps.
    /// </summary>
    /// <param name="selector">
    /// Receives the context and the alternatives which matched, in their order, and returns the index of the match to keep,
    /// or a negative value to fail. It is only invoked when an alternative matches.
    /// </param>
    /// <param name="parsers">The alternatives.</param>
    public Longest(Func<ParseContext, LongestMatches<T>, int> selector, Parser<T>[] parsers) : this(parsers)
    {
        _contextSelector = selector ?? throw new ArgumentNullException(nameof(selector));
        _selecting = true;
        _withoutValue = false;
    }

    /// <summary>
    /// Gets the alternatives, in their order.
    /// </summary>
    public IReadOnlyList<Parser<T>> Parsers => _parsers;

    /// <summary>
    /// Gets whether the choice can be selected from a single char: every alternative can, and they skip white space the
    /// same way.
    /// </summary>
    public bool CanSeek => Hints.CanSeek;

    /// <summary>
    /// Gets the chars any alternative can start with.
    /// </summary>
    public char[] ExpectedChars => Hints.ExpectedChars;

    /// <summary>
    /// Gets whether every alternative skips white space, when they can all be selected from a single char, such that a
    /// choice holding this one can skip the white space before it.
    /// </summary>
    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, _parsers, static parsers => SeekableHints.Union(parsers));

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        // Each alternative is invoked where the choice is
        var nullable = false;

        foreach (var parser in _parsers)
        {
            nullable |= analysis.Invoke(parser);
        }

        return nullable;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        var table = Volatile.Read(ref _table) ?? Resolve();

        if (_selecting || context.IsCompactingOrCollectingDiagnostics)
        {
            return ParseMatches(context, ref result, table);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        // The position to move back to when no alternative matches, before the white space this parser skipped
        var start = cursor.Offset;

        if (_skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var parsers = _parseMap is { } map ? map[cursor.Current] ?? _otherParsers : _otherParsers;

        if (parsers != null)
        {
            if (parsers.Length == 1)
            {
                // Nothing to compare, like an exclusive choice
                if (parsers[0].Parse(context, ref result))
                {
                    context.ExitParser(this);
                    return true;
                }
            }
            else if (ParseLongest(context, ref result, parsers))
            {
                context.ExitParser(this);
                return true;
            }
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    // Invokes each alternative from the current position, and moves the cursor to the end of the longest match. A method of
    // its own, so that the frame of Parse stays small for the chars which select a single alternative.
    private static bool ParseLongest(ParseContext context, ref ParseResult<T> result, Parser<T>[] parsers)
    {
        var cursor = context.Scanner.Cursor;
        var from = cursor.Offset;
        var last = parsers.Length - 1;
        var bestEnd = -1;
        var parsed = new ParseResult<T>();

        for (var i = 0; i < parsers.Length; i++)
        {
            if (parsers[i].Parse(context, ref parsed))
            {
                var end = cursor.Offset;

                // The first alternative wins a tie
                if (end > bestEnd)
                {
                    bestEnd = end;
                    result = parsed;
                }

                if (i < last)
                {
                    cursor.ResetPosition(from);
                }
            }
        }

        if (bestEnd < 0)
        {
            return false;
        }

        cursor.AdvanceTo(bestEnd);
        return true;
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    // Parse, with the alternatives invoked without their value
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        var table = Volatile.Read(ref _table) ?? Resolve();

        // The alternatives are invoked with their value when the input is a reader or the diagnostics are collected
        if (_selecting || context.IsCompactingOrCollectingDiagnostics)
        {
            return ParseMatches(context, ref result, table);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (_skipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var parsers = _parseMap is { } map ? map[cursor.Current] ?? _otherParsers : _otherParsers;

        if (parsers != null)
        {
            if (parsers.Length == 1)
            {
                if (parsers[0].ParseWithoutValue(context, ref result))
                {
                    context.ExitParser(this);
                    return true;
                }
            }
            else if (ParseLongestWithoutValue(context, ref result, parsers))
            {
                context.ExitParser(this);
                return true;
            }
        }

        cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    // ParseLongest, with the alternatives invoked without their value
    private static bool ParseLongestWithoutValue(ParseContext context, ref ParseResult<T> result, Parser<T>[] parsers)
    {
        var cursor = context.Scanner.Cursor;
        var from = cursor.Offset;
        var last = parsers.Length - 1;
        var bestEnd = -1;
        var parsed = new ParseResult<T>();

        for (var i = 0; i < parsers.Length; i++)
        {
            if (parsers[i].ParseWithoutValue(context, ref parsed))
            {
                var end = cursor.Offset;

                if (end > bestEnd)
                {
                    bestEnd = end;
                    result = parsed;
                }

                if (i < last)
                {
                    cursor.ResetPosition(from);
                }
            }
        }

        if (bestEnd < 0)
        {
            return false;
        }

        cursor.AdvanceTo(bestEnd);
        return true;
    }

    // Keeps the matches for the selector, keeps the diagnostics of the match that is used and pins the position the
    // alternatives start from when the input is a reader. Also invoked when the diagnostics are collected, and its pins do
    // nothing when the input is a string.
    private bool ParseMatches(ParseContext context, ref ParseResult<T> result, Table table)
    {
        context.EnterParser(this);

        var collecting = context.CollectDiagnostics;
        var failures = collecting ? context.BeginAlternatives() : default;

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var success = false;

        if (table.SkipWhiteSpace)
        {
            context.SkipWhiteSpace();
        }

        var alternatives = table.Select(cursor.Current);

        if (alternatives != null)
        {
            var parsers = alternatives.Parsers;
            var from = cursor.Offset;

            // Each alternative is read from the same position, and the cursor moves to the end of the one that is kept
            var pin = parsers.Length > 1 ? context.Pin() : -1;

            var matches = new ListBuilder<LongestMatch<T>>();

            // The diagnostics of each match when there is a selector, or of the longest one
            List<ParseDiagnostic[]?>? matchDiagnostics = null;
            ParseDiagnostic[]? bestDiagnostics = null;

            var bestEnd = -1;
            var parsed = new ParseResult<T>();

            try
            {
                for (var i = 0; i < parsers.Length; i++)
                {
                    if (parsers[i].Parse(context, ref parsed))
                    {
                        var end = cursor.Offset;

                        // The diagnostics of a match are only kept when the match is
                        var diagnostics = collecting ? context.TakeAlternative(in failures) : null;

                        if (_selecting)
                        {
                            matches.Add(new LongestMatch<T>(alternatives.Indexes[i], parsed.Value, parsed.Start, end, parsed.End));

                            if (collecting)
                            {
                                (matchDiagnostics ??= []).Add(diagnostics);
                            }

                            // A match isn't a failure where it ends, the selector can keep a shorter one or none
                            var recorded = cursor.RecordedFailure;
                            cursor.ResetPosition(from);
                            cursor.RestoreRecordedFailure(recorded);
                        }
                        else
                        {
                            if (end > bestEnd)
                            {
                                bestEnd = end;
                                bestDiagnostics = diagnostics;
                                result = parsed;
                            }

                            cursor.ResetPosition(from);
                        }
                    }
                    else if (collecting)
                    {
                        context.RejectAlternative(ref failures);
                    }
                }

                if (_selecting && matches.Count > 0)
                {
                    var index = _selector != null
                        ? _selector(new LongestMatches<T>(in matches))
                        : _contextSelector!(context, new LongestMatches<T>(in matches));

                    // The match is read through the builder after the callback, which can parse
                    if ((uint)index < (uint)matches.Count)
                    {
                        var match = matches[index];

                        result.Set(match.Start, match.ResultEnd, match.Value);
                        bestEnd = match.End;
                        bestDiagnostics = matchDiagnostics?[index];
                    }
                    else
                    {
                        // The matches are rejected where they start, like a value When rejects
                        cursor.RecordFailure(from);
                    }
                }

                if (bestEnd >= 0)
                {
                    cursor.AdvanceTo(bestEnd);
                    context.RestoreAlternative(bestDiagnostics);
                    success = true;
                }
            }
            finally
            {
                matches.Release();
            }

            context.Unpin(pin);
        }

        if (!success)
        {
            if (collecting)
            {
                context.RestoreAlternatives(in failures);
            }

            cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return success;
    }

    // Builds the table once the grammar is complete, the hints of the alternatives telling which chars they start with
    [MethodImpl(MethodImplOptions.NoInlining)]
    private Table Resolve()
    {
        // The lock of the choices and deferred parsers, such that the choices among the alternatives build their tables first
        lock (SeekableResolution.SyncRoot)
        {
            var table = _table;

            if (table == null)
            {
                table = Table.Build(_parsers);

                if (table.Map is { } map)
                {
                    var entries = new List<KeyValuePair<char, Parser<T>[]>>(map.ExpectedChars.Length);

                    foreach (var c in map.ExpectedChars)
                    {
                        entries.Add(new KeyValuePair<char, Parser<T>[]>(c, map[c]!.Parsers));
                    }

                    _parseMap = new CharMap<Parser<T>[]>(entries) { NonAsciiFallback = map.NonAsciiFallback?.Parsers };
                }

                _otherParsers = table.Other?.Parsers;
                _skipWhiteSpace = table.SkipWhiteSpace;

                // The fields are written before the table, which Parse reads first
                Volatile.Write(ref _table, table);
            }

            return table;
        }
    }

    public override string ToString() => $"Longest({string.Join(", ", (object[])_parsers)})";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // The factory has run to completion, so every alternative can declare its hints
        var table = Volatile.Read(ref _table) ?? Resolve();

        if (SpanSource.TryGenerate(this, typeof(T), context) is { } spanResult)
        {
            return spanResult;
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursor = context.CursorName;
        var valueTypeName = result.ValueTypeName ?? SourceGenerationContext.GetTypeName(typeof(T));
        var matchTypeName = $"global::Parlot.Fluent.LongestMatch<{valueTypeName}>";
        var diagnosticsTypeName = "global::Parlot.ParseDiagnostic[]";

        // The selector reads the values of the matches
        var discard = context.DiscardResult && !_selecting;
        var collecting = context.CollectDiagnostics;

        var number = context.NextNumber();
        var startName = $"longestStart{number}";
        var fromName = $"longestFrom{number}";
        var endName = $"longestEnd{number}";
        var pinName = context.IsCompacting ? $"longestPin{number}" : null;
        var matchesName = $"longestMatches{number}";
        var collectingName = $"longestCollecting{number}";
        var failuresName = $"longestFailures{number}";
        var bestDiagnosticsName = $"longestDiagnostics{number}";
        var matchDiagnosticsName = $"longestMatchDiagnostics{number}";

        string GetHelper(Parser<T> parser)
        {
            if (parser is not ISourceable sourceable)
            {
                throw new NotSupportedException("Longest requires all parsers to be source-generatable.");
            }

            return context.WithDiscardResult(discard, () => context.Helpers
                .GetOrCreate(parser, $"{context.MethodNamePrefix}_Longest", valueTypeName, () => sourceable.GenerateSource(context))
                .MethodName);
        }

        // var start = cursor.Offset;
        // context.SkipWhiteSpace();
        // var from = cursor.Offset;
        // var end = -1;
        result.Body.Add($"var {startName} = {cursor}.Offset;");

        if (table.SkipWhiteSpace)
        {
            result.Body.Add($"{ctx}.SkipWhiteSpace();");
        }

        result.Body.Add($"var {fromName} = {cursor}.Offset;");
        result.Body.Add($"var {endName} = -1;");

        if (pinName != null)
        {
            result.Body.Add($"var {pinName} = -1;");
        }

        if (collecting)
        {
            result.Body.Add($"var {collectingName} = {ctx}.CollectDiagnostics;");
            result.Body.Add($"var {failuresName} = {collectingName} ? {ctx}.BeginAlternatives() : default;");
            result.Body.Add($"{diagnosticsTypeName}? {bestDiagnosticsName} = null;");

            if (_selecting)
            {
                result.Body.Add($"global::System.Collections.Generic.List<{diagnosticsTypeName}?>? {matchDiagnosticsName} = null;");
            }
        }

        if (_selecting)
        {
            result.Body.Add(ListBuilderSource.Declare(matchesName, matchTypeName));
        }

        var bodyStart = result.Body.Count;

        // if (Alternative(context, out var value))
        // {
        //     if (cursor.Offset > end) { end = cursor.Offset; result = value; }
        //     cursor.ResetPosition(from);
        // }
        void EmitAlternative(Parser<T> parser, int index, bool single, bool last, string indent)
        {
            var helper = GetHelper(parser);
            var valueName = $"longestValue{context.NextNumber()}";
            var diagnosticsName = $"longestAlternativeDiagnostics{context.NextNumber()}";
            var inner = indent + "    ";

            result.Body.Add($"{indent}if ({helper}({ctx}, out {(discard ? "_" : "var " + valueName)}))");
            result.Body.Add($"{indent}{{");

            if (collecting)
            {
                // The diagnostics of a match are only kept when the match is
                result.Body.Add($"{inner}var {diagnosticsName} = {collectingName} ? {ctx}.TakeAlternative(in {failuresName}) : null;");
            }

            if (_selecting)
            {
                var recordedName = $"longestRecorded{context.NextNumber()}";

                result.Body.Add($"{inner}{matchesName}.Add(new {matchTypeName}({index}, {valueName}, {fromName}, {cursor}.Offset));");

                if (collecting)
                {
                    result.Body.Add($"{inner}if ({collectingName}) ({matchDiagnosticsName} ??= new global::System.Collections.Generic.List<{diagnosticsTypeName}?>()).Add({diagnosticsName});");
                }

                // A match isn't a failure where it ends, the selector can keep a shorter one or none
                result.Body.Add($"{inner}var {recordedName} = {cursor}.RecordedFailure;");
                result.Body.Add($"{inner}{cursor}.ResetPosition({fromName});");
                result.Body.Add($"{inner}{cursor}.RestoreRecordedFailure({recordedName});");
            }
            else
            {
                // The first alternative wins a tie. The only alternative of a char has nothing to compare.
                var assignments = new List<string> { $"{endName} = {cursor}.Offset;" };

                if (!discard)
                {
                    assignments.Add($"{result.ValueVariable} = {valueName};");
                }

                if (collecting)
                {
                    assignments.Add($"{bestDiagnosticsName} = {diagnosticsName};");
                }

                if (single)
                {
                    foreach (var assignment in assignments)
                    {
                        result.Body.Add($"{inner}{assignment}");
                    }
                }
                else
                {
                    result.Body.Add($"{inner}if ({cursor}.Offset > {endName})");
                    result.Body.Add($"{inner}{{");

                    foreach (var assignment in assignments)
                    {
                        result.Body.Add($"{inner}    {assignment}");
                    }

                    result.Body.Add($"{inner}}}");
                }

                // The cursor moves to the end of the longest match after the last alternative, it is already there when
                // the last one is the longest
                if (!last)
                {
                    result.Body.Add($"{inner}{cursor}.ResetPosition({fromName});");
                }
            }

            result.Body.Add($"{indent}}}");

            if (collecting)
            {
                result.Body.Add($"{indent}else if ({collectingName})");
                result.Body.Add($"{indent}{{");
                result.Body.Add($"{indent}    {ctx}.RejectAlternative(ref {failuresName});");
                result.Body.Add($"{indent}}}");
            }
        }

        void EmitAlternatives(Alternatives? alternatives, string indent)
        {
            if (alternatives == null)
            {
                return;
            }

            var parsers = alternatives.Parsers;

            // Each alternative is read from the same position, and the cursor moves to the end of the one that is kept
            if (pinName != null && parsers.Length > 1)
            {
                result.Body.Add($"{indent}{pinName} = {ctx}.Pin();");
            }

            for (var i = 0; i < parsers.Length; i++)
            {
                EmitAlternative(parsers[i], alternatives.Indexes[i], single: parsers.Length == 1, last: i == parsers.Length - 1, indent);
            }
        }

        if (table.Map is not { } map)
        {
            EmitAlternatives(table.Other, "");
        }
        else
        {
            // The chars which select the same alternatives share a case, like the lookup table shares their arrays
            var groups = new List<(Alternatives Alternatives, List<char> Chars)>();

            foreach (var c in map.ExpectedChars)
            {
                var alternatives = map[c]!;
                var index = groups.FindIndex(group => ReferenceEquals(group.Alternatives, alternatives));

                if (index < 0)
                {
                    groups.Add((alternatives, [c]));
                }
                else
                {
                    groups[index].Chars.Add(c);
                }
            }

            var currentName = $"longestCurrent{number}";

            result.Body.Add($"var {currentName} = {cursor}.Current;");
            result.Body.Add($"switch ({currentName})");
            result.Body.Add("{");

            foreach (var (alternatives, chars) in groups)
            {
                if (context.SupportsCSharp9SwitchPatterns)
                {
                    result.Body.Add($"    case {OneOf<T>.BuildCharPattern(chars, rangeThreshold: 4)}:");
                }
                else
                {
                    foreach (var c in chars)
                    {
                        result.Body.Add($"    case {LiteralHelper.CharToLiteral(c)}:");
                    }
                }

                result.Body.Add("    {");
                EmitAlternatives(alternatives, "        ");
                result.Body.Add("        break;");
                result.Body.Add("    }");
            }

            // The other chars select the alternatives which can start with any char, and the non-ASCII ones without an entry
            // the alternatives which can start with any of them
            if (table.Other != null || map.NonAsciiFallback != null)
            {
                result.Body.Add("    default:");
                result.Body.Add("    {");

                if (map.NonAsciiFallback != null)
                {
                    result.Body.Add($"        if ({currentName} >= '\\u0080')");
                    result.Body.Add("        {");
                    EmitAlternatives(map.NonAsciiFallback, "            ");
                    result.Body.Add("        }");

                    if (table.Other != null)
                    {
                        result.Body.Add("        else");
                        result.Body.Add("        {");
                        EmitAlternatives(table.Other, "            ");
                        result.Body.Add("        }");
                    }
                }
                else
                {
                    EmitAlternatives(table.Other, "        ");
                }

                result.Body.Add("        break;");
                result.Body.Add("    }");
            }

            result.Body.Add("}");
        }

        if (_selecting)
        {
            var indexName = $"longestIndex{number}";
            var matchName = $"longestMatch{number}";
            var matches = $"new global::Parlot.Fluent.LongestMatches<{valueTypeName}>(in {matchesName})";
            var call = _selector != null
                ? $"{context.RegisterLambda(_selector)}({matches})"
                : $"{context.RegisterLambda(_contextSelector!)}({ctx}, {matches})";

            // The match is read through the builder after the callback, which can parse
            result.Body.Add($"if ({matchesName}.Count > 0)");
            result.Body.Add("{");
            result.Body.Add($"    var {indexName} = {call};");
            result.Body.Add($"    if ((uint){indexName} < (uint){matchesName}.Count)");
            result.Body.Add("    {");
            result.Body.Add($"        var {matchName} = {matchesName}[{indexName}];");
            result.Body.Add($"        {endName} = {matchName}.End;");

            if (!context.DiscardResult)
            {
                result.Body.Add($"        {result.ValueVariable} = {matchName}.Value;");
            }

            if (collecting)
            {
                result.Body.Add($"        {bestDiagnosticsName} = {matchDiagnosticsName}?[{indexName}];");
            }

            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add("        // The matches are rejected where they start, like a value When rejects");
            result.Body.Add($"        {cursor}.RecordFailure({fromName});");
            result.Body.Add("    }");
            result.Body.Add("}");

            ListBuilderSource.ReleaseInFinally(result, bodyStart, matchesName);
        }

        // if (end >= 0) { cursor.AdvanceTo(end); success = true; } else { cursor.ResetPosition(start); }
        result.Body.Add($"if ({endName} >= 0)");
        result.Body.Add("{");
        result.Body.Add($"    {cursor}.AdvanceTo({endName});");

        if (collecting)
        {
            result.Body.Add($"    {ctx}.RestoreAlternative({bestDiagnosticsName});");
        }

        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");

        if (collecting)
        {
            result.Body.Add($"    if ({collectingName})");
            result.Body.Add("    {");
            result.Body.Add($"        {ctx}.RestoreAlternatives(in {failuresName});");
            result.Body.Add("    }");
        }

        result.Body.Add($"    {cursor}.ResetPosition({startName});");
        result.Body.Add("}");

        if (pinName != null)
        {
            context.Unpin(result, pinName);
        }

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context)
    {
        // The selector reads the matches, which span code doesn't keep
        if (_selecting)
        {
            return -1;
        }

        var parsers = (Volatile.Read(ref _table) ?? Resolve()).Parsers;

        // Span code invokes each alternative, without the lookup table of the helper methods, so a choice of many of them is
        // better dispatched on the next char
        if (parsers.Length == 0 || parsers.Length >= 8)
        {
            return -1;
        }

        return SpanSource.GetWeight(context, parsers);
    }

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var table = Volatile.Read(ref _table) ?? Resolve();
        var pos = writer.Pos;

        // The white space is skipped once for all the alternatives, like at runtime
        if (table.SkipWhiteSpace)
        {
            writer.SkipWhiteSpace();
        }

        var from = writer.DeclareLocal("int", "longestFrom", "0");
        var end = writer.DeclareLocal("int", "longestEnd", "0");
        var failure = writer.DeclareLocal("int", "longestFailure", "0");

        // An alternative which fails after another matched can have set the value of its own
        var item = valueVariable != null ? writer.DeclareLocal(typeof(T), "longestItem") : null;

        writer.Add($"{from} = {pos};");
        writer.Add($"{end} = -1;");
        writer.Add($"{failure} = {pos};");

        foreach (var parser in table.Parsers)
        {
            var next = writer.NewLabel("longestNext");

            writer.Generate(parser, item, next);

            // The first alternative wins a tie
            writer.BeginBlock($"if ({pos} > {end})");
            writer.Add($"{end} = {pos};");

            if (valueVariable != null)
            {
                writer.Add($"{valueVariable} = {item};");
            }

            writer.EndBlock();

            if (next.IsUsed)
            {
                // The position of an alternative which fails is where it failed, the furthest one is the failure of the choice
                var done = writer.NewLabel("longestDone");

                writer.Add(done.Goto());
                writer.PlaceLabel(next, $"if ({pos} > {failure}) {failure} = {pos};");
                writer.PlaceLabel(done);
            }

            writer.Add($"{pos} = {from};");
        }

        writer.BeginBlock($"if ({end} < 0)");
        writer.Add($"{pos} = {failure};");
        writer.Add(fail.Goto());
        writer.EndBlock();
        writer.Add($"{pos} = {end};");
    }

    /// <summary>
    /// Alternatives which can start with the same chars, in their order.
    /// </summary>
    private sealed class Alternatives
    {
        public Alternatives(Parser<T>[] parsers, int[] indexes)
        {
            Parsers = parsers;
            Indexes = indexes;
        }

        // The parsers to invoke, without the SkipWhiteSpace wrapper when the choice skips the white space
        public Parser<T>[] Parsers { get; }

        // The index of each parser in the alternatives given to the choice
        public int[] Indexes { get; }
    }

    /// <summary>
    /// The alternatives each char selects, like the lookup table of <see cref="OneOf{T}"/>, without flattening the choices
    /// among the alternatives, which stop at their first match.
    /// </summary>
    private sealed class Table
    {
        private Table(Parser<T>[] parsers, bool skipWhiteSpace, CharMap<Alternatives>? map, Alternatives? other)
        {
            Parsers = parsers;
            SkipWhiteSpace = skipWhiteSpace;
            Map = map;
            Other = other;
        }

        // The parsers invoked for the alternatives, in their order
        public Parser<T>[] Parsers { get; }

        // Whether every alternative skips white space, which the choice then skips once
        public bool SkipWhiteSpace { get; }

        // The alternatives each char selects, or null when every char selects all of them
        public CharMap<Alternatives>? Map { get; }

        // The alternatives the other chars select: the ones which can't tell what they start with
        public Alternatives? Other { get; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Alternatives? Select(char c) => Map is { } map ? map[c] ?? Other : Other;

        public static Table Build(Parser<T>[] parsers)
        {
            var count = parsers.Length;

            if (count == 0)
            {
                return new Table(parsers, false, null, null);
            }

            var hints = new (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace)[count];
            var allSkip = true;
            var anySkip = false;

            for (var i = 0; i < count; i++)
            {
                hints[i] = SeekableHints.Of(parsers[i]);
                allSkip &= hints[i].SkipWhitespace;
                anySkip |= hints[i].SkipWhitespace;
            }

            // The white space is skipped once, and the parsers which only skip it before their parser are bypassed
            var invoked = new Parser<T>[count];

            for (var i = 0; i < count; i++)
            {
                invoked[i] = allSkip && parsers[i] is SkipWhiteSpace<T> skip ? skip.Parser : parsers[i];
            }

            var groups = new Dictionary<string, Alternatives>(StringComparer.Ordinal);

            Alternatives? Group(Func<int, bool> invokes)
            {
                var indexes = new List<int>();

                for (var i = 0; i < count; i++)
                {
                    if (invokes(i))
                    {
                        indexes.Add(i);
                    }
                }

                if (indexes.Count == 0)
                {
                    return null;
                }

                // The chars which select the same alternatives share their arrays
                var key = string.Join(",", indexes);

                if (!groups.TryGetValue(key, out var group))
                {
                    var selected = new Parser<T>[indexes.Count];

                    for (var i = 0; i < selected.Length; i++)
                    {
                        selected[i] = invoked[indexes[i]];
                    }

                    group = new Alternatives(selected, indexes.ToArray());
                    groups.Add(key, group);
                }

                return group;
            }

            // When some alternatives skip white space and others don't, a char doesn't tell which ones can match: a white
            // space can be the start of an alternative or the white space before another one
            if (anySkip && !allSkip)
            {
                return new Table(invoked, false, null, Group(static _ => true));
            }

            // An alternative which can start with any char is invoked for each one
            var any = new bool[count];
            var anyNonAscii = new bool[count];
            var chars = new List<char>();

            for (var i = 0; i < count; i++)
            {
                var expected = hints[i].ExpectedChars;

                any[i] = !hints[i].CanSeek || Array.IndexOf(expected, OneOf<T>.OtherSeekableChar) >= 0;
                anyNonAscii[i] = !any[i] && Array.IndexOf(expected, OneOf<T>.NonAsciiSeekableChar) >= 0;

                if (any[i])
                {
                    continue;
                }

                foreach (var c in expected)
                {
                    if (c != OneOf<T>.NonAsciiSeekableChar && !chars.Contains(c))
                    {
                        chars.Add(c);
                    }
                }
            }

            var hasNonAscii = Array.IndexOf(anyNonAscii, true) >= 0;

            if (chars.Count == 0 && !hasNonAscii)
            {
                // No alternative tells what it starts with
                return new Table(invoked, allSkip, null, Group(static _ => true));
            }

            var entries = new List<KeyValuePair<char, Alternatives>>(chars.Count);

            foreach (var c in chars)
            {
                var group = Group(i => any[i] || (c >= 0x80 && anyNonAscii[i]) || Array.IndexOf(hints[i].ExpectedChars, c) >= 0);
                entries.Add(new KeyValuePair<char, Alternatives>(c, group!));
            }

            var map = new CharMap<Alternatives>(entries);

            // The non-ASCII chars without an entry select the alternatives which can start with any of them
            if (hasNonAscii)
            {
                map.NonAsciiFallback = Group(i => any[i] || anyNonAscii[i]);
            }

            return new Table(invoked, allSkip, map, Group(i => any[i]));
        }
    }
}
