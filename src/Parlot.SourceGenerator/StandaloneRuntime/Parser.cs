namespace Parlot.Fluent;

internal abstract class Parser<T>
{
    public string? Name { get; set; }

    public abstract bool Parse(ParseContext context, ref ParseResult<T> result);
}
