using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class DecimalLiteral<TParseContext> : Parser<decimal, TParseContext, char>, ICompilable<TParseContext, char>
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        private readonly NumberOptions _numberOptions;
        private readonly bool _skipWhiteSpace;

        public DecimalLiteral(NumberOptions numberOptions = NumberOptions.Default, bool skipWhiteSpace = true)
        {
            _numberOptions = numberOptions;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(TParseContext context, ref ParseResult<decimal> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;

            if (_skipWhiteSpace)
            {
                if (context is StringParseContext stringParseContext)
                    stringParseContext.SkipWhiteSpace();
                else
                    context.Scanner.SkipWhiteSpace();
            }

            var start = context.Scanner.Cursor.Offset;

            if ((_numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                if (!context.Scanner.ReadChar('-'))
                {
                    // If there is no '-' try to read a '+' but don't read both.
                    context.Scanner.ReadChar('+');
                }
            }

            if (context.Scanner.ReadDecimal())
            {
                var end = context.Scanner.Cursor.Offset;


#if NETSTANDARD2_0
                var sourceToParse = context.Scanner.Buffer.SubBuffer(start, end - start).ToString();
#else
                var sourceToParse = context.Scanner.Buffer.SubBuffer(start, end - start).Span;
#endif

                if (decimal.TryParse(sourceToParse.ToString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                {
                    result.Set(start, end, value);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(reset);

            return false;
        }

        public CompilationResult Compile(CompilationContext<TParseContext, char> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(decimal)));

            // TODO: if !_skiptWhiteSpace and !NumberOptions.AllowSign then we don't need to store the reset position
            // since the ReadDecimal method will do it at the correct location.

            //
            // var reset = context.Scanner.Cursor.Position;
            //
            // if (_skipWhiteSpace)
            // {
            //     context.SkipWhiteSpace();
            // }

            var reset = context.DeclarePositionVariable(result);

            if (_skipWhiteSpace)
            {
                result.Body.Add(context.ParserSkipWhiteSpace());
            }

            // var start = context.Scanner.Cursor.Offset;

            var start = Expression.Variable(typeof(int), $"start{context.NextNumber}");
            result.Variables.Add(start);

            result.Body.Add(Expression.Assign(start, context.Offset()));

            if ((_numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                // if (!context.Scanner.ReadChar('-'))
                // {
                //     context.Scanner.ReadChar('+');
                // }

                result.Body.Add(
                    Expression.IfThen(
                        Expression.Not(context.ReadChar('-')),
                        context.ReadChar('+')
                        )
                    );
            }

            // if (context.Scanner.ReadDecimal())
            // {
            //    var end = context.Scanner.Cursor.Offset;
            //    NETSTANDARD2_0 var sourceToParse = context.Scanner.Buffer.Substring(start, end - start);
            //    NETSTANDARD2_1 var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
            //    success = decimal.TryParse(sourceToParse, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            // }
            //
            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(begin);
            // }
            //

            var end = Expression.Variable(typeof(int), $"end{context.NextNumber}");
#if NETSTANDARD2_0
            var sourceToParse = Expression.Variable(typeof(string), $"sourceToParse{context.NextNumber}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(context.SubBufferSpan(start, Expression.Subtract(end, start)), nameof(BufferSpan<char>.ToString), Type.EmptyTypes));
            var tryParseMethodInfo = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(decimal).MakeByRefType() });
#else
            var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{context.NextNumber}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), context.SubBufferSpan(start, Expression.Subtract(end, start))));
            var tryParseMethodInfo = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(decimal).MakeByRefType()});
#endif

            // TODO: NETSTANDARD2_1 code path
            var block =
                Expression.IfThen(
                    context.ReadDecimal(),
                    Expression.Block(
                        new[] { end, sourceToParse },
                        Expression.Assign(end, context.Offset()),
                        sliceExpression,
                        Expression.Assign(success,
                            Expression.Call(
                                tryParseMethodInfo,
                                sourceToParse,
                                Expression.Constant(NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint),
                                Expression.Constant(CultureInfo.InvariantCulture),
                                value)
                            )
                    )
                );

            result.Body.Add(block);

            result.Body.Add(
                Expression.IfThen(
                    Expression.Not(success),
                    context.ResetPosition(reset)
                    )
                );

            return result;
        }
    }
}
