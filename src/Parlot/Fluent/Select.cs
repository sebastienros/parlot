using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

/// <summary>
/// Selects a parser instance at runtime and delegates parsing to it.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class Select<C, T> : Parser<T>, ICompilable, ISourceable where C : ParseContext
{
    private static readonly MethodInfo _parse = typeof(Parser<T>).GetMethod(nameof(Parse), [typeof(ParseContext), typeof(ParseResult<T>).MakeByRefType()])!;

    private readonly Func<C, Parser<T>> _selector;

    public Select(Func<C, Parser<T>> selector)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var nextParser = _selector((C)context);

        if (nextParser == null)
        {
            context.ExitParser(this);
            return false;
        }

        var parsed = new ParseResult<T>();

        if (nextParser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();
        var parserVariable = Expression.Variable(typeof(Parser<T>), $"select{context.NextNumber}");
        var parseResult = Expression.Variable(typeof(ParseResult<T>), $"value{context.NextNumber}");

        var selectorInvoke = Expression.Invoke(
            Expression.Constant(_selector),
            Expression.Convert(context.ParseContext, typeof(C)));

        var body = Expression.Block(
            [parserVariable, parseResult],
            Expression.Assign(parserVariable, selectorInvoke),
            Expression.IfThen(
                Expression.NotEqual(parserVariable, Expression.Constant(null, typeof(Parser<T>))),
                Expression.Block(
                    Expression.Assign(
                        result.Success,
                        Expression.Call(parserVariable, _parse, context.ParseContext, parseResult)),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.IfThen(
                            result.Success,
                            Expression.Assign(result.Value, Expression.Field(parseResult, nameof(ParseResult<T>.Value))))
                )));

        result.Body.Add(body);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;

        // Register the selector lambda
        var selectorLambda = context.RegisterLambda(_selector);

        var parserName = $"parser{context.NextNumber()}";
        var parseResultName = $"result{context.NextNumber()}";

        result.Body.Add($"var {parserName} = {selectorLambda}(({SourceGenerationContext.GetTypeName(typeof(C))}){ctx});");
        result.Body.Add($"if ({parserName} != null)");
        result.Body.Add("{");
        result.Body.Add($"    var {parseResultName} = new global::Parlot.ParseResult<{SourceGenerationContext.GetTypeName(typeof(T))}>();");
        result.Body.Add($"    if ({parserName}.Parse({ctx}, ref {parseResultName}))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = {parseResultName}.Value;");
        }
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => "(Select)";
}
