using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Linq;

namespace Parlot.Fluent
{
    public sealed class Sequence<T1, T2> : Parser<ValueTuple<T1, T2>>, ICompilable, ISkippableSequenceParser, ISeekable
    {
        internal readonly Parser<T1> _parser1;
        internal readonly Parser<T2> _parser2;
        public Sequence(Parser<T1> parser1, Parser<T2> parser2)
        {
            _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
            _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));
        }

        public bool CanSeek => _parser1 is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser1 is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser1 is ISeekable seekable && seekable.SkipWhitespace;

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

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
        {
            return
                [
                    new SkippableCompilationResult(_parser1.Build(context), false),
                    new SkippableCompilationResult(_parser2.Build(context), false)
                ];
        }

        public CompilationResult Compile(CompilationContext context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3> : Parser<ValueTuple<T1, T2, T3>>, ICompilable, ISkippableSequenceParser, ISeekable
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

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

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

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
        {
            if (_parser is not ISkippableSequenceParser sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
        }

        public CompilationResult Compile(CompilationContext context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T3, T4>>, ICompilable, ISkippableSequenceParser, ISeekable
    {
        private readonly Parser<ValueTuple<T1, T2, T3>> _parser;
        internal readonly Parser<T4> _lastParser;

        public Sequence(Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

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

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
        {
            if (_parser is not ISkippableSequenceParser sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
        }

        public CompilationResult Compile(CompilationContext context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T4, T5>>, ICompilable, ISkippableSequenceParser, ISeekable
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4>> _parser;
        internal readonly Parser<T5> _lastParser;
        
        public Sequence(Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

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

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
        {
            if (_parser is not ISkippableSequenceParser sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
        }

        public CompilationResult Compile(CompilationContext context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>>, ICompilable, ISkippableSequenceParser, ISeekable
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>> _parser;
        internal readonly Parser<T6> _lastParser;        

        public Sequence(Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

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

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
        {
            if (_parser is not ISkippableSequenceParser sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
        }

        public CompilationResult Compile(CompilationContext context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }

    public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>, ICompilable, ISkippableSequenceParser, ISeekable
    {
        private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> _parser;
        internal readonly Parser<T7> _lastParser;

        public Sequence(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> lastParser)
        {
            _parser = parser;
            _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));
        }

        public bool CanSeek => _parser is ISeekable seekable && seekable.CanSeek;

        public char[] ExpectedChars => _parser is ISeekable seekable ? seekable.ExpectedChars : default;

        public bool SkipWhitespace => _parser is ISeekable seekable && seekable.SkipWhitespace;

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

        public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
        {
            if (_parser is not ISkippableSequenceParser sequenceParser)
            {
                throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
            }

            return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
        }

        public CompilationResult Compile(CompilationContext context)
        {
            return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
        }
    }
}
