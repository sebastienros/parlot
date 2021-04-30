using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public interface ISequenceParser<TParseContext>
        where TParseContext : ParseContext
    {
        CompilationResult[] BuildParsers(CompilationContext<TParseContext> context);
    }

    internal static class SequenceCompileHelper
    {
        internal static string SequenceRequired = $"The parser needs to implement {nameof(ISequenceParser<ParseContext>)}";

        public static CompilationResult CreateSequenceCompileResult<TParseContext>(CompilationResult[] parserCompileResults, CompilationContext<TParseContext> context)
        where TParseContext : ParseContext
        {
            var result = new CompilationResult();

            var parserTypes = parserCompileResults.Select(x => x.Value.Type).ToArray();
            var resultType = GetValueTuple(parserCompileResults.Length).MakeGenericType(parserTypes);

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.New(resultType));


            // var start = context.Scanner.Cursor.Position;

            var start = context.DeclarePositionVariable(result);

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

            static Type GetValueTuple(int length)
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
                            Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                            context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, Expression.New(valueTupleConstructor, parserCompileResults.Select(x => x.Value).ToArray()))
                            );

            for (var i = parserCompileResults.Length - 1; i >= 0; i--)
            {
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

            result.Body.Add(block);

            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(start);
            // }

            result.Body.Add(Expression.IfThen(
                Expression.Not(success),
                context.ResetPosition(start)
                ));

            return result;
        }
    }

    public sealed class Sequence<T1, T2,TParseContext> : Parser<ValueTuple<T1, T2>,TParseContext>, ICompilable<TParseContext>, ISequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        internal readonly IParser<T1, TParseContext> _parser1;
        internal readonly IParser<T2, TParseContext> _parser2;
        public Sequence(IParser<T1, TParseContext> parser1, IParser<T2, TParseContext> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
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

        public CompilationResult[] BuildParsers(CompilationContext<TParseContext> context)
        {
            return new[]
                {
                    _parser1.Build(context),
                    _parser2.Build(context)
                };
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, TParseContext> : Parser<ValueTuple<T1, T2, T3>, TParseContext>, ICompilable<TParseContext>, ISequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<ValueTuple<T1, T2>, TParseContext> _parser;
        internal readonly IParser<T3, TParseContext> _lastParser;

        public Sequence(IParser<ValueTuple<T1, T2>, TParseContext>
            parser,
            IParser<T3, TParseContext> lastParser
            )
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3>> result)
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

        public CompilationResult[] BuildParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildParsers(context).Append(_lastParser.Build(context)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4>, TParseContext>, ICompilable<TParseContext>, ISequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<ValueTuple<T1, T2, T3>, TParseContext> _parser;
        internal readonly IParser<T4, TParseContext> _lastParser;

        public Sequence(IParser<ValueTuple<T1, T2, T3>, TParseContext> parser, IParser<T4, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
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

        public CompilationResult[] BuildParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildParsers(context).Append(_lastParser.Build(context)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext>, ICompilable<TParseContext>, ISequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<ValueTuple<T1, T2, T3, T4>, TParseContext> _parser;
        internal readonly IParser<T5, TParseContext> _lastParser;

        public Sequence(IParser<ValueTuple<T1, T2, T3, T4>, TParseContext> parser, IParser<T5, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
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

        public CompilationResult[] BuildParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildParsers(context).Append(_lastParser.Build(context)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext>, ICompilable<TParseContext>, ISequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> _parser;
        internal readonly IParser<T6, TParseContext> _lastParser;

        public Sequence(IParser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> parser, IParser<T6, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
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

        public CompilationResult[] BuildParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildParsers(context).Append(_lastParser.Build(context)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext>, ICompilable<TParseContext>, ISequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly IParser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> _parser;
        internal readonly IParser<T7, TParseContext> _lastParser;

        public Sequence(IParser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> parser, IParser<T7, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
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

        public CompilationResult[] BuildParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildParsers(context).Append(_lastParser.Build(context)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildParsers(context), context);
        }
    }
}
