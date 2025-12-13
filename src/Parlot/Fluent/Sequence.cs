using Parlot.Compilation;
using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Linq;

namespace Parlot.Fluent;

public sealed class Sequence<T1, T2> : Parser<ValueTuple<T1, T2>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<T1> _parser1;
    private readonly Parser<T2> _parser2;
    public Sequence(Parser<T1> parser1, Parser<T2> parser2)
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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2>> result)
    {
        context.EnterParser(this);

        var parseResult1 = new ParseResult<T1>();

        var start = context.Scanner.Cursor.Position;

        if (_parser1.Parse(context, ref parseResult1))
        {
            var parseResult2 = new ParseResult<T2>();

            if (_parser2.Parse(context, ref parseResult2))
            {
                result.Set(parseResult1.Start, parseResult2.End, new ValueTuple<T1, T2>(parseResult1.Value, parseResult2.Value));

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
                new SkippableCompilationResult(_parser1.Build(context), false),
                new SkippableCompilationResult(_parser2.Build(context), false)
            ];
    }

    public CompilationResult Compile(CompilationContext context)
    {
        return SequenceCompileHelper.CreateSequenceCompileResult(BuildSkippableParsers(context), context);
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser1 is not ISourceable parser1 || _parser2 is not ISourceable parser2)
        {
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Sequence_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helper1 = Helper(parser1, "P1");
        var helper2 = Helper(parser2, "P2");

        result.Body.Add($"var h1 = {helper1}({context.ParseContextName});");
        result.Body.Add($"if (h1.Item1)");
        result.Body.Add("{");
        result.Body.Add($"    var h2 = {helper2}({context.ParseContextName});");
        result.Body.Add($"    if (h2.Item1)");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(h1.Item2, h2.Item2);");
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

        return result;
    }

    public override string ToString() => $"{_parser1} & {_parser2}";
}

public sealed class Sequence<T1, T2, T3> : Parser<ValueTuple<T1, T2, T3>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2>> _parser;
    private readonly Parser<T3> _lastParser;

    public Sequence(Parser<ValueTuple<T1, T2>>
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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T3>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3>(
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

    public override string ToString() => $"{_parser} & {_lastParser} ";

    public SkippableCompilationResult[] BuildSkippableParsers(CompilationContext context)
    {
        if (_parser is not ISkippableSequenceParser sequenceParser)
        {
            throw new InvalidOperationException(SequenceCompileHelper.SequenceRequired);
        }

        return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
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
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Sequence_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        result.Body.Add($"var hp = {helperParser}({context.ParseContextName});");
        result.Body.Add($"if (hp.Item1)");
        result.Body.Add("{");
        result.Body.Add($"    var hl = {helperLast}({context.ParseContextName});");
        result.Body.Add($"    if (hl.Item1)");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hp.Item2.Item1, hp.Item2.Item2, hl.Item2);");
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

        return result;
    }
}

public sealed class Sequence<T1, T2, T3, T4> : Parser<ValueTuple<T1, T2, T3, T4>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3>> _parser;
    private readonly Parser<T4> _lastParser;

    public Sequence(Parser<ValueTuple<T1, T2, T3>> parser, Parser<T4> lastParser)
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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T4>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4>(
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

        return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
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
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Sequence_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        result.Body.Add($"var hp = {helperParser}({context.ParseContextName});");
        result.Body.Add($"if (hp.Item1)");
        result.Body.Add("{");
        result.Body.Add($"    var hl = {helperLast}({context.ParseContextName});");
        result.Body.Add($"    if (hl.Item1)");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hp.Item2.Item1, hp.Item2.Item2, hp.Item2.Item3, hl.Item2);");
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

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} ";
}

