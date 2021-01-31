using System;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent
{
    public readonly struct ParseContext
    {
        /// <summary>
        /// The scanner used for the parsing session.
        /// </summary>
        public readonly Scanner Scanner;
       
        private readonly Action<object, ParseContext> _onEnterParser;
        private readonly Parser<TextSpan> _whiteSpaceParser;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scanner">The scanner used for the parsing session.</param>
        /// <param name="onEnterParser">Delegate that is executed whenever a parser is invoked.</param>
        /// <param name="whiteSpaceParser">The parser that is used to parse whitespaces and comments. This can also include comments.</param>
        public ParseContext(
            Scanner scanner,
            Action<object, ParseContext> onEnterParser = null,
            Parser<TextSpan> whiteSpaceParser = null)
        {
            if (scanner is null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(scanner));
            }
            Scanner = scanner;
            _onEnterParser = onEnterParser;
            _whiteSpaceParser = whiteSpaceParser;
        }

        public void SkipWhiteSpace()
        {
            if (_whiteSpaceParser is null)
            {
                Scanner.SkipWhiteSpace();
            }
            else
            {
                ParseResult<TextSpan> _ = new();
                _whiteSpaceParser.Parse(this, ref _);
            }
        }

        /// <summary>
        /// Called whenever a parser is invoked. Will be used to detect invalid states and infinite loops.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnterParser<T>(Parser<T> parser)
        {
            _onEnterParser?.Invoke(parser, this);
        }
    }
}
