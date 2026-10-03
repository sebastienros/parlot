using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;

namespace Parlot.Fluent;

public sealed class Separated<U, T> : Parser<IReadOnlyList<T>>, ISeekable, ISourceable
{
    private readonly Parser<U> _separator;
    private readonly Parser<T> _parser;
    private readonly int _min;
    private readonly int _max;
    private readonly bool _removeEmptyEntries;
    private readonly bool _allowLeadingSeparator;
    private readonly bool _allowTrailingSeparator;

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

        if (min > 0 && !allowLeadingSeparator && _parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        if (_removeEmptyEntries || _allowLeadingSeparator || _allowTrailingSeparator)
        {
            return ParseWithOptions(context, ref result);
        }

        HybridList<T>? results = null;

        var start = 0;
        var end = context.Scanner.Cursor.Position;
        var initialPosition = end;

        var first = true;
        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin();

        while (_max == 0 || (results?.Count ?? 0) < _max)
        {
            var previousOffset = context.Scanner.Cursor.Offset;

            if (!first)
            {
                if (!_separator.Parse(context, ref separatorResult))
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
                end = context.Scanner.Cursor.Position;
                context.MovePin(pin);
            }

            if (context.Scanner.Cursor.Offset == previousOffset)
            {
                if (first)
                {
                    if (_min > 0)
                    {
                        context.Unpin(pin);
                        context.ExitParser(this);
                        return false;
                    }
                    break;
                }

                break;
            }

            if (first)
            {
                results = [];
                start = parsed.Start;
                first = false;
            }

            results!.Add(parsed.Value);
        }

        context.Unpin(pin);

        if (_min > 1 && results!.Count < _min)
        {
            context.Scanner.Cursor.ResetPosition(initialPosition);
            context.ExitParser(this);
            return false;
        }

        result.Set(start, end.Offset, results?.AsReadOnlyList() ?? (IReadOnlyList<T>)[]);

        context.ExitParser(this);
        return true;
    }

    private bool ParseWithOptions(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        var cursor = context.Scanner.Cursor;
        var initial = cursor.Position;
        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();

        // The end of the last element is read again when a separator isn't followed by an element
        var pin = context.Pin();
        var found = _parser.Parse(context, ref parsed) && cursor.Offset != initial.Offset;

        if (!found && _allowLeadingSeparator)
        {
            while (true)
            {
                var beforeSeparator = cursor.Offset;
                if (!_separator.Parse(context, ref separatorResult) || cursor.Offset == beforeSeparator)
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
                result.Set(0, initial.Offset, (IReadOnlyList<T>)[]);
                return true;
            }
            return false;
        }

        var start = parsed.Start;
        var end = cursor.Position;
        var results = new HybridList<T> { parsed.Value };
        context.MovePin(pin);

        while (_max == 0 || results.Count < _max)
        {
            if (!_separator.Parse(context, ref separatorResult))
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
                if (!_separator.Parse(context, ref separatorResult) || cursor.Offset == beforeSeparator)
                {
                    break;
                }
            }

            if (!foundNext)
            {
                if (_allowTrailingSeparator)
                {
                    end = cursor.Position;
                }
                else
                {
                    cursor.ResetPosition(end);
                }

                break;
            }

            end = cursor.Position;
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

        result.Set(start, end.Offset, results.AsReadOnlyList());
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
        var startName = $"start{context.NextNumber()}";
        var previousOffsetName = $"previousOffset{context.NextNumber()}";
        var countName = $"count{context.NextNumber()}";
        var initialPositionName = $"initialPosition{context.NextNumber()}";
        var needsCount = _max > 0 || _min > 1;

        if (!context.DiscardResult)
        {
            result.Body.Add($"global::Parlot.Fluent.HybridList<{elementTypeName}>? {listName} = null;");
        }
        result.Body.Add($"bool {firstName} = true;");
        result.Body.Add($"var {endName} = {cursorName}.Position;");
        if (_min > 1)
        {
            result.Body.Add($"var {initialPositionName} = {endName};");
        }
        if (needsCount)
        {
            result.Body.Add($"int {countName} = 0;");
        }
        if (!context.DiscardResult)
        {
            result.Body.Add($"int {startName} = 0;");
        }

        var parserValueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var separatorValueTypeName = SourceGenerationContext.GetTypeName(typeof(U));

        // Use helpers instead of inlining
        var parserHelperName = context.Helpers
            .GetOrCreate(parserSourceable, $"{context.MethodNamePrefix}_Separated_Parser", parserValueTypeName, () => parserSourceable.GenerateSource(context))
            .MethodName;

        var separatorHelperName = context.Helpers
            .GetOrCreate(separatorSourceable, $"{context.MethodNamePrefix}_Separated_Separator", separatorValueTypeName, () => separatorSourceable.GenerateSource(context))
            .MethodName;

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
        result.Body.Add($"    if (!{parserHelperName}({context.ParseContextName}, out var item{context.NextNumber()}))");
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
        result.Body.Add($"        {endName} = {cursorName}.Position;");
        result.Body.Add("    }");

        result.Body.Add($"    if ({cursorName}.Offset == {previousOffsetName})");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");

        result.Body.Add($"    if ({firstName})");
        result.Body.Add("    {");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {listName} = new global::Parlot.Fluent.HybridList<{elementTypeName}>();");
            result.Body.Add($"        {startName} = {endName}.Offset;");
        }
        result.Body.Add($"        {firstName} = false;");
        result.Body.Add("    }");

