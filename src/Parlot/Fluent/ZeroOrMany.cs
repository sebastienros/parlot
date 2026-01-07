using Parlot.Compilation;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class ZeroOrMany<T> : Parser<IReadOnlyList<T>>, ICompilable, ISourceable
{
    private static readonly MethodInfo _listAdd = typeof(List<T>).GetMethod("Add")!;

    private readonly Parser<T> _parser;

    public ZeroOrMany(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        HybridList<T>? results = null;

        var start = 0;
        var end = 0;

        var first = true;
        var parsed = new ParseResult<T>();

        // TODO: it's not restoring an intermediate failed text position
        // is the inner parser supposed to be clean?

        while (_parser.Parse(context, ref parsed))
        {
            if (first)
            {
                results = [];
                first = false;
                start = parsed.Start;
            }

            end = parsed.End;
            
            results!.Add(parsed.Value);
        }

        result.Set(start, end, results ?? (IReadOnlyList<T>)[]);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<IReadOnlyList<T>>(true, ExpressionHelper.ArrayEmpty<T>());

        var results = result.DeclareVariable<List<T>>($"results{context.NextNumber}");
        var first = result.DeclareVariable<bool>($"first{context.NextNumber}", Expression.Constant(true));

        // success = true;
        //
        // IReadonlyList<T> value = Array.Empty<T>();
        // List<T> results = null;
        //
        // while (true)
        // {
        //
        //   parse1 instructions
        // 
        //   if (parser1.Success)
        //   {
        //      if (results == null)
        //      {
        //          results = new List<T>();
        //          value = results;
        //      }
        //
        //      results.Add(parse1.Value);
        //   }
        //   else
        //   {
        //      break;
        //   }
        //
        //   if (context.Scanner.Cursor.Eof)
        //   {
        //      break;
        //   }
        // }

        var parserCompileResult = _parser.Build(context);

        var breakLabel = Expression.Label($"break{context.NextNumber}");

        var block =
            Expression.Loop(
                Expression.Block(
                    parserCompileResult.Variables,
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        context.DiscardResult
                        ? Expression.Empty()
                        : Expression.Block(
                            Expression.IfThen(
                                Expression.IsTrue(first),
                                Expression.Block(
                                    Expression.Assign(first, Expression.Constant(false)),
                                    Expression.Assign(results, ExpressionHelper.New<List<T>>()),
                                    Expression.Assign(result.Value, results)
                                    )
                                ),
                            Expression.Call(results, _listAdd, parserCompileResult.Value)
                            ),
                        Expression.Break(breakLabel)
                        ),
                    Expression.IfThen(
                        context.Eof(),
                        Expression.Break(breakLabel)
                        )),
                breakLabel
                );

        result.Body.Add(block);

        return result;
    }

    public override string ToString() => $"{_parser}*";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("ZeroOrMany requires a source-generatable parser.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>), defaultSuccess: true, defaultValueExpression: $"global::System.Array.Empty<{elementTypeName}>()");
        var ctx = context.ParseContextName;

        var listName = $"list{context.NextNumber()}";
        var firstName = $"first{context.NextNumber()}";

        if (!context.DiscardResult)
        {
            result.Body.Add($"System.Collections.Generic.List<{elementTypeName}>? {listName} = null;");
            result.Body.Add($"bool {firstName} = true;");
        }

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
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_ZeroOrMany_Parser", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add("while (true)");
        result.Body.Add("{");
        result.Body.Add($"    if (!{helperName}({ctx}, out var itemValue{context.NextNumber()}))");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    if ({firstName})");
            result.Body.Add("    {");
            result.Body.Add($"        {listName} = new System.Collections.Generic.List<{elementTypeName}>();");
            result.Body.Add($"        {result.ValueVariable} = {listName};");
            result.Body.Add($"        {firstName} = false;");
            result.Body.Add("    }");
            result.Body.Add($"    {listName}!.Add(itemValue{context.NextNumber() - 1});");
        }
        result.Body.Add("}");
        if (!context.DiscardResult)
        {
            result.Body.Add($"if ({listName} is null)");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = global::System.Array.Empty<{elementTypeName}>();");
            result.Body.Add("}");
        }

        return result;
    }
}
