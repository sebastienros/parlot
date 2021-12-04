﻿using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Not<T, TParseContext, TChar> : Parser<T, TParseContext, TChar>, ICompilable<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Parser<T, TParseContext> _parser;

        public Not(Parser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            if (!_parser.Parse(context, ref result))
            {
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, TChar> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            _ = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // var start = context.Scanner.Cursor.Position;

            var start = context.Position();

            var parserCompileResult = _parser.Build(context);

            // success = false;
            //
            // parser instructions
            // 
            // if (parser.succcess)
            // {
            //     context.Scanner.Cursor.ResetPosition(start);
            // }
            // else
            // {
            //     success = true;
            // }
            // 

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        context.ResetPosition(start),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                        )
                    )
                );

            return result;
        }
    }
}
