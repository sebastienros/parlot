using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class FloatLiteral : Parser<float>, ICompilable
    {
        private readonly NumberOptions _numberOptions;

        public FloatLiteral(NumberOptions numberOptions = NumberOptions.Default)
        {
            _numberOptions = numberOptions;
        }

        public override bool Parse(ParseContext context, ref ParseResult<float> result)
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

            if (context.Scanner.ReadDecimal())
            {
                var end = context.Scanner.Cursor.Offset;
#if NET6_0_OR_GREATER
                var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
#else
                var sourceToParse = context.Scanner.Buffer.Substring(start, end - start);
#endif

                if (float.TryParse(sourceToParse, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                {
                    result.Set(start, end, value);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(reset);

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(float)));

            // var start = context.Scanner.Cursor.Offset;
            // var reset = context.Scanner.Cursor.Position;

            var start = context.DeclareOffsetVariable(result);
            var reset = context.DeclarePositionVariable(result);

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
            //    success = float.TryParse(sourceToParse, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            // }
            //
            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(begin);
            // }
            //

            var end = Expression.Variable(typeof(int), $"end{context.NextNumber}");
#if NET6_0_OR_GREATER
            var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{context.NextNumber}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), context.Buffer(), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(float).GetMethod(nameof(float.TryParse), new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(float).MakeByRefType() });
#else
            var sourceToParse = Expression.Variable(typeof(string), $"sourceToParse{context.NextNumber}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(context.Buffer(), typeof(string).GetMethod("Substring", [typeof(int), typeof(int)]), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(float).GetMethod(nameof(float.TryParse), [typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(float).MakeByRefType()]);
#endif

            // TODO: NETSTANDARD2_1 code path
            var block =
                Expression.IfThen(
                    context.ReadDecimal(),
                    Expression.Block(
                        [end, sourceToParse],
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
