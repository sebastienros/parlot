using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class SkipWhiteSpace<T, TParseContext> : Parser<T, TParseContext, char>, ICompilable<TParseContext, char>
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        private readonly Parser<T, TParseContext> _parser;
        private readonly Parser<BufferSpan<char>, TParseContext> _whiteSpaceParser;

        private static readonly bool canUseNewLines = typeof(TParseContext).IsAssignableFrom(typeof(StringParseContext));

        public SkipWhiteSpace(Parser<T, TParseContext> parser, Parser<BufferSpan<char>, TParseContext> whiteSpaceParser = null)
        {
            _parser = parser;
            _whiteSpaceParser = whiteSpaceParser;
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            // Use the scanner's logic to ignore whitespaces since it knows about multi-line grammars
            if (_whiteSpaceParser is null)
            {
                if (!canUseNewLines || ((StringParseContext)(object)context).UseNewLines)
                {
                    context.Scanner.SkipWhiteSpace();
                }
                else
                {
                    context.Scanner.SkipWhiteSpaceOrNewLine();
                }
            }
            else
            {
                ParseResult<BufferSpan<char>> _ = new();
                _whiteSpaceParser.Parse(context, ref _);
            }

            if (_parser.Parse(context, ref result))
            {
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, char> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

            var start = context.DeclarePositionVariable(result);

            var parserCompileResult = _parser.Build(context);

            if (_whiteSpaceParser is not null)
            {
                var whiteSpaceParserCompileResult = _whiteSpaceParser.Build(context);
                result.Body.Add(Expression.Block(whiteSpaceParserCompileResult.Variables,
                    whiteSpaceParserCompileResult.Body));

                result.Body.Add(
                    Expression.Block(
                        parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(value, parserCompileResult.Value),
                            Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                            ),
                        context.ResetPosition(start)
                        )
                    )
                );
            }
            else
            {

                result.Body.Add(
                    Expression.Block(
                        parserCompileResult.Variables,
                            // Use the scanner's logic to ignore whitespaces since it knows about multi-line grammars
                            Expression.IfThenElse(Expression.Property(context.ParseContext, nameof(StringParseContext.UseNewLines)),
                                context.SkipWhiteSpace(),
                                context.SkipWhiteSpaceOrNewLine()),
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(value, parserCompileResult.Value),
                            Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                            ),
                        context.ResetPosition(start)
                        )
                    )
                );
            }

            return result;
        }
    }
}
