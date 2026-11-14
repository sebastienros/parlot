using System;
using System.Collections.Generic;
using Parlot.Fluent;
using Parlot.Rewriting;

namespace Parlot.SourceGeneration;

/// <summary>
/// Emits a sequence part by part for a callback which receives its start, like <c>Then((context, start, end, value) => ...)</c>.
/// </summary>
/// <remarks>
/// <para>
/// At runtime a sequence starts where the first of its parts which matched something does (see <see cref="SequenceStart"/>):
/// a part which matches nothing, like an <c>Optional</c> or a <c>ZeroOrMany</c> without elements, is before the white space the
/// next part skips. The generated helpers don't return where they start, so when the first part of a sequence can succeed
/// without moving the cursor, the sequence is emitted here, inline, and the start moves to the content of the next part as
/// long as the cursor didn't move.
/// </para>
/// <para>
/// The other sequences keep their helper: their start is the one of their first part, which the callback already reads.
/// </para>
/// </remarks>
internal sealed class SequenceStartSource
{
    private readonly SourceGenerationContext _context;
    private readonly object _parser;
    private readonly Dictionary<object, LoopAnalysis.Node> _starts;
    private IList<string> _body = [];
    private string _startName = "";

    private SequenceStartSource(SourceGenerationContext context, object parser, Dictionary<object, LoopAnalysis.Node> starts)
    {
        _context = context;
        _parser = parser;
        _starts = starts;
    }

    /// <summary>
    /// Returns the emitter of <paramref name="parser"/> when it is a sequence whose start can be after the start of its first
    /// part, or <see langword="null"/>.
    /// </summary>
    public static SequenceStartSource? Create(SourceGenerationContext context, object parser)
    {
        if (parser is not ISequenceSourceable)
        {
            return null;
        }

        var source = new SequenceStartSource(context, parser, LoopAnalysis.ExploreStarts([parser]));

        return source.Tracks(parser) ? source : null;
    }

    /// <summary>
    /// Emits the sequence.
    /// </summary>
    /// <param name="body">The lines the code is added to.</param>
    /// <param name="valueType">The type of the value of the sequence.</param>
    /// <param name="startName">The variable holding the start, at the cursor when the code runs, which the code moves.</param>
    /// <param name="successName">The variable set to <see langword="true"/> when the sequence matches.</param>
    /// <param name="valueName">The variable receiving the value of the sequence when it matches.</param>
    /// <remarks>The code doesn't move the cursor back when the sequence fails, its caller does.</remarks>
    public void Generate(IList<string> body, Type valueType, string startName, string successName, string valueName)
    {
        _body = body;
        _startName = startName;

        body.Add($"var {successName} = false;");
        body.Add($"{SourceGenerationContext.GetTypeName(valueType)} {valueName} = default;");

        _context.WithDiscardResult(false, () =>
        {
            Generate(_parser, valueType, keep: true, atStart: true, "", (value, indent) =>
            {
                body.Add($"{indent}{successName} = true;");
                body.Add($"{indent}{valueName} = {value};");
            });

            return true;
        });
    }

    // Whether the start of the parser can be after the start of its first part, when it is a sequence
    private bool Tracks(object parser)
        => parser is ISequenceSourceable sequence && (IsNullable(sequence.Parts[0].Parser) || Tracks(sequence.Parts[0].Parser));

    // A parser the analysis doesn't know is nullable
    private bool IsNullable(object parser) => !_starts.TryGetValue(parser, out var node) || node.Nullable || node.Opaque;

    // A part which can match nothing, but reads its content after skipping white space when it matches something, like an
    // Optional of a Terms parser: all the parsers it can invoke first skip white space, and match the same when it is
    // skipped before them
    private bool SkipsWhiteSpaceFirst(object parser)
        => _starts.TryGetValue(parser, out var node) && node.Edges.Count > 0 && node.Edges.TrueForAll(static edge => LeadingWhiteSpace.CanSkipBefore(edge.Parser));

