using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

public sealed class Deferred<T> : Parser<T>, ISeekablePrefixes, ISourceable
{

    private readonly object _lockObject = new();
    private Parser<T>? _parser;

    // Whether the hints are being read from the parser, see Resolve()
    private bool _resolving;

    public Parser<T>? Parser
    {
        get => _parser;
        set => _parser = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets whether the parser can be selected from a single char. The hints follow <see cref="Parser"/>, so a
    /// parser set after a choice was built is still seekable once the choice starts parsing.
    /// </summary>
    public bool CanSeek => Resolve().CanSeek;

    /// <inheritdoc />
    public char[] ExpectedChars => Resolve().ExpectedChars;

    /// <inheritdoc />
    public bool SkipWhitespace => Resolve().SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes
    {
        get
        {
            // The same guard as Resolve(): a parser that reaches itself gives no prefixes
            lock (SeekableResolution.SyncRoot)
            {
                var parser = _parser;

                if (parser is null || _resolving)
                {
                    return null;
                }

                _resolving = true;

                try
                {
                    return SeekableHints.Prefixes(parser);
                }
                finally
                {
                    _resolving = false;
                }
            }
        }
    }

    private (bool CanSeek, char[] ExpectedChars, bool SkipWhitespace) Resolve()
    {
        // The hints are read when a choice builds its lookup table, which can happen from any thread, and the
        // parser is in turn a choice when this one is the usual "expression" of a grammar
        lock (SeekableResolution.SyncRoot)
        {
            var parser = _parser;

            // A parser that reaches itself, directly or through other deferred parsers, can start with any char
            if (parser is null || _resolving)
            {
                return (false, [], false);
            }

            _resolving = true;

            try
            {
                if (parser is OneOf<T> oneOf)
                {
                    oneOf.EnsureResolved();
                }

                return parser is ISeekable seekable
                    ? (seekable.CanSeek, seekable.ExpectedChars, seekable.SkipWhitespace)
                    : (false, [], false);
            }
            finally
            {
                _resolving = false;
            }
        }
    }

    public Deferred()
    {
    }

    public Deferred(Func<Deferred<T>, Parser<T>> parser) : this()
    {
        Parser = parser(this);
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

        // Generate a call to the helper method. Like every generated parser it leaves the cursor where it
        // started when it fails, so the caller doesn't capture the position.
        if (context.DiscardResult)
        {
            result.Body.Add($"{result.SuccessVariable} = {methodName}({ctx}, out _);");
        }
        else
        {
            result.Body.Add($"{result.SuccessVariable} = {methodName}({ctx}, out {result.ValueVariable});");
        }

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
