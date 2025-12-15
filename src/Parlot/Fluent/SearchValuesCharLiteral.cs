#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Buffers;

namespace Parlot.Fluent;

internal sealed class SearchValuesCharLiteral : Parser<TextSpan>, ISeekable, ISourceable
{
    private readonly SearchValues<char> _searchValues;
    private readonly string? _valuesString;
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public SearchValuesCharLiteral(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _searchValues = searchValues ?? throw new ArgumentNullException(nameof(searchValues));
        _valuesString = null; // Cannot extract string from SearchValues
        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;
    }

    public SearchValuesCharLiteral(ReadOnlySpan<char> searchValues, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _searchValues = SearchValues.Create(searchValues);
        _valuesString = searchValues.ToString();
        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;

        if (minSize > 0 && !_negate)
        {
            CanSeek = true;
            ExpectedChars = searchValues.ToArray();
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var span = context.Scanner.Cursor.Span;

        if (_minSize > span.Length)
        {
            return false;
        }

        // First char not matching the searched values
        var index = _negate ? span.IndexOfAny(_searchValues) : span.IndexOfAnyExcept(_searchValues);

        var size = 0;

        if (index != -1)
        {
            // Too small?
            if (index < _minSize)
            {
                context.ExitParser(this);
                return false;
            }

            size = index;
        }
        else
        {
            // If index == -1 the whole input is a match
            size = span.Length;
        }

        // Too large? Take only the request size
        if (_maxSize > 0 && size > _maxSize)
        {
            size = _maxSize;
        }

        var start = context.Scanner.Cursor.Position.Offset;
        context.Scanner.Cursor.Advance(size);
        result.Set(start, start + size, new TextSpan(context.Scanner.Buffer, start, size));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"AnyOf({_searchValues})";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // We can only generate source if we have the original values string
        if (_valuesString == null)
        {
            throw new InvalidOperationException("SearchValuesCharLiteral created from SearchValues<char> cannot generate source. Use the ReadOnlySpan<char> overload instead.");
        }

        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(TextSpan));

        // Escape the values string for use in generated code
        var escapedValues = EscapeStringLiteral(_valuesString);

        // Generate a unique field name base
        var fieldNum = context.NextNumber();
        var fieldName = $"_{context.MethodNamePrefix}_anyof{fieldNum}";

        // Register conditional static fields for both NET8+ (SearchValues) and older frameworks (HashSet)
        context.StaticFields.Add($"#if NET8_0_OR_GREATER");
        context.StaticFields.Add($"private static readonly global::System.Buffers.SearchValues<char> {fieldName} = global::System.Buffers.SearchValues.Create(\"{escapedValues}\");");
        context.StaticFields.Add($"#else");
        context.StaticFields.Add($"private static readonly global::System.Collections.Generic.HashSet<char> {fieldName} = new global::System.Collections.Generic.HashSet<char>(\"{escapedValues}\");");
        context.StaticFields.Add($"#endif");

        // Use direct SourceResult construction for early return optimization
        var result = new SourceResult(
            successVariable: "success",
            valueVariable: "value",
            valueTypeName: valueTypeName);

        var spanVarNum = context.NextNumber();
        result.Body.Add($"var span{spanVarNum} = {cursorName}.Span;");
        var spanVar = $"span{spanVarNum}";

        var startVarNum = context.NextNumber();
        result.Body.Add($"var start{startVarNum} = {cursorName}.Offset;");
        var startVar = $"start{startVarNum}";

        var sizeVarNum = context.NextNumber();
        var sizeVar = $"size{sizeVarNum}";

        // Generate both NET8+ (SearchValues with IndexOfAny/IndexOfAnyExcept) and older framework code
        result.Body.Add($"#if NET8_0_OR_GREATER");

        // NET8+ path: use IndexOfAny/IndexOfAnyExcept for efficient matching
        if (_maxSize > 0)
        {
            result.Body.Add($"var searchSpan{spanVarNum} = {spanVar}.Length > {_maxSize} ? {spanVar}.Slice(0, {_maxSize}) : {spanVar};");
            var searchSpan = $"searchSpan{spanVarNum}";
            var indexMethod = _negate ? "IndexOfAny" : "IndexOfAnyExcept";
            result.Body.Add($"var index{sizeVarNum} = {searchSpan}.{indexMethod}({fieldName});");
            result.Body.Add($"var {sizeVar} = index{sizeVarNum} == -1 ? {searchSpan}.Length : index{sizeVarNum};");
        }
        else
        {
            var indexMethod = _negate ? "IndexOfAny" : "IndexOfAnyExcept";
            result.Body.Add($"var index{sizeVarNum} = {spanVar}.{indexMethod}({fieldName});");
            result.Body.Add($"var {sizeVar} = index{sizeVarNum} == -1 ? {spanVar}.Length : index{sizeVarNum};");
        }

        result.Body.Add($"#else");

        // Pre-NET8 path: use loop with HashSet.Contains
        result.Body.Add($"var {sizeVar} = 0;");
        var maxLengthExpr = _maxSize > 0
            ? $"global::System.Math.Min({spanVar}.Length, {_maxSize})"
            : $"{spanVar}.Length";
        var maxLengthVarNum = context.NextNumber();
        result.Body.Add($"var maxLength{maxLengthVarNum} = {maxLengthExpr};");
        var maxLengthVar = $"maxLength{maxLengthVarNum}";

        result.Body.Add($"for (var i = 0; i < {maxLengthVar}; i++)");
        result.Body.Add("{");

        // For negate=false: break when char is NOT in the set (!Contains)
        // For negate=true: break when char IS in the set (Contains)
        var breakCondition = _negate
            ? $"if ({fieldName}.Contains({spanVar}[i]))"
            : $"if (!{fieldName}.Contains({spanVar}[i]))";
        result.Body.Add($"    {breakCondition}");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        result.Body.Add($"    {sizeVar}++;");
        result.Body.Add("}");

        result.Body.Add($"#endif");

        // Common code for both paths
        result.Body.Add($"if ({sizeVar} < {_minSize})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = default;");
        result.Body.Add("    return false;");
        result.Body.Add("}");

        result.Body.Add($"{cursorName}.Advance({sizeVar});");
        result.Body.Add($"{result.ValueVariable} = new Parlot.TextSpan({scannerName}.Buffer, {startVar}, {sizeVar});");
        result.Body.Add("return true;");

        return result;
    }

    private static string EscapeStringLiteral(string s)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (char.IsControl(c))
                    {
                        sb.Append("\\u");
                        sb.Append(((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        return sb.ToString();
    }
}
#endif
