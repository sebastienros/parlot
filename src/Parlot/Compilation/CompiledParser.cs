using Parlot.Fluent;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Compilation;

/// <summary>
/// Marker interface to detect a Parser has already been compiled.
/// </summary>
public interface ICompiledParser
{

}

/// <summary>
/// An instance of this class encapsulates the result of a compiled parser
/// in order to expose it as a standard parser contract.
/// </summary>
/// <remarks>
/// This class is used in <see cref="Parser{T}.Compile"/>.
/// It implements <see cref="ISourceable"/> to support source generation when
/// the compiled parser is used as part of another parser composition.
/// </remarks>
public class CompiledParser<T> : Parser<T>, ICompiledParser, ISourceable
{
    private readonly Func<ParseContext, ValueTuple<bool, T>> _parse;

    public Parser<T> Source { get; }

    public CompiledParser(Func<ParseContext, ValueTuple<bool, T>> parse, Parser<T> source)
    {
        Name = "Compiled";
        _parse = parse ?? throw new ArgumentNullException(nameof(parse));
        Source = source;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;
        var start = cursor.Offset;
        var parsed = _parse(context);

        if (parsed.Item1)
        {
            result.Set(start, cursor.Offset, parsed.Item2);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    /// <summary>
    /// Generates source code for this compiled parser by delegating to the source parser.
    /// </summary>
    /// <remarks>
    /// If the underlying <see cref="Source"/> parser implements <see cref="ISourceable"/>,
    /// this method delegates to it. Otherwise, it falls back to invoking the compiled
    /// delegate directly via a registered lambda.
    /// </remarks>
    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        // If the source parser implements ISourceable, delegate to it for optimal code generation
        if (Source is ISourceable sourceable)
        {
            return sourceable.GenerateSource(context);
        }

        // Otherwise, fall back to calling the compiled delegate via a lambda
        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var typeName = SourceGenerationContext.GetTypeName(typeof(T));

        var parseResultName = $"parseResult{context.NextNumber()}";

        // Register a lambda that returns this compiled parser instance
        var compiledParserLambda = context.RegisterLambda(new Func<CompiledParser<T>>(() => this));

        result.Body.Add($"var {parseResultName} = new global::Parlot.ParseResult<{typeName}>();");
        result.Body.Add($"{result.SuccessVariable} = {compiledParserLambda}().Parse({ctx}, ref {parseResultName});");
        result.Body.Add($"if ({result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {result.ValueVariable} = {parseResultName}.Value;");
        result.Body.Add("}");

        return result;
    }
}
