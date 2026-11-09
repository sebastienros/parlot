using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Parlot.Fluent;

/// <summary>
/// Tells whether a parser that may not be invoked, like the parser of an optional or of a loop, can start at the current
/// position, from the <see cref="ISeekable"/> hints it declares.
/// </summary>
/// <remarks>
/// <para>
/// A choice doesn't invoke an alternative when the next char can't start it. A parser that only has to know whether its
/// parser matches, like <see cref="Optional{T}"/>, can do the same: when the parser is a sequence, invoking it enters every
/// level of it before its first literal fails on the first char.
/// </para>
/// <para>
/// The parser is only skipped for a char it declared it can't start with, as a choice does. The hints are read on the first
/// parse, since the parser can be a <see cref="Deferred{T}"/> set later or a choice whose lookup table is only complete once the
/// grammar is. Like the lookup table of a choice, they are then kept: they aren't read again when
/// <see cref="SeekableResolution.Version"/> changes, which any grammar being built does, so that parsing never allocates.
/// </para>
/// </remarks>
internal sealed class SeekGuard
{
    // Generated code tests the char with a pattern, which isn't emitted for a larger set like the identifier chars
    private const int MaximumGeneratedRanges = 16;

    private readonly object _parser;
    private Lookup? _lookup;

    public SeekGuard(object parser)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
    }

    /// <summary>
    /// Gets whether the parser can start at the current position, after the white space it skips. The cursor is left where it is.
    /// </summary>
    /// <remarks>
    /// When the parser skips white space, the white space is skipped here and the cursor moved back, which needs the position
    /// to be pinned when the input is compacted. The parser skips it again from the cache of <see cref="ParseContext"/>.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanStart(ParseContext context)
    {
        var lookup = _lookup;

        if (lookup is null)
        {
            lookup = Resolve();
        }

        return !lookup.IsActive || lookup.CanStart(context);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Lookup Resolve()
    {
        // Choices reached through the parser build their complete lookup table first, as when a choice builds its own
        var lookup = Lookup.Create(Hints(_parser));

        // The lookup is immutable, a thread reading another one built concurrently reads the same chars
        _lookup = lookup;
        return lookup;
    }

    /// <summary>
    /// Gets the hints a parser can be skipped with, or <see langword="null"/> when it must always be invoked.
    /// </summary>
    private static (char[] ExpectedChars, bool SkipWhitespace)? Hints(object parser)
    {
        // A parser which reads the cursor itself, like a literal, tests the char as fast as the guard would, and a choice
        // already selects its alternatives from it: the guard would only skip the white space a second time when it matches.
        // The analysis takes its own lock, so it isn't run under the lock of the hints.
        if (!LoopAnalysis.InvokesParsersAtStart(SkippedParser(parser)) || IsGeneric(parser, typeof(OneOf<>)))
        {
            return null;
        }

        lock (SeekableResolution.SyncRoot)
        {
            return SeekableHintsOf(parser);
        }
    }

    // The parser a white space skip invokes, like the literal of a Terms parser, which then also tests the char as fast
    private static object SkippedParser(object parser) => IsGeneric(parser, typeof(SkipWhiteSpace<>))
        ? parser.GetType().GetProperty(nameof(SkipWhiteSpace<object>.Parser))!.GetValue(parser)!
        : parser;

    private static bool IsGeneric(object parser, Type definition) => parser.GetType() is { IsGenericType: true } type && type.GetGenericTypeDefinition() == definition;

    private static (char[] ExpectedChars, bool SkipWhitespace)? SeekableHintsOf(object parser)
    {
        var (canSeek, expectedChars, skipWhitespace) = SeekableHints.Of(parser);

        // A choice that can start with any char, or doesn't know all its alternatives yet, tells it with OtherSeekableChar
        if (!canSeek || expectedChars.Length == 0 || Array.IndexOf(expectedChars, OneOf<object>.OtherSeekableChar) >= 0)
        {
            return null;
        }

        return (expectedChars, skipWhitespace);
    }

    /// <summary>
    /// Gets the name of the method generated code invokes for <paramref name="parser"/>: <paramref name="helperName"/> itself,
    /// or a method which first tests whether the parser can start at the current position and fails without invoking it when
    /// it can't, leaving the cursor where it was.
    /// </summary>
    public static string GetHelper(SourceGenerationContext context, object parser, string helperName, Type valueType)
    {
        // The factories of the grammar have run, so every choice can build its complete table
        if (Hints(parser) is not { } usable)
        {
            return helperName;
        }

        var ranges = ToRanges(usable.ExpectedChars);

        if (ranges.Count > MaximumGeneratedRanges)
        {
            return helperName;
        }

        return context.Helpers.GetOrCreate(new GuardKey(parser), $"{context.MethodNamePrefix}_SeekGuard", SourceGenerationContext.GetTypeName(valueType), () =>
        {
            var result = context.CreateResult(valueType);
            var ctx = context.ParseContextName;
            var cursor = context.CursorName;
            var currentName = $"current{context.NextNumber()}";

            if (usable.SkipWhitespace)
            {
                // The white space is skipped again by the parser, from the cache of the context
                var startName = $"start{context.NextNumber()}";
                var recordedName = $"recorded{context.NextNumber()}";

                result.Body.Add($"var {startName} = {cursor}.Offset;");
                result.Body.Add($"var {recordedName} = {cursor}.RecordedFailure;");
                result.Body.Add($"{ctx}.SkipWhiteSpace();");
                result.Body.Add($"var {currentName} = {cursor}.Current;");
                result.Body.Add($"if ({GenerateTest(context, currentName, ranges)})");
                result.Body.Add("{");
                result.Body.Add($"    {cursor}.ResetPosition({startName});");

                // Moving back over the white space isn't a failure the parser had
                result.Body.Add($"    {cursor}.RestoreRecordedFailure({recordedName});");
                result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {result.ValueVariable});");
                result.Body.Add("}");
                result.Body.Add("else");
                result.Body.Add("{");

                // The parser would have failed after skipping it
                result.Body.Add($"    {cursor}.ResetPosition({startName});");
                result.Body.Add("}");
            }
            else
            {
                result.Body.Add($"var {currentName} = {cursor}.Current;");
                result.Body.Add($"if ({GenerateTest(context, currentName, ranges)})");
                result.Body.Add("{");
                result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {result.ValueVariable});");
                result.Body.Add("}");
            }

            return result;
        }).MethodName;
    }

    private static string GenerateTest(SourceGenerationContext context, string variable, List<(char First, char Last)> ranges)
    {
        if (context.SupportsCSharp9SwitchPatterns)
        {
            return $"{variable} is " + string.Join(" or ", ranges.Select(static r => r.First == r.Last
                ? LiteralHelper.CharToLiteral(r.First)
                : $">= {LiteralHelper.CharToLiteral(r.First)} and <= {LiteralHelper.CharToLiteral(r.Last)}"));
        }

        return string.Join(" || ", ranges.Select(r => r.First == r.Last
            ? $"{variable} == {LiteralHelper.CharToLiteral(r.First)}"
            : $"({variable} >= {LiteralHelper.CharToLiteral(r.First)} && {variable} <= {LiteralHelper.CharToLiteral(r.Last)})"));
    }

    private static List<(char First, char Last)> ToRanges(char[] chars)
    {
        var sorted = chars.Distinct().OrderBy(static c => c).ToArray();
        var ranges = new List<(char First, char Last)>();

        foreach (var c in sorted)
        {
            if (ranges.Count > 0 && ranges[ranges.Count - 1].Last + 1 == c)
            {
                ranges[ranges.Count - 1] = (ranges[ranges.Count - 1].First, c);
            }
            else
            {
                ranges.Add((c, c));
            }
        }

        return ranges;
    }

    // The key of a guard's helper, distinct from the parser's own
    private sealed record GuardKey(object Parser);

    /// <summary>
    /// The chars the parser can start with.
    /// </summary>
    private sealed class Lookup
    {
        // The ASCII chars, one bit each
        private readonly ulong _low;
        private readonly ulong _high;

#if NET8_0_OR_GREATER
        private readonly SearchValues<char>? _nonAscii;
#else
        private readonly char[]? _nonAscii;
#endif

        private Lookup(bool isActive, bool skipWhiteSpace, ulong low, ulong high, char[]? nonAscii)
        {
            IsActive = isActive;
            SkipWhiteSpace = skipWhiteSpace;
            _low = low;
            _high = high;

            if (nonAscii is { Length: > 0 })
            {
#if NET8_0_OR_GREATER
                _nonAscii = SearchValues.Create(nonAscii);
#else
                Array.Sort(nonAscii);
                _nonAscii = nonAscii;
#endif
            }
        }

        public bool IsActive { get; }

        public bool SkipWhiteSpace { get; }

        public static Lookup Create((char[] ExpectedChars, bool SkipWhitespace)? hints)
        {
            if (hints is not { } usable)
            {
                return new Lookup(false, false, 0, 0, null);
            }

            ulong low = 0, high = 0;
            var nonAscii = new List<char>();

            foreach (var c in usable.ExpectedChars)
            {
                if (c < 64)
                {
                    low |= 1UL << c;
                }
                else if (c < 128)
                {
                    high |= 1UL << (c - 64);
                }
                else if (!nonAscii.Contains(c))
                {
                    nonAscii.Add(c);
                }
            }

            return new Lookup(true, usable.SkipWhitespace, low, high, nonAscii.ToArray());
        }

        public bool CanStart(ParseContext context)
        {
            var cursor = context.Scanner.Cursor;
            var c = cursor.Current;

            if (SkipWhiteSpace && context.MaySkipWhiteSpace(c))
            {
                var start = cursor.Offset;
                var recorded = cursor.RecordedFailure;

                context.SkipWhiteSpace();
                c = cursor.Current;
                cursor.ResetPosition(start);

                // The parser reads the white space again, and records the failure itself when it fails after it
                if (Contains(c))
                {
                    cursor.RestoreRecordedFailure(recorded);
                    return true;
                }

                return false;
            }

            return Contains(c);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool Contains(char c)
        {
            if (c < 64)
            {
                return (_low & (1UL << c)) != 0;
            }

            if (c < 128)
            {
                return (_high & (1UL << (c - 64))) != 0;
            }

            return ContainsNonAscii(c);
        }

        private bool ContainsNonAscii(char c)
        {
#if NET8_0_OR_GREATER
            return _nonAscii is { } nonAscii && nonAscii.Contains(c);
#else
            return _nonAscii is { } nonAscii && Array.BinarySearch(nonAscii, c) >= 0;
#endif
        }
    }
}
