using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Between<A, T, B> : Parser<T>
    {
        private readonly Parser<T> _parser;
        private readonly Parser<A> _before;
        private readonly Parser<B> _after;

        private readonly bool _beforeIsChar;
        private readonly char _beforeChar;
        private readonly bool _beforeSkipWhiteSpace;

        private readonly bool _afterIsChar;
        private readonly char _afterChar;
        private readonly bool _afterSkipWhiteSpace;

        public Between(Parser<A> before, Parser<T> parser, Parser<B> after)
        {
            _before = before ?? throw new ArgumentNullException(nameof(before));
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _after = after ?? throw new ArgumentNullException(nameof(after));

            if (before is CharLiteral literal1)
            {
                _beforeIsChar = true;
                _beforeChar = literal1.Char;
                _beforeSkipWhiteSpace = literal1.SkipWhiteSpace;
            }

            if (after is CharLiteral literal2)
            {
                _afterIsChar = true;
                _afterChar = literal2.Char;
                _afterSkipWhiteSpace = literal2.SkipWhiteSpace;
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_beforeIsChar)
            {
                if (_beforeSkipWhiteSpace)
                {
                    context.SkipWhiteSpace();
                }

                if (!context.Scanner.ReadChar(_beforeChar))
                {
                    return false;
                }
            }
            else
            {
                var parsedA = new ParseResult<A>();

                if (!_before.Parse(context, ref parsedA))
                {
                    return false;
                }
            }

            if (!_parser.Parse(context, ref result))
            {
                return false;
            }            

            if (_afterIsChar)
            {
                if (_afterSkipWhiteSpace)
                {
                    context.SkipWhiteSpace();
                }

                if (!context.Scanner.ReadChar(_afterChar))
                {
                    return false;
                }
            }
            else
            {
                var parsedB = new ParseResult<B>();

                if (!_after.Parse(context, ref parsedB))
                {
                    return false;
                }
            }

            return true;
        }

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(T), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));
            body.Add(Expression.Assign(value, Expression.Constant(default(T), typeof(T))));

            // before instructions
            // 
            // if (before.Success)
            // {
            //    parser instructions
            //    
            //    if (parser.Success)
            //    {
            //       after instructions
            //    
            //       if (after.Success)
            //       {
            //          success = true;
            //          value = parser.Value;
            //       }  
            //    }
            // }

            var beforeCompileResult = _before.Compile(context);
            var parserCompileResult = _parser.Compile(context);
            var afterCompileResult = _after.Compile(context);

            var block = Expression.Block(
                    beforeCompileResult.Variables,
                    Expression.Block(beforeCompileResult.Body),
                    Expression.IfThen(
                        beforeCompileResult.Success,
                        Expression.Block(
                            parserCompileResult.Variables,
                            Expression.Block(parserCompileResult.Body),
                            Expression.IfThen(
                                parserCompileResult.Success,
                                Expression.Block(
                                    afterCompileResult.Variables,
                                    Expression.Block(afterCompileResult.Body),
                                    Expression.IfThen(
                                        afterCompileResult.Success,
                                        Expression.Block(
                                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                            Expression.Assign(value, parserCompileResult.Value)
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    );

            body.Add(block);

            return new CompileResult(variables, body, success, value);
        }
    }
}
