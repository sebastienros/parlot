using System;

namespace Parlot.Fluent
{
    public class Sequence : Parser<IParseResult[]>
    {
        private readonly IParser[] _parsers;
        private readonly bool _skipWhitespace;

        public Sequence(IParser[] parsers, bool skipWhitespace = true)
        {
            _parsers = parsers;
            _skipWhitespace = skipWhitespace;
        }

        public override bool Parse(Scanner scanner, IParseResult<IParseResult[]> result)
        {
            var results = new ParseResult[_parsers.Length];

            if (_parsers.Length == 0)
            {
                return true;
            }

            var success = true;

            for (var i = 0; i < _parsers.Length; i++)
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                var parsed = new ParseResult();

                if (!_parsers[i].Parse(scanner, parsed))
                {
                    success = false;
                    break;
                }

                if (parsed != null)
                {
                    results[i] = parsed;
                }
            }

            if (success)
            {
                result?.Succeed(results[0].Buffer, results[0].Start, results[^1].End, results);
                return true;
            }
            else
            {
                result?.Fail();
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

        public override bool Parse(Scanner scanner, IParseResult<Tuple<T1, T2>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (parser1.Parse(scanner, parseResult1))
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                var parseResult2 = new ParseResult<T2>();

                if (parser2.Parse(scanner, parseResult2))
                {
                    result?.Succeed(parseResult1.Buffer, parseResult1.Start, parseResult2.End, new Tuple<T1, T2>(parseResult1.GetValue(), parseResult2.GetValue()));
                    return true;
                }
            }

            result?.Fail();
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

        public override bool Parse(Scanner scanner, IParseResult<Tuple<T1, T2, T3>> result)
        {
            if (_skipWhitespace)
            {
                scanner.SkipWhiteSpace();
            }

            var parseResult1 = new ParseResult<T1>();

            if (parser1.Parse(scanner, parseResult1))
            {
                if (_skipWhitespace)
                {
                    scanner.SkipWhiteSpace();
                }

                var parseResult2 = new ParseResult<T2>();

                if (parser2.Parse(scanner, parseResult2))
                {
                    if (_skipWhitespace)
                    {
                        scanner.SkipWhiteSpace();
                    }

                    var parseResult3 = new ParseResult<T3>();

                    if (parser3.Parse(scanner, parseResult3))
                    {
                        result?.Succeed(parseResult1.Buffer, parseResult1.Start, parseResult3.End, new Tuple<T1, T2, T3>(parseResult1.GetValue(), parseResult2.GetValue(), parseResult3.GetValue()));
                        return true;
                    }
                }
            }

            result?.Fail();
            return false;
        }
    }
}
