using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Selects a parser instance at runtime and delegates parsing to it.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type to use.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
public sealed class Select<C, T> : Parser<T>, ICompilable, ISourceable where C : ParseContext
{
    private readonly Parser<T>[] _parsers;
    private readonly Func<C, int> _selector;

    public Select(Func<C, int> selector, params Parser<T>[] parsers)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));

        ThrowHelper.ThrowIfNull(parsers, nameof(parsers));

        _parsers = parsers;

        for (var i = 0; i < _parsers.Length; i++)
        {
            _parsers[i] = _parsers[i] ?? throw new ArgumentException("Parsers array must not contain null elements.", nameof(parsers));
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var index = _selector((C)context);

        if ((uint)index >= (uint)_parsers.Length)
        {
            context.ExitParser(this);
            return false;
        }

        var nextParser = _parsers[index];

        var parsed = new ParseResult<T>();

        if (nextParser.Parse(context, ref parsed))
        {
            result.Set(parsed.Start, parsed.End, parsed.Value);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();
        var index = Expression.Variable(typeof(int), $"index{context.NextNumber}");

        var cases = new SwitchCase[_parsers.Length];

        for (var i = 0; i < _parsers.Length; i++)
        {
            var parserCompileResult = _parsers[i].Build(context);

            Expression caseBody = Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.Assign(result.Success, parserCompileResult.Success),
                context.DiscardResult
                    ? Expression.Empty()
                    : Expression.IfThen(result.Success, Expression.Assign(result.Value, parserCompileResult.Value))
            );

            cases[i] = Expression.SwitchCase(caseBody, Expression.Constant(i));
        }

        var selectorInvoke = Expression.Invoke(
            Expression.Constant(_selector),
            Expression.Convert(context.ParseContext, typeof(C)));

        var body = Expression.Block(
            [index],
            Expression.Assign(index, selectorInvoke),
            Expression.Switch(index, Expression.Empty(), cases)
        );

        result.Body.Add(body);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        for (var i = 0; i < _parsers.Length; i++)
        {
            if (_parsers[i] is not ISourceable)
            {
                throw new NotSupportedException("Select requires all target parsers to be source-generatable.");
            }
        }

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Register the selector lambda
        var selectorLambda = context.RegisterLambda(_selector);

        var indexName = $"index{context.NextNumber()}";
        result.Body.Add($"var {indexName} = {selectorLambda}(({SourceGenerationContext.GetTypeName(typeof(C))}){ctx});");
        result.Body.Add($"switch ({indexName})");
        result.Body.Add("{");

        for (var i = 0; i < _parsers.Length; i++)
        {
            var parser = _parsers[i];
            var parserSourceable = (ISourceable)parser;

            var helperName = context.Helpers
                .GetOrCreate(parser, $"{context.MethodNamePrefix}_Select_{i}", valueTypeName, () => parserSourceable.GenerateSource(context))
                .MethodName;

            var caseValueName = $"caseValue{context.NextNumber()}";

            result.Body.Add($"    case {i}:");
            result.Body.Add("    {");
            result.Body.Add($"        if ({helperName}({ctx}, out var {caseValueName}))");
            result.Body.Add("        {");
            result.Body.Add($"            {result.SuccessVariable} = true;");
            if (!context.DiscardResult)
            {
                result.Body.Add($"            {result.ValueVariable} = {caseValueName};");
            }
            result.Body.Add("        }");
            result.Body.Add("        break;");
            result.Body.Add("    }");
        }

        result.Body.Add("}");

        return result;
    }

    public override string ToString() => "(Select)";
}
