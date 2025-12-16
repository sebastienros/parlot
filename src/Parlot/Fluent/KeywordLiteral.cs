using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// Parses a keyword (text followed by non-letter character or EOF).
/// This is a specialized parser that can be source-generated without requiring lambda tracking.
/// </summary>
public sealed class KeywordLiteral : Parser<string>, ICompilable, ISeekable, ISourceable
{
    private readonly TextLiteral _textLiteral;

    public KeywordLiteral(string text, StringComparison comparison = StringComparison.Ordinal)
    {
        _textLiteral = new TextLiteral(text, comparison);
        Text = text;
        Comparison = comparison;
        CanSeek = _textLiteral.CanSeek;
        ExpectedChars = _textLiteral.ExpectedChars;
        SkipWhitespace = _textLiteral.SkipWhitespace;
    }

    public string Text { get; }

    public StringComparison Comparison { get; }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<string> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        if (_textLiteral.Parse(context, ref result))
        {
            // Check that the next character is not a letter (keyword boundary)
            var cursor = context.Scanner.Cursor;
            if (cursor.Eof || !Character.IsInRange(cursor.Current, 'a', 'z') && !Character.IsInRange(cursor.Current, 'A', 'Z'))
            {
                context.ExitParser(this);
                return true;
            }

            // Not a keyword boundary - backtrack
            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<string>();

        var parserCompileResult = _textLiteral.Build(context, requireResult: true);

        var start = context.DeclarePositionVariable(result);

        // Check for keyword boundary: cursor.Eof || (!IsInRange(current, 'a', 'z') && !IsInRange(current, 'A', 'Z'))
        var cursorExpr = Expression.Property(Expression.Property(context.ParseContext, nameof(ParseContext.Scanner)), nameof(Scanner.Cursor));
        var eofExpr = Expression.Property(cursorExpr, nameof(Cursor.Eof));
        var currentExpr = Expression.Property(cursorExpr, nameof(Cursor.Current));

        var isLowerLetter = Expression.Call(typeof(Character).GetMethod(nameof(Character.IsInRange), [typeof(char), typeof(char), typeof(char)])!,
            currentExpr, Expression.Constant('a'), Expression.Constant('z'));
        var isUpperLetter = Expression.Call(typeof(Character).GetMethod(nameof(Character.IsInRange), [typeof(char), typeof(char), typeof(char)])!,
            currentExpr, Expression.Constant('A'), Expression.Constant('Z'));

        var keywordBoundaryCheck = Expression.OrElse(
            eofExpr,
            Expression.AndAlso(Expression.Not(isLowerLetter), Expression.Not(isUpperLetter)));

        var block = Expression.Block(
            parserCompileResult.Variables,
            parserCompileResult.Body
            .Append(
                Expression.IfThenElse(
                    Expression.AndAlso(parserCompileResult.Success, keywordBoundaryCheck),
                    Expression.Block(
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                        context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(result.Value, parserCompileResult.Value)
                        ),
                    context.ResetPosition(start)
                    )
                )
            );

        result.Body.Add(block);

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_textLiteral is not ISourceable sourceable)
        {
            throw new NotSupportedException("KeywordLiteral requires a source-generatable text parser.");
        }

        var result = context.CreateResult(typeof(string));
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(string));

        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Use helper for the text literal
        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_Keyword", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        var innerValueName = $"innerValue{context.NextNumber()}";
        
        // if (Helper(context, out var innerValue))
        // {
        //     // Check keyword boundary: cursor.Eof || (!IsInRange(current, 'a', 'z') && !IsInRange(current, 'A', 'Z'))
        //     if (cursor.Eof || (!Character.IsInRange(cursor.Current, 'a', 'z') && !Character.IsInRange(cursor.Current, 'A', 'Z')))
        //     {
        //         success = true;
        //         value = innerValue;
        //     }
        //     else
        //     {
        //         cursor.ResetPosition(start);
        //     }
        // }
        
        result.Body.Add($"if ({helperName}({context.ParseContextName}, out var {innerValueName}))");
        result.Body.Add("{");
        result.Body.Add($"    if ({cursorName}.Eof || (!Parlot.Character.IsInRange({cursorName}.Current, 'a', 'z') && !Parlot.Character.IsInRange({cursorName}.Current, 'A', 'Z')))");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        if (!context.DiscardResult)
        {
            result.Body.Add($"        {result.ValueVariable} = {innerValueName};");
        }
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"Keyword(\"{Text}\")";
}
