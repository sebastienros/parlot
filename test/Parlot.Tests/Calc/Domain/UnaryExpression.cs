namespace Parlot.Tests.Calc.Domain;

using System.Numerics;

public abstract class UnaryExpression<T> : Expression<T> where T : INumber<T>
{
    protected UnaryExpression(Expression<T> inner)
    {
        Inner = inner;
    }

    public Expression<T> Inner { get; }
}
