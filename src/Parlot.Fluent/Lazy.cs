namespace Parlot.Fluent
{
    public class Lazy<T> : Parser<T>
    {
        public IParser<T> Parser { get; set; }

        public Lazy()
        {
        }

        public override bool Parse(Scanner scanner, out ParseResult<T> result)
        {
            return Parser.Parse(scanner, out result);
        }
    }
}
