namespace Parlot.Fluent;

public abstract partial class Parser<T>
{
    public abstract bool Parse(ParseContext context, ref ParseResult<T> result);
}
