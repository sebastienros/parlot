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

    public Separated(Parser<U> separator, Parser<T> parser) : this(separator, parser, 1, 0)
    {
    }

    /// <summary>Creates a parser for between <paramref name="min"/> and <paramref name="max"/> separated values. Zero maximum means unlimited.</summary>
    public Separated(Parser<U> separator, Parser<T> parser, int min = 1, int max = 0)
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

        if (min > 0 && _parser is ISeekable seekable)
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

        HybridList<T>? results = null;

        var start = 0;
        var end = context.Scanner.Cursor.Position;
        var initialPosition = end;

        var first = true;
        var parsed = new ParseResult<T>();
        var separatorResult = new ParseResult<U>();

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
                    context.ExitParser(this);
                    return false;
                }
                break;
            }
            else
            {
                end = context.Scanner.Cursor.Position;
            }

            if (context.Scanner.Cursor.Offset == previousOffset)
            {
                if (first)
                {
                    if (_min > 0)
                    {
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


    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

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

    public override string ToString() => $"Separated({_separator}, {_parser})";
}
