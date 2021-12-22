﻿using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="Parser{U}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="U">The output parser type.</typeparam>
    public sealed class Then<T, U> : Parser<U>, ICompilable, ISeekable
    {
        private readonly Func<T, U> _action1;
        private readonly Func<ParseContext, T, U> _action2;
        private readonly Parser<T> _parser;
        
        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

        public Then(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T> parser, Func<T, U> action) : this(parser)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
        }

        public Then(Parser<T> parser, Func<ParseContext, T, U> action) : this(parser)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);
            
            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                if (_action1 != null)
                {
                    result.Set(parsed.Start, parsed.End, _action1.Invoke(parsed.Value));
                }
                else if (_action2 != null)
                {
                    result.Set(parsed.Start, parsed.End, _action2.Invoke(context, parsed.Value));
                }

                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(U)));

            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    success = true;
            //    value = action(parse1.Value);
            // }

            var parserCompileResult = _parser.Build(context, requireResult: true);

            Expression transformation;

            if (_action1 != null)
            {
                transformation = Expression.Invoke(Expression.Constant(_action1), new [] { parserCompileResult.Value });
            }
            else if (_action2 != null)
            {
                transformation = Expression.Invoke(Expression.Constant(_action2), context.ParseContext, parserCompileResult.Value);
            }
            else
            {
                transformation = Expression.Default(typeof(U));
            }

            var block = Expression.Block(
                    parserCompileResult.Variables,
                    parserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            parserCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(value, transformation)
                                )
                            )
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
