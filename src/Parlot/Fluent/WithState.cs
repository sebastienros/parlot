using Parlot.Rewriting;
using Parlot.SourceGeneration;
using System;

namespace Parlot.Fluent;

/// <summary>
/// A parser that sets a state while its inner parser runs, then restores the previous one, whether the parser succeeds,
/// fails or throws.
/// </summary>
/// <typeparam name="C">The concrete <see cref="ParseContext" /> type the state is read from and written to.</typeparam>
/// <typeparam name="S">The type of the state.</typeparam>
/// <typeparam name="T">The output parser type.</typeparam>
/// <remarks>
/// The state is read, the updated state is written, the parser runs, and the state that was read is written back. Parsers
/// invoked by the inner parser, like <see cref="If{C, T}"/>, <see cref="Select{C, T}"/> or <see cref="Parser{T}.When(Func{ParseContext, T, bool})"/>,
/// see the updated state.
/// </remarks>
public sealed class WithState<C, S, T> : Parser<T>, ISeekablePrefixes, ISourceable, ILoopAnalyzable where C : ParseContext
{
    private readonly Parser<T> _parser;
    private readonly Func<C, S>? _contextGet;
    private readonly Action<C, S>? _contextSet;
    private readonly Func<S>? _get;
    private readonly Action<S>? _set;
    private readonly Func<S, S> _update;

    // Whether the parser is invoked without its value when this one's isn't used
    private readonly bool _withoutValue;

    // The key of the generated method that sets and restores the state, distinct from this parser's own
    private readonly object _scopeKey = new();

