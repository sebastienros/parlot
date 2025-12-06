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

        // start = context.Scanner.Cursor.Position;
        //
        // before instructions
        //
        // if (before.Success)
        // {
        //      parser instructions
        //      
        //      if (parser.Success)
        //      {
        //         after instructions
        //      
        //         if (after.Success)
        //         {
        //            success = true;
        //            value = parser.Value;
        //         }  
        //      }
        //
        //      if (!success)
        //      {  
        //          resetPosition(start);
        //      }
        // }

        var beforeCompileResult = _before.Build(context);
        var parserCompileResult = _parser.Build(context);
        var afterCompileResult = _after.Build(context);

        var start = context.DeclarePositionVariable(result);

        var block = Expression.Block(
                beforeCompileResult.Variables,
                Expression.Block(beforeCompileResult.Body),
                Expression.IfThen(
                    beforeCompileResult.Success,
                    Expression.Block(
                        parserCompileResult.Variables,
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThen(
                            parserCompileResult.Success,
                            Expression.Block(
                                afterCompileResult.Variables,
                                Expression.Block(afterCompileResult.Body),
                                Expression.IfThen(
                                    afterCompileResult.Success,
                                    Expression.Block(
                                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                                        context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.Assign(result.Value, parserCompileResult.Value)
                                        )
                                    )
                                )
                            ),
                        Expression.IfThen(
                            Expression.Not(result.Success),
                            context.ResetPosition(start)
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
        var ctx = context.ParseContextName;
        var startName = $"start{context.NextNumber()}";

        result.Locals.Add($"var {startName} = default(global::Parlot.TextPosition);");
        result.Body.Add($"{result.SuccessVariable} = false;");
        result.Body.Add($"{startName} = {ctx}.Scanner.Cursor.Position;");

        // Generate before parser
        var beforeResult = beforeSourceable.GenerateSource(context);
        foreach (var local in beforeResult.Locals)
        {
            result.Locals.Add(local);
        }

        foreach (var stmt in beforeResult.Body)
        {
            result.Body.Add(stmt);
        }

        result.Body.Add($"if ({beforeResult.SuccessVariable})");
        result.Body.Add("{");

        // Generate main parser
        var parserResult = parserSourceable.GenerateSource(context);
        foreach (var local in parserResult.Locals)
        {
            result.Locals.Add(local);
        }

        foreach (var stmt in parserResult.Body)
        {
            result.Body.Add($"    {stmt}");
        }

        result.Body.Add($"    if ({parserResult.SuccessVariable})");
        result.Body.Add("    {");

        // Generate after parser
        var afterResult = afterSourceable.GenerateSource(context);
        foreach (var local in afterResult.Locals)
        {
            result.Locals.Add(local);
        }

        foreach (var stmt in afterResult.Body)
        {
            result.Body.Add($"        {stmt}");
        }

        result.Body.Add($"        if ({afterResult.SuccessVariable})");
        result.Body.Add("        {");
        result.Body.Add($"            {result.SuccessVariable} = true;");
        result.Body.Add($"            {result.ValueVariable} = {parserResult.ValueVariable};");
        result.Body.Add("        }");
        result.Body.Add("    }");

        result.Body.Add($"    if (!{result.SuccessVariable})");
        result.Body.Add("    {");
        result.Body.Add($"        {ctx}.Scanner.Cursor.ResetPosition({startName});");
        result.Body.Add("    }");

        result.Body.Add("}");

        return result;
    }

    public override string ToString() => Name ?? $"Between({_before},{_parser},{_after})";

}
