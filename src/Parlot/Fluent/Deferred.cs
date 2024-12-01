using FastExpressionCompiler;
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
#if NET
using System.Linq;
#endif
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class Deferred<T> : Parser<T>, ICompilable, ISeekable
{
    private Parser<T>? _parser;

    public Parser<T>? Parser
    {
        get => _parser;
        set
        {
            _parser = value ?? throw new ArgumentNullException(nameof(value));
            Name = $"{_parser.Name} (Deferred)";
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Deferred()
    {
        Name = "Deferred";
    }

    public Deferred(Func<Deferred<T>, Parser<T>> parser) : this()
    {
        Parser = parser(this);

        if (Parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        if (Parser is null)
        {
            throw new InvalidOperationException("Parser has not been initialized");
        }

        context.EnterParser(this);

        var outcome = Parser.Parse(context, ref result);

        context.ExitParser(this);
        return outcome;
    }

    private bool _initialized;
    private readonly Closure _closure = new();

    private sealed class Closure
    {
        public object? Func;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        if (Parser == null)
        {
            throw new InvalidOperationException("Can't compile a Deferred Parser until it is fully initialized");
        }

        var result = context.CreateCompilationResult<T>();

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
            var returnTarget = Expression.Label(typeof(ValueTuple<bool, T>));
            var returnExpression = Expression.Return(returnTarget, resultExpression, typeof(ValueTuple<bool, T>));
            var returnLabel = Expression.Label(returnTarget, defaultValue: Expression.New(typeof(ValueTuple<bool, T>)));

            var lambda =
                Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        type: typeof(ValueTuple<bool, T>),
                        variables: parserCompileResult.Variables.Append(resultExpression),
                        Expression.Block(parserCompileResult.Body),
                        Expression.Assign(resultExpression, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!,
                            parserCompileResult.Success,
                            context.DiscardResult ? Expression.Default(parserCompileResult.Value.Type) : parserCompileResult.Value)),
                        returnExpression,
                        returnLabel),
                    true,
                    context.ParseContext
                    );

            // Store the source lambda for debugging
            context.Lambdas.Add(lambda);

            _closure.Func = lambda.CompileFast();
        }

        // ValueTuple<bool, T> def;

        var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"def{context.NextNumber}");
        result.Variables.Add(deferred);

        // def = ((Func<ParserContext, ValueTuple<bool, T>>)_closure.Func).Invoke(parseContext);

        var contextScope = Expression.Constant(_closure);
        var getFuncs = typeof(Closure).GetMember(nameof(Closure.Func))[0];
        var funcReturnType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
        var funcsAccess = Expression.MakeMemberAccess(contextScope, getFuncs);

        var castFunc = Expression.Convert(funcsAccess, funcReturnType);
        result.Body.Add(Expression.Assign(deferred, Expression.Invoke(castFunc, context.ParseContext)));

        // success = def.Item1;
        // value = def.Item2;

        result.Body.Add(Expression.Assign(result.Success, Expression.Field(deferred, "Item1")));
        result.Body.Add(
            context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Assign(result.Value, Expression.Field(deferred, "Item2"))
        );

        return result;
    }
}
