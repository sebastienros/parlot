﻿using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;

        public ZeroOrOne(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            _parser.Parse(context, ref result);

            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // T value;
            //
            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    value parse1.Value;
            //    success = true;
            // }
            // 

            var parserCompileResult = _parser.Build(context);

            var block = Expression.Block(
                parserCompileResult.Variables,
                    Expression.Block(
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, parserCompileResult.Value),
                            Expression.Assign(success, Expression.Constant(true))
                            )
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
