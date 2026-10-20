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

    private readonly object _lockObject = new();
    private readonly bool _seekable;
    private Parser<T>? _parser;

    public Parser<T>? Parser
    {
        get => _parser;
        set
        {
            _parser = value ?? throw new ArgumentNullException(nameof(value));

            // The hints follow the parser, otherwise a OneOf built after a replacement would skip this parser
            if (_seekable)
            {
                if (_parser is ISeekable seekable)
                {
                    CanSeek = seekable.CanSeek;
                    ExpectedChars = seekable.ExpectedChars;
                    SkipWhitespace = seekable.SkipWhitespace;
                }
                else
                {
                    CanSeek = false;
                    ExpectedChars = [];
                    SkipWhitespace = false;
                }
            }
        }
    }

    public bool CanSeek { get; private set; }

    public char[] ExpectedChars { get; private set; } = [];

    public bool SkipWhitespace { get; private set; }

    public Deferred()
    {
    }

    public Deferred(Func<Deferred<T>, Parser<T>> parser) : this()
    {
        // The parser is known when this one is built, unlike one that is set later, so it can declare
        // what the parser can start with
        _seekable = true;

        Parser = parser(this);
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        var parser = Parser;

        if (parser is null)
        {
            throw new InvalidOperationException("Parser has not been initialized");
        }

        var limitRecursion = context.MaxRecursionDepth > 0;

        if (limitRecursion)
        {
            context.EnterRecursion();
        }

        var trackPosition = !context.DisableLoopDetection;

        // Remember the position where we entered this parser
        var entryPosition = 0;

        if (trackPosition)
        {
            entryPosition = context.Scanner.Cursor.Offset;

            // Marking the parser as active also detects a cycle at this position, which saves a lookup
            // compared to checking for the cycle first.
            if (!context.PushParserAtPosition(this))
            {
                // Cycle detected at this position - fail gracefully instead of stack overflow
                if (limitRecursion)
                {
                    context.ExitRecursion();
                }

                return false;
            }
        }

        try
        {
            context.EnterParser(this);

            var outcome = parser.Parse(context, ref result);

            context.ExitParser(this);

            return outcome;
        }
        finally
        {
            // Marked as inactive even when a parser throws, otherwise a context that is reused
            // after the exception was handled would report a cycle for this position
            if (trackPosition)
            {
                context.PopParserAtPosition(this, entryPosition);
            }

            if (limitRecursion)
            {
                context.ExitRecursion();
            }
        }
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

            var limitRecursion = Expression.Variable(typeof(bool), $"limitRecursion{context.NextNumber}");
            var trackPosition = Expression.Variable(typeof(bool), $"trackPosition{context.NextNumber}");
            var entryPosition = Expression.Variable(typeof(int), $"entryPosition{context.NextNumber}");

            var maxRecursionDepthProperty = Expression.Property(context.ParseContext, nameof(ParseContext.MaxRecursionDepth));
            var disableLoopDetectionProperty = Expression.Property(context.ParseContext, nameof(ParseContext.DisableLoopDetection));
            var enterRecursionMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.EnterRecursion), [])!;
            var exitRecursionMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.ExitRecursion), [])!;
            var pushParserMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.PushParserAtPosition), [typeof(object)])!;
            var popParserMethod = typeof(ParseContext).GetMethod(nameof(ParseContext.PopParserAtPosition), [typeof(object), typeof(int)])!;
            var selfConstant = Expression.Constant(this, typeof(object));

            var lambda =
                Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                    Expression.Block(
                        type: typeof(ValueTuple<bool, T>),
                        variables: parserCompileResult.Variables.Append(resultExpression).Append(limitRecursion).Append(trackPosition).Append(entryPosition),
                        Expression.Assign(limitRecursion, Expression.GreaterThan(maxRecursionDepthProperty, Expression.Constant(0))),
                        Expression.IfThen(limitRecursion, Expression.Call(context.ParseContext, enterRecursionMethod)),
                        Expression.Assign(trackPosition, Expression.Not(disableLoopDetectionProperty)),
                        Expression.Assign(entryPosition, Expression.Constant(0)),
                        Expression.IfThen(
                            trackPosition,
                            Expression.Block(
                                Expression.Assign(entryPosition, context.Offset()),
                                Expression.IfThen(
                                    Expression.Not(Expression.Call(context.ParseContext, pushParserMethod, selfConstant)),
                                    Expression.Block(
                                        Expression.IfThen(limitRecursion, Expression.Call(context.ParseContext, exitRecursionMethod)),
                                        returnExpression
                                    )
                                )
                            )
                        ),
                        Expression.TryFinally(
                            Expression.Block(
                                Expression.Block(parserCompileResult.Body),
                                Expression.Assign(resultExpression, Expression.New(
                                    typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!,
                                    parserCompileResult.Success,
                                    context.DiscardResult ? Expression.Default(parserCompileResult.Value.Type) : parserCompileResult.Value))
                            ),
                            Expression.Block(
                                Expression.IfThen(
                                    trackPosition,
                                    Expression.Call(context.ParseContext, popParserMethod, selfConstant, entryPosition)
                                ),
                                Expression.IfThen(
                                    limitRecursion,
                                    Expression.Call(context.ParseContext, exitRecursionMethod)
                                )
                            )
                        ),
                        returnExpression,
                        returnLabel),
                    true,
                    context.ParseContext
                    );

            // Store the source lambda for debugging
#if DEBUG
            context.Lambdas.Add(lambda);
#endif

            _closure.Func = lambda.CompileFast(ifFastFailedReturnNull: false, ExpressionHelper.CompilerFlags);
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

    private bool _toString;

    public override string ToString()
    {
        // Handle recursion

        lock (_lockObject)
        {
            if (!_toString)
            {
                _toString = true;
                var result = Name == null
                    ? $"{Parser} (Deferred)"
                    : $"{Name} (Deferred)";
                _toString = false;
                return result;
            }
            else
            {
                return "(Deferred)";
            }
        }
    }
}
