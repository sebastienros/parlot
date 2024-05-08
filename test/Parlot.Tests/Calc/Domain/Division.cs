namespace Parlot.Tests.Calc.Domain;

using System.Numerics;

public class Division<T>(Expression<T> left, Expression<T> right) : BinaryExpression<T>(left, right)
    where T : INumber<T>
{
    public override T Evaluate()
    {
        return Left.Evaluate() / Right.Evaluate();
    }
}
