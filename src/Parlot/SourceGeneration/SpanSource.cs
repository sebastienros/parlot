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
    /// The label the code jumps to when the parser doesn't match, with the position where the cursor of the runtime
    /// parser is then: where the parser whose check failed started, since a literal moves the cursor once it matched. The
    /// owner of the label moves the position back. Where the runtime parser would move the cursor back, the code jumps to
    /// another label instead, which records the position for the furthest failure: to <see cref="SpanLabel.AfterReading"/>
    /// when it fails after a parser it invokes matched, like
    /// <see cref="SpanSource.GenerateSequence(SpanSourceWriter, string?, SpanLabel, ISequenceSourceable)"/> does, or after
    /// it moved the position itself, like a number its type can't represent, and to the label
    /// <see cref="SpanSourceWriter.SkipWhiteSpace(SpanLabel)"/> returns for what it reads after white space. On success
    /// the position is after the match.
    /// </param>
    void GenerateSpanSource(SpanSourceWriter writer, string? valueVariable, SpanLabel fail);
}

/// <summary>
/// A label of span code, which is only placed when a jump references it, such that the generated code has neither
/// unreferenced labels nor unreachable statements.
/// </summary>
internal sealed class SpanLabel
{
    private readonly SpanLabel? _afterReading;
    private readonly bool _movesCursorBack;
    private bool _isTarget;

    public SpanLabel(string name)
    {
        Name = name;
    }

    /// <summary>
    /// Creates a failure label, with the one its parser jumps to when it fails after reading.
    /// </summary>
    /// <param name="name">The name of the label.</param>
    /// <param name="afterReading">The label of the failures after reading.</param>
    /// <param name="movesCursorBack">Whether the owner of the label moves the cursor back from where white space was skipped to.</param>
    public SpanLabel(string name, SpanLabel afterReading, bool movesCursorBack)
    {
        Name = name;
        _afterReading = afterReading;
        _movesCursorBack = movesCursorBack;
    }

    public string Name { get; }

    /// <summary>
    /// The label to jump to when the parser fails after a parser it invokes matched, which can have read some text: the
    /// position of the check which failed is then recorded for the furthest failure, before the owner of the label moves
    /// the position back. A parser which fails where it started read nothing, and jumps to the label itself.
    /// </summary>
    public SpanLabel AfterReading => _afterReading ?? this;

    /// <summary>
    /// The label to jump to when the parser fails where it started, after white space which may have been skipped: the
    /// end of that white space is where a runtime parser moves the cursor back from. It is <see cref="AfterReading"/>,
    /// or the label itself when its owner moves the cursor back, which was moved to the end of the white space.
    /// </summary>
    public SpanLabel AfterWhiteSpace => _movesCursorBack ? this : AfterReading;

    /// <summary>
    /// Whether a jump references the label itself, which is then placed.
    /// </summary>
    public bool IsTarget => _isTarget;

    /// <summary>
    /// Whether a jump references the label or its <see cref="AfterReading"/>.
    /// </summary>
    public bool IsUsed => _isTarget || (_afterReading != null && _afterReading._isTarget);

