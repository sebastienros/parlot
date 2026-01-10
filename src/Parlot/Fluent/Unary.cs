using FastExpressionCompiler;
using Parlot.Compilation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// A parser that creates a unary operation structure.
/// Handles prefix operators that can be applied recursively.
/// </summary>
/// <typeparam name="T">The type of the value being parsed.</typeparam>
/// <typeparam name="TInput">The type of the operator parsers.</typeparam>
public sealed class Unary<T, TInput> : Parser<T>, ICompilable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<T, T> Factory)[] _operators;

    public Unary(Parser<T> parser, (Parser<TInput> op, Func<T, T> factory)[] operators)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = operators ?? throw new ArgumentNullException(nameof(operators));

        if (_operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        foreach (var (op, factory) in _operators)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            var operatorResult = new ParseResult<TInput>();

            if (op.Parse(context, ref operatorResult))
            {
                if (Parse(context, ref result))
                {
                    result = new ParseResult<T>(result.Start, result.End, factory(result.Value));
                    context.ExitParser(this);
                    return true;
                }

                // Operator matched but no operand - fail and rollback.
                context.Scanner.Cursor.ResetPosition(operatorPosition);
                context.ExitParser(this);
                return false;
            }
        }

        var success = _parser.Parse(context, ref result);

        context.ExitParser(this);
        return success;
    }

    private bool _initialized;
    private readonly Closure _closure = new();

    private sealed class Closure
    {
        public object? Func;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        if (!_initialized)
        {
            _initialized = true;

            var innerContext = context;
            var innerResult = innerContext.CreateCompilationResult<T>();

            var nextNum = innerContext.NextNumber;
            var matchedFactory = innerResult.DeclareVariable<Func<T, T>>($"unaryFactory{nextNum}");
            var operatorMatched = innerResult.DeclareVariable<bool>($"unaryOpMatched{nextNum}");
            var operatorPosition = innerResult.DeclareVariable<TextPosition>($"unaryPos{nextNum}");

            var baseParserResult = _parser.Build(innerContext);

            var operatorCheckExpressions = new List<Expression>();
            var allOperatorVariables = new List<ParameterExpression>();

            for (int i = 0; i < _operators.Length; i++)
            {
                var (op, factory) = _operators[i];
                var opCompileResult = op.Build(innerContext);

                allOperatorVariables.AddRange(opCompileResult.Variables);

                var factoryConst = Expression.Constant(factory);

                if (i == 0)
                {
                    operatorCheckExpressions.AddRange(opCompileResult.Body);
                    operatorCheckExpressions.Add(
                        Expression.IfThen(
                            opCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(operatorMatched, Expression.Constant(true)),
                                Expression.Assign(matchedFactory, factoryConst)
                            )
                        )
                    );
                }
                else
                {
                    operatorCheckExpressions.Add(
                        Expression.IfThen(
                            Expression.Not(operatorMatched),
                            Expression.Block(
                                opCompileResult.Body.Concat(new Expression[]
                                {
                                    Expression.IfThen(
                                        opCompileResult.Success,
                                        Expression.Block(
                                            Expression.Assign(operatorMatched, Expression.Constant(true)),
                                            Expression.Assign(matchedFactory, factoryConst)
                                        )
                                    )
                                })
                            )
                        )
                    );
                }
            }

            var scanner = Expression.Field(innerContext.ParseContext, nameof(ParseContext.Scanner));
            var cursor = Expression.Field(scanner, nameof(Scanner.Cursor));
            var cursorPosition = Expression.Property(cursor, nameof(Cursor.Position));
            var resetPosition = typeof(Cursor).GetMethod(nameof(Cursor.ResetPosition), [typeof(TextPosition).MakeByRefType()])!;

            var closureConst = Expression.Constant(_closure);
            var getFuncs = typeof(Closure).GetMember(nameof(Closure.Func))[0];
            var funcReturnType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
            var funcsAccess = Expression.MakeMemberAccess(closureConst, getFuncs);
            var castFunc = Expression.Convert(funcsAccess, funcReturnType);

            var recursiveResult = Expression.Variable(typeof(ValueTuple<bool, T>), $"recursiveUnary{nextNum}");
            innerResult.Variables.Add(recursiveResult);

            var innerBody = Expression.Block(
                allOperatorVariables.Concat(baseParserResult.Variables),
                new Expression[]
                {
                    Expression.Assign(operatorMatched, Expression.Constant(false)),
                    Expression.Assign(matchedFactory, Expression.Constant(null, typeof(Func<T, T>))),
                    Expression.Assign(operatorPosition, cursorPosition)
                }
                .Concat(operatorCheckExpressions)
                .Concat([
                    Expression.IfThenElse(
                        operatorMatched,
                        Expression.Block(
                            Expression.Assign(recursiveResult, Expression.Invoke(castFunc, innerContext.ParseContext)),
                            Expression.IfThenElse(
                                Expression.Field(recursiveResult, "Item1"),
                                Expression.Block(
                                    Expression.Assign(innerResult.Success, Expression.Constant(true)),
                                    context.DiscardResult
                                        ? Expression.Invoke(matchedFactory, Expression.Field(recursiveResult, "Item2"))
                                        : Expression.Assign(innerResult.Value, Expression.Invoke(matchedFactory, Expression.Field(recursiveResult, "Item2")))
                                ),
                                Expression.Call(cursor, resetPosition, operatorPosition)
                            )
                        ),
                        Expression.Block(
                            baseParserResult.Body.Concat(new Expression[]
                            {
                                Expression.IfThen(
                                    baseParserResult.Success,
                                    Expression.Block(
                                        Expression.Assign(innerResult.Success, Expression.Constant(true)),
                                        context.DiscardResult ? Expression.Empty() : Expression.Assign(innerResult.Value, baseParserResult.Value)
                                    )
                                )
                            })
                        )
                    )
                ])
            );

            var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"unaryResult{context.NextNumber}");
            var returnTarget = Expression.Label(typeof(ValueTuple<bool, T>));
            var returnExpression = Expression.Return(returnTarget, resultExpression, typeof(ValueTuple<bool, T>));
            var returnLabel = Expression.Label(returnTarget, defaultValue: Expression.New(typeof(ValueTuple<bool, T>)));

            var lambda =
                Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        type: typeof(ValueTuple<bool, T>),
                        variables: innerResult.Variables.Append(resultExpression),
                        innerBody,
                        Expression.Assign(resultExpression, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!,
                            innerResult.Success,
                            context.DiscardResult ? Expression.Default(typeof(T)) : innerResult.Value)),
                        returnExpression,
                        returnLabel),
                    true,
                    context.ParseContext
                );

