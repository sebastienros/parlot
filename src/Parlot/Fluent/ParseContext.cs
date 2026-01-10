using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Parlot.Fluent;

public class ParseContext
{
#pragma warning disable CA2211 // Non-constant fields should not be visible
    public static int DefaultCompilationThreshold;
#pragma warning restore CA2211

    /// <summary>
    /// The number of usages of the parser before it is compiled automatically. <c>0</c> to disable automatic compilation. Default is 0.
    /// </summary>
    public int CompilationThreshold { get; set; } = DefaultCompilationThreshold;

    /// <summary>
    /// Whether to disable loop detection for recursive parsers. Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, loop detection is enabled and will prevent infinite recursion at the same position.
    /// When <c>true</c>, loop detection is disabled. This may be needed when the ParseContext itself is mutated
    /// during loops and can change the end result of parsing at the same location.
    /// </remarks>
    public bool DisableLoopDetection { get; }

    /// <summary>
    /// Whether new lines are treated as normal chars or white spaces. Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// When <c>false</c>, new lines will be skipped like any other white space.
    /// Otherwise new lines need to be read explicitly by a rule.
    /// </remarks>
    public bool UseNewLines { get; }

    /// <summary>
    /// The scanner used for the parsing session.
    /// </summary>
    public readonly Scanner Scanner;

    /// <summary>
    /// Tracks parser-position pairs to detect infinite recursion at the same position.
    /// </summary>
    private readonly HashSet<ParserPosition> _activeParserPositions;

    /// <summary>
    /// The cancellation token used to stop the parsing operation.
    /// </summary>
    public readonly CancellationToken CancellationToken;

    // TODO: For backward compatibility only, remove in future versions
    public ParseContext(Scanner scanner, bool useNewLines)
        : this(scanner, useNewLines, false, CancellationToken.None)
    {
    }

    // TODO: For backward compatibility only, remove in future versions
    public ParseContext(Scanner scanner, CancellationToken cancellationToken)
        : this(scanner, false, false, cancellationToken)
    {
    }

    public ParseContext(Scanner scanner, bool useNewLines = false, bool disableLoopDetection = false, CancellationToken cancellationToken = default)
    {
        Scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
        UseNewLines = useNewLines;
        CancellationToken = cancellationToken;
        DisableLoopDetection = disableLoopDetection;

        _activeParserPositions = !disableLoopDetection ? new HashSet<ParserPosition>(ParserPositionComparer.Instance) : null!;
    }

    /// <summary>
    /// Delegate that is executed whenever a parser is invoked.
    /// </summary>
    public Action<object, ParseContext>? OnEnterParser { get; set; }

    /// <summary>
    /// Delegate that is executed whenever a parser is left.
    /// </summary>
    public Action<object, ParseContext>? OnExitParser { get; set; }

    /// <summary>
    /// The parser that is used to parse whitespaces and comments.
    /// </summary>
    public Parser<TextSpan>? WhiteSpaceParser { get; set; }

    private int _cacheOffset = -1;
    private TextPosition _cachePosition;

    public void SkipWhiteSpace()
    {
        var offset = Scanner.Cursor.Position.Offset;

        if (offset == _cacheOffset)
        {
            Scanner.Cursor.ResetPosition(_cachePosition);
            return;
        }

        if (WhiteSpaceParser is null)
        {
            if (UseNewLines)
            {
                Scanner.SkipWhiteSpace();
            }
            else
            {
                Scanner.SkipWhiteSpaceOrNewLine();
            }
        }
        else
        {
            ParseResult<TextSpan> _ = default;
            WhiteSpaceParser.Parse(this, ref _);
        }

        _cacheOffset = offset;
        _cachePosition = Scanner.Cursor.Position;
    }

    /// <summary>
    /// Called whenever a parser is invoked.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnterParser<T>(Parser<T> parser)
    {
        CancellationToken.ThrowIfCancellationRequested();
        OnEnterParser?.Invoke(parser, this);
    }

    /// <summary>
    /// Called whenever a parser exits.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitParser<T>(Parser<T> parser)
    {
        OnExitParser?.Invoke(parser, this);
    }

    /// <summary>
    /// Checks if a parser is already active at the current position.
    /// </summary>
    /// <param name="parser">The parser to check.</param>
    /// <returns>True if the parser is already active at the current position, false otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsParserActiveAtPosition(object parser)
    {
        return _activeParserPositions.Contains(new ParserPosition(parser, Scanner.Cursor.Position.Offset));
    }

    /// <summary>
    /// Marks a parser as active at the current position.
    /// </summary>
    /// <param name="parser">The parser to mark as active.</param>
    /// <returns>True if the parser was added (not previously active at this position), false if it was already active at this position.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PushParserAtPosition(object parser)
    {
        return _activeParserPositions.Add(new ParserPosition(parser, Scanner.Cursor.Position.Offset));
    }

    /// <summary>
    /// Marks a parser as inactive at the current position.
    /// </summary>
    /// <param name="parser">The parser to mark as inactive.</param>
    /// <param name="position">The position offset where the parser was entered.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopParserAtPosition(object parser, int position)
    {
        _activeParserPositions.Remove(new ParserPosition(parser, position));
    }

    /// <summary>
    /// Represents a parser instance at a specific position for cycle detection.
    /// </summary>
    private readonly record struct ParserPosition(object Parser, int Position);

    /// <summary>
    /// Uses reference equality for parsers to avoid calling user GetHashCode overrides.
    /// </summary>
    private sealed class ParserPositionComparer : IEqualityComparer<ParserPosition>
    {
        public static readonly ParserPositionComparer Instance = new();

        public bool Equals(ParserPosition x, ParserPosition y) => ReferenceEquals(x.Parser, y.Parser) && x.Position == y.Position;

        public int GetHashCode(ParserPosition obj)
        {
            unchecked
            {
                var hash = RuntimeHelpers.GetHashCode(obj.Parser);
                return (hash * 397) ^ obj.Position;
            }
        }
    }
}
