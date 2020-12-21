namespace Parlot.Tests.Calc
{
    public class InterpreterTests : CalcTests
    {
        protected override decimal Evaluate(string text)
        {
            return new Interpreter().Evaluate(text);
        }
    }
}
