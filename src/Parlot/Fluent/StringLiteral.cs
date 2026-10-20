using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

public enum StringLiteralQuotes
{
    Single,
    Double,
    Backtick,
    SingleOrDouble,
    Custom
}

public sealed class StringLiteral : Parser<TextSpan>, ICompilable, ISeekable
{
    // Expression trees can't use ReadOnlySpan<char> variables, so compiled parsers only get the escape flag from the
    // scanner and build the result from the buffer offsets
    private static readonly MethodInfo _readQuotedStringMethodInfo = typeof(Scanner).GetMethod(nameof(Scanner.ReadQuotedString), BindingFlags.Instance | BindingFlags.NonPublic, null, [typeof(char), typeof(bool).MakeByRefType()], null)!;
    private static readonly MethodInfo _createResultMethodInfo = typeof(StringLiteral).GetMethod(nameof(CreateResult), BindingFlags.Static | BindingFlags.NonPublic)!;

    static readonly char[] SingleQuotes = ['\''];
    static readonly char[] DoubleQuotes = ['\"'];
    static readonly char[] Backtick = ['`'];
    static readonly char[] SingleOrDoubleQuotes = ['\'', '\"'];

    private readonly StringLiteralQuotes _quotes;

    public StringLiteral(StringLiteralQuotes quotes)
    {
        _quotes = quotes;

        ExpectedChars = _quotes switch
        {
            StringLiteralQuotes.Single => SingleQuotes,
            StringLiteralQuotes.Double => DoubleQuotes,
            StringLiteralQuotes.Backtick => Backtick,
            StringLiteralQuotes.SingleOrDouble => SingleOrDoubleQuotes,
            _ => throw new InvalidOperationException()
        };

        Name = "StringLiteral";
    }

    public StringLiteral(char quote)
    {
        _quotes = quote switch
        {
            '\'' => StringLiteralQuotes.Single,
            '\"' => StringLiteralQuotes.Double,
            '`' => StringLiteralQuotes.Backtick,
            _ => StringLiteralQuotes.Custom,
        };

        ExpectedChars = [quote];

        Name = "StringLiteral";
    }

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;

        // Any of the quotes can start the string, the other kinds have a single one
        var quote = ExpectedChars[0];

        if (_quotes == StringLiteralQuotes.SingleOrDouble && cursor.Current == '"')
        {
            quote = '"';
        }

        if (!context.Scanner.ReadQuotedString(quote, out _, out var hasEscape))
        {
            context.ExitParser(this);
            return false;
        }

        var end = cursor.Offset;

        result.Set(start, end, CreateResult(context.Scanner.Buffer, start, end, hasEscape));

        context.ExitParser(this);
        return true;
    }

    /// <summary>
    /// Builds the value of a string read by the scanner, without its quotes.
    /// </summary>
    /// <remarks>
    /// The scanner knows whether the string has escape sequences, so a string without any isn't read again
    /// and references the input buffer.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextSpan CreateResult(string buffer, int start, int end, bool hasEscape)
    {
        return hasEscape
            ? new TextSpan(Character.DecodeStringInternal(buffer.AsSpan(start + 1, end - start - 2)))
            : new TextSpan(buffer, start + 1, end - start - 2);
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        // var start = context.Scanner.Cursor.Offset;

        var start = context.DeclareOffsetVariable(result);

        var hasEscape = Expression.Variable(typeof(bool), $"hasEscape{context.NextNumber}");
        result.Variables.Add(hasEscape);

        // Any of the quotes can start the string, the other kinds have a single one
        var quote = _quotes == StringLiteralQuotes.SingleOrDouble
            ? Expression.Condition(Expression.Equal(context.Current(), Expression.Constant('"')), Expression.Constant('"'), Expression.Constant('\''))
            : (Expression)Expression.Constant(ExpectedChars[0]);

        // if (context.Scanner.ReadQuotedString(quote, out hasEscape))
        // {
        //     success = true;
        //     value = CreateResult(context.Scanner.Buffer, start, context.Scanner.Cursor.Offset, hasEscape);
        // }

        result.Body.Add(
            Expression.IfThen(
                Expression.Call(context.Scanner(), _readQuotedStringMethodInfo, quote, hasEscape),
                Expression.Block(
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                    context.DiscardResult
                    ? Expression.Empty()
                    : Expression.Assign(result.Value,
                        Expression.Call(_createResultMethodInfo,
                            context.Buffer(),
                            start,
                            context.Offset(),
                            hasEscape))
                )
            ));

        return result;
    }
}
