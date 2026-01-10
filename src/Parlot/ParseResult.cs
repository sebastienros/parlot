namespace Parlot;

public ref struct ParseResult<T>
{
    public ParseResult(int start, int end, T value)
    {
        Start = start;
        End = end;
        Value = value;
    }

    public void Set(int start, int end, T value)
    {
        Start = start;
        End = end;
        Value = value;
    }

    public int Start;
    public int End;
    public T Value;
}
