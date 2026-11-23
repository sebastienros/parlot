using Parlot.Rewriting;
using System;
using System.Globalization;
using System.Linq;

namespace Parlot.Fluent;

internal interface IWithoutValueParser
{
    bool CanParseWithoutValue { get; }

    bool ParseWithoutValue(ParseContext context, out int start, out int end);
}

public abstract partial class Parser<T> : IParser<T>, IWithoutValueParser
{
    public abstract bool Parse(ParseContext context, ref ParseResult<T> result);

    /// <summary>
    /// Parses like <see cref="Parse(ParseContext, ref ParseResult{T})"/>, without building the value, for the parsers which
    /// don't use it, like <see cref="Capture{T}"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It matches what <see cref="Parse(ParseContext, ref ParseResult{T})"/> matches: it moves the cursor, invokes the same
    /// parsers, calls the same <see cref="ParseContext"/> methods and sets the same <see cref="ParseResult{T}.Start"/> and
    /// <see cref="ParseResult{T}.End"/>. The <see cref="ParseResult{T}.Value"/> is not set, it must not be read.
    /// </para>
    /// <para>
    /// The parsers whose value allocates, like the lists of <see cref="ZeroOrMany{T}"/>, or which invoke such parsers, like
    /// sequences, override it with <see cref="CanParseWithoutValue"/>. The generated parsers do the same with
    /// <c>SourceGenerationContext.DiscardResult</c>.
    /// </para>
    /// </remarks>
    internal virtual bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result) => Parse(context, ref result);

    /// <summary>
    /// Gets whether <see cref="ParseWithoutValue"/> does less than <see cref="Parse(ParseContext, ref ParseResult{T})"/>.
    /// </summary>
    /// <remarks>
    /// The parsers which don't use the value of a parser read it once, when they are built, and invoke
    /// <see cref="ParseWithoutValue"/> only when it is set, such that the other parsers aren't invoked through the extra call.
    /// It can't change once the parser is built, a <see cref="Deferred{T}"/> parser is always set.
    /// </remarks>
    internal virtual bool CanParseWithoutValue => false;

    bool IWithoutValueParser.CanParseWithoutValue => CanParseWithoutValue;

    bool IWithoutValueParser.ParseWithoutValue(ParseContext context, out int start, out int end)
    {
        var result = new ParseResult<T>();
        var success = CanParseWithoutValue ? ParseWithoutValue(context, ref result) : Parse(context, ref result);
        start = result.Start;
        end = result.End;
        return success;
    }

    /// <summary>
    /// Attempts to parse the input and returns whether the parse was successful.
    /// This is the covariant version of Parse for use with the IParser&lt;out T&gt; interface.
    /// </summary>
    bool IParser<T>.Parse(ParseContext context, out int start, out int end, out object? value)
    {
        var result = new ParseResult<T>();
        var success = Parse(context, ref result);
        start = result.Start;
        end = result.End;
        value = result.Value;
        return success;
    }

    /// <summary>
    /// Builds a parser that converts the previous result when it succeeds.
    /// </summary>
    public Parser<U> Then<U>(Func<T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result, and can alter the current <see cref="ParseContext"/>.
    /// </summary>
    public Parser<U> Then<U>(Func<ParseContext, T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result, and can use the <see cref="ParseContext"/> and the start and end offsets.
    /// </summary>
    public Parser<U> Then<U>(Func<ParseContext, int, int, T, U> conversion) => new Then<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result.
    /// </summary>
    public Parser<U> Then<U>(U value) => new Then<T, U>(this, value);

    /// <summary>
    /// Builds a parser that discards the previous result and returns the default value of type <typeparamref name="U"/>.
    /// For types that implement <see cref="IConvertible"/>, attempts runtime type conversion via <see cref="Convert.ChangeType(object?, Type, IFormatProvider?)"/>.
    /// </summary>
    /// <remarks>
    /// When <typeparamref name="U"/> implements <see cref="IConvertible"/> and the parsed value also implements
    /// <see cref="IConvertible"/>, conversion uses <see cref="CultureInfo.CurrentCulture"/> and may box value types.
    /// Prefer an explicit <see cref="Then{U}(Func{T, U})"/> conversion or <see cref="Then{U}(U)"/> constant on hot paths
    /// or when culture-invariant conversion is required.
    /// </remarks>
    public Parser<U?> Then<U>()
    {
        // Check at construction time whether runtime IConvertible conversion is possible
        var targetImplementsIConvertible = typeof(IConvertible).IsAssignableFrom(typeof(U));
        var sourceCanImplementIConvertible =
            typeof(IConvertible).IsAssignableFrom(typeof(T))
            || (!typeof(T).IsValueType && !typeof(T).IsSealed);

        if (targetImplementsIConvertible && sourceCanImplementIConvertible)
        {
            return new Then<T, U?>(this, static x =>
            {
                // If both T and U are IConvertible, try to convert
                if (x is IConvertible)
                {
                    try
                    {
                        return (U?)Convert.ChangeType(x, typeof(U), CultureInfo.CurrentCulture);
                    }
                    catch
                    {
                        // Fall back to default if conversion fails
                        return default;
                    }
                }

                // For non-convertible types, return default
                return default;
            });
        }
        else
        {
            // For types that cannot implement IConvertible, just return default
            return new Then<T, U?>(this, default(U));
        }
    }

    /// <summary>
    /// Builds a parser that converts the previous result with a conversion which can reject it: when
    /// <paramref name="conversion"/> returns <see langword="false"/> the parser fails and the cursor is moved back to where
    /// it was, like <see cref="When(Func{ParseContext, T, bool})"/>. It replaces a <c>When(...).Then(...)</c> pair with a
    /// single parser and a single callback.
    /// </summary>
    /// <param name="conversion">
    /// Receives the <see cref="ParseContext"/>, the start and end offsets of the value and the value, and returns whether it
    /// is converted, the converted value in its <see langword="out"/> parameter.
    /// </param>
    public Parser<U> ThenTry<U>(TryConvert<T, U> conversion) => new ThenTry<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser that converts the previous result with a conversion which can reject it: when
    /// <paramref name="conversion"/> returns a tuple whose <c>Success</c> is <see langword="false"/> the parser fails and
    /// the cursor is moved back to where it was, like <see cref="When(Func{ParseContext, T, bool})"/>.
    /// </summary>
    /// <param name="conversion">Returns whether the value is converted, and the converted value.</param>
    public Parser<U> ThenTry<U>(Func<T, (bool Success, U Value)> conversion) => new ThenTry<T, U>(this, conversion);

    /// <summary>
    /// Builds a parser like <see cref="ThenTry{U}(TryConvert{T, U})"/> which records why a value is rejected with the
    /// failure: <paramref name="reason"/> is <see cref="Cursor.FurthestReason"/> when the parse fails there.
    /// </summary>
    /// <param name="conversion">
    /// Receives the <see cref="ParseContext"/>, the start and end offsets of the value and the value, and returns whether it
    /// is converted, the converted value in its <see langword="out"/> parameter.
    /// </param>
    /// <param name="reason">Why a value is rejected: a constant, like a message or an <see langword="enum"/> value.</param>
    /// <remarks>
    /// A conversion which rejects values for different reasons records its own instead, with
    /// <see cref="Cursor.RecordFailure(int, object)"/> before it returns <see langword="false"/>.
    /// </remarks>
    public Parser<U> ThenTry<U>(TryConvert<T, U> conversion, object reason) => new ThenTry<T, U>(this, conversion, reason);

    /// <summary>
    /// Builds a parser like <see cref="ThenTry{U}(Func{T, ValueTuple{bool, U}})"/> which records why a value is rejected with
    /// the failure: <paramref name="reason"/> is <see cref="Cursor.FurthestReason"/> when the parse fails there.
    /// </summary>
    /// <param name="conversion">Returns whether the value is converted, and the converted value.</param>
    /// <param name="reason">Why a value is rejected: a constant, like a message or an <see langword="enum"/> value.</param>
    public Parser<U> ThenTry<U>(Func<T, (bool Success, U Value)> conversion, object reason) => new ThenTry<T, U>(this, conversion, reason);

    /// <summary>
    /// Builds a parser that converts the previous result when it succeeds or returns a default value if it fails.
    /// </summary>
    public Parser<U> ThenElse<U>(Func<T, U> conversion, U elseValue) => new Then<T, U>(this, conversion).Else(elseValue);

    /// <summary>
    /// Builds a parser that converts the previous result or returns a default value if it fails, and can alter the current <see cref="ParseContext"/>.
    /// </summary>
    public Parser<U> ThenElse<U>(Func<ParseContext, T, U> conversion, U elseValue) => new Then<T, U>(this, conversion).Else(elseValue);

    /// <summary>
    /// Builds a parser that converts the previous result or returns a default value if it fails.
    /// </summary>
    public Parser<U> ThenElse<U>(U value, U elseValue) => new Then<T, U>(this, value).Else(elseValue);

    /// <summary>
    /// Builds a parser that emits an error when the previous parser failed.
    /// </summary>
    public Parser<T> ElseError(string message) => new ElseError<T>(this, message);

    /// <summary>
    /// Builds a parser that emits an error.
    /// </summary>
    public Parser<T> Error(string message) => new Error<T>(this, message);

    /// <summary>
    /// Builds a parser that emits an error.
    /// </summary>
    public Parser<U> Error<U>(string message) => new Error<T, U>(this, message);

    /// <summary>
    /// Builds a parser that reports a warning when the previous parser succeeds, keeping its result.
    /// </summary>
    /// <remarks>
    /// Warnings are recorded in <see cref="ParseContext.Diagnostics"/> when <see cref="ParseContext.CollectDiagnostics"/>
    /// is enabled. When <see cref="ParseContext.TreatWarningsAsErrors"/> is enabled the warning is reported as an error
    /// and the parser fails.
    /// </remarks>
    public Parser<T> Warning(string message) => new Warning<T>(this, message);

    /// <summary>
    /// Names a parser.
    /// </summary>
    public Parser<T> Named(string name)
    {
        Name = name;
        return this;
    }

    /// <summary>
    /// Builds a parser that verifies the previous parser result matches a predicate.
    /// </summary>
    [Obsolete("Use When(Func<ParseContext, T, bool> predicate) instead.")]
    public Parser<T> When(Func<T, bool> predicate) => new When<T>(this, predicate);

    /// <summary>
    /// Builds a parser that verifies the previous parser result matches a predicate.
    /// </summary>
    public Parser<T> When(Func<ParseContext, T, bool> predicate) => new When<T>(this, predicate);

    /// <summary>
    /// Builds a parser that verifies the previous parser result matches a predicate, and records why it doesn't with the
    /// failure: <paramref name="reason"/> is <see cref="Cursor.FurthestReason"/> when the parse fails there.
    /// </summary>
    /// <param name="predicate">Whether the value is accepted.</param>
    /// <param name="reason">Why a value is rejected: a constant, like a message or an <see langword="enum"/> value.</param>
    /// <remarks>
    /// Unlike <c>ElseError</c>, the rejection doesn't stop the parse: an alternative can still match, and the reason is only
    /// reported when the parse fails at the start of the rejected value, with
    /// <c>TryParse(..., out ParseError error)</c> in <see cref="ParseError.Reason"/>.
    /// </remarks>
    public Parser<T> When(Func<ParseContext, T, bool> predicate, object reason) => new When<T>(this, predicate, reason);

    /// <summary>
    /// Builds a parser that ensures the specified parser matches at the current position without consuming input (positive lookahead).
    /// </summary>
    public Parser<T> WhenFollowedBy<U>(Parser<U> lookahead) => new WhenFollowedBy<T>(this, lookahead.Then<object>(value: null!));

    /// <summary>
    /// Builds a parser that ensures the specified parser does NOT match at the current position without consuming input (negative lookahead).
    /// </summary>
    public Parser<T> WhenNotFollowedBy<U>(Parser<U> lookahead) => new WhenNotFollowedBy<T>(this, lookahead.Then<object>(value: null!));

    /// <summary>
    /// Builds a parser that ensures the specified parser does NOT match at the current position without consuming input
    /// (negative lookahead), and records why with the failure: <paramref name="reason"/> is
    /// <see cref="Cursor.FurthestReason"/> when the parse fails where the lookahead matched.
    /// </summary>
    /// <param name="lookahead">The parser which must not match.</param>
    /// <param name="reason">Why it must not: a constant, like a message or an <see langword="enum"/> value.</param>
    public Parser<T> WhenNotFollowedBy<U>(Parser<U> lookahead, object reason) => new WhenNotFollowedBy<T>(this, lookahead.Then<object>(value: null!), reason);

    /// <summary>
    /// Builds a parser that selects a target parser based on the previous result.
    /// </summary>
    public Parser<U> Switch<U>(Func<ParseContext, T, int> selector, params Parser<U>[] parsers) => new Switch<T, U>(this, selector, parsers);

    /// <summary>
    /// Builds a parser that ensures the cursor is at the end of the input.
    /// </summary>
    public Parser<T> Eof() => new Eof<T>(this);

    /// <summary>
    /// Builds a parser that skips the white space and comments before this parser, and fails when they contain a new line of
    /// <see cref="ParseContext.WhiteSpaceSet"/>, such as JavaScript's "[no LineTerminator here]".
    /// </summary>
    /// <remarks>
    /// <c>Terms.Text("return").And(expression.NoNewLineBefore().Optional())</c> doesn't read an expression on the next line.
    /// </remarks>
    public Parser<T> NoNewLineBefore() => new NoNewLineBefore<T>(this);

    /// <summary>
    /// Builds a parser that skips the white space and comments before this parser, and fails when they contain one of
    /// <paramref name="newLines"/>.
    /// </summary>
    /// <param name="newLines">The chars that are new lines, for instance <c>"\n\r\u2028\u2029"</c> for JavaScript.</param>
    public Parser<T> NoNewLineBefore(string newLines) => new NoNewLineBefore<T>(this, newLines ?? throw new ArgumentNullException(nameof(newLines)));

    /// <summary>
    /// Builds a parser that prevents any parser from moving back before the end of the previous parser's match once it succeeds,
    /// such that its text is no longer buffered when the input is streamed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a cut: use it after a parser that, once matched, can't be followed by another alternative,
    /// like a statement in a list of statements. A parser that would then move back before it throws a <see cref="ParseException"/>
    /// when the input is streamed through a compacting buffer. The parser has no effect otherwise.
    /// </para>
    /// <para>
    /// Repetitions, separated lists and alternatives with distinct first chars already release their text,
    /// see the streaming documentation.
    /// </para>
    /// </remarks>
    public Parser<T> Commit() => new Commit<T>(this);

    /// <summary>
    /// Builds a parser that updates a state of a custom parse context while the previous parser runs, then restores it.
    /// </summary>
    /// <typeparam name="C">The concrete <see cref="ParseContext" /> type holding the state.</typeparam>
    /// <typeparam name="S">The type of the state.</typeparam>
    /// <param name="get">Reads the state from the context.</param>
    /// <param name="set">Writes the state to the context.</param>
    /// <param name="update">Returns the state the parser runs with, from the current one.</param>
    /// <remarks>
    /// <para>
    /// The state is restored when the parser succeeds, fails or throws. Parsers invoked by the parser, like <c>If</c>,
    /// <c>Select</c> or <c>When</c>, read the updated state. For instance, with flags of a custom context:
    /// <code>
    /// var functionBody = body.WithState&lt;JsContext, Flags&gt;(static c => c.Flags, static (c, f) => c.Flags = f, static f => (f | Flags.Return) &amp; ~Flags.NoIn);
    /// </code>
    /// </para>
    /// <para>
    /// A source-generated parser runs with Parlot's own context: use <see cref="WithState{S}(Func{S}, Action{S}, Func{S, S})"/>
    /// with a state held by a configuration parameter instead.
    /// </para>
    /// </remarks>
    public Parser<T> WithState<C, S>(Func<C, S> get, Action<C, S> set, Func<S, S> update) where C : ParseContext
        => new WithState<C, S, T>(this, get, set, update);

    /// <summary>
    /// Builds a parser that updates a state while the previous parser runs, then restores it.
    /// </summary>
    /// <typeparam name="S">The type of the state.</typeparam>
    /// <param name="get">Reads the state.</param>
    /// <param name="set">Writes the state.</param>
    /// <param name="update">Returns the state the parser runs with, from the current one.</param>
    /// <remarks>
    /// The state is restored when the parser succeeds, fails or throws. Parsers invoked by the parser, like <c>If</c>,
    /// <c>Select</c> or <c>When</c>, read the updated state. In a source-generated parser the state can be held by a
    /// configuration parameter: <c>body.WithState(() => options.Flags, f => options.Flags = f, static f => f | Flags.Return)</c>.
    /// A state shared by concurrent parses must be held by the parse context instead, see
    /// <see cref="WithState{C, S}(Func{C, S}, Action{C, S}, Func{S, S})"/>.
    /// </remarks>
    public Parser<T> WithState<S>(Func<S> get, Action<S> set, Func<S, S> update)
        => new WithState<ParseContext, S, T>(this, get, set, update);

    /// <summary>
    /// Builds a parser that discards the previous result and replaces it by the specified type or value.
    /// </summary>
    [Obsolete("Use Then<U>() instead.")]
    public Parser<U?> Discard<U>() => new Discard<T, U?>(this, default);

    /// <summary>
    /// Builds a parser that discards the previous result and replaces it by the specified type or value.
    /// </summary>
    [Obsolete("Use Then<U>(value) instead.")]
    public Parser<U> Discard<U>(U value) => new Discard<T, U>(this, value);

    /// <summary>
    /// Builds a parser that returns a default value if the previous parser fails.
    /// </summary>
    public Parser<T> Else(T value) => new Else<T>(this, value);

    /// <summary>
    /// Builds a parser that returns a default value computed by a function if the previous parser fails.
    /// </summary>
    public Parser<T> Else(Func<ParseContext, T> func) => new Else<T>(this, func);

    /// <summary>
    /// Builds a parser that lists all possible matches to improve performance.
    /// </summary>
    public Parser<T> Lookup(bool skipWhiteSpace = false, params ReadOnlySpan<char> expectedChars) => new Seekable<T>(this, skipWhiteSpace, expectedChars);

    /// <summary>
    /// Builds a parser that lists all possible matches to improve performance.
    /// </summary>
    /// <remarks>
    /// The hints are only used when every parser can seek and they all skip white space the same way,
    /// otherwise the resulting parser can start with any char.
    /// </remarks>
    public Parser<T> Lookup(params ISeekable[] parsers)
    {
        ThrowHelper.ThrowIfNull(parsers, nameof(parsers));

        var canSeek = parsers.Length > 0;

        foreach (var parser in parsers)
        {
            if (parser is null)
            {
                throw new ArgumentException("Parsers array must not contain null elements.", nameof(parsers));
            }

            canSeek &= parser.CanSeek && parser.SkipWhitespace == parsers[0].SkipWhitespace;
        }

        return canSeek
            ? new Seekable<T>(this, parsers[0].SkipWhitespace, parsers.SelectMany(x => x.ExpectedChars).ToArray())
            : new Seekable<T>(this, false, []) { CanSeek = false };
    }
}
