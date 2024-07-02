#if NET8_0_OR_GREATER
using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;
using System.Numerics;
using System.Reflection;

namespace Parlot.Fluent
{
    public sealed class NumberLiteral<T> : Parser<T>, ICompilable
        where T : INumber<T>
    {
        private const char DefaultDecimalSeparator = '.';
        private const char DefaultGroupSeparator = ',';

        private readonly char _decimalSeparator;
        private readonly char _groupSeparator;
        private readonly NumberStyles _numberStyles;
        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;
        private readonly bool _allowLeadingSign;
        private readonly bool _allowDecimalSeparator;
        private readonly bool _allowGroupSeparator;
        private readonly bool _allowExponent;

        private static readonly MethodInfo _tryParseMethodInfo = typeof(T).GetMethod(nameof(INumber<T>.TryParse), [typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(T).MakeByRefType()])!;

        public NumberLiteral(NumberOptions numberOptions = NumberOptions.Number, char decimalSeparator = DefaultDecimalSeparator, char groupSeparator = DefaultGroupSeparator)
        {
            _decimalSeparator = decimalSeparator;
            _groupSeparator = groupSeparator;
            _numberStyles = numberOptions.ToNumberStyles();
            
            if (decimalSeparator != NumberLiterals.DefaultDecimalSeparator ||
                groupSeparator != NumberLiterals.DefaultGroupSeparator)
            {
                _culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
                _culture.NumberFormat.NumberDecimalSeparator = decimalSeparator.ToString();
                _culture.NumberFormat.NumberGroupSeparator = groupSeparator.ToString();
            }

            _allowLeadingSign = (numberOptions & NumberOptions.AllowLeadingSign) != 0;
            _allowDecimalSeparator = (numberOptions & NumberOptions.AllowDecimalSeparator) != 0;
            _allowGroupSeparator = (numberOptions & NumberOptions.AllowGroupSeparators) != 0;
            _allowExponent = (numberOptions & NumberOptions.AllowExponent) != 0;
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var reset = context.Scanner.Cursor.Position;
            var start = reset.Offset;

            if (context.Scanner.ReadDecimal(_allowLeadingSign, _allowDecimalSeparator, _allowGroupSeparator, _allowExponent, out var number, _decimalSeparator, _groupSeparator))
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
            var result = context.CreateCompilationResult<T>();

            // var start = context.Scanner.Cursor.Offset;
            // var reset = context.Scanner.Cursor.Position;

            var start = context.DeclareOffsetVariable(result);
            var reset = context.DeclarePositionVariable(result);

            var numberStyles = result.DeclareVariable<NumberStyles>($"numberStyles{context.NextNumber}", Expression.Constant(_numberStyles));
            var culture = result.DeclareVariable<CultureInfo>($"culture{context.NextNumber}", Expression.Constant(_culture));
            var numberSpan = result.DeclareVariable($"number{context.NextNumber}", typeof(ReadOnlySpan<char>));
            var end = result.DeclareVariable<int>($"end{context.NextNumber}");

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
                    context.ReadDecimal(
                        Expression.Constant(_allowLeadingSign),
                        Expression.Constant(_allowDecimalSeparator),
                        Expression.Constant(_allowGroupSeparator),
                        Expression.Constant(_allowExponent), 
                        numberSpan, Expression.Constant(_decimalSeparator), Expression.Constant(_groupSeparator)),
                    Expression.Block(
                        Expression.Assign(end, context.Offset()),
                        Expression.Assign(result.Success,
                            Expression.Call(
                                _tryParseMethodInfo,
                                numberSpan,
                                numberStyles,
                                culture,
                                result.Value)
                            )
                    )
                );

            result.Body.Add(block);

            result.Body.Add(
                Expression.IfThen(
                    Expression.Not(result.Success),
                    context.ResetPosition(reset)
                    )
                );

            return result;
        }
    }
}
#endif
