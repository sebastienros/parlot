using System;

namespace Parlot.Fluent
{
    public interface IDeferredParser<T>: IParser<T>
    {
        public IParser<T> Parser { get; set; }
    }

    public sealed class Deferred<T> : Parser<T>, IDeferredParser<T>
    {
        public IParser<T> Parser { get; set; }

        public Deferred()
        {
        }

        public Deferred(Func<Deferred<T>, IParser<T>> parser)
        {
            Parser = parser(this);
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            return Parser.Parse(context, ref result);
        }
    }
}
