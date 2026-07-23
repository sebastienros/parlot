using System;
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
    /// <remarks>
    /// The pairs are pushed and popped in LIFO order since they follow the parsers' call stack, so they are
    /// kept in a plain stack rather than a hash set. A stack doesn't need to rehash its content while it grows,
    /// which was allocating several intermediate tables for deeply nested grammars, and a lookup is a vectorized
    /// scan over the recorded positions since two entries rarely share the same one.
    /// </remarks>
    private int[]? _activePositions;
    private object[]? _activeParsers;
    private int _activeCount;

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
        var offset = Scanner.Cursor.Offset;

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
        return IndexOfActiveParser(parser, Scanner.Cursor.Offset) >= 0;
    }

    /// <summary>
    /// Returns the index of an active parser at the specified position, or <c>-1</c> when it's not active.
    /// </summary>
    private int IndexOfActiveParser(object parser, int position)
    {
        var count = _activeCount;

        if (count == 0)
        {
            return -1;
        }

        // Scan the recorded positions first, the parsers are only compared when a position matches.
        // A parser that consumed something has a different position, so matches are rare.

        var positions = _activePositions!.AsSpan(0, count);
        var parsers = _activeParsers!;
        var start = 0;

        while (true)
        {
            var found = positions.IndexOf(position);

            if (found < 0)
            {
                return -1;
            }

            var index = start + found;

            if (ReferenceEquals(parsers[index], parser))
            {
                return index;
            }

            positions = positions.Slice(found + 1);
            start = index + 1;
        }
    }

    /// <summary>
    /// Marks a parser as active at the current position.
    /// </summary>
    /// <param name="parser">The parser to mark as active.</param>
    /// <returns>True if the parser was added (not previously active at this position), false if it was already active at this position.</returns>
    public bool PushParserAtPosition(object parser)
    {
        var position = Scanner.Cursor.Offset;

        if (IndexOfActiveParser(parser, position) >= 0)
        {
            return false;
        }

        var count = _activeCount;

        if (_activePositions is null)
        {
            _activePositions = new int[InitialActiveParsersCapacity];
            _activeParsers = new object[InitialActiveParsersCapacity];
        }
        else if (count == _activePositions.Length)
        {
            Array.Resize(ref _activePositions, count * 2);
            Array.Resize(ref _activeParsers, count * 2);
        }

        _activePositions[count] = position;
        _activeParsers![count] = parser;
        _activeCount = count + 1;

        return true;
    }

    // Kept small since most grammars only nest a few parsers, the arrays grow for the deeper ones
    private const int InitialActiveParsersCapacity = 8;

    /// <summary>
    /// Marks a parser as inactive at the current position.
    /// </summary>
    /// <param name="parser">The parser to mark as inactive.</param>
    /// <param name="position">The position offset where the parser was entered.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PopParserAtPosition(object parser, int position)
    {
        var count = _activeCount;

        if (count == 0)
        {
            return;
        }

        // Parsers are entered and left in LIFO order, so the entry to remove is the last one

        var last = count - 1;

        if (_activePositions![last] == position && ReferenceEquals(_activeParsers![last], parser))
        {
            _activeCount = last;
            return;
        }

        RemoveActiveParserNotInlined(parser, position);
    }

    private void RemoveActiveParserNotInlined(object parser, int position)
    {
        // A custom parser could leave entries out of order, in which case the entry is looked up.
        // An entry can't be recorded twice since PushParserAtPosition rejects duplicates.

        var index = IndexOfActiveParser(parser, position);

        if (index < 0)
        {
            return;
        }

        var remaining = _activeCount - index - 1;

        Array.Copy(_activePositions!, index + 1, _activePositions!, index, remaining);
        Array.Copy(_activeParsers!, index + 1, _activeParsers!, index, remaining);

        _activeCount--;
    }
}