#if DEBUG
            context.Lambdas.Add(lambda);
#endif

            _closure.Func = lambda.CompileFast(ifFastFailedReturnNull: false, ExpressionHelper.CompilerFlags);
        }

        var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"unaryDef{context.NextNumber}");
        result.Variables.Add(deferred);

        var closureScope = Expression.Constant(_closure);
        var getFunc = typeof(Closure).GetMember(nameof(Closure.Func))[0];
        var funcType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
        var funcAccess = Expression.MakeMemberAccess(closureScope, getFunc);
        var cast = Expression.Convert(funcAccess, funcType);

        result.Body.Add(Expression.Assign(deferred, Expression.Invoke(cast, context.ParseContext)));
        result.Body.Add(Expression.Assign(result.Success, Expression.Field(deferred, "Item1")));
        result.Body.Add(context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, Expression.Field(deferred, "Item2")));

        return result;
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}

public sealed class UnaryWithContext<T, TInput> : Parser<T>, ICompilable
{
    private readonly Parser<T> _parser;
    private readonly (Parser<TInput> Op, Func<ParseContext, T, T> Factory)[] _operators;

    public UnaryWithContext(Parser<T> parser, (Parser<TInput> op, Func<ParseContext, T, T> factory)[] operators)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _operators = operators ?? throw new ArgumentNullException(nameof(operators));

        if (_operators.Length == 0)
        {
            throw new ArgumentException("At least one operator must be provided.", nameof(operators));
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        foreach (var (op, factory) in _operators)
        {
            var operatorPosition = context.Scanner.Cursor.Position;
            var operatorResult = new ParseResult<TInput>();

            if (op.Parse(context, ref operatorResult))
            {
                if (Parse(context, ref result))
                {
                    result = new ParseResult<T>(result.Start, result.End, factory(context, result.Value));
                    context.ExitParser(this);
                    return true;
                }

                context.Scanner.Cursor.ResetPosition(operatorPosition);
                context.ExitParser(this);
                return false;
            }
        }

        var success = _parser.Parse(context, ref result);

        context.ExitParser(this);
        return success;
    }

    private bool _initialized;
    private readonly Closure _closure = new();

    private sealed class Closure
    {
        public object? Func;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        if (!_initialized)
        {
            _initialized = true;

            var innerContext = context;
            var innerResult = innerContext.CreateCompilationResult<T>();

            var nextNum = innerContext.NextNumber;
            var matchedFactory = innerResult.DeclareVariable<Func<ParseContext, T, T>>($"unaryCtxFactory{nextNum}");
            var operatorMatched = innerResult.DeclareVariable<bool>($"unaryCtxOpMatched{nextNum}");
            var operatorPosition = innerResult.DeclareVariable<TextPosition>($"unaryCtxPos{nextNum}");

            var baseParserResult = _parser.Build(innerContext);

            var operatorCheckExpressions = new List<Expression>();
            var allOperatorVariables = new List<ParameterExpression>();

            for (int i = 0; i < _operators.Length; i++)
            {
                var (op, factory) = _operators[i];
                var opCompileResult = op.Build(innerContext);

                allOperatorVariables.AddRange(opCompileResult.Variables);

                var factoryConst = Expression.Constant(factory);

                if (i == 0)
                {
                    operatorCheckExpressions.AddRange(opCompileResult.Body);
                    operatorCheckExpressions.Add(
                        Expression.IfThen(
                            opCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(operatorMatched, Expression.Constant(true)),
                                Expression.Assign(matchedFactory, factoryConst)
                            )
                        )
                    );
                }
                else
                {
                    operatorCheckExpressions.Add(
                        Expression.IfThen(
                            Expression.Not(operatorMatched),
                            Expression.Block(
                                opCompileResult.Body.Concat(new Expression[]
                                {
                                    Expression.IfThen(
                                        opCompileResult.Success,
                                        Expression.Block(
                                            Expression.Assign(operatorMatched, Expression.Constant(true)),
                                            Expression.Assign(matchedFactory, factoryConst)
                                        )
                                    )
                                })
                            )
                        )
                    );
                }
            }

