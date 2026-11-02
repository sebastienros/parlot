using System;
using System.Collections.Generic;

namespace Parlot.SourceGeneration;

/// <summary>
/// A parser whose code can be generated inline in the code of the parser invoking it, reading the text through a span
/// local and a position local instead of the cursor (see <see cref="SpanSource"/>).
/// </summary>
internal interface ISpanSourceable
{
    /// <summary>
    /// Returns the size of the span code of this parser, the number of parsers it contains counting each use of a shared
    /// parser, and more for parsers with large code, or -1 when this parser or one it invokes can't generate span code in
    /// this context.
    /// </summary>
    int GetSpanSourceWeight(SourceGenerationContext context);

    /// <summary>
    /// Emits the code matching this parser at the position of <paramref name="writer"/>.
    /// </summary>
    /// <param name="writer">The span code being emitted.</param>
    /// <param name="valueVariable">
    /// The variable receiving the value on success, or <see langword="null"/> when the value isn't used.
    /// </param>
    /// <param name="fail">
    /// The label the code jumps to when the parser doesn't match. The position can then have any value: the owner of
    /// the label moves it back. On success the position is after the match.
    /// </param>
    void GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail);
}

/// <summary>
/// A label of span code, which is only placed when a jump references it, such that the generated code has neither
/// unreferenced labels nor unreachable statements.
/// </summary>
internal sealed class SpanLabel
{
    public SpanLabel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool IsUsed { get; private set; }

    /// <summary>
    /// Returns a statement jumping to the label.
    /// </summary>
    public string Goto()
    {
        IsUsed = true;
        return $"goto {Name};";
    }
}

/// <summary>
/// Emits span code: the statements of a subgraph of parsers which only read the text, like the regular expression source
/// generator emits them.
/// </summary>
/// <remarks>
/// <para>
/// The code reads the text after the cursor through a span local, indexed by a position local. The cursor isn't moved
/// until the subgraph matches, then once, so a failure doesn't move it back, and choices and loops move the position
/// local back rather than the cursor.
/// </para>
/// <para>
/// The cursor sets <see cref="Cursor.HitEnd"/> when it reaches the end of the buffer, which the position local doesn't.
/// The span code thus marks it itself, on the paths which already handle the end of the span: a check of the char at the
/// end of the span marks it whatever its outcome, so does a callback invoked at the end of the span, and moving the
/// cursor to the end does when the subgraph matches. A loop which stops at the end of the span needs no mark: what
/// follows it checks the next char, invokes a callback or moves the cursor.
/// </para>
/// <para>
/// White space is skipped by the context, which reads the cursor, and can invoke a custom white space parser. The span
/// code checks the current char first, and only when it can be skipped moves the cursor to the position, skips the
/// white space and reads the position back. The span remains valid since the buffer is only replaced in compacting mode.
/// The cursor is then moved to the end of the match, or back to the start of the span, from wherever it is.
/// </para>
/// </remarks>
internal sealed class SpanSourceWriter
{
    private readonly IList<string> _locals;
    private readonly IList<string> _body;

    public SpanSourceWriter(SourceGenerationContext context, IList<string> locals, IList<string> body)
    {
        Context = context;
        _locals = locals;
        _body = body;

        var number = context.NextNumber();
        Span = $"span{number}";
        Pos = $"pos{number}";
        SpanStart = $"spanStart{number}";
    }

    public SourceGenerationContext Context { get; }

    /// <summary>
    /// The name of the <c>bool</c> local recording that the cursor was moved to skip white space, or <see langword="null"/>
    /// when the span code doesn't skip white space.
    /// </summary>
    public string? Moved { get; private set; }

    /// <summary>
    /// The name of the <c>ReadOnlySpan&lt;char&gt;</c> local holding the text after the cursor.
    /// </summary>
    public string Span { get; }

    /// <summary>
    /// The name of the <c>int</c> local holding the current position in <see cref="Span"/>.
    /// </summary>
    public string Pos { get; }

    /// <summary>
    /// The name of the <c>int</c> local holding the absolute offset of the first char of <see cref="Span"/>.
    /// </summary>
    public string SpanStart { get; }

    public string Cursor => Context.CursorName;

    public string Indent { get; private set; } = "";

    /// <summary>
    /// The statement marking that the decision depends on the text after the end of the span.
    /// </summary>
    public string MarkHitEnd => $"{Cursor}.MarkHitEnd();";

    /// <summary>
    /// The condition which is true when no char is available at the current position.
    /// </summary>
    public string AtEnd => $"{Pos} >= {Span}.Length";

    public void Add(string line) => _body.Add(Indent + line);

    public void BeginBlock(string? header = null)
    {
        if (header != null)
        {
            Add(header);
        }

        Add("{");
        Indent += "    ";
    }

