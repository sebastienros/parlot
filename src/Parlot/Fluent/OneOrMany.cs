using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class OneOrMany<T> : Parser<IReadOnlyList<T>>, ICompilable, ISeekable, ISourceable
{
    private readonly Parser<T> _parser;
    private static readonly MethodInfo _listAddMethodInfo = typeof(List<T>).GetMethod("Add")!;

    public OneOrMany(Parser<T> parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));

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

    public override bool Parse(ParseContext context, ref ParseResult<IReadOnlyList<T>> result)
    {
        context.EnterParser(this);

        var parsed = new ParseResult<T>();

        if (!_parser.Parse(context, ref parsed))
        {
            return false;
        }

        var start = parsed.Start;
        var results = new HybridList<T>();

        int end;

        do
        {
            end = parsed.End;
            results.Add(parsed.Value);

        } while (_parser.Parse(context, ref parsed));

        result.Set(start, end, results);

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<IReadOnlyList<T>>();
        var results = result.DeclareVariable<List<T>>($"results{context.NextNumber}", Expression.New(typeof(List<T>)));

        // value = new List<T>();
        //
        // while (true)
        // {
        //   parse1 instructions
        // 
        //   if (parser1.Success)
        //   {
        //      value.Add(parse1.Value);
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
        //
        // if (value.Count > 0)
        // {
        //     success = true;
        //     result = value;
        // }
        // 

        var parserCompileResult = _parser.Build(context);

        var breakLabel = Expression.Label($"exitWhile{context.NextNumber}");

        var block = Expression.Block(
            parserCompileResult.Variables,
            Expression.Loop(
                Expression.Block(
                    Expression.Block(parserCompileResult.Body),
                    Expression.IfThenElse(
                        parserCompileResult.Success,
                        Expression.Block(
                            context.DiscardResult ?
                                Expression.Empty() :
                                Expression.Call(results, _listAddMethodInfo, parserCompileResult.Value),
                            Expression.Assign(result.Success, Expression.Constant(true))
                            ),
                        Expression.Break(breakLabel)
                        ),
                    Expression.IfThen(
                        context.Eof(),
                        Expression.Break(breakLabel)
                        )),
                breakLabel),
            context.DiscardResult ?
                Expression.Empty() :
                Expression.Assign(result.Value, results)
        );

        result.Body.Add(block);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("OneOrMany requires a source-generatable parser.");
        }

        var elementTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var result = context.CreateResult(typeof(IReadOnlyList<T>));

        var listName = $"list{context.NextNumber()}";

        if (!context.DiscardResult)
        {
            result.Body.Add($"System.Collections.Generic.List<{elementTypeName}>? {listName} = null;");
        }
        result.Body.Add($"{result.SuccessVariable} = false;");

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
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_OneOrMany_Parser", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add("while (true)");
        result.Body.Add("{");
        result.Body.Add($"    if (!{helperName}({context.ParseContextName}, out var itemValue{context.NextNumber()}))");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    if ({listName} == null)");
            result.Body.Add("    {");
            result.Body.Add($"        {listName} = new System.Collections.Generic.List<{elementTypeName}>();");
            result.Body.Add("    }");
            result.Body.Add($"    {listName}!.Add(itemValue{context.NextNumber() - 1});");
        }
        result.Body.Add($"    {result.SuccessVariable} = true;");
        result.Body.Add("}");
        if (!context.DiscardResult)
        {
            result.Body.Add($"if ({listName} != null)");
            result.Body.Add("{");
            result.Body.Add($"    {result.ValueVariable} = {listName};");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser}+";
}
