using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Parlot.Fluent;

public sealed class Deferred<T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable, IDeferredParser
{

    private readonly object _lockObject = new();
    private Parser<T>? _parser;

    // Whether the hints are being read from the parser, see Resolve()
    private bool _resolving;

    // Whether Parse() looks for this parser in the parsers that are active at the position, see LoopAnalysis. Written under
    // LoopAnalysis.SyncRoot, read on each parse.
    private int _loopDetection;

    // The analyses that read the parser, see LoopAnalysis.Invalidate()
    private List<IDeferredParser[]>? _loopGroups;

    public Parser<T>? Parser
    {
        get => _parser;
        set
        {
            ThrowHelper.ThrowIfNull(value, nameof(value));

            // The deferred parsers which were analyzed with the previous parser are analyzed again when they are invoked
            lock (LoopAnalysis.SyncRoot)
            {
                _parser = value;
                LoopAnalysis.Invalidate(this);
            }

            // The parsers that start with this one read its hints again
            SeekableResolution.Invalidate();
        }
    }

    /// <summary>
    /// Gets whether this parser can't be invoked again at the position where it is already active, such that it doesn't
    /// look for itself in the active parsers of the context. The grammar is analyzed when it is first needed.
    /// </summary>
    /// <remarks>
    /// The loop detection is still enabled when the white space parser of the context can invoke a deferred parser, see
    /// <see cref="LoopAnalysis"/>.
    /// </remarks>
    internal bool IsLoopDetectionElided
    {
        get
        {
            var loopDetection = Volatile.Read(ref _loopDetection);

            return loopDetection == LoopAnalysis.Unknown ? LoopAnalysis.Analyze(this) : loopDetection == LoopAnalysis.Elided;
        }
    }

    int IDeferredParser.LoopDetection
    {
        get => _loopDetection;
        set => Volatile.Write(ref _loopDetection, value);
    }

    List<IDeferredParser[]>? IDeferredParser.LoopGroups
    {
        get => _loopGroups;
        set => _loopGroups = value;
    }

    // A parser which isn't initialized throws when it is invoked
    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => _parser is { } parser && analysis.Invoke(parser);

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
                // A choice builds its lookup table first, since this lock is held
                return SeekableHints.Of(parser);
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
        var parser = _parser;

        if (parser is null)
        {
            throw new InvalidOperationException("Parser has not been initialized");
        }

        // A parser which can't be invoked again where it is active has nothing to track, unless the context limits the
        // recursion or skips white space with a parser that can invoke it
        if (_loopDetection == LoopAnalysis.Elided && !context.TracksDeferredParsers)
        {
            context.EnterParser(this);

            var success = parser.Parse(context, ref result);

            context.ExitParser(this);

            return success;
        }

        return ParseTracked(context, ref result, parser);
    }

    private bool ParseTracked(ParseContext context, ref ParseResult<T> result, Parser<T> parser)
    {
        // The grammar is analyzed even when the context disables the loop detection, such that the next parses take the
        // shortcut when they can
        var elided = IsLoopDetectionElided;

        var trackPosition = !context.DisableLoopDetection && (!elided || context.HasReentrantWhiteSpaceParser);
        var limitRecursion = context.MaxRecursionDepth > 0;

        if (!trackPosition && !limitRecursion)
        {
            context.EnterParser(this);

            var success = parser.Parse(context, ref result);

            context.ExitParser(this);

            return success;
        }

        if (limitRecursion)
        {
            context.EnterRecursion();
        }

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
