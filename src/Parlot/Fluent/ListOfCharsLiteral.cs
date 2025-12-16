#if !NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

internal sealed class ListOfChars : Parser<TextSpan>, ISeekable, ISourceable
{
    private readonly CharMap<object> _map = new();
    private readonly string _values;
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;
    private readonly bool _hasNewLine;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public ListOfChars(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _values = values.ToString();

        foreach (var c in values)
        {
            _map.Set(c, new object());

            if (Character.IsNewLine(c))
            {
                _hasNewLine = true;
            }
        }

        if (_minSize > 0 && !_negate)
        {
            ExpectedChars = _values.ToCharArray();
            CanSeek = true;
        }

        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var span = cursor.Span;
        var start = cursor.Offset;

        var size = 0;
        var maxLength = _maxSize > 0 ? Math.Min(span.Length, _maxSize) : span.Length;

        for (var i = 0; i < maxLength; i++)
        {
            if (_map[span[i]] == null != _negate)
            {
                break;
            }

            size++;
        }

        if (size < _minSize)
        {
            context.ExitParser(this);
            return false;
        }

        if (_hasNewLine)
        {
            cursor.Advance(size);
        }
        else
        {
            cursor.AdvanceNoNewLines(size);
        }

        result.Set(start, start + size, new TextSpan(context.Scanner.Buffer, start, size));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"AnyOf([{string.Join(", ", ExpectedChars)}])";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var cursorName = context.CursorName;
        var scannerName = context.ScannerName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(TextSpan));

        // Escape the values string for use in generated code
        var escapedValues = LiteralHelper.EscapeStringContent(_values);

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

        // Advance cursor - use Advance if there might be newlines, otherwise AdvanceNoNewLines
        if (_hasNewLine)
        {
            result.Body.Add($"{cursorName}.Advance({sizeVar});");
        }
        else
        {
            result.Body.Add($"{cursorName}.AdvanceNoNewLines({sizeVar});");
        }

        result.Body.Add($"{result.ValueVariable} = new Parlot.TextSpan({scannerName}.Buffer, {startVar}, {sizeVar});");
        result.Body.Add("return true;");

        return result;
    }
}
#endif
