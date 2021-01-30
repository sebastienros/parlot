using System;

namespace Parlot.Fluent
{
    public class ParseContext
    {
        private ParseResult<TextSpan> _whiteSpaceResult = new();

        /// <summary>
        /// The scanner used for the parsing session.
        /// </summary>
        public readonly Scanner Scanner;

        public ParseContext(Scanner scanner)
        {
            Scanner = scanner;
        }

        /// <summary>
        /// Delegate that is executed whenever a parser is invoked.
        /// </summary>
        public Action<object, ParseContext>? OnEnterParser { get; set; }

        /// <summary>
        /// The parser that is used to parse whitespaces and comments.
        /// This can also include comments.
        /// </summary>
        public Parser<TextSpan>? WhiteSpaceParser { get; set;}

        public void SkipWhiteSpace()
        {
            if (WhiteSpaceParser != null)
            {
                WhiteSpaceParser.Parse(this, ref _whiteSpaceResult);
            }
            else
            {
                Scanner.SkipWhiteSpace();
            }
        }

        /// <summary>
        /// Called whenever a parser is invoked. Will be used to detect invalid states and infinite loops.
        /// </summary>
        public void EnterParser<T>(Parser<T> parser)
        {
            OnEnterParser?.Invoke(parser, this);
        }
    }
}
