using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class CharLiteral : Parser<char>, ICompilable, ISeekable, ISourceable
{
    public CharLiteral(char c)
    {
        Char = c;
        ExpectedChars = [c];
    }

    public char Char { get; }

    public bool CanSeek { get; } = true;

    public char[] ExpectedChars { get; }

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<char> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        if (cursor.Match(Char))
        {
            var start = cursor.Offset;
            cursor.Advance();
            result.Set(start, cursor.Offset, Char);

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<char>();

        result.Body.Add(
            Expression.IfThen(
                context.ReadChar(Char),
                Expression.Block(
                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                    context.DiscardResult
                    ? Expression.Empty()
                    : Expression.Assign(result.Value, Expression.Constant(Char, typeof(char)))
                    )
                )
        );

        return result;
    }

    public override string ToString() => $"Char('{Char}')";

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(char));
        var ctx = context.ParseContextName;
        var successVar = result.SuccessVariable;
        var valueVar = result.ValueVariable;
        var cursorName = $"cursor{context.NextNumber()}";
        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {cursorName} = {ctx}.Scanner.Cursor;");
        result.Body.Add($"int {startName} = 0;");
        result.Body.Add($"{successVar} = false;");
        result.Body.Add($"if ({cursorName}.Match('{Char}'))");
        result.Body.Add("{");
        result.Body.Add($"    {startName} = {cursorName}.Offset;");
        result.Body.Add("    " + cursorName + ".Advance();");
        result.Body.Add($"    {valueVar} = '{Char}';");
        result.Body.Add($"    {successVar} = true;");
        result.Body.Add("}");

        return result;
    }
}
