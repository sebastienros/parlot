using Parlot.Compilation;
using System;
using System.Globalization;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class DecimalLiteral : Parser<decimal>, ICompilable
    {
        private readonly NumberOptions _numberOptions;
        private readonly bool _skipWhiteSpace;

        public DecimalLiteral(NumberOptions numberOptions = NumberOptions.Default, bool skipWhiteSpace = true)
        {
            _numberOptions = numberOptions;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(ParseContext context, ref ParseResult<decimal> result)
        {
            context.EnterParser(this);

            if (_skipWhiteSpace)
            {
                context.SkipWhiteSpace();
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
                var sourceToParse = context.Scanner.Buffer.Substring(start, end -start);
#else
                var sourceToParse = context.Scanner.Buffer.AsSpan(start, end - start);
#endif

                if (decimal.TryParse(sourceToParse, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
                { 
                    result.Set(start, end,  value);
                    return true;
                }
            }
         
            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(decimal)));

            // if (_skipWhiteSpace)
            // {
            //     context.SkipWhiteSpace();
            // }

            if (_skipWhiteSpace)
            {
                result.Body.Add(context.ParserSkipWhiteSpace());
            }

            // var start = context.Scanner.Cursor.Offset;

            var start = Expression.Variable(typeof(int), $"start{context.Counter}");
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

            var end = Expression.Variable(typeof(int), $"end{context.Counter}");
#if NETSTANDARD2_0
            var sourceToParse = Expression.Variable(typeof(string), $"sourceToParse{context.Counter}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(context.Buffer(), typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(decimal).MakeByRefType()});
#else
            var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{++context.Counter}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), context.Buffer(), start, Expression.Subtract(end, start)));
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

            return result;
        }
    }
}
