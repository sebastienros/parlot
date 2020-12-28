using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public sealed class Sequence<T1, T2> : Parser<ValueTuple<T1, T2>>
    {
        internal readonly IParser<T1> _parser1;
        internal readonly IParser<T2> _parser2;
        private readonly bool _skipWhiteSpace;

        public Sequence(IParser<T1> parser1, IParser<T2> parser2, bool skipWhiteSpace = true)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<ValueTuple<T1, T2>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (_parser1.Parse(scanner, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(scanner, ref parseResult2))
                {
                    result.Set(parseResult1.Buffer, parseResult1.Start, parseResult2.End, new ValueTuple<T1, T2>(parseResult1.Value, parseResult2.Value));
                    return true;
                }
            }

            return false;
        }
    }

    public sealed class Sequence<T1, T2, T3> : Parser<ValueTuple<T1, T2, T3>>
    {
        internal readonly IParser<T1> _parser1;
        internal readonly IParser<T2> _parser2;
        internal readonly IParser<T3> _parser3;
        private readonly bool _skipWhiteSpace;

        public Sequence(
            IParser<T1> parser1, 
            IParser<T2> parser2, 
            IParser<T3> parser3, 
            bool skipWhiteSpace = true)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
            _parser3 = parser3 ?? throw new ArgumentNullException(nameof(parser3));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<ValueTuple<T1, T2, T3>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (_parser1.Parse(scanner, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(scanner, ref parseResult2))
                {
                    var parseResult3 = new ParseResult<T3>();

                    if (_parser3.Parse(scanner, ref parseResult3))
                    {
                        var tuple = new ValueTuple<T1, T2, T3>(
                            parseResult1.Value, 
                            parseResult2.Value, 
                            parseResult3.Value
                            )
                            ;
                        
                        result.Set(parseResult1.Buffer, parseResult1.Start, parseResult3.End, tuple);
                        return true;
                    }
                }
            }

            return false;
        }
    }

    public sealed class Sequence<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T3, T4>>
    {
        internal readonly IParser<T1> _parser1;
        internal readonly IParser<T2> _parser2;
        internal readonly IParser<T3> _parser3;
        internal readonly IParser<T4> _parser4;
        private readonly bool _skipWhiteSpace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            bool skipWhiteSpace = true)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
            _parser3 = parser3 ?? throw new ArgumentNullException(nameof(parser3));
            _parser4 = parser4 ?? throw new ArgumentNullException(nameof(parser4));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (_parser1.Parse(scanner, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(scanner, ref parseResult2))
                {
                    var parseResult3 = new ParseResult<T3>();

                    if (_parser3.Parse(scanner, ref parseResult3))
                    {
                        var parseResult4 = new ParseResult<T4>();

                        if (_parser4.Parse(scanner, ref parseResult4))
                        {
                            var tuple = new ValueTuple<T1, T2, T3, T4>(
                                parseResult1.Value,
                                parseResult2.Value,
                                parseResult3.Value,
                                parseResult4.Value
                                );

                            result.Set(parseResult1.Buffer, parseResult1.Start, parseResult4.End, tuple);
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T4, T5>>
    {
        internal readonly IParser<T1> _parser1;
        internal readonly IParser<T2> _parser2;
        internal readonly IParser<T3> _parser3;
        internal readonly IParser<T4> _parser4;
        internal readonly IParser<T5> _parser5;
        private readonly bool _skipWhiteSpace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            IParser<T5> parser5,
            bool skipWhiteSpace = true)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
            _parser3 = parser3 ?? throw new ArgumentNullException(nameof(parser3));
            _parser4 = parser4 ?? throw new ArgumentNullException(nameof(parser4));
            _parser5 = parser5 ?? throw new ArgumentNullException(nameof(parser5));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (_parser1.Parse(scanner, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(scanner, ref parseResult2))
                {
                    var parseResult3 = new ParseResult<T3>();

                    if (_parser3.Parse(scanner, ref parseResult3))
                    {
                        var parseResult4 = new ParseResult<T4>();

                        if (_parser4.Parse(scanner, ref parseResult4))
                        {
                            var parseResult5 = new ParseResult<T5>();

                            if (_parser5.Parse(scanner, ref parseResult5))
                            {
                                var tuple = new ValueTuple<T1, T2, T3, T4, T5>(
                                    parseResult1.Value,
                                    parseResult2.Value,
                                    parseResult3.Value,
                                    parseResult4.Value,
                                    parseResult5.Value
                                    );

                                result.Set(parseResult1.Buffer, parseResult1.Start, parseResult5.End, tuple);
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>>
    {
        internal readonly IParser<T1> _parser1;
        internal readonly IParser<T2> _parser2;
        internal readonly IParser<T3> _parser3;
        internal readonly IParser<T4> _parser4;
        internal readonly IParser<T5> _parser5;
        internal readonly IParser<T6> _parser6;
        private readonly bool _skipWhiteSpace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            IParser<T5> parser5,
            IParser<T6> parser6,
            bool skipWhiteSpace = true)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
            _parser3 = parser3 ?? throw new ArgumentNullException(nameof(parser3));
            _parser4 = parser4 ?? throw new ArgumentNullException(nameof(parser4));
            _parser5 = parser5 ?? throw new ArgumentNullException(nameof(parser5));
            _parser6 = parser6 ?? throw new ArgumentNullException(nameof(parser6));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (_parser1.Parse(scanner, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(scanner, ref parseResult2))
                {
                    var parseResult3 = new ParseResult<T3>();

                    if (_parser3.Parse(scanner, ref parseResult3))
                    {
                        var parseResult4 = new ParseResult<T4>();

                        if (_parser4.Parse(scanner, ref parseResult4))
                        {
                            var parseResult5 = new ParseResult<T5>();

                            if (_parser5.Parse(scanner, ref parseResult5))
                            {
                                var parseResult6 = new ParseResult<T6>();

                                if (_parser6.Parse(scanner, ref parseResult6))
                                {
                                    var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6>(
                                        parseResult1.Value,
                                        parseResult2.Value,
                                        parseResult3.Value,
                                        parseResult4.Value,
                                        parseResult5.Value,
                                        parseResult6.Value
                                        );

                                    result.Set(parseResult1.Buffer, parseResult1.Start, parseResult6.End, tuple);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            
            return false;
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        internal readonly IParser<T1> _parser1;
        internal readonly IParser<T2> _parser2;
        internal readonly IParser<T3> _parser3;
        internal readonly IParser<T4> _parser4;
        internal readonly IParser<T5> _parser5;
        internal readonly IParser<T6> _parser6;
        internal readonly IParser<T7> _parser7;
        private readonly bool _skipWhiteSpace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            IParser<T5> parser5,
            IParser<T6> parser6,
            IParser<T7> parser7,
            bool skipWhiteSpace = true)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
            _parser3 = parser3 ?? throw new ArgumentNullException(nameof(parser3));
            _parser4 = parser4 ?? throw new ArgumentNullException(nameof(parser4));
            _parser5 = parser5 ?? throw new ArgumentNullException(nameof(parser5));
            _parser6 = parser6 ?? throw new ArgumentNullException(nameof(parser6));
            _parser7 = parser7 ?? throw new ArgumentNullException(nameof(parser7));
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
        {
            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (_parser1.Parse(scanner, ref parseResult1))
            {
                var parseResult2 = new ParseResult<T2>();

                if (_parser2.Parse(scanner, ref parseResult2))
                {
                    var parseResult3 = new ParseResult<T3>();

                    if (_parser3.Parse(scanner, ref parseResult3))
                    {
                        var parseResult4 = new ParseResult<T4>();

                        if (_parser4.Parse(scanner, ref parseResult4))
                        {
                            var parseResult5 = new ParseResult<T5>();

                            if (_parser5.Parse(scanner, ref parseResult5))
                            {
                                var parseResult6 = new ParseResult<T6>();

                                if (_parser6.Parse(scanner, ref parseResult6))
                                {
                                    var parseResult7 = new ParseResult<T7>();

                                    if (_parser7.Parse(scanner, ref parseResult7))
                                    {
                                        var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6, T7>(
                                            parseResult1.Value,
                                            parseResult2.Value,
                                            parseResult3.Value,
                                            parseResult4.Value,
                                            parseResult5.Value,
                                            parseResult6.Value,
                                            parseResult7.Value
                                            );

                                        result.Set(parseResult1.Buffer, parseResult1.Start, parseResult7.End, tuple);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return false;
        }
    }

    public sealed class Sequence : Parser<IList<ParseResult<object>>>
    {
        internal readonly IParser[] _parsers;
        private readonly bool _skipWhiteSpace;

        public Sequence(IParser[] parsers, bool skipWhiteSpace = true)
        {
            _parsers = parsers;
            _skipWhiteSpace = skipWhiteSpace;
        }

        public override bool Parse(Scanner scanner, ref ParseResult<IList<ParseResult<object>>> result)
        {
            if (_parsers.Length == 0)
            {
                return true;
            }

            var results = new List<ParseResult<object>>(_parsers.Length);

            var success = true;

            if (_skipWhiteSpace)
            {
                scanner.SkipWhiteSpace();
            }

            var parsed = new ParseResult<object>();

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (!_parsers[i].Parse(scanner, ref parsed))
                {
                    success = false;
                    break;
                }

                results[i] = parsed;
            }

            if (success)
            {
                result.Set(results[0].Buffer, results[0].Start, results[^1].End, results);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
