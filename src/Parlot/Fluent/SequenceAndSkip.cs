using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class SequenceAndSkip<T1, T2, TParseContext> : Parser<T1, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        internal readonly Parser<T1, TParseContext> _parser1;
        internal readonly Parser<T2, TParseContext> _parser2;

        public SequenceAndSkip(Parser<T1, TParseContext> parser1, Parser<T2, TParseContext> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public override bool Parse(TParseContext context, ref ParseResult<T1> result)
        {
            context.EnterParser(this);

            var parseResult1 = new ParseResult<T1>();

            var start = context.Scanner.Cursor.Position;

            if (_parser1.Parse(context, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(context, ref parseResult2))
                {
                    result.Set(parseResult1.Start, parseResult2.End, parseResult1.Value);
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(start);
            }

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            return new[]
                {
                    new SkippableCompilationResult(_parser1.Build(context), false),
                    new SkippableCompilationResult(_parser2.Build(context), true)
                };
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            // The common skippable sequence compilation helper can't be reused since this doesn't return a tuple

            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T1)));

            // T value;
            //
            // parse1 instructions
            // 
            // var start = context.Scanner.Cursor.Position;
            //
            // parse1 instructions
            //
            // if (parser1.Success)
            // {
            //    
            //    parse2 instructions
            //   
            //    if (parser2.Success)
            //    {
            //       success = true;
            //       value = parse1.Value;
            //    }
            //    else
            //    {
            //        context.Scanner.Cursor.ResetPosition(start);
            //    }
            // }

            // var start = context.Scanner.Cursor.Position;

            var start = context.DeclarePositionVariable(result);

            var parser1CompileResult = _parser1.Build(context);
            var parser2CompileResult = _parser2.Build(context);

            result.Body.Add(
                Expression.Block(
                    parser1CompileResult.Variables,
                    Expression.Block(parser1CompileResult.Body),
                    Expression.IfThen(
                        parser1CompileResult.Success,
                            Expression.Block(
                            parser2CompileResult.Variables,
                            Expression.Block(parser2CompileResult.Body),
                            Expression.IfThenElse(
                                parser2CompileResult.Success,
                                Expression.Block(
                                    context.DiscardResult ? Expression.Empty() : Expression.Assign(value, parser1CompileResult.Value),
                                    Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                                ),
                                context.ResetPosition(start)
                                )
                            )
                        )
                    )
            );

            return result;
        }
    }

    public sealed class SequenceAndSkip<T1, T2, T3, TParseContext> : Parser<ValueTuple<T1, T2>, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<ValueTuple<T1, T2>, TParseContext> _parser;
        internal readonly Parser<T3, TParseContext> _lastParser;

        public SequenceAndSkip(Parser<ValueTuple<T1, T2>, TParseContext> parser, Parser<T3, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T3>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISkippableSequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), true)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class SequenceAndSkip<T1, T2, T3, T4, TParseContext> : Parser<ValueTuple<T1, T2, T3>, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<ValueTuple<T1, T2, T3>, TParseContext> _parser;
        internal readonly Parser<T4, TParseContext> _lastParser;

        public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3>, TParseContext> parser, Parser<T4, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T4>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISkippableSequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), true)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4>, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4>, TParseContext> _parser;
        internal readonly Parser<T5, TParseContext> _lastParser;

        public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4>, TParseContext> parser, Parser<T5, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T5>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }
            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISkippableSequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), true)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, T6, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> _parser;
        internal readonly Parser<T6, TParseContext> _lastParser;

        public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext> parser, Parser<T6, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T6>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4, T5>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4,
                        tupleResult.Value.Item5
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }

            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISkippableSequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), true)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> _parser;
        internal readonly Parser<T7, TParseContext> _lastParser;

        public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext> parser, Parser<T7, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T7>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4,
                        tupleResult.Value.Item5,
                        tupleResult.Value.Item6
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }

            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISkippableSequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), true)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class SequenceAndSkip<T1, T2, T3, T4, T5, T6, T7, T8, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext>, ICompilable<TParseContext>, ISkippableSequenceParser<TParseContext>
    where TParseContext : ParseContext
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext> _parser;
        internal readonly Parser<T8, TParseContext> _lastParser;

        public SequenceAndSkip(Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext> parser, Parser<T8, TParseContext> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public override bool Parse(TParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
        {
            context.EnterParser(this);

            var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>();

            var start = context.Scanner.Cursor.Position;

            if (_parser.Parse(context, ref tupleResult))
            {
                var lastResult = new ParseResult<T8>();

                if (_lastParser.Parse(context, ref lastResult))
                {
                    var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6, T7>(
                        tupleResult.Value.Item1,
                        tupleResult.Value.Item2,
                        tupleResult.Value.Item3,
                        tupleResult.Value.Item4,
                        tupleResult.Value.Item5,
                        tupleResult.Value.Item6,
                        tupleResult.Value.Item7
                        );

                    result.Set(tupleResult.Start, lastResult.End, tuple);
                    return true;
                }

            }

            context.Scanner.Cursor.ResetPosition(start);

            return false;
        }

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext<TParseContext> context)
        {
            if (_parser is not ISkippableSequenceParser<TParseContext> sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), true)).ToArray();
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }
}
