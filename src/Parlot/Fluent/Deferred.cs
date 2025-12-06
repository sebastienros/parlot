using FastExpressionCompiler;
using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

#if NET
using System.Linq;
#endif
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class Deferred<T> : Parser<T>, ICompilable, ISeekable, ISourceable
{
    private Parser<T>? _parser;

    public Parser<T>? Parser
    {
        get => _parser;
        set
        {
            _parser = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Deferred()
    {
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

        // Check for infinite recursion at the same position (unless disabled)
        if (!context.DisableLoopDetection && context.IsParserActiveAtPosition(this))
        {
            // Cycle detected at this position - fail gracefully instead of stack overflow
            return false;
        }

        // Remember the position where we entered this parser
        var entryPosition = context.Scanner.Cursor.Position.Offset;

        // Mark this parser as active at the current position (unless loop detection is disabled)
        var trackPosition = !context.DisableLoopDetection && context.PushParserAtPosition(this);

        context.EnterParser(this);

        var outcome = Parser.Parse(context, ref result);

        context.ExitParser(this);

        // Mark this parser as inactive at the entry position (only if we tracked it)
        if (trackPosition)
        {
            context.PopParserAtPosition(this, entryPosition);
        }

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

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (Parser is null)
        {
            throw new InvalidOperationException("Can't generate source for a Deferred parser until it is fully initialized");
        }

        // Check if this deferred parser is already being generated (recursion)
        var methodName = context.Deferred.GetOrCreateMethodName(this, Name ?? "Deferred");

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Generate a call to the helper method
        var callResultName = $"deferredResult{context.NextNumber()}";
        result.Body.Add($"global::System.ValueTuple<bool, {valueTypeName}> {callResultName} = default;");
        result.Body.Add($"{callResultName} = {methodName}({ctx});");
        result.Body.Add($"{result.SuccessVariable} = {callResultName}.Item1;");
        result.Body.Add($"{result.ValueVariable} = {callResultName}.Item2;");

        return result;
    }

    public override string ToString()
    {
        // Handle recursion

        lock (this)
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
