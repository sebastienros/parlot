using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class IntegerLiteral<TParseContext> : Parser<long, TParseContext, char>, ICompilable<TParseContext, char>
    where TParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        private readonly NumberOptions _numberOptions;

        public IntegerLiteral(NumberOptions numberOptions = NumberOptions.Default)
        {
            _numberOptions = numberOptions;
        }

        public override bool Parse(TParseContext context, ref ParseResult<long> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;
            var start = reset.Offset;

            if ((_numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                if (!context.Scanner.ReadChar('-'))
                {
                    // If there is no '-' try to read a '+' but don't read both.
                    context.Scanner.ReadChar('+');
                }
            }

            if (context.Scanner.ReadInteger())
            {
                var end = context.Scanner.Cursor.Offset;

#if NETSTANDARD2_0
                var sourceToParse = context.Scanner.Buffer.SubBuffer(start, end - start).ToString();
#else
                var sourceToParse = context.Scanner.Buffer.SubBuffer(start, end - start).Span;
#endif

                if (long.TryParse(sourceToParse, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
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
            var value = context.DeclareValueVariable<long, TParseContext>(result);

            var reset = context.DeclarePositionVariable(result);
            var start = context.DeclareOffsetVariable(result);

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

            // if (context.Scanner.ReadInteger())
            // {
            //    var end = context.Scanner.Cursor.Offset;
            //    NETSTANDARD2_0 var sourceToParse = context.Scanner.Buffer.Substring(start, end - start);
            //    NETSTANDARD2_1 var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
            //    success = long.TryParse(sourceToParse, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
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
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(context.Buffer(), typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(long).GetMethod(nameof(long.TryParse), new[] { typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(long).MakeByRefType() });
#else
            var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{context.NextNumber}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), context.Buffer(), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(long).GetMethod(nameof(long.TryParse), new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(long).MakeByRefType()});
#endif

            // TODO: NETSTANDARD2_1 code path
            var block =
                Expression.IfThen(
                    context.ReadInteger(),
                    Expression.Block(
                        new[] { end, sourceToParse },
                        Expression.Assign(end, context.Offset()),
                        sliceExpression,
                        Expression.Assign(success,
                            Expression.Call(
                                tryParseMethodInfo,
                                sourceToParse,
                                Expression.Constant(NumberStyles.AllowLeadingSign),
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
