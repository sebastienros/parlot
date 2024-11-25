using Parlot.Compilation;
using Parlot.Rewriting;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Returns a default value if the previous parser failed.
/// </summary>
public sealed class Else<T> : Parser<T>, ICompilable, ISeekable
{
    private readonly Parser<T> _parser;
    private readonly T _value;

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Else(Parser<T> parser, T value)
    {
        _parser = parser;
        _value = value;

        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        if (!_parser.Parse(context, ref result))
        {
            result.Set(result.Start, result.End, _value);
        }

        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(true);

        var parserCompileResult = _parser.Build(context);

        // success = true;
        //
        // parser instructions
        // 
        // if (parser.success)
        // {
        //    value = parser.Value
        // }
        // else
        // {
        //   value = defaultValue
        // }

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                context.DiscardResult
                ? Expression.Empty()
                : Expression.IfThenElse(
                    parserCompileResult.Success,
                    Expression.Assign(result.Value, parserCompileResult.Value),
                    Expression.Assign(result.Value, Expression.Constant(_value, typeof(T)))
                )
            )
        );

        return result;
    }
}
