using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Deferred<T> : Parser<T>, ISeekable, ISourceable
{

    private readonly object _lockObject = new();
    private Parser<T>? _parser;

    public Parser<T>? Parser
    {
        get => _parser;
        set
        {
            _parser = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Deferred()
    {
    }

    public Deferred(Func<Deferred<T>, Parser<T>> parser) : this()
    {
        Parser = parser(this);

        if (Parser is ISeekable seekable)
        {
            CanSeek = seekable.CanSeek;
            ExpectedChars = seekable.ExpectedChars;
            SkipWhitespace = seekable.SkipWhitespace;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        var parser = Parser;

        if (parser is null)
        {
            throw new InvalidOperationException("Parser has not been initialized");
        }

        var limitRecursion = context.MaxRecursionDepth > 0;

        if (limitRecursion)
        {
            context.EnterRecursion();
        }

        var trackPosition = !context.DisableLoopDetection;

        // Remember the position where we entered this parser
        var entryPosition = 0;

        if (trackPosition)
        {
            entryPosition = context.Scanner.Cursor.Offset;

            // Marking the parser as active also detects a cycle at this position, which saves a lookup
            // compared to checking for the cycle first.
            if (!context.PushParserAtPosition(this))
            {
                // Cycle detected at this position - fail gracefully instead of stack overflow
                if (limitRecursion)
                {
                    context.ExitRecursion();
                }

                return false;
            }
        }

        try
        {
            context.EnterParser(this);

            var outcome = parser.Parse(context, ref result);

            context.ExitParser(this);

            return outcome;
        }
        finally
        {
            // Marked as inactive even when a parser throws, otherwise a context that is reused
            // after the exception was handled would report a cycle for this position
            if (trackPosition)
            {
                context.PopParserAtPosition(this, entryPosition);
            }

            if (limitRecursion)
            {
                context.ExitRecursion();
            }
        }
    }

    private bool _toString;

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (Parser is null)
        {
            throw new InvalidOperationException("Can't generate source for a Deferred parser until it is fully initialized");
        }

        // Check if this deferred parser is already being generated (recursion)
        var methodName = context.Deferred.GetOrCreateMethodName(this, Name ?? "Deferred");

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));

        // Track position for backtracking on failure
        var startName = $"start{context.NextNumber()}";
        result.Body.Add($"var {startName} = {cursorName}.Position;");

        // Generate a call to the helper method
        if (context.DiscardResult)
        {
            result.Body.Add($"{result.SuccessVariable} = {methodName}({ctx}, out _);");
        }
        else
        {
            result.Body.Add($"{result.SuccessVariable} = {methodName}({ctx}, out {result.ValueVariable});");
        }

        // Reset position if the deferred parser failed
        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    public override string ToString()
    {
        // Handle recursion

        lock (_lockObject)
        {
            if (!_toString)
            {
                _toString = true;
                var result = Name == null
                    ? $"{Parser} (Deferred)"
                    : $"{Name} (Deferred)";
                _toString = false;
                return result;
            }
            else
            {
                return "(Deferred)";
            }
        }
    }
}
