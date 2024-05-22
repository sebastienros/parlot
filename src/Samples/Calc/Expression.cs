namespace Parlot.Tests.Calc
{
    using System;

    public abstract class Expression
    {
        public abstract decimal Evaluate();
    }

    public abstract class BinaryExpression : Expression
    {
        protected BinaryExpression(Expression left, Expression right)
        {
            Left = left;
            Right = right;
        }

        public Expression Left { get; }
        public Expression Right { get; }

    }

    public abstract class UnaryExpression : Expression
    {
        protected UnaryExpression(Expression inner)
        {
            Inner = inner;
        }

        public Expression Inner { get; }
    }

    public class NegateExpression : UnaryExpression
    {
        public NegateExpression(Expression inner) : base(inner)
        {
        }

        public override decimal Evaluate()
        {
            return -1 * Inner.Evaluate();
        }
    }


    public class Addition : BinaryExpression
    {
        public Addition(Expression left, Expression right) : base(left, right)
        {
        }

        public override decimal Evaluate()
        {
            return Left.Evaluate() + Right.Evaluate();
        }
    }

    public class Subtraction : BinaryExpression
    {
        public Subtraction(Expression left, Expression right) : base(left, right)
        {
        }

        public override decimal Evaluate()
        {
            return Left.Evaluate() - Right.Evaluate();
        }
    }


    public class Multiplication : BinaryExpression
    {
        public Multiplication(Expression left, Expression right) : base(left, right)
        {
        }

        public override decimal Evaluate()
        {
            return Left.Evaluate() * Right.Evaluate();
        }
    }


    public class Division : BinaryExpression
    {
        public Division(Expression left, Expression right) : base(left, right)
        {
        }

        public override decimal Evaluate()
        {
            return Left.Evaluate() / Right.Evaluate();
        }
    }

    public class Exponent : BinaryExpression
    {
        public Exponent(Expression left, Expression right) : base(left, right)
        {
        }

        public override decimal Evaluate()
        {
            return (decimal)Math.Pow((double)Left.Evaluate(), (double)Right.Evaluate());
        }
    }

    public class Number : Expression
    {
        public Number(decimal value)
        {
            Value = value;
        }

        public decimal Value { get; }

        public override decimal Evaluate()
        {
            return Value;
        }
    }
}
