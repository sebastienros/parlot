using System;

namespace Parlot.Fluent
{
    public class ParseContext
    {
        public static int DefaultCompilationThreshold = 0;

        /// <summary>
        /// The number of usages of the parser before it is compiled automatically. <c>0</c> to disable automatic compilation. Default is 0.
        /// </summary>
        public int CompilationThreshold { get; set; } = DefaultCompilationThreshold;

        /// <summary>
        /// Whether new lines are treated as normal chars or white spaces. Default is <c>false</c>.
        /// </summary>
        /// <remarks>
        /// When <c>false</c>, new lines will be skipped like any other white space.
        /// Otherwise new lines need to be read explicitely by a rule.
        /// </remarks>
        public bool UseNewLines { get; private set; }

        /// <summary>
        /// The scanner used for the parsing session.
        /// </summary>
        public readonly Scanner Scanner;

        public ParseContext(Scanner scanner, bool useNewLines = false)
        {
            Scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            UseNewLines = useNewLines;
        }

        /// <summary>
        /// Delegate that is executed whenever a parser is invoked.
        /// </summary>
        public Action<object, ParseContext> OnEnterParser { get; set; }

        /// <summary>
        /// The parser that is used to parse whitespaces and comments.
        /// </summary>
        public Parser<TextSpan> WhiteSpaceParser { get; set;}

        public void SkipWhiteSpace()
        {
            if (WhiteSpaceParser is null)
            {
                if (UseNewLines)
                {
                    Scanner.SkipWhiteSpace();
                }
                else
                {
                    Scanner.SkipWhiteSpaceOrNewLine();
                }
            }
            else
            {
                ParseResult<TextSpan> _ = new();
                WhiteSpaceParser.Parse(this, ref _);
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