    public void EndBlock()
    {
        Indent = Indent.Substring(4);
        Add("}");
    }

    /// <summary>
    /// Declares a local at the start of the generated method, such that jumps can't skip its declaration.
    /// </summary>
    public string DeclareLocal(string typeName, string prefix, string initializer = "default")
    {
        var name = $"{prefix}{Context.NextNumber()}";
        _locals.Add($"{typeName} {name} = {initializer};");
        return name;
    }

    /// <inheritdoc cref="DeclareLocal(string, string, string)"/>
    public string DeclareLocal(Type type, string prefix) => DeclareLocal(SourceGenerationContext.GetTypeName(type), prefix);

    public SpanLabel NewLabel(string prefix) => new($"{prefix}{Context.NextNumber()}");

    /// <summary>
    /// Places a label, when it's used, before <paramref name="statement"/>.
    /// </summary>
    public void PlaceLabel(SpanLabel label, string statement = ";")
    {
        if (label.IsUsed)
        {
            Add($"{label.Name}: {statement}");
        }
    }

    /// <summary>
    /// Emits the span code of a parser.
    /// </summary>
    public void Generate(object parser, string? valueVariable, SpanLabel fail)
        => ((ISpanSourceable)parser).GenerateSpanSource(this, valueVariable, fail);

    /// <summary>
    /// Returns an expression creating the <see cref="TextSpan"/> of the chars of the span between <paramref name="start"/> and <paramref name="start"/> + <paramref name="length"/>.
    /// </summary>
    public string CreateSpan(string start, string length) => $"{Cursor}.CreateSpan({SpanStart} + {start}, {length})";

    /// <summary>
    /// Emits the failure of a check of the char at the current position: the decision depended on the text after the span
    /// when there is no char.
    /// </summary>
    public void Fail(SpanLabel fail)
    {
        Add($"if ({AtEnd}) {MarkHitEnd}");
        Add(fail.Goto());
    }

    /// <summary>
    /// Emits the code skipping the white space at the current position, like <c>SkipWhiteSpace</c> does.
    /// </summary>
    public void SkipWhiteSpace()
    {
        var ctx = Context.ParseContextName;
        Moved ??= DeclareLocal("bool", "moved", "false");

        // The check of ParseContext.MaySkipWhiteSpace, with the constant mask of the default set first like SkipWhiteSpace.
        // There is no white space at the end of the span.
        BeginBlock($"if ({ctx}.WhiteSpaceParser is not null || ((uint){Pos} < (uint){Span}.Length && global::Parlot.Character.HasMask({Span}[{Pos}], (byte)global::Parlot.CharacterMask.WhiteSpaceOrNewLine) && global::Parlot.Character.HasMask({Span}[{Pos}], {ctx}._skipMask)))");
        Add($"{Cursor}.ResetPosition({SpanStart} + {Pos});");
        Add($"{ctx}.SkipWhiteSpace();");
        Add($"{Pos} = {Cursor}.Offset - {SpanStart};");
        Add($"{Moved} = true;");
        EndBlock();
    }

    /// <summary>
    /// Emits the mark a callback needs before it's invoked at the end of the span: the cursor would already have marked
    /// that it reached the end, and a <c>ParseException</c> thrown while it's marked lets a token be parsed again with
    /// more text.
    /// </summary>
    public void BeforeCallback()
    {
        Add($"if ({AtEnd}) {MarkHitEnd}");
    }
}

/// <summary>
/// Generates span code for the parsers implementing <see cref="ISpanSourceable"/>.
/// </summary>
internal static class SpanSource
{
    // The maximum size of a span subgraph, about the number of parsers it inlines, which bounds the code duplicated for
    // shared parsers
    internal const int MaxWeight = 64;

    /// <summary>
    /// Returns the weight of a parser, see <see cref="ISpanSourceable.GetSpanSourceWeight(SourceGenerationContext)"/>.
    /// </summary>
    internal static int GetWeight(object? parser, SourceGenerationContext context)
        => parser is ISpanSourceable sourceable ? sourceable.GetSpanSourceWeight(context) : -1;

    /// <summary>
    /// Returns the weight of a parser invoking <paramref name="parsers"/>, or -1 when one of them can't generate span code.
    /// </summary>
    internal static int GetWeight(SourceGenerationContext context, params object?[] parsers)
    {
        var weight = 1;

        foreach (var parser in parsers)
        {
            var parserWeight = GetWeight(parser, context);

            if (parserWeight < 0)
            {
                return -1;
            }

            weight += parserWeight;

            if (weight > MaxWeight)
            {
                // Large enough to be refused, without overflowing
                return MaxWeight + 1;
            }
        }

        return weight;
    }

