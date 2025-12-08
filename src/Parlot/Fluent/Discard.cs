using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Doesn't parse anything and return the default value.
/// </summary>
[Obsolete("Use the Then parser instead.")]
public sealed class Discard<T, U> : Parser<U>, ICompilable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly U _value;

    public Discard(Parser<T> parser, U value)
    {
        _parser = parser;
        _value = value;
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (_parser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, _value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<U>(false, Expression.Constant(_value, typeof(U)));

        var parserCompileResult = _parser.Build(context);

        // success = false;
        // value = _value;
        // 
        // parser instructions
        // 
        // success = parser.success;

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.Assign(result.Success, parserCompileResult.Success)
                )
            );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Discard requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(U));
        
        // Store _value as a lambda field since U can be any type
        var lambdaId = context.RegisterLambda(new Func<U>(() => _value));

        var inner = sourceable.GenerateSource(context);

        // Emit inner parser locals and body
        foreach (var local in inner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in inner.Body)
        {
            result.Body.Add(stmt);
        }

        // success = inner.success;
        // value = _value;
        result.Body.Add($"{result.SuccessVariable} = {inner.SuccessVariable};");
        result.Body.Add($"{result.ValueVariable} = {lambdaId}();");

        return result;
    }

    public override string ToString() => $"{_parser} (Discard)";
}
