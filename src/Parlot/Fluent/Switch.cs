using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;


/// <summary>
/// Routes the parsing based on a custom delegate.
/// </summary>
public sealed class Switch<T, U> : Parser<U>, ICompilable, ISourceable
{
    private static readonly MethodInfo _uParse = typeof(Parser<U>).GetMethod("Parse", [typeof(ParseContext), typeof(ParseResult<U>).MakeByRefType()])!;

    private readonly Parser<T> _previousParser;
    private readonly Func<ParseContext, T, Parser<U>> _action;
    public Switch(Parser<T> previousParser, Func<ParseContext, T, Parser<U>> action)
    {
        _previousParser = previousParser ?? throw new ArgumentNullException(nameof(previousParser));
        _action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public override bool Parse(ParseContext context, ref ParseResult<U> result)
    {
        context.EnterParser(this);

        var previousResult = new ParseResult<T>();

        if (!_previousParser.Parse(context, ref previousResult))
        {
            context.ExitParser(this);
            return false;
        }

        var nextParser = _action(context, previousResult.Value);

        if (nextParser == null)
        {
            context.ExitParser(this);
            return false;
        }

        var parsed = new ParseResult<U>();

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
        var result = context.CreateCompilationResult<U>();

        // previousParser instructions
        // 
        // if (previousParser.Success)
        // {
        //    var nextParser = _action(context, previousParser.Value);
        //
        //    if (nextParser != null)
        //    {
        //       var parsed = new ParseResult<U>();
        //
        //       if (nextParser.Parse(context, ref parsed))
        //       {
        //           value = parsed.Value;
        //           success = true;
        //       }
        //    }
        // }

        var previousParserCompileResult = _previousParser.Build(context, requireResult: true);
        var nextParser = Expression.Parameter(typeof(Parser<U>));
        var parseResult = Expression.Variable(typeof(ParseResult<U>), $"value{context.NextNumber}");

        var block = Expression.Block(
                previousParserCompileResult.Variables,
                previousParserCompileResult.Body
                .Append(
                    Expression.IfThen(
                        previousParserCompileResult.Success,
                        Expression.Block(
                            [nextParser, parseResult],
                            Expression.Assign(nextParser, Expression.Invoke(Expression.Constant(_action), new[] { context.ParseContext, previousParserCompileResult.Value })),
                            Expression.IfThen(
                                Expression.NotEqual(Expression.Constant(null, typeof(Parser<U>)), nextParser),
                                Expression.Block(
                                    Expression.Assign(result.Success,
                                        Expression.Call(
                                            nextParser,
                                            _uParse,
                                            context.ParseContext,
                                            parseResult)),
                                    context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.IfThen(result.Success, Expression.Assign(result.Value, Expression.Field(parseResult, "Value")))
                                    )
                                )
                            )
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_previousParser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Switch requires a source-generatable previous parser.");
        }

        var result = context.CreateResult(typeof(U));
        var ctx = context.ParseContextName;

        var previousInner = sourceable.GenerateSource(context);

        // Emit previous parser locals and body
        foreach (var local in previousInner.Locals)
        {
            result.Body.Add(local);
        }

        foreach (var stmt in previousInner.Body)
        {
            result.Body.Add(stmt);
        }

        // Register the action lambda
        var actionLambda = context.RegisterLambda(_action);

        var nextParserName = $"nextParser{context.NextNumber()}";
        var parseResultName = $"result{context.NextNumber()}";

        result.Body.Add($"if ({previousInner.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    var {nextParserName} = {actionLambda}({ctx}, {previousInner.ValueVariable});");
        result.Body.Add($"    if ({nextParserName} != null)");
        result.Body.Add("    {");
        result.Body.Add($"        var {parseResultName} = new global::Parlot.ParseResult<{SourceGenerationContext.GetTypeName(typeof(U))}>();");
        result.Body.Add($"        if ({nextParserName}.Parse({ctx}, ref {parseResultName}))");
        result.Body.Add("        {");
        result.Body.Add($"            {result.SuccessVariable} = true;");
        result.Body.Add($"            {result.ValueVariable} = {parseResultName}.Value;");
        result.Body.Add("        }");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_previousParser} (Switch)";
}
