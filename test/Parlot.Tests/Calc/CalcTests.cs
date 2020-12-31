using Xunit;

namespace Parlot.Tests.Calc
{
    public abstract class CalcTests
    {
        protected abstract decimal Evaluate(string text);

        [Theory]
        [InlineData("123", 123)]
        [InlineData("0", 0)]
        public void TestNumber(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("123.0", 123)]
        [InlineData("123.1", 123.1)]
        [InlineData("123.456789", 123.456789)]
        public void TestDecimalNumber(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("123 + 123", 246)]
        [InlineData("123 + 123 + 123", 369)]
        public void TestAddition(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("123 - 123", 0)]
        public void TestSubtraction(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("123 * 2", 246)]
        public void TestMultiplication(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("123 / 123", 1)]
        public void TestDivision(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("3 + (1 - 2)", 2)]
        [InlineData("(3 + 1) * 2", 8)]
        [InlineData("( (3 + 1) * 2 ) + 1", 9)]
        public void TestGroup(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("3 + 1 * 2", 5)]
        public void TestPrecedence(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }

        [Theory]
        [InlineData("-2", -2)]
        [InlineData("-(1+2)", -3)]
        [InlineData("--(1+2)", 3)]
        public void TestUnary(string text, decimal value)
        {
            var result = Evaluate(text);

            Assert.Equal(value, result);
        }
    }
}
