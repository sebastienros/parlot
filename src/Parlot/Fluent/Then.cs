using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// Returns a new <see cref="Parser{U,TParseContext}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="U">The output parser type.</typeparam>
    /// <typeparam name="TParseContext">The parse context type.</typeparam>
    public sealed class Then<T, U, TParseContext> : Parser<U, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Func<T, U> _transform1;
        private readonly Func<TParseContext, T, U> _transform2;
        private readonly Parser<T, TParseContext> _parser;

        public Then(Parser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T, TParseContext> parser, Func<T, U> action)
        {
            _transform1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T, TParseContext> parser, Func<TParseContext, T, U> action)
        {
            _transform2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                if (_transform1 != null)
                {
                    result.Set(parsed.Start, parsed.End, _transform1(parsed.Value));
                }
                else if (_transform2 != null)
                {
                    result.Set(parsed.Start, parsed.End, _transform2(context, parsed.Value));
                }

                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
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

            if (_transform1 != null)
            {
                transformation = Expression.Invoke(Expression.Constant(_transform1), new[] { parserCompileResult.Value });
            }
            else if (_transform2 != null)
            {
                transformation = Expression.Invoke(Expression.Constant(_transform2), context.ParseContext, parserCompileResult.Value);
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

    /// <summary>
    /// Returns a new <see cref="Parser{U,TParseContext}" /> converting the input value of 
    /// type T to the output value of type U using a custom function.
    /// </summary>
    /// <typeparam name="T">The input parser type.</typeparam>
    /// <typeparam name="TParseContext">The parse context type.</typeparam>
    public sealed class Then<T, TParseContext> : Parser<T, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Action<T> _action1;
        private readonly Action<TParseContext, T> _action2;
        private readonly Parser<T, TParseContext> _parser;

        public Then(Parser<T, TParseContext> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T, TParseContext> parser, Action<T> action)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T, TParseContext> parser, Action<TParseContext, T> action)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_parser.Parse(context, ref result))
            {
                if (_action1 != null)
                {
                    _action1(result.Value);
                }
                else if (_action2 != null)
                {
                    _action2(context, result.Value);
                }

                return true;
            }

            return false;
        }


        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    success = true;
            //    value = action(parse1.Value);
            // }

            var parserCompileResult = _parser.Build(context, requireResult: true);

            Expression action;

            if (_action1 != null)
            {
                action = Expression.Invoke(Expression.Constant(_action1), new[] { parserCompileResult.Value });
            }
            else if (_action2 != null)
            {
                action = Expression.Invoke(Expression.Constant(_action2), context.ParseContext, parserCompileResult.Value);
            }
            else
            {
                action = Expression.Default(typeof(T));
            }

            var block = Expression.Block(
                    parserCompileResult.Variables,
                    parserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            parserCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                action,
                                context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(value, parserCompileResult.Value)
                                )
                            )
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
