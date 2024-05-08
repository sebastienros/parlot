namespace Parlot.Tests.Calc.Domain;

using System.Numerics;

public class Number<T>(T value) : Expression<T>
    where T : INumber<T>
{
    public T Value { get; } = value;

    public override T Evaluate()
    {
        return Value;
    }
}
