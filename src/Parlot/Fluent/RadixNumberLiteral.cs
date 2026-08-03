using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Parlot.Fluent;

internal delegate bool TryParseRadix<T>(ReadOnlySpan<char> span, int radix, out int charsRead, [MaybeNullWhen(false)] out T value);

internal sealed class RadixNumberLiteral<T> : Parser<T>, ISeekable, ISourceable
{
    private readonly int _radix;
    private readonly TryParseRadix<T> _tryParse;

    public RadixNumberLiteral(int radix, TryParseRadix<T> tryParse)
    {
        _radix = radix;
        _tryParse = tryParse;
        ExpectedChars = GetDigits(radix).ToCharArray();
        Name = radix switch
        {
            2 => "BinaryNumberLiteral",
            8 => "OctalNumberLiteral",
            16 => "HexadecimalNumberLiteral",
            _ => throw new ArgumentOutOfRangeException(nameof(radix)),
        };
    }

    public bool CanSeek => true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace => false;

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        if (_tryParse(cursor.Span, _radix, out var charsRead, out var value))
        {
            cursor.AdvanceNoNewLines(charsRead);
            result.Set(start, start + charsRead, value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var charsReadName = $"charsRead{context.NextNumber()}";
        var parsedValueName = $"parsedValue{context.NextNumber()}";
        var supportsGenericMath =
            context.TargetFramework.Identifier == TargetFrameworkIdentifier.NetCoreApp &&
            context.TargetFramework.Version >= new Version(8, 0);
        var tryParseMethod = Numbers.HasTryParseRadixOverload(typeof(T))
            ? "global::Parlot.Numbers.TryParseRadix"
            : supportsGenericMath
                ? $"global::Parlot.Numbers.TryParseRadix<{valueTypeName}>"
                : throw new NotSupportedException($"Source generation for radix numbers of type '{typeof(T)}' requires .NET 8 or later.");

        result.Body.Add($"var {charsReadName} = 0;");
        result.Body.Add($"{valueTypeName} {parsedValueName} = default;");
        result.Body.Add($"{result.SuccessVariable} = {tryParseMethod}({cursorName}.Span, {_radix}, out {charsReadName}, out {parsedValueName});");
        result.Body.Add($"if ({result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines({charsReadName});");

        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {parsedValueName};");
        }

        result.Body.Add("}");

        return result;
    }

    private static string GetDigits(int radix) => radix switch
    {
        2 => Character.BinaryDigits,
        8 => Character.OctalDigits,
        16 => Character.HexDigits,
        _ => throw new ArgumentOutOfRangeException(nameof(radix)),
    };
}
