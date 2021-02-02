using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class DecimalLiteral : Parser<decimal>
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

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(decimal), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));
            body.Add(Expression.Assign(value, Expression.Constant(default(decimal), typeof(decimal))));

            // if (_skipWhiteSpace)
            // {
            //     context.SkipWhiteSpace();
            // }

            if (_skipWhiteSpace)
            {
                var skipWhiteSpaceMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.SkipWhiteSpace), Array.Empty<Type>());
                body.Add(Expression.Call(context.ParseContext, ExpressionHelper.ParserContext_SkipWhiteSpaceMethod));
            }

            // var start = context.Scanner.Cursor.Offset;

            var start = Expression.Variable(typeof(int), $"start{context.Counter}");
            variables.Add(start);
            
            body.Add(Expression.Assign(start, ExpressionHelper.Offset(context.ParseContext)));

            if ((_numberOptions & NumberOptions.AllowSign) == NumberOptions.AllowSign)
            {
                // if (!context.Scanner.ReadChar('-'))
                // {
                //     context.Scanner.ReadChar('+');
                // }

                body.Add(
                    Expression.IfThen(
                        Expression.Not(ExpressionHelper.ReadChar(context.ParseContext, '-')),
                        ExpressionHelper.ReadChar(context.ParseContext, '+')
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
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(ExpressionHelper.Buffer(context.ParseContext), typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(string), typeof(NumberStyles), typeof(IFormatProvider), typeof(decimal).MakeByRefType()});
#else
            var sourceToParse = Expression.Variable(typeof(ReadOnlySpan<char>), $"sourceToParse{++context.Counter}");
            var sliceExpression = Expression.Assign(sourceToParse, Expression.Call(typeof(MemoryExtensions).GetMethod("AsSpan", new[] { typeof(string), typeof(int), typeof(int) }), ExpressionHelper.Buffer(context.ParseContext), start, Expression.Subtract(end, start)));
            var tryParseMethodInfo = typeof(decimal).GetMethod(nameof(decimal.TryParse), new[] { typeof(ReadOnlySpan<char>), typeof(NumberStyles), typeof(IFormatProvider), typeof(decimal).MakeByRefType()});
#endif

            // TODO: NETSTANDARD2_1 code path
            var block = 
                Expression.IfThen(
                    ExpressionHelper.ReadDecimal(context.ParseContext),
                    Expression.Block(
                        new[] { end, sourceToParse },
                        Expression.Assign(end, ExpressionHelper.Offset(context.ParseContext)),
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

            body.Add(block);

            return new CompileResult(variables, body, success, value);
        }
    }
}
