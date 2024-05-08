namespace Parlot.Tests.Calc.Domain;

using System;
using System.Numerics;

public class NegateExpression<T>(Expression<T> inner) : UnaryExpression<T>(inner)
    where T : INumber<T>
{
    public override T Evaluate()
    {
        //Don't know how to solve this without dynamic.
        return (dynamic)(-1 * Convert.ToInt32(Inner.Evaluate()));
    }
}
