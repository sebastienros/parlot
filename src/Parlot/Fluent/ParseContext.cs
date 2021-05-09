using System;

namespace Parlot.Fluent
{
    public partial class ParseContext
    {

        /// <summary>
        /// Delegate that is executed whenever a parser is invoked.
        /// </summary>
        public Action<object, ParseContext> OnEnterParser { get; set; }

        /// <summary>
        /// Called whenever a parser is invoked. Will be used to detect invalid states and infinite loops.
        /// </summary>
        public void EnterParser<T, TParseContext>(Parser<T, TParseContext> parser)
        where TParseContext : ParseContext
        {
            OnEnterParser?.Invoke(parser, this);
        }
    }
    public partial class ParseContextWithScanner<TScanner, T> : ParseContext
    where TScanner : Scanner<T>
    where T : IEquatable<T>, IConvertible
    {
        /// <summary>
        /// The scanner used for the parsing session.
        /// </summary>
        public readonly TScanner Scanner;

        public ParseContextWithScanner(TScanner scanner)
        {
            Scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        }
    }

    public class StringParseContext : ParseContextWithScanner<Scanner<char>, char>
    {
        /// <summary>
        /// Whether new lines are treated as normal chars or white spaces.
        /// </summary>
        /// <remarks>
        /// When <c>false</c>, new lines will be skipped like any other white space.
        /// Otherwise white spaces need to be read explicitely by a rule.
        /// </remarks>
        public bool UseNewLines { get; private set; }

        public StringParseContext(Scanner<char> scanner, bool useNewLines = false)
        : base(scanner)
        {
            UseNewLines = useNewLines;
        }

        public StringParseContext(string text, bool useNewLines = false)
        : this(new Scanner<char>(text.ToCharArray()), useNewLines)
        {
        }

    }
}
