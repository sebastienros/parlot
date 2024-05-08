namespace Parlot.Tests.Calc.Domain;

using System.Numerics;

public abstract class Expression<T> where T : INumber<T>
{
    public abstract T Evaluate();
}