    /// <summary>
    /// Generates the code of a composite parser as span code, when it and the parsers it invokes support it.
    /// </summary>
    /// <returns>The code, or <see langword="null"/> when the parser should be generated as usual.</returns>
    internal static SourceResult? TryGenerate(ISpanSourceable parser, Type valueType, SourceGenerationContext context)
    {
        // A compacting buffer can be replaced while a subgraph is read: its parsers are read as separate tokens
        if (context.IsCompacting || !context.GenerateSpanCode)
        {
            return null;
        }

        var weight = parser.GetSpanSourceWeight(context);

        if (weight < 0 || weight > MaxWeight)
        {
            return null;
        }

        var result = context.CreateResult(valueType);
        var writer = new SpanSourceWriter(context, result.Locals, result.Body);
        var cursor = context.CursorName;
        var fail = writer.NewLabel("fail");

        writer.Add($"var {writer.Span} = {cursor}.Span;");
        writer.Add($"var {writer.SpanStart} = {cursor}.Offset;");
        writer.Add($"var {writer.Pos} = 0;");

        writer.Generate(parser, context.DiscardResult ? null : result.ValueVariable, fail);

        // The cursor doesn't move when the span code fails: the position the failing check read is recorded instead, for the
        // entry points returning the furthest failure. A single parser, with at most one wrapper such as a white space skip,
        // fails where it started, which the cursor records already.
        var recordFailure = weight > 2 ? $"{cursor}.RecordFailure({writer.SpanStart} + {writer.Pos});" : ";";

        // The cursor moves once, which marks that it reached the end of the span when it does
        if (writer.Moved == null)
        {
            writer.Add($"{cursor}.AdvanceWithinBuffer({writer.Pos});");
            writer.Add($"{result.SuccessVariable} = true;");
            writer.PlaceLabel(fail, recordFailure);
        }
        else
        {
            // Skipping white space moved the cursor, which is moved from where it is
            writer.Add($"if ({writer.Moved}) {cursor}.ResetPosition({writer.SpanStart} + {writer.Pos}); else {cursor}.AdvanceWithinBuffer({writer.Pos});");
            writer.Add($"{result.SuccessVariable} = true;");

            if (fail.IsUsed)
            {
                var done = writer.NewLabel("done");
                writer.Add(done.Goto());
                if (weight > 2)
                {
                    writer.PlaceLabel(fail, recordFailure);
                    writer.Add($"if ({writer.Moved}) {cursor}.ResetPosition({writer.SpanStart});");
                }
                else
                {
                    writer.PlaceLabel(fail, $"if ({writer.Moved}) {cursor}.ResetPosition({writer.SpanStart});");
                }
                writer.PlaceLabel(done);
            }
        }

        return result;
    }

