using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class PatternLiteral<TParseContext> : Parser<TextSpan, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Func<char, bool> _predicate;
        private readonly int _minSize;
        private readonly int _maxSize;

        public PatternLiteral(Func<char, bool> predicate, int minSize = 1, int maxSize = 0)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _minSize = minSize;
            _maxSize = maxSize;
        }

        public override bool Parse(TParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            if (context.Scanner.Cursor.Eof || !_predicate(context.Scanner.Cursor.Current))
            {
                return false;
            }

            var startPosition = context.Scanner.Cursor.Position;
            var start = startPosition.Offset;

            context.Scanner.Cursor.Advance();
            var size = 1;

            while (!context.Scanner.Cursor.Eof && (_maxSize <= 0 || size < _maxSize) && _predicate(context.Scanner.Cursor.Current))
            {
                context.Scanner.Cursor.Advance();
                size++;
            }

            if (size >= _minSize)
            {
                var end = context.Scanner.Cursor.Offset;
                result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));

                return true;
            }

            // When the size constraint has not been met the parser may still have advanced the cursor.
            context.Scanner.Cursor.ResetPosition(startPosition);

            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.NextNumber}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, context.Position()));

            // var size = 0;

            var size = Expression.Variable(typeof(int), $"size{context.NextNumber}");
            result.Variables.Add(size);
            result.Body.Add(Expression.Assign(size, Expression.Constant(0, typeof(int))));

            // while (true)
            // {
            //     if (context.Scanner.Cursor.Eof)
            //     {
            //        break;
            //     }
            //
            //     if (!_predicate(context.Scanner.Cursor.Current))
            //     {
            //        break;
            //     }
            //
            //     context.Scanner.Cursor.Advance();
            // 
            //     size++;
            //
            //     #if _maxSize > 0 ?
            //     if (size == _maxSize)
            //     {
            //        break;
            //     }
            //     #endif
            // }

            var breakLabel = Expression.Label("break");

            result.Body.Add(
                Expression.Loop(
                    Expression.Block(
                        Expression.IfThen(
                            context.Eof(),
                            Expression.Break(breakLabel)
                        ),
                        Expression.IfThen(
                            Expression.Not(Expression.Invoke(Expression.Constant(_predicate), context.Current())),
                            Expression.Break(breakLabel)
                        ),
                        context.Advance(),
                        Expression.Assign(size, Expression.Add(size, Expression.Constant(1))),
                        _maxSize == 0 
                        ? Expression.Empty()
                        : Expression.IfThen(
                            Expression.Equal(size, Expression.Constant(_maxSize)),
                            Expression.Break(breakLabel)
                            )
                    ),
                    breakLabel)
                );


            // if (size < _minSize)
            // {
            //     context.Scanner.Cursor.ResetPosition(startPosition);
            // }
            // else
            // {
            //     value = new TextSpan(context.Scanner.Buffer, start, end - start);
            //     success = true;
            // }

            var startOffset = Expression.Field(start, nameof(TextPosition.Offset));

            result.Body.Add(
                Expression.IfThenElse(
                    Expression.LessThan(size, Expression.Constant(_minSize)),
                    context.ResetPosition(start),
                    Expression.Block(
                        context.DiscardResult 
                        ? Expression.Empty()
                        : Expression.Assign(value,
                            context.NewTextSpan(
                                context.Buffer(),
                                startOffset,
                                Expression.Subtract(context.Offset(), startOffset)
                                )),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                        )
                    )
                );

            return result;
        }
    }
}
