using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// Doesn't parse anything and return the default value.
    /// </summary>
    public sealed class Discard<T, U> : Parser<U>, ICompilable
    {
        private readonly Parser<T> _parser;
        private readonly U _value;

        public Discard(Parser<T> parser)
        {
            _value = default(U);
            _parser = parser;
        }

        public Discard(Parser<T> parser, U value)
        {
            _parser = parser;
            _value = value;
        }

        public override bool Parse(ParseContext context, ref ParseResult<U> result)
        {
            context.EnterParser(this);

            var parsed = new ParseResult<T>();

            if (_parser.Parse(context, ref parsed))
            {
                result.Set(parsed.Start, parsed.End, _value);
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            _ = context.DeclareValueVariable(result, Expression.Constant(_value));

            var parserCompileResult = _parser.Build(context);

            // success = false;
            // value = _value;
            // 
            // parser instructions
            // 
            // success = parser.success;

            result.Body.Add(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.Assign(success, parserCompileResult.Success)
                    )
                );

            return result;
        }
    }
}
