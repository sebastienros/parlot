using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Adapts an IParser&lt;T&gt; to a Parser&lt;T&gt; for use in contexts that require Parser.
/// This is used internally to support covariance.
/// </summary>
internal sealed class IParserAdapter<T> : Parser<T>, ISeekable, ICompilable, ISourceable
{
    private readonly IParser<T> _parser;

    public IParserAdapter(IParser<T> parser)
    {
        _parser = parser ?? throw new System.ArgumentNullException(nameof(parser));

        // Forward ISeekable properties from the wrapped parser if it implements ISeekable
        if (_parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        var success = _parser.Parse(context, out int start, out int end, out object? value);
        if (success)
        {
            result.Set(start, end, (T)value!);
        }
        return success;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        // If the wrapped parser is actually a Parser<T>, delegate compilation to it
        if (_parser is Parser<T> parser)
        {
            return parser.Build(context);
        }

        // Otherwise, fall back to the default non-compilable behavior
        // This uses the Parse method which will work with any IParser<T>
        var result = context.CreateCompilationResult<T>(false);

        // ParseResult<T> parseResult;
        var parseResult = Expression.Variable(typeof(ParseResult<T>), $"value{context.NextNumber}");
        result.Variables.Add(parseResult);

        // success = this.Parse(context.ParseContext, ref parseResult)
        result.Body.Add(
            Expression.Assign(result.Success,
                Expression.Call(
                    Expression.Constant(this),
                    GetType().GetMethod("Parse", [typeof(ParseContext), typeof(ParseResult<T>).MakeByRefType()])!,
                    context.ParseContext,
                    parseResult))
            );

        if (!context.DiscardResult)
        {
            var value = result.Value = Expression.Variable(typeof(T), $"value{context.NextNumber}");
            result.Variables.Add(value);

            result.Body.Add(
                Expression.IfThen(
                    result.Success,
                    Expression.Assign(value, Expression.Field(parseResult, "Value"))
                    )
                );
        }

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // If the wrapped parser is actually a Parser<T> that implements ISourceable, delegate to it
        if (_parser is Parser<T> { } parser && parser is ISourceable sourceable)
        {
            return sourceable.GenerateSource(context);
        }

        // Otherwise, fall back to using the Parse method
        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;

        var parseResultName = $"parseResult{context.NextNumber()}";

        // Create a lambda that captures this instance
        var adapterLambda = context.RegisterLambda(new Func<IParserAdapter<T>>(() => this));

        result.Body.Add($"var {parseResultName} = new global::Parlot.ParseResult<{SourceGenerationContext.GetTypeName(typeof(T))}>();");
        result.Body.Add($"{result.SuccessVariable} = {adapterLambda}().Parse({ctx}, ref {parseResultName});");
        result.Body.Add($"if ({result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {parseResultName}.Value;");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => _parser.ToString() ?? "IParserAdapter";
}
