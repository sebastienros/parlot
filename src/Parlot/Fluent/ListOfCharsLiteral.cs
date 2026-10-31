#if !NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

internal sealed class ListOfChars : Parser<TextSpan>, ISeekable, ISourceable, ISpanSourceable
{
    private readonly CharMap<object> _map = new();
    private readonly string _values;
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public ListOfChars(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _values = values.ToString();

        foreach (var c in values)
        {
            _map.Set(c, new object());
        }

        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;

        if (minSize > 0 && !negate)
        {
            ExpectedChars = _values.ToCharArray();
            CanSeek = true;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

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
            if (size == span.Length)
            {
                // More matching chars could follow the end of the buffer
                cursor.MarkHitEnd();
            }

            context.ExitParser(this);
            return false;
        }

        cursor.AdvanceNoNewLines(size);

        result.Set(start, start + size, context.Scanner.Cursor.CreateSpan(start, size));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"AnyOf([{string.Join(", ", ExpectedChars)}])";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(TextSpan), () => GenerateSource(context));
        }

        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(TextSpan));
        var (fieldName, useAsciiBitsetFallback) = RegisterFields(context);

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

        GenerateCount(context, result.Body, fieldName, useAsciiBitsetFallback, spanVar, sizeVar);

        // Common code for both paths
        result.Body.Add($"if ({sizeVar} < {_minSize})");
        result.Body.Add("{");
        result.Body.Add($"    if ({sizeVar} == {spanVar}.Length) {cursorName}.MarkHitEnd();");
        result.Body.Add($"    {result.ValueVariable} = default;");
        result.Body.Add("    return false;");
        result.Body.Add("}");

        result.Body.Add($"{cursorName}.AdvanceNoNewLines({sizeVar});");

        result.Body.Add($"{result.ValueVariable} = {context.CursorName}.CreateSpan({startVar}, {sizeVar});");
        result.Body.Add("return true;");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => 1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var (fieldName, useAsciiBitsetFallback) = RegisterFields(writer.Context);
        var rest = writer.DeclareLocal("global::System.ReadOnlySpan<char>", "rest");
        var size = writer.DeclareLocal("int", "size", "0");
        var body = new System.Collections.Generic.List<string>();

        writer.Add($"{rest} = {writer.Span}.Slice({writer.Pos});");

        // The count is declared in a block, and assigned to the local declared for the method
        writer.BeginBlock();
        GenerateCount(writer.Context, body, fieldName, useAsciiBitsetFallback, rest, size + "Count");

        foreach (var line in body)
        {
            writer.Add(line);
        }

        writer.Add($"{size} = {size}Count;");
        writer.EndBlock();
        writer.BeginBlock($"if ({size} < {_minSize})");
        // More matching chars could follow the end of the span
        writer.Add($"if ({size} == {rest}.Length) {writer.MarkHitEnd}");
        writer.Add(fail.Goto());
        writer.EndBlock();

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {writer.CreateSpan(writer.Pos, size)};");
        }

        writer.Add($"{writer.Pos} += {size};");
    }

    // The fields registered in the last context, which inlined span code uses each time the parser is inlined
    private (SourceGenerationContext Context, string FieldName, bool UseAsciiBitsetFallback)? _fields;

    // Registers the fields holding the chars, and returns their name
    private (string FieldName, bool UseAsciiBitsetFallback) RegisterFields(SourceGenerationContext context)
    {
        if (_fields is { } fields && ReferenceEquals(fields.Context, context))
        {
            return (fields.FieldName, fields.UseAsciiBitsetFallback);
        }

        // Escape the values string for use in generated code
        var escapedValues = LiteralHelper.EscapeStringContent(_values);

        var useAsciiBitsetFallback = true;
        ulong asciiLow = 0;
        ulong asciiHigh = 0;

        foreach (var c in _values)
        {
            if (c > 127)
            {
                useAsciiBitsetFallback = false;
                break;
            }

            var bit = 1UL << (c & 63);
            if (c < 64)
            {
                asciiLow |= bit;
            }
            else
            {
                asciiHigh |= bit;
            }
        }

        // Generate a unique field name base
        var fieldNum = context.NextNumber();
        var fieldName = $"_{context.MethodNamePrefix}_anyof{fieldNum}";

        // Register conditional static fields for both NET8+ (SearchValues) and older frameworks (HashSet)
        context.StaticFields.Add($"#if NET8_0_OR_GREATER");
        context.StaticFields.Add($"private static readonly global::System.Buffers.SearchValues<char> {fieldName} = global::System.Buffers.SearchValues.Create(\"{escapedValues}\");");
        context.StaticFields.Add($"#else");
        if (useAsciiBitsetFallback)
        {
            context.StaticFields.Add($"private const ulong {fieldName}_low = 0x{asciiLow:X}UL;");
            context.StaticFields.Add($"private const ulong {fieldName}_high = 0x{asciiHigh:X}UL;");
        }
        else
        {
            context.StaticFields.Add($"private static readonly global::System.Collections.Generic.HashSet<char> {fieldName} = new global::System.Collections.Generic.HashSet<char>(\"{escapedValues}\");");
        }
        context.StaticFields.Add($"#endif");

        _fields = (context, fieldName, useAsciiBitsetFallback);

        return (fieldName, useAsciiBitsetFallback);
    }

    // Declares the number of chars of the set (or not in the set when negated) which start the span
    private void GenerateCount(SourceGenerationContext context, System.Collections.Generic.IList<string> body, string fieldName, bool useAsciiBitsetFallback, string spanVar, string sizeVar)
    {
        var sizeVarNum = context.NextNumber();
        var spanVarNum = context.NextNumber();

        // Generate both NET8+ (SearchValues with IndexOfAny/IndexOfAnyExcept) and older framework code
        body.Add("#if NET8_0_OR_GREATER");

        // NET8+ path: use IndexOfAny/IndexOfAnyExcept for efficient matching
        if (_maxSize > 0)
        {
            body.Add($"var searchSpan{spanVarNum} = {spanVar}.Length > {_maxSize} ? {spanVar}.Slice(0, {_maxSize}) : {spanVar};");
            var searchSpan = $"searchSpan{spanVarNum}";
            var indexMethod = _negate ? "IndexOfAny" : "IndexOfAnyExcept";
            body.Add($"var index{sizeVarNum} = {searchSpan}.{indexMethod}({fieldName});");
            body.Add($"var {sizeVar} = index{sizeVarNum} == -1 ? {searchSpan}.Length : index{sizeVarNum};");
        }
        else
        {
            var indexMethod = _negate ? "IndexOfAny" : "IndexOfAnyExcept";
            body.Add($"var index{sizeVarNum} = {spanVar}.{indexMethod}({fieldName});");
            body.Add($"var {sizeVar} = index{sizeVarNum} == -1 ? {spanVar}.Length : index{sizeVarNum};");
        }

        body.Add("#else");

        // Pre-NET8 path: prefer ASCII bitset membership when possible; otherwise fall back to HashSet.Contains
        body.Add($"var {sizeVar} = 0;");
        var maxLengthExpr = _maxSize > 0
            ? $"global::System.Math.Min({spanVar}.Length, {_maxSize})"
            : $"{spanVar}.Length";
        var maxLengthVarNum = context.NextNumber();
        body.Add($"var maxLength{maxLengthVarNum} = {maxLengthExpr};");
        var maxLengthVar = $"maxLength{maxLengthVarNum}";

        body.Add($"for (var i = 0; i < {maxLengthVar}; i++)");
        body.Add("{");

        if (useAsciiBitsetFallback)
        {
            body.Add($"    var c = {spanVar}[i];");
            body.Add("    var isMember = false;");
            body.Add("    if ((uint)c < 128u)");
            body.Add("    {");
            body.Add($"        if (c < 64)");
            body.Add("        {");
            body.Add($"            isMember = (({fieldName}_low >> (c & 63)) & 1UL) != 0;");
            body.Add("        }");
            body.Add("        else");
            body.Add("        {");
            body.Add($"            isMember = (({fieldName}_high >> (c & 63)) & 1UL) != 0;");
            body.Add("        }");
            body.Add("    }");

            // For negate=false: consume while member
            // For negate=true: consume while NOT member (non-ASCII counts as NOT member)
            if (_negate)
            {
                body.Add("    if (isMember)");
            }
            else
            {
                body.Add("    if (!isMember)");
            }
        }
        else
        {
            // For negate=false: break when char is NOT in the set (!Contains)
            // For negate=true: break when char IS in the set (Contains)
            var breakCondition = _negate
                ? $"if ({fieldName}.Contains({spanVar}[i]))"
                : $"if (!{fieldName}.Contains({spanVar}[i]))";
            body.Add($"    {breakCondition}");
        }

        body.Add("    {");
        body.Add("        break;");
        body.Add("    }");
        body.Add($"    {sizeVar}++;");
        body.Add("}");

        body.Add("#endif");
    }
}
#endif