        if (!context.DiscardResult)
        {
            result.Body.Add($"    {listName}!.Add(item{context.NextNumber() - 1});");
        }
        if (needsCount)
        {
            result.Body.Add($"    {countName}++;");
        }
        result.Body.Add("}");

        if (_min > 1)
        {
            result.Body.Add($"if ({countName} < {_min})");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({initialPositionName});");
            result.Body.Add("}");
        }

        if (!context.DiscardResult)
        {
            result.Body.Add($"if ({listName} != null)");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = {listName}.AsReadOnlyList();");
            result.Body.Add($"    {result.SuccessVariable} = {(_min > 1 ? $"{countName} >= {_min}" : "true")};");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = global::System.Array.Empty<{elementTypeName}>();");
            // No items parsed - Separated requires at least one element
            result.Body.Add($"    {result.SuccessVariable} = {(_min == 0 ? "true" : "false")};");
            result.Body.Add("}");
        }
        else
        {
            // When discarding result, success depends on whether we parsed at least one item
            result.Body.Add($"if (!{firstName})");
            result.Body.Add("{");
            result.Body.Add($"    {result.SuccessVariable} = {(_min > 1 ? $"{countName} >= {_min}" : "true")};");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            // No items parsed - Separated requires at least one element
            result.Body.Add($"    {result.SuccessVariable} = {(_min == 0 ? "true" : "false")};");
            result.Body.Add("}");
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
        var separatorHelper = context.Helpers
            .GetOrCreate(separatorSourceable, $"{context.MethodNamePrefix}_Separated_Separator", separatorTypeName, () => separatorSourceable.GenerateSource(context))
            .MethodName;
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

        result.Body.Add($"var {initial} = {cursor}.Position;");
        result.Body.Add($"bool {found} = {parserHelper}({parseContext}, out {firstValue}) && {cursor}.Offset != {initial}.Offset;");

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
        if (!context.DiscardResult)
        {
            result.Body.Add($"    var {list} = new global::Parlot.Fluent.HybridList<{elementTypeName}>();");
            result.Body.Add($"    {list}.Add({firstItem});");
        }
        if (!_allowTrailingSeparator)
        {
            result.Body.Add($"    var {end} = {cursor}.Position;");
        }
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
            result.Body.Add($"        {end} = {cursor}.Position;");
        }
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {list}.Add({nextItem});");
        }
        if (needsCount)
        {
            result.Body.Add($"        {count}++;");
        }
        result.Body.Add("    }");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {list}.AsReadOnlyList();");
        }
        if (_min > 1)
        {
            result.Body.Add($"    if ({count} < {_min})");
            result.Body.Add("    {");
            result.Body.Add($"        {cursor}.ResetPosition({initial});");
            result.Body.Add("    }");
        }
        result.Body.Add($"    {result.SuccessVariable} = {(_min > 1 ? $"{count} >= {_min}" : "true")};");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"Separated({_separator}, {_parser})";
}
