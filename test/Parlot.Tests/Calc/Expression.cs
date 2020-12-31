namespace Parlot.Tests.Calc
{
    public abstract class Expression
    {
        public abstract decimal Evaluate();
    }

    public abstract class BinaryExpression : Expression
    {
        public BinaryExpression(Expression left, Expression right)
        {
            Left = left;
            Right = right;
        }

        public Expression Left { get; set; }
        public Expression Right { get; set; }

    }

    public abstract class UnaryExpression : Expression
    {
        public UnaryExpression(Expression inner)
        {
            Inner = inner;
        }

        public Expression Inner { get; set; }
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
            Left = left;
            Right = right;
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
            Left = left;
            Right = right;
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
            Left = left;
            Right = right;
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
            Left = left;
            Right = right;
        }

        public override decimal Evaluate()
        {
            return Left.Evaluate() / Right.Evaluate();
        }
    }

    public class Number : Expression
    {
        public Number(decimal value)
        {
            Value = value;
        }

        public decimal Value { get; set; }

        public override decimal Evaluate()
        {
            return Value;
        }
    }
}