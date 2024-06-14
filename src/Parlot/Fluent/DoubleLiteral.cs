using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    [Obsolete]
    public sealed class DoubleLiteral : Parser<double>, ICompilable
    {
        private readonly NumberOptions _numberOptions;
        private readonly NumberStyles _numberStyles;

        public DoubleLiteral(NumberOptions numberOptions = NumberOptions.Float)
        {
            _numberOptions = numberOptions;
            _numberStyles = _numberOptions.ToNumberStyles();
        }

        public override bool Parse(ParseContext context, ref ParseResult<double> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;
            var start = reset.Offset;

            if (context.Scanner.ReadDecimal(_numberOptions, out var number))
            {
                var end = context.Scanner.Cursor.Offset;
#if NET6_0_OR_GREATER
                var sourceToParse = number;
#else
                var sourceToParse = number.ToString();
#endif

                if (double.TryParse(sourceToParse, _numberStyles, CultureInfo.InvariantCulture, out var value))
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
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(double)));

            // var start = context.Scanner.Cursor.Offset;
            // var reset = context.Scanner.Cursor.Position;

            var start = context.DeclareOffsetVariable(result);
            var reset = context.DeclarePositionVariable(result);

            var numberStyles = context.DeclareVariable<NumberStyles>(result, $"numberStyles{context.NextNumber}", Expression.Constant(_numberStyles));

            // if (context.Scanner.ReadDecimal())
            // {
            //    var end = context.Scanner.Cursor.Offset;
            //    NETSTANDARD2_0 var sourceToParse = context.Scanner.Buffer.Substring(start, end - start);
            //    NETSTANDARD2_1 var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
            //    success = double.TryParse(sourceToParse, numberStyles, CultureInfo.InvariantCulture, out var value))
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
            var tryParseMethodInfo = typeof(double).GetMethod(nameof(double.TryParse), new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(double).MakeByRefType()});
#else
            var sourceToParse = Expression.Variable(typeof(string), $"sourceToParse{context.NextNumber}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(context.Buffer(), typeof(string).GetMethod("Substring", [typeof(int), typeof(int)]), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(double).GetMethod(nameof(double.TryParse), [typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(double).MakeByRefType()]);
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
                                numberStyles,
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
