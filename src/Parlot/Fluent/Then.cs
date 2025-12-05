using Parlot;
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;
using System.Linq.Expressions;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

/// <summary>
/// Returns a new <see cref="Parser{U}" /> converting the input value of 
/// type T to the output value of type U using a custom function.
/// </summary>
/// <typeparam name="T">The input parser type.</typeparam>
/// <typeparam name="U">The output parser type.</typeparam>
public sealed class Then<T, U> : Parser<U>, ICompilable, ISeekable, ISourceable
{
    private readonly Func<T, U>? _action1;
    private readonly Func<ParseContext, T, U>? _action2;
    private readonly Func<ParseContext, int, int, T, U>? _action3;
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

    public Then(Parser<T> parser, Func<ParseContext, int, int, T, U> action) : this(parser)
    {
        _action3 = action ?? throw new ArgumentNullException(nameof(action));
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
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            if (_action1 != null)
            {
                result.Set(parsed.Start, parsed.End, _action1.Invoke(parsed.Value));
            }
            else if (_action2 != null)
            {
                result.Set(parsed.Start, parsed.End, _action2.Invoke(context, parsed.Value));
            }
            else if (_action3 != null)
            {
                result.Set(parsed.Start, parsed.End, _action3.Invoke(context, parsed.Start, parsed.End, parsed.Value));
            }
            else
            {
                // _value can't be null if action1, action2, and action3 are null
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
        // var startOffset = context.Scanner.Cursor.Offset; // Only for _action3
        // parser1 body (which may include whitespace skipping for Terms)
        // if (parser1.Success)
        // {
        //    var endOffset = context.Scanner.Cursor.Offset;  // Only for _action3
        //    success = true;
        //    value = action(parse1.Value) // or action(context, start, end, parse1.Value) for _action3
        // }

        ParameterExpression? startOffset = null;
        ParameterExpression? endOffset = null;

        if (_action3 != null)
        {
            // Capture the start offset before the parser runs
            // Note: For Terms parsers (which skip whitespace), this will be before whitespace is skipped
            // This differs from non-compiled mode where parsed.Start is after whitespace skipping
            startOffset = result.DeclareVariable<int>($"startOffset{context.NextNumber}", context.Offset());
            endOffset = result.DeclareVariable<int>($"endOffset{context.NextNumber}");
        }

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
        else if (_action3 != null)
        {
            // Capture end offset when parser succeeds, then invoke the action
            assignValue = Expression.Block(
                Expression.Assign(endOffset!, context.Offset()),
                context.DiscardResult
                    ? Expression.Invoke(Expression.Constant(_action3), [context.ParseContext, startOffset!, endOffset!, parserCompileResult.Value])
                    : Expression.Assign(result.Value, Expression.Invoke(Expression.Constant(_action3), [context.ParseContext, startOffset!, endOffset!, parserCompileResult.Value]))
            );
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

    
    public Parlot.SourceGeneration.SourceResult GenerateSource(Parlot.SourceGeneration.SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(U));
        var ctx = context.ParseContextName;
        var parsedName = $"parsed{context.NextNumber()}";

        result.Locals.Add($"global::Parlot.ParseResult<T> {parsedName} = default;");
        result.Body.Add($"if (_parser.Parse({ctx}, ref {parsedName}))");
        result.Body.Add("{");
        result.Body.Add("    U tempValue;");

        if (_action1 != null)
        {
            result.Body.Add($"    tempValue = _action1.Invoke({parsedName}.Value);");
        }
        else if (_action2 != null)
        {
            result.Body.Add($"    tempValue = _action2.Invoke({ctx}, {parsedName}.Value);");
        }
        else if (_action3 != null)
        {
            result.Body.Add($"    tempValue = _action3.Invoke({ctx}, {parsedName}.Start, {parsedName}.End, {parsedName}.Value);");
        }
        else
        {
            result.Body.Add("    tempValue = _value;");
        }

        result.Body.Add($"    {result.ValueVariable} = tempValue;");
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = false;");
        result.Body.Add("}");

        return result;
    }

override public string ToString() => $"{_parser} (Then)";
}
