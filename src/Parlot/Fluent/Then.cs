using System;
using System.Collections.Generic;
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
    public sealed class Then<T, U> : Parser<U>
    {
        private readonly Func<T, U> _action1;
        private readonly Func<ParseContext, T, U> _action2;
        private readonly Parser<T> _parser;

        public Then(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T> parser, Func<T, U> action)
        {
            _action1 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        }

        public Then(Parser<T> parser, Func<ParseContext, T, U> action)
        {
            _action2 = action ?? throw new ArgumentNullException(nameof(action));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
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

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(U), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));

            // parse1 instructions
            // 
            // if (parser1.Success)
            // {
            //    success = true;
            //    value = action(parse1.Value);
            // }

            var parserCompileResult = _parser.Compile(context);

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
                                Expression.Assign(value, transformation)
                                )
                            )
                        )
                    );

            body.Add(block);

            return new CompileResult(variables, body, success, value);
        }
    }
}
