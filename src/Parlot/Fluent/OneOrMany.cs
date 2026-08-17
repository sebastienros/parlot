using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class OneOrMany<T> : Parser<IReadOnlyList<T>>, ISeekable, ISourceable
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
        var previousOffset = context.Scanner.Cursor.Offset;
        if (!_parser.Parse(context, ref parsed)
            || context.Scanner.Cursor.Offset == previousOffset)
        {
            context.ExitParser(this);
            return false;
        }

        var start = parsed.Start;
        var end = parsed.End;
        var results = new HybridList<T>
        {
            parsed.Value
        };

        while (true)
        {
            previousOffset = context.Scanner.Cursor.Offset;
            if (!_parser.Parse(context, ref parsed)
                || context.Scanner.Cursor.Offset == previousOffset)
            {
                break;
            }

            end = parsed.End;
            results.Add(parsed.Value);
        }

        result.Set(start, end, results.AsReadOnlyList());

        context.ExitParser(this);
        return true;
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
        var previousOffsetName = $"previousOffset{context.NextNumber()}";
        var itemValueName = $"itemValue{context.NextNumber()}";

        result.Body.Add("while (true)");
        result.Body.Add("{");
        result.Body.Add($"    var {previousOffsetName} = {context.CursorName}.Offset;");
        result.Body.Add($"    if (!{helperName}({context.ParseContextName}, out var {itemValueName}) || {context.CursorName}.Offset == {previousOffsetName})");
        result.Body.Add("    {");
        result.Body.Add("        break;");
        result.Body.Add("    }");
        if (!context.DiscardResult)
        {
            result.Body.Add($"    if ({listName} == null)");
            result.Body.Add("    {");
            result.Body.Add($"        {listName} = new System.Collections.Generic.List<{elementTypeName}>();");
            result.Body.Add("    }");
            result.Body.Add($"    {listName}!.Add({itemValueName});");
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
