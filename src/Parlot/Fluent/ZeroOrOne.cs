﻿using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T> : Parser<T>, ICompilable, ISeekable
    {
        private readonly Parser<T> _parser;

        public ZeroOrOne(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            _parser.Parse(context, ref result);

            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, true);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // T value;
            //
            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    value parse1.Value;
            // }
            // 

            var parserCompileResult = _parser.Build(context);

            var block = Expression.Block(
                parserCompileResult.Variables,
                    Expression.Block(
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThen(
                            parserCompileResult.Success,
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, parserCompileResult.Value)
                            )
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
