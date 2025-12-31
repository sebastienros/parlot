using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

public sealed class SequenceSkipAnd<T1, T2> : Parser<T2>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<T1> _parser1;
    private readonly Parser<T2> _parser2;

    public SequenceSkipAnd(Parser<T1> parser1, Parser<T2> parser2)
    {
        _parser1 = parser1 ?? throw new ArgumentNullException(nameof(parser1));
        _parser2 = parser2 ?? throw new ArgumentNullException(nameof(parser2));

        if (_parser1 is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T2> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = context.Scanner.Cursor.Position;

        if (_parser1.Parse(context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_parser2.Parse(context, ref parseResult2))
            {
                result.Set(parseResult1.Start, parseResult2.End, parseResult2.Value);

                context.ExitParser(this);
                return true;
            }

            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        return
            [
                new SkippableCompilationResult(_parser1.Build(context), true),
                new SkippableCompilationResult(_parser2.Build(context), false)
            ];
    }

    public CompilationResult Compile(CompilationContext context)
    {
        // The common skippable sequence compilation helper can't be reused since this doesn't return a tuple

        var result = context.CreateCompilationResult<T2>();

        // T value;
        //
        // parse1 instructions
        // 
        // var start = context.Scanner.Cursor.Position;
        //
        // parse1 instructions
        //
        // if (parser1.Success)
        // {
        //    
        //    parse2 instructions
        //   
        //    if (parser2.Success)
        //    {
        //       success = true;
        //       value = parse2.Value;
        //    }
        //    else
        //    {
        //        context.Scanner.Cursor.ResetPosition(start);
        //    }
        // }

        // var start = context.Scanner.Cursor.Position;

        var start = context.DeclarePositionVariable(result);

        var parser1CompileResult = _parser1.Build(context);
        var parser2CompileResult = _parser2.Build(context);

        result.Body.Add(
            Expression.Block(
                parser1CompileResult.Variables,
                Expression.Block(parser1CompileResult.Body),
                Expression.IfThen(
                    parser1CompileResult.Success,
                        Expression.Block(
                            parser2CompileResult.Variables,
                            Expression.Block(parser2CompileResult.Body),
                            Expression.IfThenElse(
                                parser2CompileResult.Success,
                                Expression.Block(
                                    context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, parser2CompileResult.Value),
                                    Expression.Assign(result.Success, Expression.Constant(true, typeof(bool)))
                                ),
                                context.ResetPosition(start)
                                )
                            )
                        )
                    )
        );

        return result;
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser1 is not ISourceable parser1 || _parser2 is not ISourceable parser2)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(T2));
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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helper1 = Helper(parser1, "P1");
        var helper2 = Helper(parser2, "P2");

        result.Body.Add($"if ({helper1}({context.ParseContextName}, out _))");
        result.Body.Add("{");
        if (context.DiscardResult)
        {
            result.Body.Add($"    if ({helper2}({context.ParseContextName}, out _))");
        }
        else
        {
            result.Body.Add($"    if ({helper2}({context.ParseContextName}, out {result.ValueVariable}))");
        }
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add("    }");
        result.Body.Add("    else");
        result.Body.Add("    {");
        result.Body.Add($"        {cursorName}.ResetPosition({startName});");
        result.Body.Add("    }");
        result.Body.Add("}");
        result.Body.Add("else");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");;

        return result;
    }

    public override string ToString() => $"{_parser1} & {_parser2} (skip)";
}

public sealed class SequenceSkipAnd<T1, T2, T3> : Parser<ValueTuple<T1, T3>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2>> _parser;
    private readonly Parser<T3> _lastParser;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2>>
        parser,
        Parser<T3> lastParser
        )
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T3>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T3>(
                    tupleResult.Value.Item1,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        var parsers = sequenceParser.BuildSkippableParsers(context);
        parsers.Last().Skip = true;

        return parsers.Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T3>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T3>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out _))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }
        else
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out var hlValue))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hlValue);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} (skip)";
}

public sealed class SequenceSkipAnd<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T4>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3>> _parser;
    private readonly Parser<T4> _lastParser;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T4>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T4>(
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        var parsers = sequenceParser.BuildSkippableParsers(context);
        parsers.Last().Skip = true;

        return parsers.Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T4>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T4>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out _))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }
        else
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out var hlValue))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hlValue);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} (skip)";

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T5>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4>> _parser;
    private readonly Parser<T5> _lastParser;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T5>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T5>(
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);

                context.ExitParser(this);
                return true;
            }
        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        var parsers = sequenceParser.BuildSkippableParsers(context);
        parsers.Last().Skip = true;

        return parsers.Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T5>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T5>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out _))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }
        else
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out var hlValue))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hlValue);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} (skip)";

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T6>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>> _parser;
    private readonly Parser<T6> _lastParser;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T6>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4, T6>(
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);

                context.ExitParser(this);
                return true;
            }

        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        var parsers = sequenceParser.BuildSkippableParsers(context);
        parsers.Last().Skip = true;

        return parsers.Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T6>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T6>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out _))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }
        else
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out var hlValue))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4, hlValue);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} (skip)";

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T7>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> _parser;
    private readonly Parser<T7> _lastParser;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T7>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4, T5, T7>(
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);

                context.ExitParser(this);
                return true;
            }

        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        var parsers = sequenceParser.BuildSkippableParsers(context);
        parsers.Last().Skip = true;

        return parsers.Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5, T7>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T7>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out _))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }
        else
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out var hlValue))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4, hpValue.Item5, hlValue);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} (skip)";

}

public sealed class SequenceSkipAnd<T1, T2, T3, T4, T5, T6, T7, T8> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T8>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> _parser;
    private readonly Parser<T8> _lastParser;

    public SequenceSkipAnd(Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> parser, Parser<T8> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T8>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T8>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6, T8>(
                    tupleResult.Value.Item1,
                    tupleResult.Value.Item2,
                    tupleResult.Value.Item3,
                    tupleResult.Value.Item4,
                    tupleResult.Value.Item5,
                    tupleResult.Value.Item6,
                    lastResult.Value
                    );

                result.Set(tupleResult.Start, lastResult.End, tuple);

                context.ExitParser(this);
                return true;
            }

        }

        context.Scanner.Cursor.ResetPosition(start);

        context.ExitParser(this);
        return false;
    }

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        var parsers = sequenceParser.BuildSkippableParsers(context);
        parsers.Last().Skip = true;

        return parsers.Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable parser || _lastParser is not ISourceable lastParser)
        {
            throw new NotSupportedException("SequenceSkipAnd requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T8>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T8>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_SequenceSkipAnd_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        if (context.DiscardResult)
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out _))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out _))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }
        else
        {
            result.Body.Add($"if ({helperParser}({context.ParseContextName}, out var hpValue))");
            result.Body.Add("{");
            result.Body.Add($"    if ({helperLast}({context.ParseContextName}, out var hlValue))");
            result.Body.Add("    {");
            result.Body.Add($"        {result.SuccessVariable} = true;");
            result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hpValue.Item1, hpValue.Item2, hpValue.Item3, hpValue.Item4, hpValue.Item5, hpValue.Item6, hlValue);");
            result.Body.Add("    }");
            result.Body.Add("    else");
            result.Body.Add("    {");
            result.Body.Add($"        {cursorName}.ResetPosition({startName});");
            result.Body.Add("    }");
            result.Body.Add("}");
            result.Body.Add("else");
            result.Body.Add("{");
            result.Body.Add($"    {cursorName}.ResetPosition({startName});");
            result.Body.Add("}");
        }

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} (skip)";

}
