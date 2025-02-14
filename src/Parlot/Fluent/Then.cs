using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Returns a new <see cref="Parser{U}" /> converting the input value of 
/// type T to the output value of type U using a custom function.
/// </summary>
/// <typeparam name="T">The input parser type.</typeparam>
/// <typeparam name="U">The output parser type.</typeparam>
public sealed class Then<T, U> : Parser<U>, ICompilable, ISeekable
{
    private readonly Func<T, U>? _action1;
    private readonly Func<ParseContext, T, U>? _action2;
    private readonly U? _value;
    private readonly Parser<T> _parser;

    private Then(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

        if (parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public Then(Parser<T> parser, Func<T, U> action) : this(parser)
    {
        _action1 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, Func<ParseContext, T, U> action) : this(parser)
    {
        _action2 = action ?? throw new ArgumentNullException(nameof(action));
    }

    public Then(Parser<T> parser, U value) : this(parser)
    {
        _value = value;
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            context.EnterParser(this);

            if (_action1 != null)
            {
                result.Set(parsed.Start, parsed.End, _action1.Invoke(parsed.Value));
            }
            else if (_action2 != null)
            {
                result.Set(parsed.Start, parsed.End, _action2.Invoke(context, parsed.Value));
            }
            else
            {
                // _value can't be null if action1 and action2 are null
                result.Set(parsed.Start, parsed.End, _value!);
            }

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<U>(false, Expression.Default(typeof(U)));

        // parse1 instructions
        // 
        // if (parser1.Success)
        // {
        //    success = true;
        //    value = action(parse1.Value);
        // }

        var parserCompileResult = _parser.Build(context, requireResult: true);

        Expression assignValue;

        if (_action1 != null)
        {
            assignValue = context.DiscardResult
                ? Expression.Invoke(Expression.Constant(_action1), [parserCompileResult.Value])
                : Expression.Assign(result.Value, Expression.Invoke(Expression.Constant(_action1), [parserCompileResult.Value]));
        }
        else if (_action2 != null)
        {
            assignValue = context.DiscardResult
                ? Expression.Invoke(Expression.Constant(_action2), [context.ParseContext, parserCompileResult.Value])
                : Expression.Assign(result.Value, Expression.Invoke(Expression.Constant(_action2), [context.ParseContext, parserCompileResult.Value]));
        }
        else
        {
            assignValue = context.DiscardResult
                ? Expression.Empty()
                : Expression.Assign(result.Value, Expression.Constant(_value, typeof(U)));
        }

        var block = Expression.Block(
                parserCompileResult.Variables,
                parserCompileResult.Body
                .Append(
                    Expression.IfThen(
                        parserCompileResult.Success,
                        Expression.Block(
                            Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                            assignValue
                            )
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }

    override public string ToString() => $"{_parser} (Then)";
}
