using Parlot.Compilation;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Deferred<T, TParseContext> : Parser<T, TParseContext>, ICompilable<TParseContext>
    where TParseContext : ParseContext
    {
        public Parser<T, TParseContext> Parser { get; set; }

        public Deferred()
        {
        }

        public Deferred(Func<Deferred<T, TParseContext>, Parser<T, TParseContext>> parser)
        {
            Parser = parser(this);
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            return Parser.Parse(context, ref result);
        }

        private bool _initialized = false;
        private readonly Closure _closure = new();

        private class Closure
        {
            public object Func;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            if (Parser == null)
            {
                throw new InvalidOperationException("Can't compile a Deferred Parser until it is fully initialized");
            }

            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

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

                var parserCompileResult = Parser.Build(context);

                var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"result{context.NextNumber}");

                var returnLabelTarget = Expression.Label(typeof(ValueTuple<bool, T>));
                var returnLabelExpression = Expression.Label(returnLabelTarget, resultExpression);

                var lambda =
                    Expression.Lambda<Func<TParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        typeof(ValueTuple<bool, T>),
                        parserCompileResult.Variables.Append(resultExpression),
                        Expression.Block(parserCompileResult.Body),
                        Expression.Assign(resultExpression, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor(new[] { typeof(bool), typeof(T) }),
                            parserCompileResult.Success,
                            context.DiscardResult ? Expression.Default(parserCompileResult.Value.Type) : parserCompileResult.Value)),
                        returnLabelExpression),
                    true,
                    context.ParseContext)
                    ;

                // Store the source lambda for debugging
                context.Lambdas.Add(lambda);

                _closure.Func = lambda.Compile();
            }

            // ValueTuple<bool, T> def;

            var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"def{context.NextNumber}");
            result.Variables.Add(deferred);

            // def = ((Func<ParserContext, ValueTuple<bool, T>>)_closure.Func).Invoke(parseContext);

            var contextScope = Expression.Constant(_closure);
            var getFuncs = typeof(Closure).GetMember(nameof(Closure.Func))[0];
            var funcReturnType = typeof(Func<TParseContext, ValueTuple<bool, T>>);
            var funcsAccess = Expression.MakeMemberAccess(contextScope, getFuncs);

            var castFunc = Expression.Convert(funcsAccess, funcReturnType);
            result.Body.Add(Expression.Assign(deferred, Expression.Invoke(castFunc, context.ParseContext)));

            // success = def.Item1;
            // value = def.Item2;

            result.Body.Add(Expression.Assign(success, Expression.Field(deferred, "Item1")));
            result.Body.Add(Expression.Assign(value, Expression.Field(deferred, "Item2")));

            return result;
        }
    }


    public sealed class Deferred<T, TParseContext, TChar> : Parser<T, TParseContext, TChar>, ICompilable<TParseContext>
    where TParseContext : ParseContextWithScanner<Scanner<TChar>, TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        public Parser<T, TParseContext, TChar> Parser { get; set; }

        public Deferred()
        {
        }

        public Deferred(Func<Deferred<T, TParseContext, TChar>, Parser<T, TParseContext, TChar>> parser)
        {
            Parser = parser(this);
        }

        public override bool Parse(TParseContext context, ref ParseResult<T> result)
        {
            return Parser.Parse(context, ref result);
        }

        private bool _initialized = false;
        private readonly Closure _closure = new();

        private class Closure
        {
            public object Func;
        }

        public CompilationResult Compile(CompilationContext<TParseContext> context)
        {
            if (Parser == null)
            {
                throw new InvalidOperationException("Can't compile a Deferred Parser until it is fully initialized");
            }

            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));

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

                var parserCompileResult = Parser.Build(context);

                var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"result{context.NextNumber}");

                var returnLabelTarget = Expression.Label(typeof(ValueTuple<bool, T>));
                var returnLabelExpression = Expression.Label(returnLabelTarget, resultExpression);

                var lambda =
                    Expression.Lambda<Func<TParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        typeof(ValueTuple<bool, T>),
                        parserCompileResult.Variables.Append(resultExpression),
                        Expression.Block(parserCompileResult.Body),
                        Expression.Assign(resultExpression, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor(new[] { typeof(bool), typeof(T) }),
                            parserCompileResult.Success,
                            context.DiscardResult ? Expression.Default(parserCompileResult.Value.Type) : parserCompileResult.Value)),
                        returnLabelExpression),
                    true,
                    context.ParseContext)
                    ;

                // Store the source lambda for debugging
                context.Lambdas.Add(lambda);

                _closure.Func = lambda.Compile();
            }

            // ValueTuple<bool, T> def;

            var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"def{context.NextNumber}");
            result.Variables.Add(deferred);

            // def = ((Func<ParserContext, ValueTuple<bool, T>>)_closure.Func).Invoke(parseContext);

            var contextScope = Expression.Constant(_closure);
            var getFuncs = typeof(Closure).GetMember(nameof(Closure.Func))[0];
            var funcReturnType = typeof(Func<TParseContext, ValueTuple<bool, T>>);
            var funcsAccess = Expression.MakeMemberAccess(contextScope, getFuncs);

            var castFunc = Expression.Convert(funcsAccess, funcReturnType);
            result.Body.Add(Expression.Assign(deferred, Expression.Invoke(castFunc, context.ParseContext)));

            // success = def.Item1;
            // value = def.Item2;

            result.Body.Add(Expression.Assign(success, Expression.Field(deferred, "Item1")));
            result.Body.Add(Expression.Assign(value, Expression.Field(deferred, "Item2")));

            return result;
        }
    }
}
