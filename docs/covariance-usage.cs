using Parlot.Fluent;
using static Parlot.Fluent.Parsers;

namespace CovarianceExample;

// Example domain model for an expression parser
abstract class Expression
{
    public abstract int Evaluate();
}

class NumberExpression : Expression
{
    public int Value { get; set; }
    public override int Evaluate() => Value;
}

class AddExpression : Expression
{
    public Expression Left { get; set; } = null!;
    public Expression Right { get; set; } = null!;
    public override int Evaluate() => Left.Evaluate() + Right.Evaluate();
}

class MultiplyExpression : Expression
{
    public Expression Left { get; set; } = null!;
    public Expression Right { get; set; } = null!;
    public override int Evaluate() => Left.Evaluate() * Right.Evaluate();
}

class Program
{
    static void Main()
    {
        // Define parsers for specific expression types
        var numberParser = Terms.Integer().Then(n => new NumberExpression { Value = n });
        
        // BEFORE: Would need explicit conversions like this:
        // var expressionParser = numberParser.Then<Expression>(x => x);
        
        // AFTER: Can use covariance directly!
        // The OneOf method now accepts IParser<T>[] where T is covariant
        Parser<Expression> expressionParser = OneOf<Expression>(
            numberParser
            // Could add more expression types here without .Then<Expression>(x => x)
        );

        var result = expressionParser.Parse("42");
        if (result != null)
        {
            Console.WriteLine($"Parsed: {result.Evaluate()}"); // Output: Parsed: 42
        }

        // More complex example with multiple types
        var addParser = Terms.Text("add").SkipAnd(Terms.Integer())
            .And(Terms.Integer())
            .Then(tuple => new AddExpression 
            { 
                Left = new NumberExpression { Value = tuple.Item1 },
                Right = new NumberExpression { Value = tuple.Item2 }
            });

        var multiplyParser = Terms.Text("mul").SkipAnd(Terms.Integer())
            .And(Terms.Integer())
            .Then(tuple => new MultiplyExpression 
            { 
                Left = new NumberExpression { Value = tuple.Item1 },
                Right = new NumberExpression { Value = tuple.Item2 }
            });

        // BEFORE: Would need .Then<Expression>(x => x) on each parser
        // var complexParser = numberParser.Then<Expression>(x => x)
        //     .Or(addParser.Then<Expression>(x => x))
        //     .Or(multiplyParser.Then<Expression>(x => x));

        // AFTER: Clean and simple with covariance
        var complexParser = OneOf<Expression>(numberParser, addParser, multiplyParser);

        var result1 = complexParser.Parse("42");
        Console.WriteLine($"Number: {result1?.Evaluate()}"); // Output: Number: 42

        var result2 = complexParser.Parse("add 10 20");
        Console.WriteLine($"Add: {result2?.Evaluate()}"); // Output: Add: 30

        var result3 = complexParser.Parse("mul 5 6");
        Console.WriteLine($"Multiply: {result3?.Evaluate()}"); // Output: Multiply: 30
    }
}