    /// <summary>
    /// Creates a parser that updates a state of the parse context while <paramref name="parser"/> runs.
    /// </summary>
    /// <param name="parser">The parser to run with the updated state.</param>
    /// <param name="get">Reads the state from the context.</param>
    /// <param name="set">Writes the state to the context.</param>
    /// <param name="update">Returns the state the parser runs with, from the current one.</param>
    public WithState(Parser<T> parser, Func<C, S> get, Action<C, S> set, Func<S, S> update)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _contextGet = get ?? throw new ArgumentNullException(nameof(get));
        _contextSet = set ?? throw new ArgumentNullException(nameof(set));
        _update = update ?? throw new ArgumentNullException(nameof(update));
        _withoutValue = parser.CanParseWithoutValue;
    }

    /// <summary>
    /// Creates a parser that updates a state held outside of the parse context while <paramref name="parser"/> runs.
    /// </summary>
    /// <param name="parser">The parser to run with the updated state.</param>
    /// <param name="get">Reads the state.</param>
    /// <param name="set">Writes the state.</param>
    /// <param name="update">Returns the state the parser runs with, from the current one.</param>
    public WithState(Parser<T> parser, Func<S> get, Action<S> set, Func<S, S> update)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _get = get ?? throw new ArgumentNullException(nameof(get));
        _set = set ?? throw new ArgumentNullException(nameof(set));
        _update = update ?? throw new ArgumentNullException(nameof(update));
        _withoutValue = parser.CanParseWithoutValue;
    }

    // The hints of the parser, read when they are asked for since it can be a Deferred set later. The state doesn't change
    // how white space is skipped.
    private ForwardedHints? _hints;

    /// <summary>
    /// Gets whether the parser can be selected from a single char, like its inner parser.
    /// </summary>
    public bool CanSeek => ForwardedHints.Get(ref _hints, _parser).CanSeek;

    /// <summary>
    /// Gets the chars the inner parser can start with.
    /// </summary>
    public char[] ExpectedChars => ForwardedHints.Get(ref _hints, _parser).ExpectedChars;

    /// <summary>
    /// Gets whether the inner parser skips white space first.
    /// </summary>
    public bool SkipWhitespace => ForwardedHints.Get(ref _hints, _parser).SkipWhitespace;

    string[]? ISeekablePrefixes.ExpectedPrefixes => SeekableHints.Prefixes(_parser);

    bool ILoopAnalyzable.AnalyzeStart(LoopAnalysis analysis) => analysis.Invoke(_parser);

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var saved = Enter(context);

        try
        {
            var success = _parser.Parse(context, ref result);

            context.ExitParser(this);
            return success;
        }
        finally
        {
            Restore(context, saved);
        }
    }

    internal override bool CanParseWithoutValue => _withoutValue;

    // Parse, with the parser invoked without its value
    internal override bool ParseWithoutValue(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var saved = Enter(context);

        try
        {
            var success = _withoutValue ? _parser.ParseWithoutValue(context, ref result) : _parser.Parse(context, ref result);

            context.ExitParser(this);
            return success;
        }
        finally
        {
            Restore(context, saved);
        }
    }

    // Writes the updated state and returns the current one
    private S Enter(ParseContext context)
    {
        if (_contextGet is not null)
        {
            var c = (C)context;
            var saved = _contextGet(c);
            _contextSet!(c, _update(saved));
            return saved;
        }
        else
        {
            var saved = _get!();
            _set!(_update(saved));
            return saved;
        }
    }

    private void Restore(ParseContext context, S saved)
    {
        if (_contextSet is not null)
        {
            _contextSet((C)context, saved);
        }
        else
        {
            _set!(saved);
        }
    }

    public SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        if (_parser is not ISourceable sourceable)
        {
            throw new NotSupportedException("WithState requires a source-generatable parser.");
        }

        var result = context.CreateResult(typeof(T));
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;

        // The try/finally is in a method of its own, out of the method of the parser invoking this one, which can be a
        // recursive one whose frame it would grow
        var scopeHelper = context.Helpers
            .GetOrCreate(_scopeKey, $"{context.MethodNamePrefix}_WithState", valueTypeName, () => GenerateScope(context, sourceable))
            .MethodName;

        result.Body.Add($"{result.SuccessVariable} = {scopeHelper}({context.ParseContextName}, out {outTarget});");

        return result;
    }

    private SourceResult GenerateScope(SourceGenerationContext context, ISourceable sourceable)
    {
        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var valueTypeName = SourceGenerationContext.GetTypeName(typeof(T));
        var stateTypeName = SourceGenerationContext.GetTypeName(typeof(S));
        var outTarget = context.DiscardResult ? "_" : result.ValueVariable;
        var saved = $"savedState{context.NextNumber()}";
        var update = context.RegisterLambda(_update);

        string read, write, restore;

        if (_contextGet is not null)
        {
            var typedContext = $"(({SourceGenerationContext.GetTypeName(typeof(C))}){ctx})";
            var get = context.RegisterLambda(_contextGet);
            var set = context.RegisterLambda(_contextSet!);
            read = $"{get}({typedContext})";
            write = $"{set}({typedContext}, {update}({saved}));";
            restore = $"{set}({typedContext}, {saved});";
        }
        else
        {
            var get = context.RegisterLambda(_get!);
            var set = context.RegisterLambda(_set!);
            read = $"{get}()";
            write = $"{set}({update}({saved}));";
            restore = $"{set}({saved});";
        }

        var helperName = context.Helpers
            .GetOrCreate(sourceable, $"{context.MethodNamePrefix}_WithState_Parser", valueTypeName, () => sourceable.GenerateSource(context))
            .MethodName;

        result.Body.Add($"{stateTypeName} {saved} = {read};");
        result.Body.Add(write);
        result.Body.Add("try");
        result.Body.Add("{");
        result.Body.Add($"    {result.SuccessVariable} = {helperName}({ctx}, out {outTarget});");
        result.Body.Add("}");
        result.Body.Add("finally");
        result.Body.Add("{");
        result.Body.Add($"    {restore}");
        result.Body.Add("}");

        return result;
    }

    public override string ToString() => $"{_parser} (With State)";
}
