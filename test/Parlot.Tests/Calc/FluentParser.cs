using Parlot.Fluent;
using Xunit;

namespace Parlot.Tests.Calc
{
    public class FluentParser
    {
        [Fact]
        public void Test()
        {
            var plus = ParserBuilder.Char('+');
            var minus = ParserBuilder.Char('-');
            var number = ParserBuilder.Number;
            var numberToDecimal = ParserBuilder.Number.AsDecimal();
            var customNumber = ParserBuilder.Number.Then(static token => decimal.Parse(token.Span));
            var addition = ParserBuilder.Sequence(number, ParserBuilder.FirstOf(plus, minus), number);

            Assert.True(ParserBuilder.Number.Parse("123").Success);
            Assert.False(ParserBuilder.Number.Parse("a").Success);

            numberToDecimal.TryParse("123", out var value1);
            Assert.Equal(123, value1);

            customNumber.TryParse("123", out var value2);
            Assert.Equal(123, value2);

            addition.TryParse("1 + 2", out var additionResult);

            Assert.Equal("1", additionResult[0].Text);
            Assert.Equal("+", additionResult[1].Text);
            Assert.Equal("2", additionResult[2].Text);

        }
    }
}
