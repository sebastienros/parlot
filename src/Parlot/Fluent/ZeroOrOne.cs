using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class ZeroOrOne<T> : Parser<OptionalResult<T>>, ICompilable, ISeekable
    {
        private readonly Parser<T> _parser;

        public ZeroOrOne(Parser<T> parser)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            
            if (_parser is ISeekable seekable)
            {
                CanSeek = seekable.CanSeek;
                ExpectedChars = seekable.ExpectedChars;
                SkipWhitespace = seekable.SkipWhitespace;
            }
        }

        public bool CanSeek { get; }

        public char[] ExpectedChars { get; } = [];

        public bool SkipWhitespace { get; }

        public override bool Parse(ParseContext context, ref ParseResult<OptionalResult<T>> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            var success = _parser.Parse(context, ref parsed);

            result.Set(parsed.Start, parsed.End, new OptionalResult<T>(success, parsed.Value));

            // ZeroOrOne always succeeds
            return true;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, true);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(OptionalResult<T>)));

            // T value;
            //
            // parse1 instructions
            // 
            // value = new OptionalResult<T>(parser1.Success, parse1.Value);
            //

            var parserCompileResult = _parser.Build(context);

            var block = Expression.Block(
                parserCompileResult.Variables,
                    Expression.Block(
                        Expression.Block(parserCompileResult.Body),
                        context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, context.NewOptionalResult<T>(parserCompileResult.Success, parserCompileResult.Value))
                        )
                    );

            result.Body.Add(block);

            return result;
        }
    }
}
