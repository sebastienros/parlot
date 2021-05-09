using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    internal static class SequenceCompileHelper
    {
        internal static string SequenceRequired = $"The parser needs to implement {nameof(ISkippableSequenceParser<StringParseContext, char>)}";

        public static CompilationResult CreateSequenceCompileResult<TParseContext, TChar>(SkippableCompilationResult[] parserCompileResults, CompilationContext<TParseContext, TChar> context)
        where TParseContext : ParseContextWithScanner<Scanner<TChar>, TChar>
        where TChar : IEquatable<TChar>, IConvertible
        {
            var result = new CompilationResult();

            var nonSkippableResults = parserCompileResults.Where(x => !x.Skip).ToArray();
            var parserTypes = nonSkippableResults.Select(x => x.CompilationResult.Value.Type).ToArray();
            var resultType = GetValueTuple(nonSkippableResults.Length).MakeGenericType(parserTypes);

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.New(resultType));

            // var start = context.Scanner.Cursor.Position;

            var start = context.DeclarePositionVariable(result);

            // parse1 instructions
            // 
            // if (parser1Success)
            // {
            //
            //   parse2 instructions
            //   
            //   if (parser2Success)
            //   {
            //      success = true;
            //      value = new ValueTuple<T1, T2>(parser1.Value, parse2.Value)
            //   }
            // }
            // 

            static Type GetValueTuple(int length)
            {
                return length switch
                {
                    2 => typeof(ValueTuple<,>),
                    3 => typeof(ValueTuple<,,>),
                    4 => typeof(ValueTuple<,,,>),
                    5 => typeof(ValueTuple<,,,,>),
                    6 => typeof(ValueTuple<,,,,,>),
                    7 => typeof(ValueTuple<,,,,,,>),
                    8 => typeof(ValueTuple<,,,,,,,>),
                    _ => null
                };
            }

            var valueTupleConstructor = resultType.GetConstructor(parserTypes);

            // Initialize the block variable with the inner else statement
            var block = Expression.Block(
                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, Expression.New(valueTupleConstructor, nonSkippableResults.Select(x => x.CompilationResult.Value).ToArray()))
                            );

            for (var i = parserCompileResults.Length - 1; i >= 0; i--)
            {
                var parserCompileResult = parserCompileResults[i].CompilationResult;

                block = Expression.Block(
                    parserCompileResult.Variables,
                    parserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            parserCompileResult.Success,
                            block
                        ))
                    );

            }

            result.Body.Add(block);

            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(start);
            // }

            result.Body.Add(Expression.IfThen(
                Expression.Not(success),
                context.ResetPosition(start)
                ));

            return result;
        }
    }
}
