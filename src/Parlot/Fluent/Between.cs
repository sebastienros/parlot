using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// This parser parses a value between two other parsers. It returns the value parsed by the middle parser
/// making it easier to skip delimiters than writing <code>a.SkipAnd(b).AndSkip(c)</code>.
/// </summary>
/// <typeparam name="A">The type of the parser before the main parser.</typeparam>
/// <typeparam name="T">The type of the value parsed by the main parser.</typeparam>
/// <typeparam name="B">The type of the parser after the main parser.</typeparam>
public sealed class Between<A, T, B> : Parser<T>, ICompilable, ISeekable, ISourceable
{
    private readonly Parser<T> _parser;
    private readonly Parser<A> _before;
    private readonly Parser<B> _after;

    public Between(Parser<A> before, Parser<T> parser, Parser<B> after)
    {
        _before = before ?? throw new ArgumentNullException(nameof(before));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _after = after ?? throw new ArgumentNullException(nameof(after));

        if (_before is ISeekable seekable)
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
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        var start = cursor.Position;

        var parsedA = new ParseResult<A>();

        if (!_before.Parse(context, ref parsedA))
        {
            context.ExitParser(this);

            // Don't reset position since _before should do it
            return false;
        }

        if (!_parser.Parse(context, ref result))
        {
            cursor.ResetPosition(start);

            context.ExitParser(this);
            return false;
        }

        var parsedB = new ParseResult<B>();

        if (!_after.Parse(context, ref parsedB))
        {
            cursor.ResetPosition(start);

            context.ExitParser(this);
            return false;
        }

        context.ExitParser(this);
        return true;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        var start = context.DeclarePositionVariable(result);

        var beforeCR = _before.Build(context);
        var parserCR = _parser.Build(context);
        var afterCR = _after.Build(context);

        // Build the block: before -> parser -> after with resets on failure
        var block = Expression.Block(
            beforeCR.Variables,
            Expression.Block(beforeCR.Body),
            Expression.IfThen(
                beforeCR.Success,
                Expression.Block(
                    parserCR.Variables,
                    Expression.Block(parserCR.Body),
                    Expression.IfThen(
                        parserCR.Success,
                        Expression.Block(
                            afterCR.Variables,
                            Expression.Block(afterCR.Body),
                            Expression.IfThenElse(
                                afterCR.Success,
                                Expression.Block(
                                    context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, parserCR.Value),
                                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                                ),
                                context.ResetPosition(start)
                            )
                        )
                    )
                )
            )
        );

        result.Body.Add(block);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_before is not ISourceable beforeSourceable || _parser is not ISourceable parserSourceable || _after is not ISourceable afterSourceable)
        {
            throw new NotSupportedException("Between requires all parsers to be source-generatable.");
        }

        var result = context.CreateResult(typeof(T));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Position;");
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

        string Helper(ISourceable p, string suffix)
        {
            var valueTypeName = SourceGenerationContext.GetTypeName(GetParserValueType(p));
            return context.Helpers
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Between_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperBefore = Helper(beforeSourceable, "Before");
        var helperParser = Helper(parserSourceable, "Parser");
        var helperAfter = Helper(afterSourceable, "After");

        result.Body.Add($"if ({helperBefore}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        result.Body.Add($"    if ({helperParser}({context.ParseContextName}, out {result.ValueVariable}))");
        result.Body.Add("    {");
        result.Body.Add($"        if ({helperAfter}({context.ParseContextName}, out _))");
        result.Body.Add("        {");
        result.Body.Add($"            {result.SuccessVariable} = true;");
        result.Body.Add("        }");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"Between({_before},{_parser},{_after})";

}
