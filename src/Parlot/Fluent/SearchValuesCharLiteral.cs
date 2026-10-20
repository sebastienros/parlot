#if NET8_0_OR_GREATER
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

internal sealed class SearchValuesCharLiteral : Parser<TextSpan>, ICompilable, ISeekable
{
    private static readonly MethodInfo _parseSpanMethodInfo = typeof(SearchValuesCharLiteral).GetMethod(nameof(ParseSpan), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly SearchValues<char> _searchValues;
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;

    // Whether the matched chars can contain '\n', which then needs to be tracked for the line and column
    private readonly bool _hasNewLine;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public SearchValuesCharLiteral(SearchValues<char> searchValues, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _searchValues = searchValues ?? throw new ArgumentNullException(nameof(searchValues));
        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;

        // A negated set matches every char but its values, so it reads new lines unless '\n' is one of them
        _hasNewLine = searchValues.Contains('\n') != negate;
    }

    public SearchValuesCharLiteral(ReadOnlySpan<char> searchValues, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        _searchValues = SearchValues.Create(searchValues);
        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;
        _hasNewLine = (searchValues.IndexOf('\n') >= 0) != negate;

        if (minSize > 0 && !_negate)
        {
            CanSeek = true;
            ExpectedChars = searchValues.ToArray();
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (ParseSpan(context.Scanner.Cursor, _searchValues, _minSize, _maxSize, _negate, _hasNewLine, out var span))
        {
            result.Set(span.Offset, span.Offset + span.Length, span);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    private static bool ParseSpan(Cursor cursor, SearchValues<char> searchValues, int minSize, int maxSize, bool negate, bool hasNewLine, out TextSpan span)
    {
        var cursorSpan = cursor.Span;

        if (minSize > cursorSpan.Length)
        {
            span = default;
            return false;
        }

        // Only the chars that can be taken are scanned
        if (maxSize > 0 && cursorSpan.Length > maxSize)
        {
            cursorSpan = cursorSpan.Slice(0, maxSize);
        }

        // First char not matching the searched values
        var index = negate ? cursorSpan.IndexOfAny(searchValues) : cursorSpan.IndexOfAnyExcept(searchValues);

        int size;

        if (index != -1)
        {
            // Too small?
            if (index < minSize)
            {
                span = default;
                return false;
            }

            size = index;
        }
        else
        {
            // If index == -1 the whole input is a match
            size = cursorSpan.Length;
        }

        var start = cursor.Offset;

        // The line and column only need to be tracked char by char when there are new lines
        if (hasNewLine)
        {
            cursor.Advance(size);
        }
        else
        {
            cursor.AdvanceNoNewLines(size);
        }

        span = new TextSpan(cursor.Buffer, start, size);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        var span = Expression.Variable(typeof(TextSpan), $"span{context.NextNumber}");
        result.Variables.Add(span);

        result.Body.Add(
            Expression.IfThen(
                Expression.Call(
                    _parseSpanMethodInfo,
                    context.Cursor(),
                    Expression.Constant(_searchValues),
                    Expression.Constant(_minSize),
                    Expression.Constant(_maxSize),
                    Expression.Constant(_negate),
                    Expression.Constant(_hasNewLine),
                    span
                ),
                Expression.Block(
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, span),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                )
            )
        );

        return result;
    }

    public override string ToString() => $"AnyOf({_searchValues})";
}
#endif
