using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    internal static class SequenceCompileHelper
    {
        public static CompileResult CreateSequenceCompileResult(ICompilable[] parsers, Expression parseContext)
        {
            var parserCompileResults = parsers.Select(x => x.Compile(parseContext)).ToArray();
            var parserTypes = parserCompileResults.Select(x => x.Value.Type).ToArray();
            var resultType = GetValueTuple(parsers.Length).MakeGenericType(parserTypes);

            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), "AndSuccess");
            var value = Expression.Variable(resultType, "AndValue");

            variables.Add(success);
            variables.Add(value);

            // var start = context.Scanner.Cursor.Position;

            var start = Expression.Variable(typeof(TextPosition), "AndStart");
            variables.Add(start);

            body.Add(Expression.Assign(start, Expression.Property(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), "Position")));

            // parse1 instructions
            // 
            // if (parser1Success)
            // {
            //
            //   parse2 instructions
            //   
            //   if (parser2Success)
            //   {
            //      success = true;
            //      value = new ValueTuple<T1, T2>(parser1.Value, parse2.Value)
            //   }
            // }
            // 

            Type GetValueTuple(int length)
            {
                return length switch
                {
                    2 => typeof(ValueTuple<,>),
                    3 => typeof(ValueTuple<,,>),
                    4 => typeof(ValueTuple<,,,>),
                    5 => typeof(ValueTuple<,,,,>),
                    6 => typeof(ValueTuple<,,,,,>),
                    7 => typeof(ValueTuple<,,,,,,>),
                    8 => typeof(ValueTuple<,,,,,,,>),
                    _ => null
                };
            }

            var valueTupleConstructor = resultType.GetConstructor(parserTypes);

            // Initialize the block variable with the inner else statement
            var block = Expression.Block(
                            Expression.Assign(success, Expression.Constant(false, typeof(bool))),
                            Expression.Assign(value, Expression.New(valueTupleConstructor, parserCompileResults.Select(x => x.Value).ToArray()))
                            );

            for (var i = parsers.Length - 1; i >= 0; i--)
            {
                var parser = parsers[i];
                var parserCompileResult = parserCompileResults[i];

                block = Expression.Block(
                    parserCompileResult.Variables,
                    parserCompileResult.Body
                    .Append(
                        Expression.IfThen(
                            parserCompileResult.Success,
                            block
                        ))
                    );

            }

            body.Add(block);

            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(start);
            // }

            body.Add(Expression.IfThen(
                Expression.Not(success),
                Expression.Call(Expression.Field(Expression.Field(parseContext, "Scanner"), "Cursor"), typeof(Cursor).GetMethod("ResetPosition"), start)
                ));

            return new CompileResult(variables, body, success, value);
        }
    }

    public sealed class Sequence<T1, T2> : Parser<ValueTuple<T1, T2>>
    {
        internal readonly Parser<T1> _parser1;
        internal readonly Parser<T2> _parser2;
        public Sequence(Parser<T1> parser1, Parser<T2> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public ICompilable[] Parsers => new ICompilable[] { _parser1, _parser2 };

        public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
        {
            context.EnterParser(this);

            var parseResult1 = new ParseResult<T1>();

            var start = context.Scanner.Cursor.Position;

            if (_parser1.Parse(context, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(context, ref parseResult2))
                {
                    result.Set(parseResult1.Start, parseResult2.End, new ValueTuple<T1, T2>(parseResult1.Value, parseResult2.Value));
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(Parsers, parseContext);
        }
    }

    public sealed class Sequence<T1, T2, T3> : Parser<ValueTuple<T1, T2, T3>>
    {
        private readonly Parser<ValueTuple<T1, T2>> _parser;
        internal readonly Parser<T3> _lastParser;

        public Sequence(Parser<ValueTuple<T1, T2>> 
            parser,
            Parser<T3> lastParser
            )
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public ICompilable[] Parsers => ((Sequence<T1, T2>)_parser).Parsers.Append(_lastParser).ToArray();

        public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T3>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        lastResult.Value
                        );
                        
                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(Parsers, parseContext);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T3, T4>>
    {
        private readonly Parser<ValueTuple<T1, T2, T3>> _parser;
        internal readonly Parser<T4> _lastParser;

        public Sequence(Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public ICompilable[] Parsers => ((Sequence<T1, T2, T3>)_parser).Parsers.Append(_lastParser).ToArray();

        public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T4>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        lastResult.Value
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(Parsers, parseContext);
        }
    }
    
    public sealed class Sequence<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T4, T5>>
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4>> _parser;
        internal readonly Parser<T5> _lastParser;
        
        public Sequence(Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public ICompilable[] Parsers => ((Sequence<T1, T2, T3, T4>)_parser).Parsers.Append(_lastParser).ToArray();

        public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T5>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4, T5>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4,
                        lastResult.Value
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(Parsers, parseContext);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>>
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>> _parser;
        internal readonly Parser<T6> _lastParser;        

        public Sequence(Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public ICompilable[] Parsers => ((Sequence<T1, T2, T3, T4, T5>)_parser).Parsers.Append(_lastParser).ToArray();

        public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T6>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4,
                        tupleResult.Value.Item5,
                        lastResult.Value
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }

            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(Parsers, parseContext);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> _parser;
        internal readonly Parser<T7> _lastParser;

        public Sequence(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public ICompilable[] Parsers => ((Sequence<T1, T2, T3, T4, T5, T6>)_parser).Parsers.Append(_lastParser).ToArray();

        public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T7>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6, T7>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4,
                        tupleResult.Value.Item5,
                        tupleResult.Value.Item6,
                        lastResult.Value
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }

            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public override CompileResult Compile(Expression parseContext)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(Parsers, parseContext);
        }
    }
}
