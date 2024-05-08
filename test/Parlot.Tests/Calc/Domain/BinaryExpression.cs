namespace Parlot.Tests.Calc.Domain;

using System.Numerics;

public abstract class BinaryExpression<T>(Expression<T> left, Expression<T> right) : Expression<T>
    where T : INumber<T>
{
    public Expression<T> Left { get; } = left;
    public Expression<T> Right { get; } = right;
}
