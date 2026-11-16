using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Parlot.Fluent;

public sealed class Separated<U, T> : Parser<IReadOnlyList<T>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, ISeparatedList
{
    private readonly Parser<U> _separator;
    private readonly Parser<T> _parser;

    // The element which ends the list, like a rest element, or null
    private readonly Parser<T>? _last;
    private readonly int _min;
    private readonly int _max;
    private readonly bool _removeEmptyEntries;
    private readonly bool _allowLeadingSeparator;
    private readonly bool _allowTrailingSeparator;

    // Whether an option or a last element is set, so that Parse doesn't run the loop without options
    private readonly bool _hasOptions;

    // Whether the parsers are invoked without their value when it isn't used, the separator's never is
    private readonly bool _separatorWithoutValue;
    private readonly bool _withoutValue;
    private readonly bool _lastWithoutValue;

    // Whether the separator can start at the current position, so the list ends without invoking it when it can't
    private readonly SeekGuard _separatorGuard;

    // Whether the last element can start at the current position, so it isn't invoked when it can't
    private readonly SeekGuard? _lastGuard;

    public Separated(Parser<U> separator, Parser<T> parser) : this(separator, parser, 1, 0)
    {
    }

    /// <summary>Creates a parser for between <paramref name="min"/> and <paramref name="max"/> separated values. Zero maximum means unlimited.</summary>
    public Separated(Parser<U> separator, Parser<T> parser, int min = 1, int max = 0)
        : this(separator, parser, min, max, false, false, false)
    {
    }

    public Separated(
        Parser<U> separator,
        Parser<T> parser,
        bool removeEmptyEntries = false,
        bool allowLeadingSeparator = false,
        bool allowTrailingSeparator = false)
        : this(separator, parser, 1, 0, removeEmptyEntries, allowLeadingSeparator, allowTrailingSeparator)
    {
    }

    /// <summary>Creates a bounded separated parser with configurable separator handling. Zero maximum means unlimited.</summary>
    public Separated(
        Parser<U> separator,
        Parser<T> parser,
        int min,
        int max,
        bool removeEmptyEntries,
        bool allowLeadingSeparator,
        bool allowTrailingSeparator)
    {
        _separator = separator ?? throw new ArgumentNullException(nameof(separator));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        ThrowHelper.ThrowIfNegative(min, nameof(min));
        ThrowHelper.ThrowIfNegative(max, nameof(max));
        if (max != 0 && max < min)
        {
            throw new ArgumentOutOfRangeException(nameof(max));
        }
        _min = min;
        _max = max;
        _removeEmptyEntries = removeEmptyEntries;
        _allowLeadingSeparator = allowLeadingSeparator;
        _allowTrailingSeparator = allowTrailingSeparator;
        _separatorWithoutValue = separator.CanParseWithoutValue;
        _withoutValue = parser.CanParseWithoutValue;
        _separatorGuard = new SeekGuard(separator);
        _hasOptions = removeEmptyEntries || allowLeadingSeparator || allowTrailingSeparator;
    }

    /// <summary>
    /// Creates a parser for values separated by <paramref name="separator"/> which can end with a <paramref name="last"/>
    /// element of another kind, like the rest element of a list of parameters, which no separator follows.
    /// </summary>
    /// <param name="separator">The separator between values.</param>
    /// <param name="parser">The parser for each value but the last one.</param>
    /// <param name="last">The parser for an element which ends the list, tried where <paramref name="parser"/> doesn't match.</param>
    /// <param name="min">The minimum number of values, the last one included.</param>
    /// <param name="allowTrailingSeparator">Whether to consume a separator after the last value of <paramref name="parser"/>.</param>
    /// <param name="removeEmptyEntries">Whether to skip successive separators, before the last element too.</param>
    /// <remarks>
    /// An element is only found when it reads text. No separator is consumed after <paramref name="last"/>, even with
    /// <paramref name="allowTrailingSeparator"/>.
    /// </remarks>
    public Separated(
        Parser<U> separator,
        Parser<T> parser,
        Parser<T> last,
        int min = 0,
        bool allowTrailingSeparator = false,
        bool removeEmptyEntries = false)
        : this(separator, parser, min, 0, removeEmptyEntries, false, allowTrailingSeparator)
    {
        _last = last ?? throw new ArgumentNullException(nameof(last));
        _lastWithoutValue = last.CanParseWithoutValue;
        _lastGuard = new SeekGuard(last);
        _hasOptions = true;
    }

