using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public sealed class Separated<U, T> : Parser<IReadOnlyList<T>>, ISeekablePrefixes, ISourceable, ILoopAnalyzable
{
    private readonly Parser<U> _separator;
    private readonly Parser<T> _parser;
    private readonly int _min;
    private readonly int _max;
    private readonly bool _removeEmptyEntries;
    private readonly bool _allowLeadingSeparator;
    private readonly bool _allowTrailingSeparator;

    // Whether the parsers are invoked without their value when it isn't used, the separator's never is
    private readonly bool _separatorWithoutValue;
    private readonly bool _withoutValue;

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
    }

    // The hints of the first element, read when they are asked for since it can be a Deferred set later. There are none when
    // the list can be empty or start with a separator.
    private ForwardedHints? _hints;

    public bool CanSeek => Hints.CanSeek;

    public char[] ExpectedChars => Hints.ExpectedChars;

    public bool SkipWhitespace => Hints.SkipWhitespace;

    private ForwardedHints Hints => ForwardedHints.Get(ref _hints, this, static self => self._min > 0 && !self._allowLeadingSeparator ? SeekableHints.Of(self._parser) : (false, [], false));

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis)
    {
        analysis.Invoke(_parser);

        // Otherwise the separator only follows an element that moved the cursor
        if (_allowLeadingSeparator)
        {
            analysis.Invoke(_separator);
        }

        // The first element must move the cursor
        return _min == 0;
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        if (_removeEmptyEntries || _allowLeadingSeparator || _allowTrailingSeparator)
        {
            return ParseWithOptions(context, ref result);
        }

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
                    if (!(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
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

                if (context.Scanner.Cursor.Offset == previousOffset)
                {
                    if (first && _min > 0)
                    {
                        context.Unpin(pin);
                        context.ExitParser(this);
                        return false;
                    }

                    break;
                }

                if (first)
                {
                    start = parsed.Start;
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

    internal override bool CanParseWithoutValue => !(_removeEmptyEntries || _allowLeadingSeparator || _allowTrailingSeparator);

    // The loop of Parse, without the list. The options keep a single implementation.
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        if (_removeEmptyEntries || _allowLeadingSeparator || _allowTrailingSeparator)
        {
            return Parse(context, ref result);
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
                if (!(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
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

            if (context.Scanner.Cursor.Offset == previousOffset)
            {
                if (first && _min > 0)
                {
                    context.Unpin(pin);
                    context.ExitParser(this);
                    return false;
                }

                break;
            }

            if (first)
            {
                start = parsed.Start;
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
                if (!(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)) || cursor.Offset == beforeSeparator)
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
                if (!(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)))
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
                    if (!(_separatorWithoutValue ? _separator.ParseWithoutValue(context, ref separatorResult) : _separator.Parse(context, ref separatorResult)) || cursor.Offset == beforeSeparator)
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

        if (_removeEmptyEntries || _allowLeadingSeparator || _allowTrailingSeparator)
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

        var parserValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var separatorValueTypeName = SourceGenerationContext.GetTypeName(typeof(U));

        // Use helpers instead of inlining
        var parserHelperName = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_Separated_Parser", parserValueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

        var separatorHelperName = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(separatorSourceable, $"{context.MethodNamePrefix}_Separated_Separator", separatorValueTypeName, () => separatorSourceable.GenerateSource(context))
            .MethodName);

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

        result.Body.Add($"    if ({cursorName}.Offset == {previousOffsetName})");
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

    private SourceResult GenerateSourceWithOptions(SourceGenerationContext context)
    {
        if (_parser is not ISourceable parserSourceable || _separator is not ISourceable separatorSourceable)
        {
            throw new NotSupportedException("Separated requires source-generatable parsers.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var separatorTypeName = SourceGenerationContext.GetTypeName(typeof(U));
        var result = context.CreateResult(typeof(IReadOnlyList<T>), defaultSuccess: false, defaultValueExpression: $"global::System.Array.Empty<{elementTypeName}>()");
        var cursor = context.CursorName;
        var parseContext = context.ParseContextName;
        var parserHelper = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_Separated_Parser", elementTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;
        var separatorHelper = context.WithDiscardResult(true, () => context.Helpers
            .GetOrCreate(separatorSourceable, $"{context.MethodNamePrefix}_Separated_Separator", separatorTypeName, () => separatorSourceable.GenerateSource(context))
            .MethodName);
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

    public override string ToString() => $"Separated({_separator}, {_parser})";
}
