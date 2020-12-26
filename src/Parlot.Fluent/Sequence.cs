using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
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

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

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

    public class Sequence<T1, T2> : Parser<Tuple<T1, T2>>
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

        public override bool Parse(Scanner scanner, out ParseResult<Tuple<T1, T2>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (parser2.Parse(scanner, out var parseResult2))
                {
                    result = new ParseResult<Tuple<T1, T2>>(parseResult1.Buffer, parseResult1.Start, parseResult2.End, new Tuple<T1, T2>(parseResult1.GetValue(), parseResult2.GetValue()));
                    return true;
                }
            }

            result = ParseResult<Tuple<T1, T2>>.Empty;
            return false;
        }
    }

    public class Sequence<T1, T2, T3> : Parser<Tuple<T1, T2, T3>>
    {
        private readonly IParser<T1> parser1;
        private readonly IParser<T2> parser2;
        private readonly IParser<T3> parser3;
        private readonly bool _skipWhitespace;

        public Sequence(IParser<T1> parser1, IParser<T2> parser2, IParser<T3> parser3, bool skipWhitespace = true)
        {
            this.parser1 = parser1;
            this.parser2 = parser2;
            this.parser3 = parser3;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, out ParseResult<Tuple<T1, T2, T3>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            if (parser1.Parse(scanner, out var parseResult1))
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                if (parser2.Parse(scanner, out var parseResult2))
                {
                    if (_skipWhitespace)
                    {
                        scanner.SkipWhiteSpace();
                    }

                    if (parser3.Parse(scanner, out var parseResult3))
                    {
                        result = new ParseResult<Tuple<T1, T2, T3>>(parseResult1.Buffer, parseResult1.Start, parseResult3.End, new Tuple<T1, T2, T3>(parseResult1.GetValue(), parseResult2.GetValue(), parseResult3.GetValue()));
                        return true;
                    }
                }
            }

            result = ParseResult<Tuple<T1, T2, T3>>.Empty;
            return false;
        }
    }
}
