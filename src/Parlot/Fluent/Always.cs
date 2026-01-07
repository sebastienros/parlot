using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Doesn't parse anything and return the default value.
/// </summary>
public sealed class Always<T> : Parser<T>, ICompilable, ISourceable
{
    private readonly T _value;

    public Always(T value)
    {
        Name = "Always";
        _value = value;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        result.Set(context.Scanner.Cursor.Offset, context.Scanner.Cursor.Offset, _value);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return context.CreateCompilationResult<T>(true, Expression.Constant(_value, typeof(T)));
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T), defaultSuccess: true);
        
        // Try to get a string representation of the value for inlining
        var valueExpr = LiteralHelper.ToLiteral(_value);
        
        if (valueExpr == null)
        {
            throw new NotSupportedException(
                $"Always<{typeof(T).Name}> with value '{_value}' cannot be source-generated. " +
                $"Use a lambda that returns the value instead, e.g., SomeParser.Then(static _ => yourValue)");
        }

        result.Body.Add($"{result.SuccessVariable} = true;");
        result.Body.Add($"{result.ValueVariable} = {valueExpr};");

        return result;
    }
}
