using System.Linq.Expressions;
using Parlot.Fluent;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using static Parlot.Fluent.Parsers;

namespace Parlot.Tests
{
    public class CompileTests
    {
        public static Func<ParseContext, T> Compile<T>(Parser<T> parser)
        {
            var parseContext = Expression.Parameter(typeof(ParseContext));
            var compilationContext = new CompilationContext(parseContext)
            {
                //OnParse = (context, parser) => { Console.WriteLine($"'{context.Scanner.Cursor.Current}' {context.Scanner.Cursor.Position}"); }
            };

            var compileResult = parser.Compile(compilationContext);

            var returnLabelTarget = Expression.Label(typeof(T));
            var returnLabelExpression = Expression.Label(returnLabelTarget, compileResult.Value);

            compileResult.Body.Add(returnLabelExpression);

            var allVariables = new List<ParameterExpression>();
            allVariables.AddRange(compilationContext.GlobalVariables);
            allVariables.AddRange(compileResult.Variables);

            var allExpressions = new List<Expression>();
            allExpressions.AddRange(compilationContext.GlobalExpressions);
            allExpressions.AddRange(compileResult.Body);

            var body = Expression.Block(
                typeof(T),
                allVariables,
                allExpressions
                );

            var result = Expression.Lambda<Func<ParseContext, T>>(body, parseContext);

            var foo = body.ToString();

            return result.Compile();
        }

        [Fact]
        public void ShouldCompileTextLiterals()
        {
            var parse = Compile(Terms.Text("hello"));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.NotNull(result);
            Assert.Equal("hello", result);
        }

        [Fact]
        public void ShouldCompileCharLiterals()
        {
            var parse = Compile(Terms.Char('h'));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal('h', result);
        }

        [Fact]
        public void ShouldCompileDecimalLiterals()
        {
            var parse = Compile(Terms.Decimal());

            var scanner = new Scanner(" 123");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(123, result);
        }

        [Fact]
        public void ShouldCompileLiteralsWithoutSkipWhiteSpace()
        {
            var parse = Compile(Literals.Text("hello"));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Null(result);
        }

        [Fact]
        public void ShouldCompileOrs()
        {
            var parse = Compile(Terms.Text("hello").Or(Terms.Text("world")));

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
            var parse = Compile(Terms.Text("hello").And(Terms.Text("world")));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(("hello", "world"), result);
        }

        [Fact]
        public void ShouldCompileThens()
        {
            var parse = Compile(Terms.Text("hello").And(Terms.Text("world")).Then(x => x.Item1.ToUpper()));

            var scanner = new Scanner(" hello world");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal("HELLO", result);
        }

        [Fact]
        public void ShouldCompileDeferreds()
        {
            var deferred = Deferred<string>();

            deferred.Parser = Terms.Text("hello");

            var parse = Compile(deferred.And(deferred));


            var scanner = new Scanner(" hello hello hello");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(("hello", "hello"), result);
        }

        [Fact]
        public void ShouldCompileDeferreds2()
        {
            var openParen = Terms.Char('(');
            var closeParen = Terms.Char(')');
            var expression = Deferred<decimal>();

            var groupExpression = Between(openParen, expression, closeParen);
            expression.Parser = Terms.Decimal().Or(groupExpression);

            var parse = Compile(ZeroOrMany(expression));

            var scanner = new Scanner("1 (2) 3");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(new decimal[] {1, 2, 3 }, result);
        }

        [Fact]
        public void ShouldCompileZeroOrManys()
        {
            var parse = Compile(ZeroOrMany(Terms.Text("hello").Or(Terms.Text("world"))));

            var scanner = new Scanner(" hello world hello");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(new[] { "hello", "world", "hello" }, result);
        }


        [Fact]
        public void ShouldCompileBetweens()
        {
            var parse = Compile(Between(Terms.Text("hello"), Terms.Text("world"), Terms.Text("hello")));

            var scanner = new Scanner(" hello world hello");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal("world", result);
        }

        [Fact]
        public void ShouldCompileExpressionParser()
        {
            var expression = Deferred<Calc.Expression>();

            var number = Terms.Decimal()
                .Then<Calc.Expression>(static d => new Calc.Number(d))
                ;

            var divided = Terms.Char('/');
            var times = Terms.Char('*');
            var minus = Terms.Char('-');
            var plus = Terms.Char('+');
            var openParen = Terms.Char('(');
            var closeParen = Terms.Char(')');

            var primary = number;

            // factor => unary ( ( "/" | "*" ) unary )* ;
            var factor = primary.And(ZeroOrMany(divided.Or(times).And(primary)))
                .Then(static x =>
                {
                    // unary
                    var result = x.Item1;

                    // (("/" | "*") unary ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            '/' => new Calc.Division(result, op.Item2),
                            '*' => new Calc.Multiplication(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });

            // expression => factor ( ( "-" | "+" ) factor )* ;
            expression.Parser = factor.And(ZeroOrMany(plus.Or(minus).And(factor)))
                .Then(static x =>
                {
                    // factor
                    var result = x.Item1;

                    // (("-" | "+") factor ) *
                    foreach (var op in x.Item2)
                    {
                        result = op.Item1 switch
                        {
                            '+' => new Calc.Addition(result, op.Item2),
                            '-' => new Calc.Subtraction(result, op.Item2),
                            _ => null
                        };
                    }

                    return result;
                });

            var parse = Compile(Calc.FluentParser.Expression);

            var scanner = new Scanner("(2 + 1) * 3");
            var context = new ParseContext(scanner);
            var result = parse(context);

            Assert.Equal(9, result.Evaluate());
        }

        [Fact]
        public void Code()
        {
            (string, string) Parse(ParseContext parseContext)
            {
                var success4 = false;
                var value4 = new ValueTuple<string, string>();
                var start4 = parseContext.Scanner.Cursor.Position;
                var success1 = false;
                string value1 = null;
                Func<ParseContext, ValueTuple<bool, string>> lambda1 = parseContext =>
                {
                    ValueTuple<bool, string> result2;
                    var success2 = false;
                    string value2 = null;
                    parseContext.SkipWhiteSpace();

                    if (parseContext.Scanner.ReadText("hello", null, null))
                    {
                        success2 = true;
                        value2 = "hello";
                    }
                    result2.Item1 = success2;
                    result2.Item2 = value2;

                    return result2;
                };

                var deferred2 = lambda1.Invoke(parseContext);
                success1 = deferred2.Item1;
                value1 = deferred2.Item2;

                if (success1)
                {
                    var success3 = false;
                    string value3 = null;
                    var deferred3 = lambda1.Invoke(parseContext);
                    success3 = deferred3.Item1;
                    value3 = deferred3.Item2;

                    if (success3)
                    {
                        success4 = true;
                        value4 = new ValueTuple<string, string>(value1, value3);
                    }
                }

                if (!success4)
                {
                    parseContext.Scanner.Cursor.ResetPosition(start4);
                }

                var value = value4;

                return value;
            }

            var scanner = new Scanner(" hello hello hello");
            var context = new ParseContext(scanner);
            var result = Parse(context);

            Assert.Equal(("hello", "hello"), result);

        }
    }
}
