using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Parlot.Fluent;

/// <summary>
/// An identifier whose chars are code points tested with predicates, which can be escape sequences or surrogate pairs.
/// </summary>
/// <remarks>
/// The ASCII chars the predicates accept are read once, when the parser is built, and tested with bit masks: the
/// predicates are only invoked for the other chars, the escape sequences and the surrogate pairs. The value is the text
/// of the identifier, escape sequences included, which <see cref="Character.DecodeIdentifier(TextSpan)"/> decodes.
/// </remarks>
internal sealed class CodePointIdentifier : Parser<TextSpan>, ISeekable, ISourceable, ILoopAnalyzable, ISpanSourceable
{
    private readonly Func<int, bool> _isStart;
    private readonly Func<int, bool> _isPart;
    private readonly IdentifierOptions _options;
    private readonly ulong _startLow;
    private readonly ulong _startHigh;
    private readonly ulong _partLow;
    private readonly ulong _partHigh;

    public CodePointIdentifier(Func<int, bool> isStart, Func<int, bool> isPart, IdentifierOptions options)
    {
        _isStart = isStart ?? throw new ArgumentNullException(nameof(isStart));
        _isPart = isPart ?? throw new ArgumentNullException(nameof(isPart));
        _options = options;

        (_startLow, _startHigh) = Character.GetIdentifierAsciiMask(isStart, options);
        (_partLow, _partHigh) = Character.GetIdentifierAsciiMask(isPart, options);

        ExpectedChars = GetExpectedChars(_startLow, _startHigh, options);

        Name = "Identifier";
    }

    // The ASCII chars of the start mask, the backslash of the escape sequences, and any char above ASCII: the non-ASCII
    // chars the start predicate accepts are unknown, so a choice tries the identifier for each of them
    public bool CanSeek => true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace => false;

    private static char[] GetExpectedChars(ulong low, ulong high, IdentifierOptions options)
    {
        var chars = new List<char>();

        for (var c = 0; c < 128; c++)
        {
            if ((((c < 64 ? low : high) >> c) & 1) != 0)
            {
                chars.Add((char)c);
            }
        }

        if ((options & (IdentifierOptions.UnicodeEscapes | IdentifierOptions.CodePointEscapes)) != 0)
        {
            chars.Add('\\');
        }

        chars.Add(OneOf<object>.NonAsciiSeekableChar);

        return chars.ToArray();
    }

    // An identifier reads at least one char
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => false;

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        if (context.IsCompacting)
        {
            return context.ParseToken(this, ref result);
        }

        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var size = Character.ScanIdentifier(cursor.Span, _startLow, _startHigh, _partLow, _partHigh, _isStart, _isPart, _options, out var hitEnd);

        if (hitEnd)
        {
            // An escape sequence or a surrogate pair was cut by the end of the text
            cursor.MarkHitEnd();
        }

        if (size == 0)
        {
            context.ExitParser(this);
            return false;
        }

        var start = cursor.Offset;
        cursor.AdvanceNoNewLines(size);
        result.Set(start, start + size, cursor.CreateSpan(start, size));

        context.ExitParser(this);
        return true;
    }

    private string GenerateScan(SourceGenerationContext context, string span, string hitEnd)
    {
        var isStart = context.RegisterLambda(_isStart);
        var isPart = context.RegisterLambda(_isPart);

        return $"global::Parlot.Character.ScanIdentifier({span}, {Hex(_startLow)}, {Hex(_startHigh)}, {Hex(_partLow)}, {Hex(_partHigh)}, {isStart}, {isPart}, (global::Parlot.Fluent.IdentifierOptions){((int)_options).ToString(CultureInfo.InvariantCulture)}, out {hitEnd})";

        static string Hex(ulong value) => "0x" + value.ToString("X", CultureInfo.InvariantCulture) + "UL";
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (context.IsCompacting)
        {
            return context.GenerateToken(this, typeof(TextSpan), () => GenerateSource(context));
        }

        var result = context.CreateResult(typeof(TextSpan));
        var cursorName = context.CursorName;
        var sizeName = $"size{context.NextNumber()}";
        var hitEndName = $"hitEnd{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {sizeName} = {GenerateScan(context, $"{cursorName}.Span", $"var {hitEndName}")};");
        result.Body.Add($"if ({hitEndName})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.MarkHitEnd();");
        result.Body.Add("}");
        result.Body.Add($"if ({sizeName} != 0)");
        result.Body.Add("{");
        result.Body.Add($"    var {startName} = {cursorName}.Offset;");
        result.Body.Add($"    {cursorName}.AdvanceNoNewLines({sizeName});");

        if (!context.DiscardResult)
        {
            result.Body.Add($"    {result.ValueVariable} = {cursorName}.CreateSpan({startName}, {sizeName});");
        }

        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");

        return result;
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => 1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        var span = writer.Span;
        var pos = writer.Pos;
        var size = writer.DeclareLocal("int", "identifierSize", "0");
        var hitEnd = writer.DeclareLocal("bool", "identifierHitEnd", "false");

        writer.Add($"{size} = {GenerateScan(writer.Context, $"{span}.Slice({pos})", hitEnd)};");

        // An escape sequence or a surrogate pair was cut by the end of the span
        writer.Add($"if ({hitEnd}) {writer.MarkHitEnd}");

        writer.BeginBlock($"if ({size} == 0)");
        writer.Fail(fail);
        writer.EndBlock();

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {writer.CreateSpan(pos, size)};");
        }

        writer.Add($"{pos} += {size};");
    }
}