            var scanner = Expression.Field(innerContext.ParseContext, nameof(ParseContext.Scanner));
            var cursor = Expression.Field(scanner, nameof(Scanner.Cursor));
            var cursorPosition = Expression.Property(cursor, nameof(Cursor.Position));
            var resetPosition = typeof(Cursor).GetMethod(nameof(Cursor.ResetPosition), [typeof(TextPosition).MakeByRefType()])!;

            var closureConst = Expression.Constant(_closure);
            var getFuncs = typeof(Closure).GetMember(nameof(Closure.Func))[0];
            var funcReturnType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
            var funcsAccess = Expression.MakeMemberAccess(closureConst, getFuncs);
            var castFunc = Expression.Convert(funcsAccess, funcReturnType);

            var recursiveResult = Expression.Variable(typeof(ValueTuple<bool, T>), $"recursiveUnaryCtx{nextNum}");
            innerResult.Variables.Add(recursiveResult);

            var innerBody = Expression.Block(
                allOperatorVariables.Concat(baseParserResult.Variables),
                new Expression[]
                {
                    Expression.Assign(operatorMatched, Expression.Constant(false)),
                    Expression.Assign(matchedFactory, Expression.Constant(null, typeof(Func<ParseContext, T, T>))),
                    Expression.Assign(operatorPosition, cursorPosition)
                }
                .Concat(operatorCheckExpressions)
                .Concat([
                    Expression.IfThenElse(
                        operatorMatched,
                        Expression.Block(
                            Expression.Assign(recursiveResult, Expression.Invoke(castFunc, innerContext.ParseContext)),
                            Expression.IfThenElse(
                                Expression.Field(recursiveResult, "Item1"),
                                Expression.Block(
                                    Expression.Assign(innerResult.Success, Expression.Constant(true)),
                                    context.DiscardResult
                                        ? Expression.Invoke(matchedFactory, innerContext.ParseContext, Expression.Field(recursiveResult, "Item2"))
                                        : Expression.Assign(innerResult.Value, Expression.Invoke(matchedFactory, innerContext.ParseContext, Expression.Field(recursiveResult, "Item2")))
                                ),
                                Expression.Call(cursor, resetPosition, operatorPosition)
                            )
                        ),
                        Expression.Block(
                            baseParserResult.Body.Concat(new Expression[]
                            {
                                Expression.IfThen(
                                    baseParserResult.Success,
                                    Expression.Block(
                                        Expression.Assign(innerResult.Success, Expression.Constant(true)),
                                        context.DiscardResult ? Expression.Empty() : Expression.Assign(innerResult.Value, baseParserResult.Value)
                                    )
                                )
                            })
                        )
                    )
                ])
            );

            var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"unaryCtxResult{context.NextNumber}");
            var returnTarget = Expression.Label(typeof(ValueTuple<bool, T>));
            var returnExpression = Expression.Return(returnTarget, resultExpression, typeof(ValueTuple<bool, T>));
            var returnLabel = Expression.Label(returnTarget, defaultValue: Expression.New(typeof(ValueTuple<bool, T>)));

            var lambda =
                Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        type: typeof(ValueTuple<bool, T>),
                        variables: innerResult.Variables.Append(resultExpression),
                        innerBody,
                        Expression.Assign(resultExpression, Expression.New(
                            typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!,
                            innerResult.Success,
                            context.DiscardResult ? Expression.Default(typeof(T)) : innerResult.Value)),
                        returnExpression,
                        returnLabel),
                    true,
                    context.ParseContext
                );

#if DEBUG
            context.Lambdas.Add(lambda);
#endif

            _closure.Func = lambda.CompileFast(ifFastFailedReturnNull: false, ExpressionHelper.CompilerFlags);
        }

        var deferred = Expression.Variable(typeof(ValueTuple<bool, T>), $"unaryCtxDef{context.NextNumber}");
        result.Variables.Add(deferred);

        var closureScope = Expression.Constant(_closure);
        var getFunc = typeof(Closure).GetMember(nameof(Closure.Func))[0];
        var funcType = typeof(Func<ParseContext, ValueTuple<bool, T>>);
        var funcAccess = Expression.MakeMemberAccess(closureScope, getFunc);
        var cast = Expression.Convert(funcAccess, funcType);

        result.Body.Add(Expression.Assign(deferred, Expression.Invoke(cast, context.ParseContext)));
        result.Body.Add(Expression.Assign(result.Success, Expression.Field(deferred, "Item1")));
        result.Body.Add(context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, Expression.Field(deferred, "Item2")));

        return result;
    }

    public override string ToString() => Name ?? $"Unary({_parser})";
}
