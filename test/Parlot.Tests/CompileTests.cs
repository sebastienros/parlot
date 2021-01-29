using Parlot.Fluent;
using System;
using System.Linq;
using System.Linq.Expressions;
using Xunit;

namespace Parlot.Tests
{
    public class CompileTests
    {
        private Func<ParseContext, T> Compile<T>(Parser<T> parser)
        {
            var parseContext = Expression.Parameter(typeof(ParseContext));

            var compileResult = parser.Compile(parseContext);

            var value = Expression.Variable(typeof(T), "value");

            var returnLabelTarget = Expression.Label(typeof(T));
            var returnLabelExpression = Expression.Label(returnLabelTarget, value);

            compileResult.Body.Add(Expression.Assign(value, compileResult.Value));
            compileResult.Body.Add(returnLabelExpression);

            BlockExpression body = Expression.Block(compileResult.Variables.Append(value), compileResult.Body);

            var result = Expression.Lambda<Func<ParseContext, T>>(body, parseContext);
            
            return result.Compile();
        }

        [Fact]
        public void ShouldCompileLiterals()
        {
            var parse = Compile(Parsers.Terms.Text("hello"));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.NotNull(result);
            Assert.Equal("hello", result);
        }

        [Fact]
        public void ShouldCompileLiteralsWithoutSkipWhiteSpace()
        {
            var parse = Compile(Parsers.Literals.Text("hello"));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Null(result);
        }

        [Fact]
        public void ShouldCompileOrs()
        {
            var parse = Compile(Parsers.Terms.Text("hello").Or(Parsers.Terms.Text("world")));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.NotNull(result);
            Assert.Equal("hello", result);

            result = parse(context);

            Assert.NotNull(result);
            Assert.Equal("world", result);
        }

        [Fact]
        public void ShouldCompileAnds()
        {
            var parse = Compile(Parsers.Terms.Text("hello").And(Parsers.Terms.Text("world")));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(("hello", "world"), result);
        }
    }
}
