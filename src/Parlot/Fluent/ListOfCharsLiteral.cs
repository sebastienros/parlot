#if !NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

internal sealed class ListOfChars : Parser<TextSpan>, ISeekable, ISourceable, ILoopAnalyzable, ISpanSourceable, IInvokesOnlyLiterals
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
            ExpectedChars = SeekableHints.ExpectedChars(values);
            CanSeek = true;
        }
    }

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => _minSize <= 0;

    // It reads its chars only, see IInvokesOnlyLiterals
    bool IInvokesOnlyLiterals.InvokesOnlyLiterals => true;

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

    private (SourceGenerationContext Context, string FieldName, bool UseAsciiBitsetFallback)? _fields;

    public SourceResult GenerateSource(SourceGenerationContext context)
        => CharListSource.GenerateSource(this, _values, _minSize, _maxSize, _negate, ref _fields, context);

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => 1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
        => CharListSource.GenerateSpanSource(_values, _minSize, _maxSize, _negate, ref _fields, writer, valueVariable, fail);
}
#endif
