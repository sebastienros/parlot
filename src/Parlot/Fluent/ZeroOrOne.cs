using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class ZeroOrOne<T> : Parser<T>, ICompilable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly T _defaultValue;

    public ZeroOrOne(Parser<T> parser, T defaultValue)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _defaultValue = defaultValue;
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        var success = _parser.Parse(context, ref parsed);

        result.Set(parsed.Start, parsed.End, success ? parsed.Value : _defaultValue);

        // ZeroOrOne always succeeds
        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>(true, Expression.Constant(_defaultValue, typeof(T)));

        // T value = _defaultValue;
        //
        // parse1 instructions
        // 
        // value = new OptionalResult<T>(parser1.Success, parse1.Value);
        //

        var parserCompileResult = _parser.Build(context);

        var block = Expression.Block(
            parserCompileResult.Variables,
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    context.DiscardResult
                        ? Expression.Empty()
                        : Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Assign(result.Value, parserCompileResult.Value),
                            Expression.Assign(result.Value, Expression.Constant(_defaultValue, typeof(T)))
                        )
                    )
                );

        result.Body.Add(block);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("ZeroOrOne requires a source-generatable parser.");
        }

        var defaultValueExpr = _defaultValue == null ? "default" : SourceGenerationContext.GetTypeName(typeof(T)) + ".Parse(\"" + _defaultValue?.ToString() + "\")";
        if (_defaultValue == null || _defaultValue.Equals(default(T)))
        {
            defaultValueExpr = "default";
        }
        else if (typeof(T) == typeof(string))
        {
            defaultValueExpr = "\"" + _defaultValue?.ToString()?.Replace("\"", "\\\"") + "\"";
        }
        else if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
        {
            defaultValueExpr = _defaultValue?.ToString() ?? "default";
        }

        var result = context.CreateResult(typeof(T), defaultSuccess: true, defaultValueExpression: defaultValueExpr);

        static Type GetParserValueType(object parser)
        {
            var type = parser.GetType();
            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition().FullName == "Parlot.Fluent.Parser`1")
                {
                    return type.GetGenericArguments()[0];
                }
                type = type.BaseType!;
            }
            throw new InvalidOperationException("Unable to determine parser value type.");
        }

        var valueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(sourceable));
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_ZeroOrOne_Parser", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add($"if ({helperName}({context.ParseContextName}, out {result.ValueVariable}))");
        result.Body.Add("{");
        result.Body.Add($"    // Value already assigned via out parameter");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser}?";
}
