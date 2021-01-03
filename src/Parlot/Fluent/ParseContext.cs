using System;
using System.Collections.Generic;

namespace Parlot.Fluent
{
    public class ParseContext
    {
        private Dictionary<string, object> _properties;
        private ParseResult<object> _whiteSpaceResult = new();

        /// <summary>
        /// The scanner used for the parsing session.
        /// </summary>
        public readonly Scanner Scanner;

        public ParseContext(Scanner scanner)
        {
            Scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        }

        /// <summary>
        /// A custom collection of objects that can be shared across parsers.
        /// </summary>
        public Dictionary<string, object> Properties => _properties ??= new();

        /// <summary>
        /// Delegate that is executed whenever a parser is invoked.
        /// </summary>
        public Action<IParser, ParseContext> OnEnterParser { get; set; }

        /// <summary>
        /// The parser that is used to parse whitespaces and comments.
        /// This can also include comments.
        /// </summary>
        public IParser WhiteSpaceParser { get; set;}

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
        public void EnterParser(IParser parser)
        {
            OnEnterParser?.Invoke(parser, this);
        }
    }
}
