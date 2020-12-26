namespace Parlot.Fluent
{
    public class Lazy<T> : Parser<T>
    {
        public IParser<T> Parser { get; set; }

        public Lazy()
        {
        }

        public override bool Parse(Scanner scanner, IParseResult<T> result)
        {
            return Parser.Parse(scanner, result);
        }
    }

    public class Lazy : Parser<IParseResult>
    {
        public IParser Parser { get; set; }

        public Lazy()
        {
        }

        public override bool Parse(Scanner scanner, IParseResult<IParseResult> result)
        {
            return Parser.Parse(scanner, result);
        }
    }
}
