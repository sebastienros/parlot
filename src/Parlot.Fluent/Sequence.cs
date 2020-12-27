using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public class Sequence<T1, T2> : Parser<ValueTuple<T1, T2>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly bool _skipWhitespace;

        public Sequence(IParser<T1> parser1, IParser<T2> parser2, bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ValueTuple<T1, T2>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (parser2.Parse(scanner, out var parseResult2))
                {
                    result = new ParseResult<ValueTuple<T1, T2>>(parseResult1.Buffer, parseResult1.Start, parseResult2.End, new ValueTuple<T1, T2>(parseResult1.GetValue(), parseResult2.GetValue()));
                    return true;
                }
            }

            result = ParseResult<ValueTuple<T1, T2>>.Empty;
            return false;
        }
    }

    public class Sequence<T1, T2, T3> : Parser<ValueTuple<T1, T2, T3>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly IParser<T3> parser3;
        private readonly bool _skipWhitespace;

        public Sequence(
            IParser<T1> parser1, 
            IParser<T2> parser2, 
            IParser<T3> parser3, 
            bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            this.parser3 = parser3;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ValueTuple<T1, T2, T3>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (parser2.Parse(scanner, out var parseResult2))
                {
                    if (parser3.Parse(scanner, out var parseResult3))
                    {
                        var tuple = new ValueTuple<T1, T2, T3>(
                            parseResult1.GetValue(), 
                            parseResult2.GetValue(), 
                            parseResult3.GetValue()
                            )
                            ;
                        result = new ParseResult<ValueTuple<T1, T2, T3>>(parseResult1.Buffer, parseResult1.Start, parseResult3.End, tuple);
                        return true;
                    }
                }
            }

            result = ParseResult<ValueTuple<T1, T2, T3>>.Empty;
            return false;
        }
    }

    public class Sequence<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T3, T4>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly IParser<T3> parser3;
        private readonly IParser<T4> parser4;
        private readonly bool _skipWhitespace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            this.parser3 = parser3;
            this.parser4 = parser4;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ValueTuple<T1, T2, T3, T4>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (parser2.Parse(scanner, out var parseResult2))
                {
                    if (parser3.Parse(scanner, out var parseResult3))
                    {
                        if (parser4.Parse(scanner, out var parseResult4))
                        {
                            var tuple = new ValueTuple<T1, T2, T3, T4>(
                                parseResult1.GetValue(),
                                parseResult2.GetValue(),
                                parseResult3.GetValue(),
                                parseResult4.GetValue()
                                );

                            result = new ParseResult<ValueTuple<T1, T2, T3, T4>>(parseResult1.Buffer, parseResult1.Start, parseResult4.End, tuple);
                            return true;
                        }
                    }
                }
            }

            result = ParseResult<ValueTuple<T1, T2, T3, T4>>.Empty;
            return false;
        }
    }

    public class Sequence<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T4, T5>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly IParser<T3> parser3;
        private readonly IParser<T4> parser4;
        private readonly IParser<T5> parser5;
        private readonly bool _skipWhitespace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            IParser<T5> parser5,
            bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            this.parser3 = parser3;
            this.parser4 = parser4;
            this.parser5 = parser5;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (parser2.Parse(scanner, out var parseResult2))
                {
                    if (parser3.Parse(scanner, out var parseResult3))
                    {
                        if (parser4.Parse(scanner, out var parseResult4))
                        {
                            if (parser5.Parse(scanner, out var parseResult5))
                            {
                                var tuple = new ValueTuple<T1, T2, T3, T4, T5>(
                                    parseResult1.GetValue(),
                                    parseResult2.GetValue(),
                                    parseResult3.GetValue(),
                                    parseResult4.GetValue(),
                                    parseResult5.GetValue()
                                    );

                                result = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>(parseResult1.Buffer, parseResult1.Start, parseResult5.End, tuple);
                                return true;
                            }
                        }
                    }
                }
            }

            result = ParseResult<ValueTuple<T1, T2, T3, T4, T5>>.Empty;
            return false;
        }
    }

    public class Sequence<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly IParser<T3> parser3;
        private readonly IParser<T4> parser4;
        private readonly IParser<T5> parser5;
        private readonly IParser<T6> parser6;
        private readonly bool _skipWhitespace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            IParser<T5> parser5,
            IParser<T6> parser6,
            bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            this.parser3 = parser3;
            this.parser4 = parser4;
            this.parser5 = parser5;
            this.parser6 = parser6;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (parser2.Parse(scanner, out var parseResult2))
                {
                    if (parser3.Parse(scanner, out var parseResult3))
                    {
                        if (parser4.Parse(scanner, out var parseResult4))
                        {
                            if (parser5.Parse(scanner, out var parseResult5))
                            {
                                if (parser6.Parse(scanner, out var parseResult6))
                                {
                                    var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6>(
                                        parseResult1.GetValue(),
                                        parseResult2.GetValue(),
                                        parseResult3.GetValue(),
                                        parseResult4.GetValue(),
                                        parseResult5.GetValue(),
                                        parseResult6.GetValue()
                                        );

                                    result = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>(parseResult1.Buffer, parseResult1.Start, parseResult6.End, tuple);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            result = ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>.Empty;
            return false;
        }
    }

    public class Sequence<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly IParser<T3> parser3;
        private readonly IParser<T4> parser4;
        private readonly IParser<T5> parser5;
        private readonly IParser<T6> parser6;
        private readonly IParser<T7> parser7;
        private readonly bool _skipWhitespace;

        public Sequence(
            IParser<T1> parser1,
            IParser<T2> parser2,
            IParser<T3> parser3,
            IParser<T4> parser4,
            IParser<T5> parser5,
            IParser<T6> parser6,
            IParser<T7> parser7,
            bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            this.parser3 = parser3;
            this.parser4 = parser4;
            this.parser5 = parser5;
            this.parser6 = parser6;
            this.parser7 = parser7;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (parser2.Parse(scanner, out var parseResult2))
                {
                    if (parser3.Parse(scanner, out var parseResult3))
                    {
                        if (parser4.Parse(scanner, out var parseResult4))
                        {
                            if (parser5.Parse(scanner, out var parseResult5))
                            {
                                if (parser6.Parse(scanner, out var parseResult6))
                                {
                                    if (parser7.Parse(scanner, out var parseResult7))
                                    {
                                        var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6, T7>(
                                            parseResult1.GetValue(),
                                            parseResult2.GetValue(),
                                            parseResult3.GetValue(),
                                            parseResult4.GetValue(),
                                            parseResult5.GetValue(),
                                            parseResult6.GetValue(),
                                            parseResult7.GetValue()
                                            );

                                        result = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>(parseResult1.Buffer, parseResult1.Start, parseResult7.End, tuple);
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            result = ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>.Empty;
            return false;
        }
    }

    public class Sequence : Parser<IList<ParseResult<object>>>
    {
        private readonly IParser[] _parsers;
        private readonly bool _skipWhitespace;

        public Sequence(IParser[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<IList<ParseResult<object>>> result)
        {
            if (_parsers.Length == 0)
            {
                result = ParseResult<IList<ParseResult<object>>>.Empty;
                return true;
            }

            var results = new List<ParseResult<object>>(_parsers.Length);

            var success = true;

            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (!_parsers[i].Parse(scanner, out var parsed))
                {
                    success = false;
                    break;
                }

                results[i] = parsed;
            }

            if (success)
            {
                result = new ParseResult<IList<ParseResult<object>>>(results[0].Buffer, results[0].Start, results[^1].End, results);
                return true;
            }
            else
            {
                result = ParseResult<IList<ParseResult<object>>>.Empty;
                return false;
            }
        }
    }
}
