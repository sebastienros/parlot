using FastExpressionCompiler;
using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    private static readonly ConstructorInfo _valueTupleConstructor = typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!;

    /// <summary>
    /// Compiles the current parser.
    /// </summary>
    /// <returns>A compiled parser.</returns>
    public Parser<T> Compile()
    {
        lock (this)
        {
            if (this is ICompiledParser)
            {
                return this;
            }

            var compilationContext = new CompilationContext();

            var compilationResult = Build(compilationContext);

            // return value;

            var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"result{compilationContext.NextNumber}");
            var returnTarget = Expression.Label(typeof(ValueTuple<bool, T>));
            var returnExpression = Expression.Return(returnTarget, resultExpression, typeof(ValueTuple<bool, T>));
            var returnLabel = Expression.Label(returnTarget, defaultValue: Expression.New(typeof(ValueTuple<bool, T>)));

            compilationResult.Variables.Add(resultExpression);
            compilationResult.Body.Add(
                Expression.Block(
                    Expression.Assign(resultExpression, Expression.New(_valueTupleConstructor, compilationResult.Success, compilationResult.Value)),
                    returnExpression,
                    returnLabel
                )
            );

            // global variables;

            // parser variables;

            var allVariables = new List<ParameterExpression>();
            allVariables.AddRange(compilationContext.GlobalVariables);
            allVariables.AddRange(compilationResult.Variables);

            // global statements;

            // parser statements;

            var allExpressions = new List<Expression>();
            allExpressions.AddRange(compilationContext.GlobalExpressions);
            allExpressions.AddRange(compilationResult.Body);

            var body = Expression.Block(
                typeof(ValueTuple<bool, T>),
                allVariables,
                allExpressions
                );

            var result = Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(body, compilationContext.ParseContext);

            // In Debug mode, inspect the generated code with
            // result.ToCSharpString();

            var parser = result.CompileFast(ifFastFailedReturnNull: false, ExpressionHelper.CompilerFlags);

            // parser is a Func, so we use CompiledParser to encapsulate it in a Parser<T>
            return new CompiledParser<T>(parser, this);
        }
    }

    /// <summary>
    /// Invokes the <see cref="ICompilable.Compile(CompilationContext)"/> method of the <see cref="Parser{T}"/> if it's available or 
    /// creates a generic one.
    /// </summary>
    /// <param name="context">The <see cref="CompilationContext"/> instance.</param>
    /// <param name="requireResult">Forces the instruction to compute the resulting value whatever the state of <see cref="CompilationContext.DiscardResult"/> is.</param>
    public CompilationResult Build(CompilationContext context, bool requireResult = false)
    {
        if (this is ICompilable compilable)
        {
            var discardResult = context.DiscardResult;
            if (requireResult)
            {
                context.DiscardResult = false;
            }

            var compilationResult = compilable.Compile(context);

            context.DiscardResult = discardResult;

            return compilationResult;
        }
        else if (this is CompiledParser<T> compiled)
        {
            // If the parser is already compiled (reference on an already compiled parser, like a sub-tree) create a new build of the source parser.

            return compiled.Source.Build(context, requireResult);
        }
        else
        {
            // The parser doesn't provide custom compiled instructions, so we are building generic ones based on its Parse() method.
            // Any other parser it uses won't be compiled either, even if they implement ICompilable.

            return BuildAsNonCompilableParser(context);
        }
    }

    private CompilationResult BuildAsNonCompilableParser(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(false);

        // 
        // T value;
        // ParseResult parseResult;
        //
        // success = parser.Parse(context.ParseContext, ref parseResult)
        // #if not DiscardResult
        // if (success)
        // {
        //    value = parseResult.Value;
        // }
        // #endif
        // 

        // ParseResult<T> parseResult;

        var parseResult = Expression.Variable(typeof(ParseResult<T>), $"value{context.NextNumber}");
        result.Variables.Add(parseResult);

        // success = parser.Parse(context.ParseContext, ref parseResult)

        result.Body.Add(
            Expression.Assign(result.Success,
                Expression.Call(
                    Expression.Constant(this),
                    GetType().GetMethod("Parse", [typeof(ParseContext), typeof(ParseResult<T>).MakeByRefType()])!,
                    context.ParseContext,
                    parseResult))
            );

        if (!context.DiscardResult)
        {
            var value = result.Value = Expression.Variable(typeof(T), $"value{context.NextNumber}");
            result.Variables.Add(value);

            result.Body.Add(
                Expression.IfThen(
                    result.Success,
                    Expression.Assign(value, Expression.Field(parseResult, "Value"))
                    )
                );
        }

        return result;
    }
}