    // atStart: whether nothing can have matched yet when the parser is invoked, which the code checks with the cursor
    private void Generate(object parser, Type valueType, bool keep, bool atStart, string indent, Action<string?, string> onSuccess)
    {
        if (Tracks(parser))
        {
            var sequence = (ISequenceSourceable)parser;
            var parts = sequence.Parts;
            var (head, headType, keepHead) = parts[0];
            var (tail, tailType, keepTail) = parts[1];
            var headNullable = IsNullable(head);

            Generate(head, headType, keep && keepHead, atStart, indent, (headValue, headIndent) =>
            {
                if (atStart && headNullable)
                {
                    // Nothing was matched yet: the sequence starts where the content of the next part does
                    _body.Add($"{headIndent}if ({_context.CursorName}.Offset == {_startName})");
                    _body.Add($"{headIndent}{{");

                    if (LeadingWhiteSpace.CanSkipBefore(tail))
                    {
                        AddSkipWhiteSpace(headIndent + "    ");
                    }

                    _body.Add($"{headIndent}    {_startName} = {_context.CursorName}.Offset;");
                    _body.Add($"{headIndent}}}");
                }

                // Once a part matched something, the start doesn't move, the next parts keep their helper
                Action<string?, string> onTailSuccess = (tailValue, tailIndent) =>
                    onSuccess(keep ? sequence.CreateValue([keepHead ? headValue : null, keepTail ? tailValue : null]) : null, tailIndent);

                if (atStart && headNullable)
                {
                    Generate(tail, tailType, keep && keepTail, atStart: true, headIndent, onTailSuccess);
                }
                else
                {
                    Invoke(tail, tailType, keep && keepTail, atStart: false, headIndent, onTailSuccess);
                }
            });
        }
        else
        {
            Invoke(parser, valueType, keep, atStart, indent, onSuccess);
        }
    }

    private void Invoke(object parser, Type valueType, bool keep, bool atStart, string indent, Action<string?, string> onSuccess)
    {
        if (parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("Sequence requires source-generatable parsers.");
        }

        var ctx = _context.ParseContextName;
        var cursorName = _context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(valueType);
        var helperName = _context.WithDiscardResult(!keep, () => _context.Helpers
            .GetOrCreate(sourceable, $"{_context.MethodNamePrefix}_Sequence_Part", valueTypeName, () => sourceable.GenerateSource(_context))
            .MethodName);

        var valueName = keep ? $"part{_context.NextNumber()}" : null;
        var call = $"{helperName}({ctx}, out {(valueName != null ? "var " + valueName : "_")})";

        if (atStart && IsNullable(parser) && SkipsWhiteSpaceFirst(parser))
        {
            // The content of the part starts after the white space it skips, which is given back when it matches nothing.
            // The text is read again then, so it stays buffered while the part runs.
            var beforeName = $"before{_context.NextNumber()}";
            var matchedName = $"matched{_context.NextNumber()}";
            var pinName = _context.IsCompacting ? $"pin{_context.NextNumber()}" : null;

            _body.Add($"{indent}var {beforeName} = {cursorName}.Offset;");

            if (pinName != null)
            {
                _body.Add($"{indent}var {pinName} = {ctx}.Pin();");
            }

            _body.Add($"{indent}if ({cursorName}.Offset == {_startName})");
            _body.Add($"{indent}{{");
            AddSkipWhiteSpace(indent + "    ");
            _body.Add($"{indent}    {_startName} = {cursorName}.Offset;");
            _body.Add($"{indent}}}");
            _body.Add($"{indent}var {matchedName} = {call};");
            _body.Add($"{indent}if ({matchedName} && {cursorName}.Offset == {_startName})");
            _body.Add($"{indent}{{");
            _body.Add($"{indent}    {cursorName}.ResetPosition({beforeName});");
            _body.Add($"{indent}    {_startName} = {beforeName};");
            _body.Add($"{indent}}}");

            if (pinName != null)
            {
                _body.Add($"{indent}{ctx}.Unpin({pinName});");
            }

            _body.Add($"{indent}if ({matchedName})");
        }
        else
        {
            _body.Add($"{indent}if ({call})");
        }

        _body.Add($"{indent}{{");
        onSuccess(valueName, indent + "    ");
        _body.Add($"{indent}}}");
    }

    private void AddSkipWhiteSpace(string indent)
    {
        _body.Add($"{indent}if ({_context.ParseContextName}.MaySkipWhiteSpace({_context.CursorName}.Current))");
        _body.Add($"{indent}{{");
        _body.Add($"{indent}    {_context.ParseContextName}.SkipWhiteSpace();");
        _body.Add($"{indent}}}");
    }
}
