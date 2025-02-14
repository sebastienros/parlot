using Parlot.Compilation;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class Capture<T> : Parser<TextSpan>, ICompilable
{
    private readonly Parser<T> _parser;

    public Capture(Parser<T> parser)
    {
        _parser = parser;
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        ParseResult<T> _ = new();

        // Did parser succeed.
        if (_parser.Parse(context, ref _))
        {
            var end = context.Scanner.Cursor.Offset;
            var length = end - start.Offset;

            result.Set(start.Offset, end, new TextSpan(context.Scanner.Buffer, start.Offset, length));

            context.ExitParser(this);
            return true;
        }

        context.ExitParser(this);
        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        // var start = context.Scanner.Cursor.Position;
        var start = context.DeclarePositionVariable(result);

        var ignoreResults = context.DiscardResult;
        context.DiscardResult = true;

        var parserCompileResult = _parser.Build(context);

        context.DiscardResult = ignoreResults;

        // parse1 instructions
        //
        // if (parser1.Success)
        // {
        //     var end = context.Scanner.Cursor.Offset;
        //     var length = end - start.Offset;
        //   
        //     value = new TextSpan(context.Scanner.Buffer, start.Offset, length);
        //   
        //     success = true;
        // }

        var startOffset = result.DeclareVariable<int>($"startOffset{context.NextNumber}", context.Offset(start));

        result.Body.Add(
            Expression.Block(
                parserCompileResult.Variables,
                Expression.Block(parserCompileResult.Body),
                Expression.IfThen(
                    test: parserCompileResult.Success,
                    ifTrue: Expression.Block(
                        // Never discard result here, that would nullify this parser
                        Expression.Assign(result.Value,
                            context.NewTextSpan(
                                context.Buffer(),
                                startOffset,
                                Expression.Subtract(context.Offset(), startOffset)
                                )),
                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                        )
                )
            )
        );

        return result;
    }

    public override string ToString() => $"{_parser} (Capture)";
}