public sealed class Sequence<T1, T2, T3, T4, T5> : Parser<ValueTuple<T1, T2, T3, T4, T5>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4>> _parser;
    private readonly Parser<T5> _lastParser;

    public Sequence(Parser<ValueTuple<T1, T2, T3, T4>> parser, Parser<T5> lastParser)
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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T5>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4, T5>(
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

        return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
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
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Sequence_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        result.Body.Add($"var hp = {helperParser}({context.ParseContextName});");
        result.Body.Add($"if (hp.Item1)");
        result.Body.Add("{");
        result.Body.Add($"    var hl = {helperLast}({context.ParseContextName});");
        result.Body.Add($"    if (hl.Item1)");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hp.Item2.Item1, hp.Item2.Item2, hp.Item2.Item3, hp.Item2.Item4, hl.Item2);");
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

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} ";
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5>> _parser;
    private readonly Parser<T6> _lastParser;

    public Sequence(Parser<ValueTuple<T1, T2, T3, T4, T5>> parser, Parser<T6> lastParser)
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

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T6>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6>(
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

        return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
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
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5, T6>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T6>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Sequence_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        result.Body.Add($"var hp = {helperParser}({context.ParseContextName});");
        result.Body.Add($"if (hp.Item1)");
        result.Body.Add("{");
        result.Body.Add($"    var hl = {helperLast}({context.ParseContextName});");
        result.Body.Add($"    if (hl.Item1)");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hp.Item2.Item1, hp.Item2.Item2, hp.Item2.Item3, hp.Item2.Item4, hp.Item2.Item5, hl.Item2);");
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

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} ";
}

public sealed class Sequence<T1, T2, T3, T4, T5, T6, T7> : Parser<ValueTuple<T1, T2, T3, T4, T5, T6, T7>>, ICompilable, ISkippableSequenceParser, ISeekable, ISourceable
{
    private readonly Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> _parser;
    private readonly Parser<T7> _lastParser;

    public Sequence(Parser<ValueTuple<T1, T2, T3, T4, T5, T6>> parser, Parser<T7> lastParser)
    {
        _parser = parser;
        _lastParser = lastParser ?? throw new ArgumentNullException(nameof(lastParser));

        if (_lastParser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public override bool Parse(ParseContext context, ref ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6, T7>> result)
    {
        context.EnterParser(this);

        var tupleResult = new ParseResult<ValueTuple<T1, T2, T3, T4, T5, T6>>();

        var start = context.Scanner.Cursor.Position;

        if (_parser.Parse(context, ref tupleResult))
        {
            var lastResult = new ParseResult<T7>();

            if (_lastParser.Parse(context, ref lastResult))
            {
                var tuple = new ValueTuple<T1, T2, T3, T4, T5, T6, T7>(
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

        return sequenceParser.BuildSkippableParsers(context).Append(new SkippableCompilationResult(_lastParser.Build(context), false)).ToArray();
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
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var result = context.CreateResult(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>));
        var cursorName = context.CursorName;
        var startName = $"start{context.NextNumber()}";
        var tupleTypeName = SourceGenerationContext.GetTypeName(typeof(ValueTuple<T1, T2, T3, T4, T5, T6, T7>));

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
                .GetOrCreate(p, $"{context.MethodNamePrefix}_Sequence_{suffix}", valueTypeName, () => p.GenerateSource(context))
                .MethodName;
        }

        var helperParser = Helper(parser, "Parser");
        var helperLast = Helper(lastParser, "Last");

        result.Body.Add($"var hp = {helperParser}({context.ParseContextName});");
        result.Body.Add($"if (hp.Item1)");
        result.Body.Add("{");
        result.Body.Add($"    var hl = {helperLast}({context.ParseContextName});");
        result.Body.Add($"    if (hl.Item1)");
        result.Body.Add("    {");
        result.Body.Add($"        {result.SuccessVariable} = true;");
        result.Body.Add($"        {result.ValueVariable} = new {tupleTypeName}(hp.Item2.Item1, hp.Item2.Item2, hp.Item2.Item3, hp.Item2.Item4, hp.Item2.Item5, hp.Item2.Item6, hl.Item2);");
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

        return result;
    }

    public override string ToString() => $"{_parser} & {_lastParser} ";
}
