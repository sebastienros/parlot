using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public partial class Parsers
    {
        /// <summary>
        /// Compiles the current parser.
        /// </summary>
        /// <returns>A compiled parser.</returns>
        public static Parser<T, StringParseContext, char> Compile<T>(this Parser<T, StringParseContext, char> self)
        {
            return self.Compile<T, StringParseContext, char>();
        }
        /// <summary>
        /// Compiles the current parser.
        /// </summary>
        /// <returns>A compiled parser.</returns>
        public static Parser<T, TParseContext, TChar> Compile<T, TParseContext, TChar>(this Parser<T, TParseContext, TChar> self)
        where TParseContext : ParseContextWithScanner<TChar>
        where TChar : IEquatable<TChar>, IConvertible
        {
            if (self is ICompiledParser)
            {
                return self;
            }

            var compilationContext = new CompilationContext<TParseContext, TChar>();

            var compilationResult = self.Build(compilationContext);

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

            var result = Expression.Lambda<Func<TParseContext, ValueTuple<bool, T>>>(body, compilationContext.ParseContext);

            var parser = result.Compile();

            // parser is a Func, so we use CompiledParser to encapsulate it in a Parser<T>
            return new CompiledParser<T, TParseContext, TChar>(parser);
        }

        /// <summary>
        /// Invokes the <see cref="ICompilable{TParseContext}.Compile(CompilationContext{TParseContext})"/> method of the <see cref="Parser{T, TParseContext}"/> if it's available or 
        /// creates a generic one.
        /// </summary>
        /// <param name="self">The <see cref="Parser{T, TParseContext}"/> instance.</param>
        /// <param name="context">The <see cref="CompilationContext{TParseContext}"/> instance.</param>
        /// <param name="requireResult">Forces the instruction to compute the resulting value whatever the state of <see cref="CompilationContext{TParseContext}.DiscardResult"/> is.</param>
        public static CompilationResult Build<T, TParseContext>(this Parser<T, TParseContext> self, CompilationContext<TParseContext> context, bool requireResult = false)
        where TParseContext : ParseContext
        {
            if (self is ICompilable<TParseContext> compilable)
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
            else
            {
                // The parser doesn't provide custom compiled instructions, so we are building generic ones based on its Parse() method.
                // Any other parser it uses won't be compiled either, even if they implement ICompilable.

                return BuildAsNonCompilableParser<T, TParseContext>(context, self);
            }
        }

        /// <summary>
        /// Invokes the <see cref="ICompilable{TParseContext}.Compile(CompilationContext{TParseContext})"/> method of the <see cref="Parser{T, TParseContext}"/> if it's available or 
        /// creates a generic one.
        /// </summary>
        /// <param name="self">The <see cref="Parser{T, TParseContext}"/> instance.</param>
        /// <param name="context">The <see cref="CompilationContext{TParseContext}"/> instance.</param>
        /// <param name="requireResult">Forces the instruction to compute the resulting value whatever the state of <see cref="CompilationContext{TParseContext}.DiscardResult"/> is.</param>
        public static CompilationResult Build<T, TParseContext, TChar>(this Parser<T, TParseContext, TChar> self, CompilationContext<TParseContext, TChar> context, bool requireResult = false)
        where TParseContext : ParseContextWithScanner<TChar>
        where TChar : IEquatable<TChar>, IConvertible
        {
            if (self is ICompilable<TParseContext, TChar> compilable)
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
            else
            {
                // The parser doesn't provide custom compiled instructions, so we are building generic ones based on its Parse() method.
                // Any other parser it uses won't be compiled either, even if they implement ICompilable.

                return BuildAsNonCompilableParser<T, TParseContext>(context, self);
            }
        }

        private static CompilationResult BuildAsNonCompilableParser<T, TParseContext>(CompilationContext<TParseContext> context, Parser<T, TParseContext> self)
        where TParseContext : ParseContext
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
                        Expression.Constant(self),
                        self.GetType().GetMethod("Parse", new[] { typeof(TParseContext), typeof(ParseResult<T>).MakeByRefType() }),
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
