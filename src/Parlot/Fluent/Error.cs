using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    using Compilation;

    public sealed class ElseError<T> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;
        private readonly string _message;

        public ElseError(Parser<T> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (!_parser.Parse(context, ref result))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult<T>(true);

            // parse1 instructions
            // success = true
            // 
            // if (parser1.Success)
            // {
            //   value = parser1.Value
            // }
            // else
            // {
            //    throw new ParseException(_message, context.Scanner.Cursor.Position);
            // }
            //

            var parserCompileResult = _parser.Build(context, requireResult: true);

            var block = Expression.Block(
                parserCompileResult.Variables,
                parserCompileResult.Body
                .Append(
                    context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(result.Value, parserCompileResult.Value))
                    .Append(
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(result.Value, parserCompileResult.Value),
                            context.ThrowParseException(Expression.Constant(_message))


                    ))
            );

            result.Body.Add(block);

            return result;
        }
    }

    public sealed class Error<T> : Parser<T>, ICompilable
    {
        private readonly Parser<T> _parser;
        private readonly string _message;

        public Error(Parser<T> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_parser.Parse(context, ref result))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult<T>();

            // parse1 instructions
            // success = false;
            //
            // if (parser1.Success)
            // {
            //    value = parser1.Value;
            //    throw new ParseException(_message, context.Scanner.Cursor.Position);
            // }

            var parserCompileResult = _parser.Build(context, requireResult: false);

            var block = Expression.Block(
                parserCompileResult.Variables,
                parserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            parserCompileResult.Success,
                            context.ThrowParseException(Expression.Constant(_message))
                        )
                    )
            );

            result.Body.Add(block);

            return result;
        }
    }

    public sealed class Error<T, U> : Parser<U>, ICompilable
    {
        private readonly Parser<T> _parser;
        private readonly string _message;

        public Error(Parser<T> parser, string message)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _message = message;
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                throw new ParseException(_message, context.Scanner.Cursor.Position);
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult<U>();

            // parse1 instructions
            // success = false;
            // 
            // if (parser1.Success)
            // {
            //    throw new ParseException(_message, context.Scanner.Cursor.Position);
            // }

            var parserCompileResult = _parser.Build(context, requireResult: false);

            var block = Expression.Block(
                parserCompileResult.Variables,
                parserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            parserCompileResult.Success,
                            context.ThrowParseException(Expression.Constant(_message))
                        )
                    )
            );

            result.Body.Add(block);

            return result;
        }
    }
}