    /// <summary>
    /// Emits the span code of a sequence.
    /// </summary>
    /// <param name="writer">The span code being emitted.</param>
    /// <param name="valueVariable">The variable receiving the value of the sequence, or <see langword="null"/>.</param>
    /// <param name="fail">The failure label of the sequence.</param>
    /// <param name="parts">The parsers of the sequence, the type of their value, and whether the value is used.</param>
    /// <param name="createValue">Creates the expression of the value from the variables of the values of the parts, <see langword="null"/> for those not used.</param>
    internal static void GenerateSequence(
        SpanSourceWriter writer,
        string? valueVariable,
        SpanLabel fail,
        IReadOnlyList<(object Parser, Type ValueType, bool Keep)> parts,
        Func<string?[], string> createValue)
    {
        var values = new string?[parts.Count];

        for (var i = 0; i < parts.Count; i++)
        {
            var (parser, valueType, keep) = parts[i];

            values[i] = valueVariable != null && keep ? writer.DeclareLocal(valueType, "item") : null;

            // A failure of any part is the failure of the sequence, whose owner moves the position back
            writer.Generate(parser, values[i], fail);
        }

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {createValue(values)};");
        }
    }

    /// <summary>
    /// Emits the span code of a loop of <paramref name="parser"/>, like <c>ZeroOrMany</c> and <c>OneOrMany</c>.
    /// </summary>
    internal static void GenerateLoop(SpanSourceWriter writer, object parser, Type elementType, int max, bool oneOrMore, string? valueVariable, SpanLabel fail)
    {
        var elementTypeName = SourceGenerationContext.GetTypeName(elementType);
        var count = writer.DeclareLocal("int", "count", "0");
        var itemStart = writer.DeclareLocal("int", "itemStart", "0");
        var list = valueVariable != null ? writer.DeclareLocal($"global::Parlot.Fluent.ListBuilder<{elementTypeName}>", "list") : null;
        var item = valueVariable != null ? writer.DeclareLocal(elementType, "item") : null;
        var itemFail = writer.NewLabel("itemFail");
        var pos = writer.Pos;

        // The locals are declared at the start of the method, and a loop can run several times in an enclosing loop
        writer.Add($"{count} = 0;");

        // The items of a long list are on the scratch stack of the thread, which an exception mustn't leave them on
        if (list != null)
        {
            writer.Add($"{list} = default;");
            writer.BeginBlock("try");
        }

        writer.BeginBlock(max > 0 ? $"while ({count} < {max})" : "while (true)");

        // Cancellation is checked for each element, like when each one was a helper method: the loop is the only part of
        // span code whose length depends on the input
        writer.Add($"{writer.Context.ParseContextName}.CheckCancellation();");
        writer.Add($"{itemStart} = {pos};");
        writer.Generate(parser, item, itemFail);

        // An element which doesn't move the position ends the loop, and isn't added, like at runtime
        writer.Add($"if ({pos} == {itemStart}) break;");

        if (list != null)
        {
            writer.Add($"{list}.Add({item});");
        }

        writer.Add($"{count}++;");

        if (itemFail.IsUsed)
        {
            writer.Add("continue;");
            writer.PlaceLabel(itemFail, $"{pos} = {itemStart};");
            writer.Add("break;");
        }

        writer.EndBlock();

        if (oneOrMore)
        {
            writer.Add($"if ({count} == 0) {fail.Goto()}");
        }

        if (list != null)
        {
            writer.Add($"{valueVariable} = {list}.ToArray();");
            writer.EndBlock();
            writer.BeginBlock("finally");
            writer.Add($"{list}.Release();");
            writer.EndBlock();
        }
    }

    /// <summary>
    /// Emits the span code of a parser that always matches, with <paramref name="matched"/> when its parser matches and
    /// <paramref name="notMatched"/> otherwise, like <c>Optional</c>.
    /// </summary>
    internal static void GenerateOptional(SpanSourceWriter writer, object parser, string? itemVariable, string? matched, string? notMatched)
    {
        var start = writer.DeclareLocal("int", "optionalStart", "0");
        var notFound = writer.NewLabel("notFound");
        var done = writer.NewLabel("optionalDone");

        writer.Add($"{start} = {writer.Pos};");
        writer.Generate(parser, itemVariable, notFound);

        if (matched != null)
        {
            writer.Add(matched);
        }

        if (notFound.IsUsed)
        {
            writer.Add(done.Goto());
            writer.PlaceLabel(notFound, $"{writer.Pos} = {start};");

            if (notMatched != null)
            {
                writer.Add(notMatched);
            }

            writer.PlaceLabel(done);
        }
    }
}

/// <summary>
/// Generates the span code of the number parsers.
/// </summary>
internal static class NumberSource
{
    /// <summary>
    /// Emits the span code reading a decimal number like <c>Scanner.ReadDecimal</c>, then parsing it.
    /// </summary>
    internal static void GenerateSpanSource(
        SpanSourceWriter writer,
        string? valueVariable,
        SpanLabel fail,
        Type valueType,
        string tryParseMethod,
        string numberStyles,
        string culture,
        bool allowLeadingSign,
        bool allowDecimalSeparator,
        bool allowGroupSeparator,
        bool allowExponent,
        bool allowUnderscore,
        char decimalSeparator,
        char groupSeparator)
    {
        var text = writer.DeclareLocal("global::System.ReadOnlySpan<char>", "numberText");
        var length = writer.DeclareLocal("int", "numberLength", "0");
        var reachedEnd = writer.DeclareLocal("bool", "numberReachedEnd", "false");
        var number = writer.DeclareLocal(valueType, "number");

        static string Bool(bool value) => value ? "true" : "false";

        writer.Add($"{text} = {writer.Span}.Slice({writer.Pos});");
        writer.Add($"{length} = global::Parlot.Scanner.ScanDecimal({text}, {Bool(allowLeadingSign)}, {Bool(allowDecimalSeparator)}, {Bool(allowGroupSeparator)}, {Bool(allowExponent)}, {Bool(allowUnderscore)}, (char){(int)decimalSeparator}, (char){(int)groupSeparator}, out {reachedEnd});");

        // More digits could follow the end of the span
        writer.Add($"if ({reachedEnd}) {writer.MarkHitEnd}");

        // The value is parsed even when it's not used, since a number its type can't represent doesn't match
        var digits = allowUnderscore
            ? $"global::Parlot.Numbers.RemoveUnderscores({text}.Slice(0, {length}))"
            : $"{text}.Slice(0, {length})";
        writer.Add($"if ({length} == 0 || !{tryParseMethod}({digits}, {numberStyles}, {culture}, out {number})) {fail.Goto()}");
        writer.Add($"{writer.Pos} += {length};");

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {number};");
        }
    }
}
