namespace Parlot.Fluent;

// We don't care about the performance of these helpers since they are called only once 
// during the parser tree creation

public static partial class Parsers
{
    /// <summary>
    /// Builds a parser that return either of the first successful of the specified parsers.
    /// </summary>
    public static Parser<T> Or<T>(this Parser<T> parser, Parser<T> or)
    {
        // We don't care about the performance of these helpers since they are called only once 
        // during the parser tree creation

        if (parser is OneOf<T> oneOf)
        {
            // Return a single OneOf instance with this new one
            return new OneOf<T>([.. oneOf.OriginalParsers, or]);
        }
        else
        {
            return new OneOf<T>([parser, or]);
        }
    }

    /// <summary>
    /// Builds a parser that return either of the first successful of the specified parsers.
    /// </summary>
    public static Parser<T> Or<A, B, T>(this Parser<A> parser, Parser<B> or)
        where A : T
        where B : T
    {
        return new OneOf<A, B, T>(parser, or);
    }

    /// <summary>
    /// Builds a parser that return either of the first successful of the specified parsers.
    /// Uses covariance to accept parsers of derived types.
    /// </summary>
    public static Parser<T> Or<A, B, T>(this IParser<A> parser, IParser<B> or)
        where A : T
        where B : T
    {
        // Convert IParser to Parser if needed
        var parserA = parser as Parser<A> ?? new IParserAdapter<A>(parser);
        var parserB = or as Parser<B> ?? new IParserAdapter<B>(or);
        return new OneOf<A, B, T>(parserA, parserB);
    }

    /// <summary>
    /// Builds a parser that return either of the first successful of the specified parsers.
    /// </summary>
    public static Parser<T> OneOf<T>(params Parser<T>[] parsers) => new OneOf<T>(parsers);

    /// <summary>
    /// Builds a parser that return either of the first successful of the specified parsers.
    /// Uses covariance to accept parsers of derived types.
    /// </summary>
    public static Parser<T> OneOf<T>(params IParser<T>[] parsers)
    {
        // Convert IParser to Parser if needed
        var converted = new Parser<T>[parsers.Length];
        for (int i = 0; i < parsers.Length; i++)
        {
            converted[i] = parsers[i] as Parser<T> ?? new IParserAdapter<T>(parsers[i]);
        }
        return new OneOf<T>(converted);
    }
}
