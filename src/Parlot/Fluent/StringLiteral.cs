using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;
using System.Reflection;

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
    private static readonly MethodInfo _decodeStringMethodInfo = typeof(Character).GetMethod("DecodeString", [typeof(string), typeof(int), typeof(int)])!;

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

        var start = context.Scanner.Cursor.Offset;

        var success = _quotes switch
        {
            StringLiteralQuotes.Single => context.Scanner.ReadSingleQuotedString(),
            StringLiteralQuotes.Double => context.Scanner.ReadDoubleQuotedString(),
            StringLiteralQuotes.SingleOrDouble => context.Scanner.ReadQuotedString(),
            StringLiteralQuotes.Backtick => context.Scanner.ReadBacktickString(),
            StringLiteralQuotes.Custom => context.Scanner.ReadQuotedString(ExpectedChars),
            _ => false
        };

        var end = context.Scanner.Cursor.Offset;

        if (success)
        {
            // Remove quotes
            var decoded = Character.DecodeString(context.Scanner.Buffer, start + 1, end - start - 2);

            result.Set(start, end, decoded);

            context.ExitParser(this);
            return true;
        }
        else
        {
            context.ExitParser(this);
            return false;
        }
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        // var start = context.Scanner.Cursor.Offset;

        var start = Expression.Variable(typeof(int), $"start{context.NextNumber}");
        result.Variables.Add(start);

        result.Body.Add(Expression.Assign(start, context.Offset()));

        var parseStringExpression = _quotes switch
        {
            StringLiteralQuotes.Single => context.ReadSingleQuotedString(),
            StringLiteralQuotes.Double => context.ReadDoubleQuotedString(),
            StringLiteralQuotes.SingleOrDouble => context.ReadQuotedString(),
            StringLiteralQuotes.Backtick => context.ReadBacktickString(),
            StringLiteralQuotes.Custom => context.ReadCustomString(Expression.Constant(ExpectedChars)),
            _ => throw new InvalidOperationException()
        };

        // if (context.Scanner.ReadSingleQuotedString())
        // {
        //     var end = context.Scanner.Cursor.Offset;
        //     success = true;
        //     value = Character.DecodeString(context.Scanner.Buffer, start + 1, end - start - 2);
        // }

        var end = Expression.Variable(typeof(int), $"end{context.NextNumber}");

        result.Body.Add(
            Expression.IfThen(
                parseStringExpression,
                Expression.Block(
                    [end],
                    Expression.Assign(end, context.Offset()),
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                    context.DiscardResult
                    ? Expression.Empty()
                    : Expression.Assign(result.Value,
                        Expression.Call(_decodeStringMethodInfo,
                            context.Buffer(),
                            Expression.Add(start, Expression.Constant(1)),
                            Expression.Subtract(Expression.Subtract(end, start), Expression.Constant(2))
                            ))
                )
            ));

        return result;
    }
}
