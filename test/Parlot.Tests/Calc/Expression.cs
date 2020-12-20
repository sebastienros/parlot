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

    public class Substraction : BinaryExpression
    {
        public Substraction(Expression left, Expression right) : base(left, right)
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