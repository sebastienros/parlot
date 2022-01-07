using FastExpressionCompiler;
using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public abstract partial class Parser<T>
    {
        /// <summary>
        /// Compiles the current parser.
        /// </summary>
        /// <returns>A compiled parser.</returns>
        public Parser<T> Compile()
        {
            if (this is ICompiledParser)
            {
                return this;
            }

            var compilationContext = new CompilationContext();

            var compilationResult = Build(compilationContext);

            // return value;

            var returnLabelTarget = Expression.Label(typeof(ValueTuple<bool, T>));
            var returnLabelExpression = Expression.Label(returnLabelTarget, Expression.New(typeof(ValueTuple<bool, T>).GetConstructor(new[] { typeof(bool), typeof(T) }), compilationResult.Success, compilationResult.Value));

            compilationResult.Body.Add(returnLabelExpression);

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

            var parser = result.CompileFast();

            // parser is a Func, so we use CompiledParser to encapsulate it in a Parser<T>
            return new CompiledParser<T>(parser, this);
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
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);

            // 
            // T value;
            // ParseResult parseResult;
            //
            // success = parser.Parse(context.ParseContext, ref parseResult)
            // #if not DicardResult
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
                Expression.Assign(success, 
                    Expression.Call(
                        Expression.Constant(this), 
                        GetType().GetMethod("Parse", new[] { typeof(ParseContext), typeof(ParseResult<T>).MakeByRefType() }), 
                        context.ParseContext, 
                        parseResult))
                );

            if (!context.DiscardResult)
            {
                var value = result.Value = Expression.Variable(typeof(T), $"value{context.NextNumber}");
                result.Variables.Add(value);

                result.Body.Add(
                    Expression.IfThen(
                        success,
                        Expression.Assign(value, Expression.Field(parseResult, "Value"))
                        )
                    );
            }

            return result;
        }
    }
}
