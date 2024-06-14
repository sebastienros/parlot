#if NET8_0_OR_GREATER
using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent
{
    public static class NumberLiteral
    {
        public const char DefaultDecimalSeparator = '.';
        public const char DefaultGroupSeparator = ',';

        public static NumberLiteral<T> CreateNumberLiteralParser<T>(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
            where T : INumber<T>
        {
            return new NumberLiteral<T>(numberOptions, decimalSeparator, groupSeparator);
        }
    }

    public sealed class NumberLiteral<T> : Parser<T>, ICompilable
        where T : INumber<T>
    {
        private const char DefaultDecimalSeparator = '.';
        private const char DefaultGroupSeparator = ',';

        private readonly NumberOptions _numberOptions;
        private readonly char _decimalSeparator;
        private readonly char _groupSeparator;
        private readonly NumberStyles _numberStyles;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        private static readonly MethodInfo _tryParseMethodInfo = typeof(T).GetMethod(nameof(INumber<T>.TryParse), [typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()]);

        public NumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
        {
            _numberOptions = numberOptions;
            _decimalSeparator = decimalSeparator;
            _groupSeparator = groupSeparator;
            _numberStyles = _numberOptions.ToNumberStyles();
            
            if (decimalSeparator != CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator[0] || 
                groupSeparator != CultureInfo.InvariantCulture.NumberFormat.NumberGroupSeparator[0])
            {
                _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                _culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
                _culture.NumberFormat.NumberGroupSeparator = groupSeparator.ToString();
            }
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;
            var start = reset.Offset;

            if (context.Scanner.ReadDecimal(_numberOptions, out var number, _decimalSeparator, _groupSeparator))
            {
                var end = context.Scanner.Cursor.Offset;

                if (T.TryParse(number, _numberStyles, _culture, out var value))
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
            var value = context.DeclareValueVariable<T>(result);

            // var start = context.Scanner.Cursor.Offset;
            // var reset = context.Scanner.Cursor.Position;

            var start = context.DeclareOffsetVariable(result);
            var reset = context.DeclarePositionVariable(result);

            var numberStyles = context.DeclareVariable<NumberStyles>(result, $"numberStyles{context.NextNumber}", Expression.Constant(_numberStyles));
            var culture = context.DeclareVariable<CultureInfo>(result, $"culture{context.NextNumber}", Expression.Constant(_culture));
            var numberSpan = context.DeclareVariable(result, $"number{context.NextNumber}", typeof(ReadOnlySpan<char>));
            var end = context.DeclareVariable<int>(result, $"end{context.NextNumber}");

            // if (context.Scanner.ReadDecimal(_numberOptions, out var numberSpan, _decimalSeparator, _groupSeparator))
            // {
            //    var end = context.Scanner.Cursor.Offset;
            //    success = T.TryParse(numberSpan, numberStyles, culture, out var value));
            // }
            //
            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(begin);
            // }
            //

            //var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{context.NextNumber}");
            //var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), context.Buffer(), start, Expression.Subtract(end, start)));

            var block =
                Expression.IfThen(
                    context.ReadDecimal(Expression.Constant(_numberOptions), numberSpan, Expression.Constant(_decimalSeparator), Expression.Constant(_groupSeparator)),
                    Expression.Block(
                        Expression.Assign(end, context.Offset()),
                        Expression.Assign(success,
                            Expression.Call(
                                _tryParseMethodInfo,
                                numberSpan,
                                numberStyles,
                                culture,
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
#endif
