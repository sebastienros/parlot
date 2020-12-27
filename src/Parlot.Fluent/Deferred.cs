namespace Parlot.Fluent
{
    public sealed class Deferred<T> : Parser<T>
    {
        public IParser<T> Parser { get; set; }

        public Deferred()
        {
        }

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            return Parser.Parse(scanner, out result);
        }
    }
}