    // A list with a last element can start with it, one with a leading separator with the separator, and an empty list
    // matches without its first parser
    bool ISeparatedList.FirstMustSucceed => _last is null && !_allowLeadingSeparator && _min > 0;

    object? ISeparatedList.Last => _last;

    // The hints of the first element, read when they are asked for since it can be a Deferred set later. There are none when
    // the list can be empty or start with a separator.
    private ForwardedHints? _hints;

    public bool CanSeek => Hints.CanSeek;

    public char[] ExpectedChars => Hints.ExpectedChars;

    public bool SkipWhitespace => Hints.SkipWhitespace;

    // With a last element, the list starts with either element
    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, this, static self => self._min == 0 || self._allowLeadingSeparator
        ? (false, [], false)
        : self._last is null ? SeekableHints.Of(self._parser) : SeekableHints.Union(self._parser, self._last));

    string[]? ISeekablePrefixes.ExpectedPrefixes => _last is null ? SeekableHints.Prefixes(_parser) : UnionPrefixes();

    // The prefixes of both elements, kept until SeekableResolution.Version changes, like the hints and LeadingWhiteSpace.Cache
    private PrefixesCache? _prefixes;

    private sealed class PrefixesCache
    {
        public PrefixesCache(int version, string[]? prefixes)
        {
            Version = version;
            Prefixes = prefixes;
        }

        public int Version { get; }

        public string[]? Prefixes { get; }
    }

    private string[]? UnionPrefixes()
    {
        // The prefixes are only read where the list can seek: both elements tell their chars and skip white space the same way
        if (!Hints.CanSeek)
        {
            return null;
        }

        var version = SeekableResolution.Version;
        var cached = Volatile.Read(ref _prefixes);

        if (cached == null || cached.Version != version)
        {
            string[]? prefixes = null;

            if (ElementPrefixes(_parser) is { } first && ElementPrefixes(_last!) is { } second)
            {
                var union = new List<string>(first);

                foreach (var prefix in second)
                {
                    if (!union.Contains(prefix))
                    {
                        union.Add(prefix);
                    }
                }

                prefixes = union.ToArray();
            }

            cached = new PrefixesCache(version, prefixes);
            Volatile.Write(ref _prefixes, cached);
        }

        return cached.Prefixes;
    }

    // The prefixes of an element, or its chars as prefixes of one char when it doesn't tell more, like a choice reads them
    private static string[]? ElementPrefixes(Parser<T> element)
    {
        if (SeekableHints.Prefixes(element) is { } prefixes)
        {
            return prefixes;
        }

        var hints = SeekableHints.Of(element);

        if (!hints.CanSeek || !SeekableHints.IsExact(hints.ExpectedChars))
        {
            return null;
        }

        var chars = new string[hints.ExpectedChars.Length];

        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = hints.ExpectedChars[i].ToString();
        }

        return chars;
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        if (_last is not null)
        {
            // Both elements can be tried where the list starts, and each one counts only when it reads text
            analysis.Invoke(_parser);
            analysis.Invoke(_last);
            return _min == 0;
        }

        // Without a last element, the options are the ones of _hasOptions
        var options = _hasOptions;

        // Without the options a first element that doesn't move the cursor is a value, the separator is then invoked where
        // the list started. With them it is an empty entry, and the separator only follows one when it can lead the list.
        var emptyFirst = analysis.Invoke(_parser) && !options;

        if (emptyFirst || _allowLeadingSeparator)
        {
            analysis.Invoke(_separator);
        }

        // An element after the first one moves the cursor with its separator
        return _min == 0 || (emptyFirst && _min == 1);
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        if (_hasOptions)
        {
            if (_last is not null)
            {
                return ParseWithLast(context, ref result, withValue: true);
            }

            context.EnterParser(this);
            return ParseWithOptions(context, ref result);
        }

        context.EnterParser(this);

        var end = context.Scanner.Cursor.Offset;
        var initialPosition = end;

        // An empty result is at the current position
        var start = end;

        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();
        var results = new ListBuilder<T>();

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin();

        try
        {
            while (_max == 0 || results.Count < _max)
            {
                var first = results.Count == 0;
                var previousOffset = context.Scanner.Cursor.Offset;

                if (!first)
                {
                    if (!_separatorGuard.CanStart(context) || !(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
                    {
                        break;
                    }
                }

                if (!_parser.Parse(context, ref parsed))
                {
                    if (!first)
                    {
                        // A separator was found, but not followed by another value.
                        // It's still successful if there was one value parsed, but we reset the cursor to before the separator
                        context.Scanner.Cursor.ResetPosition(end);
                        break;
                    }

                    if (_min > 0)
                    {
                        context.Unpin(pin);
                        context.ExitParser(this);
                        return false;
                    }
                    break;
                }
                else
                {
                    end = context.Scanner.Cursor.Offset;
                    context.MovePin(pin);
                }

                // The list ends when a separator and its element don't move the cursor, as they would match again. The first
                // element has no separator and is only read once, it is a value even when it doesn't move the cursor.
                if (first)
                {
                    start = parsed.Start;
                }
                else if (context.Scanner.Cursor.Offset == previousOffset)
                {
                    break;
                }

                results.Add(parsed.Value);
            }

            context.Unpin(pin);

            if (_min > 1 && results.Count < _min)
            {
                context.Scanner.Cursor.ResetPosition(initialPosition);
                context.ExitParser(this);
                return false;
            }

            result.Set(start, end, results.ToArray());
        }
        finally
        {
            results.Release();
        }

        context.ExitParser(this);
        return true;
    }

    internal override bool CanParseWithoutValue => !_hasOptions || _last is not null;

    // The loop of Parse, without the list. The options keep a single implementation, the last element one for both.
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        if (_hasOptions)
        {
            return _last is not null ? ParseWithLast(context, ref result, withValue: false) : Parse(context, ref result);
        }

        context.EnterParser(this);

        var end = context.Scanner.Cursor.Offset;
        var initialPosition = end;

        // An empty result is at the current position
        var start = end;

        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();
        var count = 0;

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin();

        while (_max == 0 || count < _max)
        {
            var first = count == 0;
            var previousOffset = context.Scanner.Cursor.Offset;

            if (!first)
            {
                if (!_separatorGuard.CanStart(context) || !(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
                {
                    break;
                }
            }

            if (!(_withoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed)))
            {
                if (!first)
                {
                    // A separator was found, but not followed by another value: the cursor is reset to before the separator
                    context.Scanner.Cursor.ResetPosition(end);
                    break;
                }

                if (_min > 0)
                {
                    context.Unpin(pin);
                    context.ExitParser(this);
                    return false;
                }
                break;
            }
            else
            {
                end = context.Scanner.Cursor.Offset;
                context.MovePin(pin);
            }

            // The first element is a value even when it doesn't move the cursor, see Parse()
            if (first)
            {
                start = parsed.Start;
            }
            else if (context.Scanner.Cursor.Offset == previousOffset)
            {
                break;
            }

            count++;
        }

        context.Unpin(pin);

        if (_min > 1 && count < _min)
        {
            context.Scanner.Cursor.ResetPosition(initialPosition);
            context.ExitParser(this);
            return false;
        }

        result.Start = start;
        result.End = end;

        context.ExitParser(this);
        return true;
    }

    // An element is tried where the list starts and after each separator: a value of the parser, then the separator, or the
    // last element, which ends the list. An element is only found when it reads text.
    private bool ParseWithLast(ParseContext context, ref ParseResult<IReadOnlyList<T>> result, bool withValue)
    {
        // Without the value nothing is added to the builder, so there is nothing to release
        if (!withValue)
        {
            var none = new ListBuilder<T>();
            return ParseWithLast(context, ref result, ref none, withValue: false);
        }

        var results = new ListBuilder<T>();

        try
        {
            return ParseWithLast(context, ref result, ref results, withValue: true);
        }
        finally
        {
            results.Release();
        }
    }

    private bool ParseWithLast(ParseContext context, ref ParseResult<IReadOnlyList<T>> result, ref ListBuilder<T> results, bool withValue)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var initial = cursor.Offset;
        var start = initial;
        var end = initial;
        var count = 0;
        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();
        var parserWithoutValue = !withValue && _withoutValue;
        var lastWithoutValue = !withValue && _lastWithoutValue;

        // The end of the last element is read again when a separator isn't followed by an element, and the last element is
        // read where the parser failed
        var pin = context.Pin();

        while (true)
        {
            var before = cursor.Offset;
            var isLast = false;

            if (!((parserWithoutValue ? _parser.ParseWithoutValue(context, ref parsed) : _parser.Parse(context, ref parsed)) && cursor.Offset != before))
            {
                if (!(_lastGuard!.CanStart(context) && (lastWithoutValue ? _last!.ParseWithoutValue(context, ref parsed) : _last!.Parse(context, ref parsed)) && cursor.Offset != before))
                {
                    // No element where the list starts: a separator can't lead it
                    if (count == 0)
                    {
                        break;
                    }

                    // An empty entry, the separator which follows it is skipped
                    if (_removeEmptyEntries)
                    {
                        var beforeSeparator = cursor.Offset;

                        if (_separatorGuard.CanStart(context) && (_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)) && cursor.Offset != beforeSeparator)
                        {
                            continue;
                        }
                    }

                    // A separator not followed by an element
                    if (_allowTrailingSeparator)
                    {
                        end = cursor.Offset;
                    }
                    else
                    {
                        cursor.ResetPosition(end);
                    }

                    break;
                }

                isLast = true;
            }

            if (count++ == 0)
            {
                start = parsed.Start;
            }

            if (withValue)
            {
                results.Add(parsed.Value);
            }

            end = cursor.Offset;
            context.MovePin(pin);

            // No separator follows the last element
            if (isLast || !_separatorGuard.CanStart(context) || !(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
            {
                break;
            }
        }

        context.Unpin(pin);

        if (count < _min)
        {
            cursor.ResetPosition(initial);
            context.ExitParser(this);
            return false;
        }

        if (withValue)
        {
            result.Set(start, end, results.ToArray());
        }
        else
        {
            result.Start = start;
            result.End = end;
        }

        context.ExitParser(this);
        return true;
    }

    private bool ParseWithOptions(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        var cursor = context.Scanner.Cursor;
        var initial = cursor.Offset;
        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin();
        var found = _parser.Parse(context, ref parsed) && cursor.Offset != initial;

        if (!found && _allowLeadingSeparator)
        {
            while (true)
            {
                var beforeSeparator = cursor.Offset;
                if (!_separatorGuard.CanStart(context) || !(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)) || cursor.Offset == beforeSeparator)
                {
                    break;
                }

                var beforeValue = cursor.Offset;
                if (_parser.Parse(context, ref parsed) && cursor.Offset != beforeValue)
                {
                    found = true;
                    break;
                }

                if (!_removeEmptyEntries)
                {
                    break;
                }
            }
        }

        if (!found)
        {
            context.Unpin(pin);
            cursor.ResetPosition(initial);
            context.ExitParser(this);
            if (_min == 0)
            {
                result.Set(initial, initial, (IReadOnlyList<T>)[]);
                return true;
            }
            return false;
        }

        var start = parsed.Start;
        var end = cursor.Offset;
        var results = new ListBuilder<T>();
        results.Add(parsed.Value);
        context.MovePin(pin);

        try
        {
            while (_max == 0 || results.Count < _max)
            {
                if (!_separatorGuard.CanStart(context) || !(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
                {
                    break;
                }

                var foundNext = false;
                while (true)
                {
                    var beforeValue = cursor.Offset;
                    if (_parser.Parse(context, ref parsed) && cursor.Offset != beforeValue)
                    {
                        foundNext = true;
                        break;
                    }

                    if (!_removeEmptyEntries)
                    {
                        break;
                    }

                    var beforeSeparator = cursor.Offset;
                    if (!_separatorGuard.CanStart(context) || !(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)) || cursor.Offset == beforeSeparator)
                    {
                        break;
                    }
                }

                if (!foundNext)
                {
                    if (_allowTrailingSeparator)
                    {
                        end = cursor.Offset;
                    }
                    else
                    {
                        cursor.ResetPosition(end);
                    }

                    break;
                }

                end = cursor.Offset;
                context.MovePin(pin);
                results.Add(parsed.Value);
            }

            context.Unpin(pin);

            if (results.Count < _min)
            {
                cursor.ResetPosition(initial);
                context.ExitParser(this);
                return false;
            }

            result.Set(start, end, results.ToArray());
        }
        finally
        {
            results.Release();
        }

        context.ExitParser(this);
        return true;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_last is not null)
        {
            return GenerateSourceWithLast(context);
        }

        if (_hasOptions)
        {
            return GenerateSourceWithOptions(context);
        }

        if (_parser is not ISourceable parserSourceable || _separator is not ISourceable separatorSourceable)
        {
            throw new NotSupportedException("Separated requires source-generatable parsers.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>), defaultSuccess: false, defaultValueExpression: $"global::System.Array.Empty<{elementTypeName}>()");
        var cursorName = context.CursorName;

        var listName = $"list{context.NextNumber()}";
        var firstName = $"first{context.NextNumber()}";
        var endName = $"end{context.NextNumber()}";
        var previousOffsetName = $"previousOffset{context.NextNumber()}";
        var countName = $"count{context.NextNumber()}";
        var initialPositionName = $"initialPosition{context.NextNumber()}";
        var itemName = $"item{context.NextNumber()}";
        var needsCount = _max > 0 || _min > 1;

        if (!context.DiscardResult)
        {
            result.Body.Add(ListBuilderSource.Declare(listName, elementTypeName));
        }
        result.Body.Add($"bool {firstName} = true;");
        result.Body.Add($"var {endName} = {cursorName}.Offset;");
        if (_min > 1)
        {
            result.Body.Add($"var {initialPositionName} = {endName};");
        }
        if (needsCount)
        {
            result.Body.Add($"int {countName} = 0;");
        }

        // Use helpers instead of inlining
        var parserHelperName = GetElementHelper(context, parserSourceable, "Parser");
        var separatorHelperName = GetSeparatorHelper(context, separatorSourceable);

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin(result);
        var bodyStart = result.Body.Count;
        result.Body.Add(_max == 0 ? "while (true)" : $"while ({countName} < {_max})");
        result.Body.Add("{");
        result.Body.Add($"    var {previousOffsetName} = {cursorName}.Offset;");

        // If not first, try to parse separator
        result.Body.Add($"    if (!{firstName})");
        result.Body.Add("    {");
        result.Body.Add($"        if (!{separatorHelperName}({context.ParseContextName}, out _))");
        result.Body.Add("        {");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add("    }");

        // Try to parse element
        result.Body.Add($"    if (!{parserHelperName}({context.ParseContextName}, out var {itemName}))");
        result.Body.Add("    {");
        result.Body.Add($"        if (!{firstName})");
        result.Body.Add("        {");
        result.Body.Add($"            {cursorName}.ResetPosition({endName});");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        {result.SuccessVariable} = false;");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {endName} = {cursorName}.Offset;");
        context.MovePin(result, pin, "        ");
        result.Body.Add("    }");

        // The first element is a value even when it doesn't move the cursor, see Parse()
        result.Body.Add($"    if (!{firstName} && {cursorName}.Offset == {previousOffsetName})");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");

        result.Body.Add($"    {firstName} = false;");

        if (!context.DiscardResult)
        {
            result.Body.Add($"    {listName}.Add({itemName});");
        }
        if (needsCount)
        {
            result.Body.Add($"    {countName}++;");
        }
        result.Body.Add("}");
        context.Unpin(result, pin);

        if (_min > 1)
        {
            result.Body.Add($"if ({countName} < {_min})");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({initialPositionName});");
            result.Body.Add("}");
        }

        // Separated requires at least one element, unless its minimum is zero
        result.Body.Add($"if (!{firstName})");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = {(_min > 1 ? $"{countName} >= {_min}" : "true")};");
        if (!context.DiscardResult)
        {
            result.Body.Add(_min > 1 ? $"    if ({result.SuccessVariable}) {result.ValueVariable} = {listName}.ToArray();" : $"    {result.ValueVariable} = {listName}.ToArray();");
        }
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = {(_min == 0 ? "true" : "false")};");
        result.Body.Add("}");

        if (!context.DiscardResult)
        {
            ListBuilderSource.ReleaseInFinally(result, bodyStart, listName);
        }

        return result;
    }

    // The code of ParseWithLast
    private SourceResult GenerateSourceWithLast(SourceGenerationContext context)
    {
        if (_parser is not ISourceable parserSourceable || _last is not ISourceable lastSourceable || _separator is not ISourceable separatorSourceable)
        {
            throw new NotSupportedException("Separated requires source-generatable parsers.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>), defaultSuccess: false, defaultValueExpression: $"global::System.Array.Empty<{elementTypeName}>()");
        var cursor = context.CursorName;
        var parseContext = context.ParseContextName;
        var parserHelper = GetElementHelper(context, parserSourceable, "Parser");
        var lastHelper = GetElementHelper(context, lastSourceable, "Last");

        // The last element isn't invoked when it can't start at the current position
        lastHelper = SeekGuard.GetHelper(context, _last, lastHelper, typeof(T));
        var separatorHelper = GetSeparatorHelper(context, separatorSourceable);

        var initial = $"initial{context.NextNumber()}";
        var end = $"end{context.NextNumber()}";
        var count = $"count{context.NextNumber()}";
        var before = $"before{context.NextNumber()}";
        var isLast = $"isLast{context.NextNumber()}";
        var beforeSeparator = $"beforeSeparator{context.NextNumber()}";
        var item = $"item{context.NextNumber()}";
        var list = $"list{context.NextNumber()}";
        var itemValue = context.DiscardResult ? "_" : item;

        result.Body.Add($"var {initial} = {cursor}.Offset;");
        result.Body.Add($"var {end} = {initial};");
        result.Body.Add($"int {count} = 0;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"{elementTypeName} {item} = default!;");
            result.Body.Add(ListBuilderSource.Declare(list, elementTypeName));
        }

        // The end of the last element is read again when a separator isn't followed by an element, and the last element is
        // read where the parser failed
        var pin = context.Pin(result);
        var bodyStart = result.Body.Count;
        result.Body.Add("while (true)");
        result.Body.Add("{");
        result.Body.Add($"    var {before} = {cursor}.Offset;");
        result.Body.Add($"    var {isLast} = false;");
        result.Body.Add($"    if (!({parserHelper}({parseContext}, out {itemValue}) && {cursor}.Offset != {before}))");
        result.Body.Add("    {");
        result.Body.Add($"        if (!({lastHelper}({parseContext}, out {itemValue}) && {cursor}.Offset != {before}))");
        result.Body.Add("        {");

        // No element where the list starts: a separator can't lead it
        result.Body.Add($"            if ({count} == 0)");
        result.Body.Add("            {");
        result.Body.Add("                break;");
        result.Body.Add("            }");
        if (_removeEmptyEntries)
        {
            // An empty entry, the separator which follows it is skipped
            result.Body.Add($"            var {beforeSeparator} = {cursor}.Offset;");
            result.Body.Add($"            if ({separatorHelper}({parseContext}, out _) && {cursor}.Offset != {beforeSeparator})");
            result.Body.Add("            {");
            result.Body.Add("                continue;");
            result.Body.Add("            }");
        }

        // A separator not followed by an element
        result.Body.Add(_allowTrailingSeparator ? $"            {end} = {cursor}.Offset;" : $"            {cursor}.ResetPosition({end});");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        {isLast} = true;");
        result.Body.Add("    }");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {list}.Add({item});");
        }
        result.Body.Add($"    {count}++;");
        result.Body.Add($"    {end} = {cursor}.Offset;");
        context.MovePin(result, pin, "    ");

        // No separator follows the last element
        result.Body.Add($"    if ({isLast} || !{separatorHelper}({parseContext}, out _))");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        result.Body.Add("}");
        context.Unpin(result, pin);

        if (_min > 0)
        {
            result.Body.Add($"if ({count} < {_min})");
            result.Body.Add("{");
            result.Body.Add($"    {cursor}.ResetPosition({initial});");
            result.Body.Add("}");
            result.Body.Add("else");
        }
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {list}.ToArray();");
        }
        result.Body.Add("}");

        if (!context.DiscardResult)
        {
            ListBuilderSource.ReleaseInFinally(result, bodyStart, list);
        }

        return result;
    }

    // The helper of an element, which returns its value unless the list's value is discarded
    private static string GetElementHelper(SourceGenerationContext context, ISourceable element, string role) =>
        context.Helpers
            .GetOrCreate(element, $"{context.MethodNamePrefix}_Separated_{role}", SourceGenerationContext.GetTypeName(typeof(T)), () => element.GenerateSource(context))
            .MethodName;

    // The helper of the separator, whose value is never used
    private string GetSeparatorHelper(SourceGenerationContext context, ISourceable separator) =>
        context.WithDiscardResult(true, () =>
        {
            var helper = context.Helpers
                .GetOrCreate(separator, $"{context.MethodNamePrefix}_Separated_Separator", SourceGenerationContext.GetTypeName(typeof(U)), () => separator.GenerateSource(context))
                .MethodName;

            // The list ends without invoking the separator when it can't start at the current position
            return SeekGuard.GetHelper(context, _separator, helper, typeof(U));
        });

    private SourceResult GenerateSourceWithOptions(SourceGenerationContext context)
    {
        if (_parser is not ISourceable parserSourceable || _separator is not ISourceable separatorSourceable)
        {
            throw new NotSupportedException("Separated requires source-generatable parsers.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>), defaultSuccess: false, defaultValueExpression: $"global::System.Array.Empty<{elementTypeName}>()");
        var cursor = context.CursorName;
        var parseContext = context.ParseContextName;
        var parserHelper = GetElementHelper(context, parserSourceable, "Parser");
        var separatorHelper = GetSeparatorHelper(context, separatorSourceable);
        var initial = $"initial{context.NextNumber()}";
        var beforeSeparator = $"beforeSeparator{context.NextNumber()}";
        var beforeValue = $"beforeValue{context.NextNumber()}";
        var found = $"found{context.NextNumber()}";
        var foundNext = $"foundNext{context.NextNumber()}";
        var firstItem = $"firstItem{context.NextNumber()}";
        var nextItem = $"nextItem{context.NextNumber()}";
        var list = $"list{context.NextNumber()}";
        var end = $"end{context.NextNumber()}";
        var count = $"count{context.NextNumber()}";
        var needsCount = _max > 0 || _min > 1;
        var firstValue = context.DiscardResult ? "_" : $"var {firstItem}";
        var subsequentFirstValue = context.DiscardResult ? "_" : firstItem;
        var nextValue = context.DiscardResult ? "_" : nextItem;

        result.Body.Add($"var {initial} = {cursor}.Offset;");

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin(result);
        result.Body.Add($"bool {found} = {parserHelper}({parseContext}, out {firstValue}) && {cursor}.Offset != {initial};");

        if (_allowLeadingSeparator)
        {
            result.Body.Add($"if (!{found})");
            result.Body.Add("{");
            result.Body.Add("    while (true)");
            result.Body.Add("    {");
            result.Body.Add($"        var {beforeSeparator} = {cursor}.Offset;");
            result.Body.Add($"        if (!{separatorHelper}({parseContext}, out _) || {cursor}.Offset == {beforeSeparator})");
            result.Body.Add("        {");
            result.Body.Add("            break;");
            result.Body.Add("        }");
            result.Body.Add($"        var {beforeValue} = {cursor}.Offset;");
            result.Body.Add($"        if ({parserHelper}({parseContext}, out {subsequentFirstValue}) && {cursor}.Offset != {beforeValue})");
            result.Body.Add("        {");
            result.Body.Add($"            {found} = true;");
            result.Body.Add("            break;");
            result.Body.Add("        }");
            if (!_removeEmptyEntries)
            {
                result.Body.Add("        break;");
            }
            result.Body.Add("    }");
            result.Body.Add("}");
        }

        result.Body.Add($"if (!{found})");
        result.Body.Add("{");
        result.Body.Add($"    {cursor}.ResetPosition({initial});");
        if (_min == 0)
        {
            result.Body.Add($"    {result.SuccessVariable} = true;");
        }
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        if (needsCount)
        {
            result.Body.Add($"    int {count} = 1;");
        }
        var bodyStart = -1;
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {ListBuilderSource.Declare(list, elementTypeName)}");
            result.Body.Add($"    {list}.Add({firstItem});");
            bodyStart = result.Body.Count;
        }
        if (!_allowTrailingSeparator)
        {
            result.Body.Add($"    var {end} = {cursor}.Offset;");
        }
        context.MovePin(result, pin, "    ");
        result.Body.Add(_max == 0 ? "    while (true)" : $"    while ({count} < {_max})");
        result.Body.Add("    {");
        result.Body.Add($"        if (!{separatorHelper}({parseContext}, out _))");
        result.Body.Add("        {");
        result.Body.Add("            break;");
        result.Body.Add("        }");
        result.Body.Add($"        bool {foundNext} = false;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {elementTypeName} {nextItem} = default!;");
        }
        result.Body.Add("        while (true)");
        result.Body.Add("        {");
        result.Body.Add($"            var {beforeValue} = {cursor}.Offset;");
        result.Body.Add($"            if ({parserHelper}({parseContext}, out {nextValue}) && {cursor}.Offset != {beforeValue})");
        result.Body.Add("            {");
        result.Body.Add($"                {foundNext} = true;");
        result.Body.Add("                break;");
        result.Body.Add("            }");
        if (_removeEmptyEntries)
        {
            result.Body.Add($"            var {beforeSeparator} = {cursor}.Offset;");
            result.Body.Add($"            if (!{separatorHelper}({parseContext}, out _) || {cursor}.Offset == {beforeSeparator})");
            result.Body.Add("            {");
            result.Body.Add("                break;");
            result.Body.Add("            }");
        }
        else
        {
            result.Body.Add("            break;");
        }
        result.Body.Add("        }");
        result.Body.Add($"        if (!{foundNext})");
        result.Body.Add("        {");
        if (!_allowTrailingSeparator)
        {
            result.Body.Add($"            {cursor}.ResetPosition({end});");
        }
        result.Body.Add("            break;");
        result.Body.Add("        }");
        if (!_allowTrailingSeparator)
        {
            result.Body.Add($"        {end} = {cursor}.Offset;");
        }
        context.MovePin(result, pin, "        ");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {list}.Add({nextItem});");
        }
        if (needsCount)
        {
            result.Body.Add($"        {count}++;");
        }
        result.Body.Add("    }");
        if (_min > 1)
        {
            result.Body.Add($"    if ({count} < {_min})");
            result.Body.Add("    {");
            result.Body.Add($"        {cursor}.ResetPosition({initial});");
            result.Body.Add("    }");
        }
        result.Body.Add($"    {result.SuccessVariable} = {(_min > 1 ? $"{count} >= {_min}" : "true")};");
        if (!context.DiscardResult)
        {
            result.Body.Add(_min > 1
                ? $"    if ({result.SuccessVariable}) {result.ValueVariable} = {list}.ToArray();"
                : $"    {result.ValueVariable} = {list}.ToArray();");
            ListBuilderSource.ReleaseInFinally(result, bodyStart, list, "    ");
        }
        result.Body.Add("}");
        context.Unpin(result, pin);

        return result;
    }

    public override string ToString() => _last is null ? $"Separated({_separator}, {_parser})" : $"Separated({_separator}, {_parser}, {_last})";
}

/// <summary>
/// What the grammar analysis reads of a <see cref="Separated{U, T}"/> list, whatever its value types.
/// </summary>
internal interface ISeparatedList
{
    /// <summary>
    /// Whether the list only succeeds when its first parser does, rather than with a last element, a leading separator or
    /// no value.
    /// </summary>
    bool FirstMustSucceed { get; }

    /// <summary>
    /// The element which ends the list, or <see langword="null"/>.
    /// </summary>
    object? Last { get; }
}
