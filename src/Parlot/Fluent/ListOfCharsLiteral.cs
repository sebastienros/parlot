#if !NET8_0_OR_GREATER
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

internal sealed class ListOfChars : Parser<TextSpan>, ICompilable, ISeekable
{
    private static readonly MethodInfo _parseSpanMethodInfo = typeof(ListOfChars).GetMethod(nameof(ParseSpan), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly CharMap<object> _map = new();
    private readonly int _minSize;
    private readonly int _maxSize;
    private readonly bool _negate;
    private readonly bool _hasNewLine;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public ListOfChars(ReadOnlySpan<char> values, int minSize = 1, int maxSize = 0, bool negate = false)
    {
        foreach (var c in values)
        {
            _map.Set(c, new object());
        }

        _minSize = minSize;
        _maxSize = maxSize;
        _negate = negate;

        // A negated set matches every char but its values, so it reads new lines unless '\n' is one of them
        _hasNewLine = (values.IndexOf('\n') >= 0) != negate;

        if (minSize > 0 && !negate)
        {
            ExpectedChars = values.ToString().ToCharArray();
            CanSeek = true;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        if (ParseSpan(context.Scanner.Cursor, _map, _minSize, _maxSize, _negate, _hasNewLine, out var span))
        {
            result.Set(span.Offset, span.Offset + span.Length, span);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    private static bool ParseSpan(Cursor cursor, CharMap<object> map, int minSize, int maxSize, bool negate, bool hasNewLine, out TextSpan span)
    {
        var cursorSpan = cursor.Span;
        var start = cursor.Offset;

        var size = 0;
        var maxLength = maxSize > 0 ? Math.Min(cursorSpan.Length, maxSize) : cursorSpan.Length;

        for (var i = 0; i < maxLength; i++)
        {
            if (map[cursorSpan[i]] == null != negate)
            {
                break;
            }

            size++;
        }

        if (size < minSize)
        {
            span = default;
            return false;
        }

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
                    Expression.Constant(_map),
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

    public override string ToString() => $"AnyOf([{string.Join(", ", ExpectedChars)}])";
}
#endif