    /// <summary>
    /// Returns a statement jumping to the label.
    /// </summary>
    public string Goto()
    {
        _isTarget = true;
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
/// <para>
/// The cursor records the furthest offset it is moved back from, which the position local doesn't either. The span code
/// thus records it itself where a runtime parser would move the cursor back: on the paths of a failure after some text
/// was read (<see cref="SpanLabel.AfterReading"/>), before the position is moved back to read an alternative or what
/// follows an optional parser or a loop, and when the span code fails. A check which fails where its parser started,
/// like most alternatives of a choice, records nothing.
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
    /// Creates the label of the failure of a parser after which the span code goes on, or ends: it has a second label for
    /// the failures after reading, see <see cref="PlaceFailureLabel(SpanLabel, string, string)"/>.
    /// </summary>
    /// <param name="prefix">The prefix of the names of the labels.</param>
    /// <param name="movesCursorBack">
    /// Whether the owner of the label moves the cursor back, and not only the position: the cursor then records where
    /// white space was skipped to, and a failure there needs no other record.
    /// </param>
    public SpanLabel NewFailureLabel(string prefix, bool movesCursorBack = false)
    {
        var number = Context.NextNumber();

        return new SpanLabel($"{prefix}{number}", new SpanLabel($"{prefix}Read{number}"), movesCursorBack);
    }

    /// <summary>
    /// Places a label, when it's used, before <paramref name="statement"/>.
    /// </summary>
    public void PlaceLabel(SpanLabel label, string statement = ";")
    {
        if (label.IsTarget)
        {
            Add($"{label.Name}: {statement}");
        }
    }

    /// <summary>
    /// Places a label created by <see cref="NewFailureLabel(string, bool)"/>, when it's used, before <paramref name="statement"/>.
    /// </summary>
    /// <param name="label">The label.</param>
    /// <param name="start">The position its parser started at.</param>
    /// <param name="statement">The statement of the failure, which moves the position back.</param>
    /// <remarks>
    /// The failures after reading record the position first, like the cursor records the offset a runtime parser moves it
    /// back from, and nothing when it wasn't moved: the parsers which matched before the failure can have read nothing.
    /// The failures of the first check of the parser, the most frequent ones, skip that.
    /// </remarks>
    public void PlaceFailureLabel(SpanLabel label, string start, string statement = ";")
    {
        var afterReading = label.AfterReading;

        if (afterReading != label && afterReading.IsTarget)
        {
            Add($"{afterReading.Name}: if ({Pos} > {start}) {Cursor}.RecordFailure({SpanStart} + {Pos});");

            if (!label.IsTarget)
            {
                if (statement != ";")
                {
                    Add(statement);
                }

                return;
            }
        }

        PlaceLabel(label, statement);
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
    /// <param name="fail">The failure label of the parser which skips the white space.</param>
    /// <returns>
    /// The label that what is read after the white space jumps to when it fails: where the white space ends is a failure
    /// of what was looked for there, which the position doesn't keep when it's moved back (<see cref="SpanLabel.AfterWhiteSpace"/>).
    /// </returns>
    public SpanLabel SkipWhiteSpace(SpanLabel fail)
    {
        var ctx = Context.ParseContextName;
        Moved ??= DeclareLocal("bool", "moved", "false");

        // The check of ParseContext.MaySkipWhiteSpace, with the constant mask of the default set first like SkipWhiteSpace.
        // There is no white space at the end of the span.
        BeginBlock($"if ({ctx}.WhiteSpaceParser is not null || ((uint){Pos} < (uint){Span}.Length && global::Parlot.Character.HasMask({Span}[{Pos}], (byte)global::Parlot.CharacterMask.WhiteSpaceOrNewLine) && global::Parlot.Character.HasMask({Span}[{Pos}], {ctx}._skipMask)))");
        // To where the span was read up to. Forward isn't a failure where the cursor was, back is: the cursor is where
        // it skipped white space to, before something which wasn't found
        Add($"{Cursor}.MoveTo({SpanStart} + {Pos});");
        Add($"{ctx}.SkipWhiteSpace();");
        Add($"{Pos} = {Cursor}.Offset - {SpanStart};");
        Add($"{Moved} = true;");
        EndBlock();

        return fail.AfterWhiteSpace;
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
        var fail = writer.NewFailureLabel("fail", movesCursorBack: true);

        writer.Add($"var {writer.Span} = {cursor}.Span;");
        writer.Add($"var {writer.SpanStart} = {cursor}.Offset;");
        writer.Add($"var {writer.Pos} = 0;");

        writer.Generate(parser, context.DiscardResult ? null : result.ValueVariable, fail);

        // The cursor moves once, which marks that it reached the end of the span when it does. Skipping white space moved
        // it, and it's then moved from where it is: forward, or back when what was looked for after the last white space
        // wasn't found, which is a failure there.
        writer.Add(writer.Moved == null
            ? $"{cursor}.AdvanceWithinBuffer({writer.Pos});"
            : $"if ({writer.Moved}) {cursor}.MoveTo({writer.SpanStart} + {writer.Pos}); else {cursor}.AdvanceWithinBuffer({writer.Pos});");
        writer.Add($"{result.SuccessVariable} = true;");

        if (fail.IsUsed)
        {
            // The cursor doesn't move when the span code fails after reading: the position the failing check read is
            // recorded instead, for the entry points returning the furthest failure. A match records nothing, a value which
            // is rejected fails where it starts.
            var done = writer.NewLabel("done");
            var moveBack = writer.Moved == null ? ";" : $"if ({writer.Moved}) {cursor}.ResetPosition({writer.SpanStart});";

            if (fail.AfterReading.IsTarget || moveBack != ";")
            {
                writer.Add(done.Goto());
            }

            writer.PlaceFailureLabel(fail, "0", moveBack);
            writer.PlaceLabel(done);
        }

        return result;
    }

    /// <summary>
    /// Emits the span code of the sequence <paramref name="sequence"/>.
    /// </summary>
    internal static void GenerateSequence(SpanSourceWriter writer, string? valueVariable, SpanLabel fail, ISequenceSourceable sequence)
        => GenerateSequence(writer, valueVariable, fail, sequence.Parts, sequence.CreateValue);

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

            // A failure of any part is the failure of the sequence, whose owner moves the position back: from the second
            // part on, after the ones which matched read some text
            writer.Generate(parser, values[i], i == 0 ? fail : fail.AfterReading);
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
        var itemFail = writer.NewFailureLabel("itemFail");
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
            writer.PlaceFailureLabel(itemFail, itemStart, $"{pos} = {itemStart};");
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
        var notFound = writer.NewFailureLabel("notFound");
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
            writer.PlaceFailureLabel(notFound, start, $"{writer.Pos} = {start};");

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

        // The value is parsed even when it's not used, since a number its type can't represent doesn't match: it fails
        // after its digits, which the parser moves the cursor back from
        var digits = allowUnderscore
            ? $"global::Parlot.Numbers.RemoveUnderscores({text}.Slice(0, {length}))"
            : $"{text}.Slice(0, {length})";
        writer.Add($"if ({length} == 0) {fail.Goto()}");
        writer.Add($"{writer.Pos} += {length};");
        writer.Add($"if (!{tryParseMethod}({digits}, {numberStyles}, {culture}, out {number})) {fail.AfterReading.Goto()}");

        if (valueVariable != null)
        {
            writer.Add($"{valueVariable} = {number};");
        }
    }
}
