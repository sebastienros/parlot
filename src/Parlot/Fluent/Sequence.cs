using System;

namespace Parlot.Fluent
{
    public sealed class Sequence<T1, T2, TParseContext> : Parser<ValueTuple<T1, T2>, TParseContext>
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
    }

    public sealed class Sequence<T1, T2, T3, TParseContext> : Parser<ValueTuple<T1, T2, T3>, TParseContext>
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
    }

    public sealed class Sequence<T1, T2, T3, T4, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4>, TParseContext>
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
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5>, TParseContext>
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
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>, TParseContext>
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
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7, TParseContext> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>, TParseContext>
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
    }
}
