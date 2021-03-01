using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Deferred<T> : Parser<T>
    {
        public Parser<T> Parser { get; set; }

        public Deferred()
        {
        }

        public Deferred(Func<Deferred<T>, Parser<T>> parser)
        {
            Parser = parser(this);
        }

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            return Parser.Parse(context, ref result);
        }

        private int _funcIndex;
        private bool _initialized = false;

        public override CompileResult Compile(CompilationContext context)
        {
            var variables = new List<ParameterExpression>();
            var body = new List<Expression>();
            var success = Expression.Variable(typeof(bool), $"success{++context.Counter}");
            var value = Expression.Variable(typeof(T), $"value{context.Counter}");

            variables.Add(success);
            variables.Add(value);

            // Compile the parser code as a lambda the first time,
            // then reuse the lambda the subsequent times.

            body.Add(Expression.Assign(success, Expression.Constant(false, typeof(bool))));
            body.Add(Expression.Assign(value, Expression.Constant(default(T), typeof(T))));

            var contextScope = Expression.Constant(context);
            var getFuncs = typeof(CompilationContext).GetMember("Funcs")[0];
            var funcsAccess = Expression.MakeMemberAccess(contextScope, getFuncs);

            var funcReturnType = typeof(Func<ParseContext, ValueTuple<bool, T>>);

            // Create the body of this parser only once
            if (!_initialized)
            {
                _initialized = true;

                // lambda (ParserContext)
                // {
                //   parse1 instructions
                //   
                //   var result = new ValueTuple<bool, T>(parser1.Success, parse1.Value);
                //   return result;
                // }

                var parserCompileResult = Parser.Compile(context);

                var result = Expression.Variable(typeof(ValueTuple<bool, T>), $"result{context.Counter}");

                var returnLabelTarget = Expression.Label(typeof(ValueTuple<bool, T>));
                var returnLabelExpression = Expression.Label(returnLabelTarget, result);

                var lambda =
                    Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        typeof(ValueTuple<bool, T>),
                        parserCompileResult.Variables.Append(result),
                        Expression.Block(parserCompileResult.Body),
                        Expression.Assign(result, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor(new[] { typeof(bool), typeof(T) }),
                            parserCompileResult.Success,
                            parserCompileResult.Value)),
                        returnLabelExpression),
                    true,
                    context.ParseContext)
                    .Compile()
                    ;

                // The parser is added to CompilerContext.Funcs, and its index recorded
                _funcIndex = context.Funcs.Count;
                context.Funcs.Add(lambda);
            }

            // ValueTuple<bool, T> def;

            var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"def{context.Counter}");
            variables.Add(deferred);

            // def = ((Func<ParserContext, ValueTuple<bool, T>>)Funcs[_funcIndex]).Invoke(parseContext);

            var listIndexer = typeof(List<object>).GetProperties().First(x => x.GetIndexParameters().Any()).GetGetMethod();
            var funcInClosure = Expression.Call(funcsAccess, listIndexer, Expression.Constant(_funcIndex));
            var castFunc = Expression.Convert(funcInClosure, funcReturnType);

            body.Add(Expression.Assign(deferred, Expression.Invoke(castFunc, context.ParseContext)));

            // success = def.Item1;
            // value = def.Item2;

            body.Add(Expression.Assign(success, Expression.Field(deferred, "Item1")));
            body.Add(Expression.Assign(value, Expression.Field(deferred, "Item2")));

            return new CompileResult(variables, body, success, value);
        }
    }
}
