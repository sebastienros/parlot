using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class PatternLiteral : Parser<TextSpan>, ICompilable
    {
        private readonly Func<char, bool> _predicate;
        private readonly int _minSize;
        private readonly int _maxSize;
        private readonly bool _skipWhiteSpace;

        public PatternLiteral(Func<char, bool> predicate, int minSize = 1, int maxSize = 0, bool skipWhiteSpace = true)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _minSize = minSize;
            _maxSize = maxSize;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
            }

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

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = result.Success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = result.Value = Expression.Variable(typeof(TextSpan), $"value{context.Counter}");

            result.Variables.Add(success);

            if (!context.DiscardResult)
            {
                result.Variables.Add(value);
            }

            result.Body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));

            //if (_skipWhiteSpace)
            //{
            //    context.SkipWhiteSpace();
            //}

            if (_skipWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                result.Body.Add(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), $"start{context.Counter}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, ExpressionHelper.Position(context.ParseContext)));

            // var size = 0;

            var size = Expression.Variable(typeof(int), $"size{context.Counter}");
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
                            ExpressionHelper.Eof(context.ParseContext),
                            Expression.Break(breakLabel)
                        ),
                        Expression.IfThen(
                            Expression.Not(Expression.Invoke(Expression.Constant(_predicate), ExpressionHelper.Current(context.ParseContext))),
                            Expression.Break(breakLabel)
                        ),
                        ExpressionHelper.Advance(context.ParseContext),
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

            var textSpanCtor = typeof(TextSpan).GetConstructor(new[] { typeof(string), typeof(int), typeof(int) });
            var startOffset = Expression.Field(start, nameof(TextPosition.Offset));

            result.Body.Add(
                Expression.IfThenElse(
                    Expression.LessThan(size, Expression.Constant(_minSize)),
                    ExpressionHelper.ResetPosition(context.ParseContext, start),
                    Expression.Block(
                        context.DiscardResult 
                        ? Expression.Empty()
                        : Expression.Assign(value, 
                            Expression.New(textSpanCtor,
                                ExpressionHelper.Buffer(context.ParseContext),
                                startOffset,
                                Expression.Subtract(ExpressionHelper.Offset(context.ParseContext), startOffset)
                                )),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                        )
                    )
                );

            return result;
        }
    }
}
