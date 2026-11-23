#if NET8_0_OR_GREATER
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Buffers;

namespace Parlot.Fluent;

internal sealed class SearchValuesCharLiteral : Parser<TextSpan>, ISeekable, ISourceable, ILoopAnalyzable, ISpanSourceable, IInvokesOnlyLiterals
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
            ExpectedChars = SeekableHints.ExpectedChars(searchValues);
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

        // Only the chars that can be taken are scanned
        if (_maxSize > 0 && span.Length > _maxSize)
        {
            span = span.Slice(0, _maxSize);
        }

        // First char not matching the searched values
        var index = _negate ? span.IndexOfAny(_searchValues) : span.IndexOfAnyExcept(_searchValues);

        // If index == -1 the whole input is a match
        var size = index == -1 ? span.Length : index;

        // Too small?
        if (size < _minSize)
        {
            if (index == -1)
            {
                // More matching chars could follow the end of the buffer
                cursor.MarkHitEnd();
            }

            context.ExitParser(this);
            return false;
        }

        var start = cursor.Offset;

        cursor.AdvanceNoNewLines(size);

        result.Set(start, start + size, cursor.CreateSpan(start, size));

        context.ExitParser(this);
        return true;
    }

    public override string ToString() => $"AnyOf({_searchValues})";

    private (SourceGenerationContext Context, string FieldName, bool UseAsciiBitsetFallback)? _fields;

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        if (_valuesString == null)
        {
            throw new InvalidOperationException("SearchValuesCharLiteral created from SearchValues<char> cannot generate source. Use the ReadOnlySpan<char> overload instead.");
        }

        return CharListSource.GenerateSource(this, _valuesString, _minSize, _maxSize, _negate, ref _fields, context);
    }

    int ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext context) => _valuesString != null ? 1 : -1;

    void ISpanSourceable.GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail)
    {
        if (_valuesString == null)
        {
            throw new InvalidOperationException("SearchValuesCharLiteral created from SearchValues<char> cannot generate source. Use the ReadOnlySpan<char> overload instead.");
        }

        CharListSource.GenerateSpanSource(_valuesString, _minSize, _maxSize, _negate, ref _fields, writer, valueVariable, fail);
    }
}
#endif
