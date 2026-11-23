using System;
using System.Collections.Generic;
using Parlot.Fluent;

namespace Parlot.SourceGeneration;

/// <summary>
/// Shared source and span-source emitter for <c>ListOfChars</c> (pre-.NET 8) and <c>SearchValuesCharLiteral</c> (.NET 8+).
/// </summary>
internal static class CharListSource
{
    internal static SourceResult GenerateSource(
        object owner,
        string values,
        int minSize,
        int maxSize,
        bool negate,
        ref (SourceGenerationContext Context, string FieldName, bool UseAsciiBitsetFallback)? cachedFields,
        SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            var localCached = cachedFields;
            var tokenResult = context.GenerateToken(owner, typeof(TextSpan), () => GenerateSource(owner, values, minSize, maxSize, negate, ref localCached, context));
            cachedFields = localCached;
            return tokenResult;
        }

        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(TextSpan));
        var (fieldName, useAsciiBitsetFallback) = RegisterFields(values, ref cachedFields, context);

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

        GenerateCount(context, result.Body, fieldName, useAsciiBitsetFallback, maxSize, negate, spanVar, sizeVar);

        result.Body.Add($"if ({sizeVar} < {minSize})");
        result.Body.Add("{");
        result.Body.Add($"    if ({sizeVar} == {spanVar}.Length) {cursorName}.MarkHitEnd();");
        result.Body.Add($"    {result.ValueVariable} = default;");
        result.Body.Add("    return false;");
        result.Body.Add("}");

        result.Body.Add($"{cursorName}.AdvanceNoNewLines({sizeVar});");
        if (!context.DiscardResult)
        {
            result.Body.Add($"{result.ValueVariable} = {cursorName}.CreateSpan({startVar}, {sizeVar});");
        }
        else
        {
            result.Body.Add($"{result.ValueVariable} = default;");
        }
        result.Body.Add("return true;");

        return result;
    }

    internal static void GenerateSpanSource(
        string values,
        int minSize,
        int maxSize,
        bool negate,
        ref (SourceGenerationContext Context, string FieldName, bool UseAsciiBitsetFallback)? cachedFields,
        SpanSourceWriter writer,
        string? valueVariable,
        SpanLabel fail)
    {
        var (fieldName, useAsciiBitsetFallback) = RegisterFields(values, ref cachedFields, writer.Context);
        var rest = writer.DeclareLocal("global::System.ReadOnlySpan<char>", "rest");
        var size = writer.DeclareLocal("int", "size", "0");
        var body = new List<string>();

        writer.Add($"{rest} = {writer.Span}.Slice({writer.Pos});");

        writer.BeginBlock();
        GenerateCount(writer.Context, body, fieldName, useAsciiBitsetFallback, maxSize, negate, rest, size + "Count");

        foreach (var line in body)
        {
            writer.Add(line);
        }

        writer.Add($"{size} = {size}Count;");
        writer.EndBlock();
        writer.BeginBlock($"if ({size} < {minSize})");
        writer.Add($"if ({size} == {rest}.Length) {writer.MarkHitEnd}");
        writer.Add(fail.Goto());
        writer.EndBlock();

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {writer.CreateSpan(writer.Pos, size)};");
        }

        writer.Add($"{writer.Pos} += {size};");
    }

    private static (string FieldName, bool UseAsciiBitsetFallback) RegisterFields(
        string values,
        ref (SourceGenerationContext Context, string FieldName, bool UseAsciiBitsetFallback)? cachedFields,
        SourceGenerationContext context)
    {
        if (cachedFields is { } fields && ReferenceEquals(fields.Context, context))
        {
            return (fields.FieldName, fields.UseAsciiBitsetFallback);
        }

        var escapedValues = LiteralHelper.EscapeStringContent(values);

        var useAsciiBitsetFallback = true;
        ulong asciiLow = 0;
        ulong asciiHigh = 0;

        foreach (var c in values)
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

        var fieldNum = context.NextNumber();
        var fieldName = $"_{context.MethodNamePrefix}_anyof{fieldNum}";

        context.StaticFields.Add("#if NET8_0_OR_GREATER");
        context.StaticFields.Add($"private static readonly global::System.Buffers.SearchValues<char> {fieldName} = global::System.Buffers.SearchValues.Create(\"{escapedValues}\");");
        context.StaticFields.Add("#else");
        if (useAsciiBitsetFallback)
        {
            context.StaticFields.Add($"private const ulong {fieldName}_low = 0x{asciiLow:X}UL;");
            context.StaticFields.Add($"private const ulong {fieldName}_high = 0x{asciiHigh:X}UL;");
        }
        else
        {
            context.StaticFields.Add($"private static readonly global::System.Collections.Generic.HashSet<char> {fieldName} = new global::System.Collections.Generic.HashSet<char>(\"{escapedValues}\");");
        }
        context.StaticFields.Add("#endif");

        cachedFields = (context, fieldName, useAsciiBitsetFallback);

        return (fieldName, useAsciiBitsetFallback);
    }

    private static void GenerateCount(
        SourceGenerationContext context,
        IList<string> body,
        string fieldName,
        bool useAsciiBitsetFallback,
        int maxSize,
        bool negate,
        string spanVar,
        string sizeVar)
    {
        var sizeVarNum = context.NextNumber();
        var spanVarNum = context.NextNumber();

        body.Add("#if NET8_0_OR_GREATER");

        if (maxSize > 0)
        {
            body.Add($"var searchSpan{spanVarNum} = {spanVar}.Length > {maxSize} ? {spanVar}.Slice(0, {maxSize}) : {spanVar};");
            var searchSpan = $"searchSpan{spanVarNum}";
            var indexMethod = negate ? "IndexOfAny" : "IndexOfAnyExcept";
            body.Add($"var index{sizeVarNum} = {searchSpan}.{indexMethod}({fieldName});");
            body.Add($"var {sizeVar} = index{sizeVarNum} == -1 ? {searchSpan}.Length : index{sizeVarNum};");
        }
        else
        {
            var indexMethod = negate ? "IndexOfAny" : "IndexOfAnyExcept";
            body.Add($"var index{sizeVarNum} = {spanVar}.{indexMethod}({fieldName});");
            body.Add($"var {sizeVar} = index{sizeVarNum} == -1 ? {spanVar}.Length : index{sizeVarNum};");
        }

        body.Add("#else");

        body.Add($"var {sizeVar} = 0;");
        var maxLengthExpr = maxSize > 0
            ? $"global::System.Math.Min({spanVar}.Length, {maxSize})"
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
            body.Add("        if (c < 64)");
            body.Add("        {");
            body.Add($"            isMember = (({fieldName}_low >> (c & 63)) & 1UL) != 0;");
            body.Add("        }");
            body.Add("        else");
            body.Add("        {");
            body.Add($"            isMember = (({fieldName}_high >> (c & 63)) & 1UL) != 0;");
            body.Add("        }");
            body.Add("    }");

            body.Add(negate ? "    if (isMember)" : "    if (!isMember)");
        }
        else
        {
            var breakCondition = negate
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
