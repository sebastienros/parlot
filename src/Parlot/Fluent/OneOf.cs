using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Parlot.SourceGeneration;

namespace Parlot.Fluent;

/// <summary>
/// OneOf the inner choices when all parsers return the same type.
/// We then return the actual result of each parser.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class OneOf<T> : Parser<T>, ISeekable, ISourceable /*, ICompilable*/
{
    // Used as a lookup for OneOf<T> to find other OneOf<T> parsers that could
    // be invoked when there is no match.

    public const char OtherSeekableChar = '\0';
    internal readonly CharMap<List<Parser<T>>>? _map;
    internal readonly List<Parser<T>>? _otherParsers;

    // For compilation, ignored for now
    //private readonly CharMap<Func<ParseContext, ValueTuple<bool, T>>> _lambdaMap = new();
    //private Func<ParseContext, ValueTuple<bool, T>>? _lambdaOtherParsers;

    public OneOf(Parser<T>[] parsers)
    {
        Parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));
        OriginalParsers = parsers;

        static void AddUniqueRange(List<Parser<T>> target, IReadOnlyList<Parser<T>> items)
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];

                var exists = false;
                for (var j = 0; j < target.Count; j++)
                {
                    if (ReferenceEquals(target[j], item))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    target.Add(item);
                }
            }
        }

        static void AddUniqueSingle(List<Parser<T>> target, Parser<T> item)
        {
            for (var j = 0; j < target.Count; j++)
            {
                if (ReferenceEquals(target[j], item))
                {
                    return;
                }
            }

            target.Add(item);
        }

        // We can't build a lookup table if there is only one parser
        if (Parsers.Count <= 1)
        {
            return;
        }

        // Technically we shouldn't be able to build a lookup table if not all parsers are seekable.
        // For instance, "or" | identifier is not seekable since 'identifier' is not seekable.
        // Also the order of the parsers need to be conserved in the lookup table results.
        // The solution is to add the parsers that are not seekable in all the groups, and
        // keep the relative order of each. So if the parser p1, p2 ('a'), p3, p4 ('b') are available,
        // the group 'a' will have p1, p2, p3 and the group 'b' will have p1, p3, p4.
        // And then when we parse, we need to try the non-seekable parsers when the _map doesn't have the character to lookup.

        // NB:We could extract the lookup logic as a separate parser and then have a each lookup group as a OneOf one.

        if (Parsers.Any(x => x is ISeekable seekable))
        {
            var lookupTable = Parsers
                            .Where(p => p is ISeekable seekable && seekable.CanSeek)
                            .Cast<ISeekable>()
                            .SelectMany(s => s.ExpectedChars.Select(x => (Key: x, Parser: (Parser<T>)s)))
                            .GroupBy(s => s.Key)
                            .ToDictionary(group => group.Key, group => new List<Parser<T>>());

            foreach (var parser in Parsers)
            {
                if (parser is ISeekable seekable && seekable.CanSeek)
                {
                    foreach (var c in seekable.ExpectedChars)
                    {
                        IReadOnlyList<Parser<T>> subParsers = parser is OneOf<T> oneof ? oneof._map?[c] ?? [parser] : [parser!];

                        if (c != OtherSeekableChar)
                        {
                            AddUniqueRange(lookupTable[c], subParsers);
                        }
                        else
                        {
                            _otherParsers ??= [];
                            AddUniqueRange(_otherParsers, subParsers);

                            foreach (var entry in lookupTable)
                            {
                                AddUniqueRange(entry.Value, subParsers);
                            }
                        }
                    }
                }
                else
                {
                    _otherParsers ??= [];
                    AddUniqueSingle(_otherParsers, parser);

                    foreach (var entry in lookupTable)
                    {
                        AddUniqueSingle(entry.Value, parser);
                    }
                }
            }

            // If only some parser use SkipWhiteSpace, we can't use a lookup table
            // Meaning, All/None is fine, but not Any
            if (Parsers.All(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                SkipWhitespace = true;

                // Remove the SkipWhiteSpace parser if we can
                Parsers = Parsers.Select(x => x is SkipWhiteSpace<T> skip ? skip.Parser : x).ToArray();
            }
            else if (Parsers.Any(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                // There is a mix of parsers that can skip whitespaces.
                // If the ones that can skip don't have a custom WS parser then
                // we can group them in a special OneOf and add them to a lookup
                // with space chars.

                // But there are still cases where this can't be done, like if the
                // parsers have a space in their lookup table, or if non-seekable
                // accept a space. So we can't always redirect spaces automatically.

                lookupTable = null;
            }

            lookupTable?.Remove(OtherSeekableChar);
            var expectedChars = string.Join(",", lookupTable?.Keys.ToArray() ?? []);

            if (lookupTable != null && lookupTable.Count > 0)
            {
                _map = new CharMap<List<Parser<T>>>(lookupTable);

                // This parser is only seekable if there isn't a parser
                // that can't be reached without a lookup.
                // However we use a trick to match other parsers
                // by assigning them to `OtherSeekableChar` such that
                // we can pass on this collection through parsers
                // that forward the ISeekable implementation (e.g. Error, Then)
                // This way we make more OneOf parsers seekable.

                if (_otherParsers == null)
                {
                    CanSeek = true;
                    ExpectedChars = _map.ExpectedChars;
                }
                else
                {
                    CanSeek = true;
                    ExpectedChars = [.. _map.ExpectedChars, OtherSeekableChar];
                    _map.Set(OtherSeekableChar, _otherParsers);
                }
            }
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    /// <summary>
    /// Gets the parsers before they get SkipWhitespace removed.
    /// </summary>
    public IReadOnlyList<Parser<T>> OriginalParsers { get; }

    public IReadOnlyList<Parser<T>> Parsers { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        var start = context.Scanner.Cursor.Position;

        // If all sub-parsers skip whitespaces, do it once here
        if (SkipWhitespace)
        {
            context.SkipWhiteSpace();
        }

        if (_map != null)
        {
            // Each lookup entry also contains the non-seekable parsers
            var seekableParsers = _map[cursor.Current] ?? _otherParsers;

            if (seekableParsers != null)
            {
                var length = seekableParsers.Count;

                for (var i = 0; i < length; i++)
                {
                    if (seekableParsers[i].Parse(context, ref result))
                    {
                        context.ExitParser(this);
                        return true;
                    }
                }
            }
        }
        else
        {
            var parsers = Parsers;
            var length = parsers.Count;

            for (var i = 0; i < length; i++)
            {
                if (parsers[i].Parse(context, ref result))
                {
                    context.ExitParser(this);
                    return true;
                }
            }
        }

        // We only need to reset the position if we are skipping whitespaces
        // as the parsers would have reverted their own state

        if (SkipWhitespace)
        {
            context.Scanner.Cursor.ResetPosition(start);
        }

        context.ExitParser(this);
        return false;
    }

    public override string ToString() => $"{string.Join(" | ", Parsers)}) on [{string.Join(" ", ExpectedChars)}]";

    public Parlot.SourceGeneration.SourceResult GenerateSource(SourceGenerationContext context)
    {
        ThrowHelper.ThrowIfNull(context, nameof(context));

        var result = context.CreateResult(typeof(T));
        var ctx = context.ParseContextName;
        var cursorName = context.CursorName;

        var valueTypeName = result.ValueTypeName ?? SourceGenerationContext.GetTypeName(typeof(T));

        string GetHelper(Parser<T> parser)
        {
            if (parser is not ISourceable sourceable)
            {
                throw new NotSupportedException("OneOf requires all parsers to be source-generatable.");
            }

            var (methodName, _, _, _) = context.Helpers.GetOrCreate(
                parser,
                $"{context.MethodNamePrefix}_OneOf",
                valueTypeName,
                () => sourceable.GenerateSource(context));

            return methodName;
        }

        var startName = $"start{context.NextNumber()}";

        result.Body.Add($"var {startName} = {cursorName}.Position;");

        if (SkipWhitespace)
        {
            result.Body.Add($"{ctx}.SkipWhiteSpace();");
        }

        // If a lookup map was built, emit a switch-based dispatch that only tries
        // the relevant sub-parsers for the current char (mirroring the runtime path).
        if (_map != null && _map.ExpectedChars.Length > 0)
        {
            var currentName = $"current{context.NextNumber()}";
            result.Body.Add($"var {currentName} = {cursorName}.Current;");

            // Group characters that share the same parser list (by contents) to avoid duplicating code.
            var groups = new Dictionary<ParserListKey, ParserGroup>(ParserListKeyComparer.Instance);

            foreach (var ch in _map.ExpectedChars)
            {
                // Skip '\0' as it represents "no match" / default case
                if (ch == '\0')
                {
                    continue;
                }

                var parsersForChar = _map[ch];
                if (parsersForChar is null)
                {
                    continue;
                }

                var key = new ParserListKey(parsersForChar);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ParserGroup(parsersForChar);
                    groups.Add(key, group);
                }

                group.Chars.Add(ch);
            }

            ParserGroup? defaultGroup = null;
            if (_otherParsers != null)
            {
                var key = new ParserListKey(_otherParsers);
                if (!groups.TryGetValue(key, out var group))
                {
                    group = new ParserGroup(_otherParsers);
                    groups.Add(key, group);
                }

                group.IsDefault = true;
                defaultGroup = group;
            }

            result.Body.Add($"switch ({currentName})");
            result.Body.Add("{");

            foreach (var group in groups.Values)
            {
                if (group.Chars.Count == 0)
                {
                    continue;
                }

                if (context.SupportsCSharp9SwitchPatterns)
                {
                    // Use pattern matching to reduce the number of case labels, including range detection.
                    // Example: case '.' or >= '0' and <= '9' or 'E' or 'e':
                    var pattern = BuildCharPattern(group.Chars, rangeThreshold: 4);
                    result.Body.Add($"    case {pattern}:");
                }
                else
                {
                    foreach (var ch in group.Chars)
                    {
                        result.Body.Add($"    case {ToCharLiteral(ch)}:");
                    }
                }

                result.Body.Add("    {");
                EmitParsers(group.Parsers, result, ctx, indent: "        ", getHelper: GetHelper, context);
                result.Body.Add("        break;");
                result.Body.Add("    }");
            }

            if (defaultGroup is not null)
            {
                result.Body.Add("    default:");
                result.Body.Add("    {");
                EmitParsers(defaultGroup.Parsers, result, ctx, indent: "        ", getHelper: GetHelper, context);
                result.Body.Add("        break;");
                result.Body.Add("    }");
            }

            result.Body.Add("}");
        }
        else
        {
            EmitParsers(Parsers, result, ctx, indent: string.Empty, getHelper: GetHelper, context);
        }

        result.Body.Add($"if (!{result.SuccessVariable})");
        result.Body.Add("{");
        result.Body.Add($"    {cursorName}.ResetPosition({startName});");
        result.Body.Add("}");

        return result;
    }

    private static void EmitParsers(
        IReadOnlyList<Parser<T>> parsers,
        SourceResult outerResult,
        string contextVariableName,
        string indent,
        Func<Parser<T>, string> getHelper,
        SourceGenerationContext context)
    {
        if (parsers.Count == 0)
        {
            return;
        }

        var successVar = outerResult.SuccessVariable;
        var valueVar = outerResult.ValueVariable;

        var outTarget = context.DiscardResult ? "_" : valueVar;

        // Build a short-circuiting OR-chain:
        // success = Helper1(ctx, out value) || Helper2(ctx, out value) || ...;
        // (value may be assigned by failed helpers; callers only read it when success==true)
        var firstHelper = getHelper(parsers[0]);

        if (parsers.Count == 1)
        {
            outerResult.Body.Add($"{indent}{successVar} = {firstHelper}({contextVariableName}, out {outTarget});");
            return;
        }

        outerResult.Body.Add($"{indent}{successVar} = {firstHelper}({contextVariableName}, out {outTarget})");

        for (var i = 1; i < parsers.Count; i++)
        {
            var helperName = getHelper(parsers[i]);
            var terminator = i == parsers.Count - 1 ? ";" : string.Empty;
            outerResult.Body.Add($"{indent}    || {helperName}({contextVariableName}, out {outTarget}){terminator}");
        }
    }

    private static string ToCharLiteral(char c)
    {
        return "'" + (c switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '\"' => "\\\"",
            '\0' => "\\0",
            '\a' => "\\a",
            '\b' => "\\b",
            '\f' => "\\f",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            '\v' => "\\v",
            _ when char.IsControl(c) || c > 0x7e => $"\\u{(int)c:X4}",
            _ => c.ToString()
        }) + "'";
    }

    private static string BuildCharPattern(IReadOnlyList<char> chars, int rangeThreshold)
    {
        // Sort and de-dup.
        var list = new List<char>(chars);
        list.Sort();

        var parts = new List<string>();

        for (var i = 0; i < list.Count; i++)
        {
            var start = list[i];
            if (i > 0 && start == list[i - 1])
            {
                continue;
            }

            var end = start;
            var runLength = 1;

            while (i + 1 < list.Count)
            {
                var next = list[i + 1];
                if (next == end)
                {
                    i++;
                    continue;
                }

                if (next == (char)(end + 1))
                {
                    end = next;
                    runLength++;
                    i++;
                    continue;
                }

                break;
            }

            if (runLength >= rangeThreshold)
            {
                parts.Add($">= {ToCharLiteral(start)} and <= {ToCharLiteral(end)}");
            }
            else
            {
                // Small runs are emitted as individual char patterns.
                var c = start;
                for (var r = 0; r < runLength; r++)
                {
                    parts.Add(ToCharLiteral((char)(c + r)));
                }
            }
        }

        return string.Join(" or ", parts);
    }

    private readonly record struct ParserListKey(IReadOnlyList<Parser<T>> Parsers);

    private sealed class ParserReferenceComparer : IEqualityComparer<Parser<T>>
    {
        public static readonly ParserReferenceComparer Instance = new();

        public bool Equals(Parser<T>? x, Parser<T>? y) => ReferenceEquals(x, y);

        public int GetHashCode(Parser<T> obj) => RuntimeHelpers.GetHashCode(obj);
    }

    private sealed class ParserListKeyComparer : IEqualityComparer<ParserListKey>
    {
        public static readonly ParserListKeyComparer Instance = new();

        public bool Equals(ParserListKey x, ParserListKey y)
        {
            if (ReferenceEquals(x.Parsers, y.Parsers))
            {
                return true;
            }

            var xCount = x.Parsers.Count;
            var yCount = y.Parsers.Count;
            if (xCount != yCount)
            {
                return false;
            }

            for (var i = 0; i < xCount; i++)
            {
                if (!ReferenceEquals(x.Parsers[i], y.Parsers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(ParserListKey key)
        {
            unchecked
            {
                var hash = key.Parsers.Count;
                for (var i = 0; i < key.Parsers.Count; i++)
                {
                    hash = (hash * 31) + RuntimeHelpers.GetHashCode(key.Parsers[i]!);
                }

                return hash;
            }
        }
    }

    private sealed class ParserGroup
    {
        public ParserGroup(IReadOnlyList<Parser<T>> parsers)
        {
            Parsers = parsers;
            Chars = [];
        }

        public IReadOnlyList<Parser<T>> Parsers { get; }
        public List<char> Chars { get; }
        public bool IsDefault { get; set; }
    }

    /* We don't use the ICompilable interface anymore since the generated code is still slower than the original one.
     * Furthermore the current implementation is creating too many lambdas (there might be a bug in the code).

        public CompilationResult Compile(CompilationContext context)
        {
            var result = context.CreateCompilationResult<T>();

            // var reset = context.Scanner.Cursor.Position;

            ParameterExpression? reset = null;

            if (SkipWhitespace)
            {
                reset = context.DeclarePositionVariable(result);
                result.Body.Add(context.ParserSkipWhiteSpace());
            }

            Expression block = Expression.Empty();

            if (_map != null)
            {
                // Switch is too slow, even for 2 elements compared to CharMap
                // Expression are also not optimized like the compiler can do.

                // UseSwitch();
                UseLookup();

                void UseLookup()
                {
                    //var seekableParsers = _map[cursor.Current] ?? _otherParsers;

                    //if (seekableParsers != null)
                    //{
                    //    var length = seekableParsers.Count;

                    //    for (var i = 0; i < length; i++)
                    //    {
                    //        if (seekableParsers[i].Parse(context, ref result))
                    //        {
                    //            context.ExitParser(this);
                    //            return true;
                    //        }
                    //    }
                    //}

                    var charMapSetMethodInfo = typeof(CharMap<Func<ParseContext, ValueTuple<bool, T>>>).GetMethod("Set")!;
                    var nullSeekableParser = Expression.Constant(null, typeof(Func<ParseContext, ValueTuple<bool, T>>));

                    var lambdaCache = new List<(List<Parser<T>> Parsers, Func<ParseContext, ValueTuple<bool, T>> CompiledLambda)>();

                    foreach (var key in _map!.ExpectedChars)
                    {
                        Expression group = Expression.Empty();

                        var parsers = _map[key]!;

                        // Search if the same parsers set was already generated and compiled

                        Func<ParseContext, ValueTuple<bool, T>>? cacheCompiledLambda = null;

                        for (var i = 0; i < lambdaCache.Count; i++)
                        {
                            var cacheEntry = lambdaCache[i];
                            if (cacheEntry.Parsers.Count != parsers.Count)
                            {
                                continue;
                            }

                            var match = true;

                            for (var j = 0; j < parsers.Count; j++)
                            {
                                if (cacheEntry.Parsers[j] != parsers[j])
                                {
                                    match = false;
                                    break;
                                }
                            }

                            if (match)
                            {
                                cacheCompiledLambda = cacheEntry.CompiledLambda;
                            }
                        }

                        if (cacheCompiledLambda == null)
                        {
                            var lambdaSuccess = Expression.Variable(typeof(bool), $"successL{context.NextNumber}");
                            var lambdaResult = Expression.Variable(typeof(T), $"resultL{context.NextNumber}");

                            // The list is reversed since the parsers are unwrapped
                            foreach (var parser in parsers.ToArray().Reverse())
                            {
                                var groupResult = parser.Build(context);

                                // lambdaSuccess and lambdaResult will be registered at the top of the method

                                group = Expression.Block(
                                    groupResult.Variables,
                                    Expression.Block(groupResult.Body),
                                    Expression.IfThenElse(
                                        groupResult.Success,
                                        Expression.Block(
                                            Expression.Assign(lambdaSuccess, Expression.Constant(true, typeof(bool))),
                                            context.DiscardResult
                                            ? Expression.Empty()
                                            : Expression.Assign(lambdaResult, groupResult.Value)
                                            ),
                                        group
                                        )
                                    );
                            }

                            var resultExpression = Expression.Variable(typeof(ValueTuple<bool, T>), $"result{context.NextNumber}");
                            var returnTarget = Expression.Label(typeof(ValueTuple<bool, T>));
                            var returnExpression = Expression.Return(returnTarget, resultExpression, typeof(ValueTuple<bool, T>));
                            var returnLabel = Expression.Label(returnTarget, defaultValue: Expression.New(typeof(ValueTuple<bool, T>)));

                            var groupBlock = (BlockExpression)group;

                            var lambda = Expression.Lambda<Func<ParseContext, ValueTuple<bool, T>>>(
                                body: Expression.Block(
                                    type: typeof(ValueTuple<bool, T>),
                                    variables: groupBlock.Variables
                                        .Append(resultExpression)
                                        .Append(lambdaSuccess)
                                        .Append(lambdaResult),
                                    group,
                                    Expression.Assign(
                                        resultExpression,
                                        Expression.New(
                                            typeof(ValueTuple<bool, T>).GetConstructor([typeof(bool), typeof(T)])!,
                                            lambdaSuccess,
                                            context.DiscardResult ?
                                                Expression.Constant(default(T), typeof(T)) :
                                                lambdaResult)
                                    ),
                                    returnExpression,
                                    returnLabel),
                                name: $"_map_{key}_{context.NextNumber}",
                                parameters: [context.ParseContext] // Only the name is used, so it will match the ones inside each compiler
                                );

                            cacheCompiledLambda = lambda.CompileFast(ifFastFailedReturnNull: false, ExpressionHelper.CompilerFlags);
                            lambdaCache.Add((parsers, cacheCompiledLambda));

    #if DEBUG
                            context.Lambdas.Add(lambda);
    #endif
                        }

                        if (key == OtherSeekableChar)
                        {
                            _lambdaOtherParsers = cacheCompiledLambda;
                        }
                        else
                        {
                            _lambdaMap.Set(key, cacheCompiledLambda!);
                        }
                    }

                    var seekableParsers = result.DeclareVariable<Func<ParseContext, ValueTuple<bool, T>>>($"seekableParser{context.NextNumber}", nullSeekableParser);

                    var tupleResult = result.DeclareVariable<ValueTuple<bool, T>>($"tupleResult{context.NextNumber}");

                    result.Body.Add(
                        Expression.Block(
                            // seekableParser = mapValue[key];
                            Expression.Assign(seekableParsers, Expression.Call(Expression.Constant(_lambdaMap), CharMap<Func<ParseContext, ValueTuple<bool, T>>>.IndexerMethodInfo, Expression.Convert(context.Current(), typeof(uint)))),
                            // seekableParser ??= _otherParsers)
                            Expression.IfThen(
                                Expression.Equal(
                                    nullSeekableParser,
                                    seekableParsers
                                    ),
                                Expression.Assign(seekableParsers, Expression.Constant(_lambdaOtherParsers, typeof(Func<ParseContext, ValueTuple<bool, T>>)))
                                ),
                            Expression.IfThen(
                                Expression.NotEqual(
                                    nullSeekableParser,
                                    seekableParsers
                                    ),
                                Expression.Block(
                                    Expression.Assign(tupleResult, Expression.Invoke(seekableParsers, context.ParseContext)),
                                    Expression.Assign(result.Success, Expression.Field(tupleResult, "Item1")),
                                    context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.Assign(result.Value, Expression.Field(tupleResult, "Item2"))
                                    )
                            )
                        )
                    );
                }

    #pragma warning disable CS8321 // Local function is declared but never used
                void UseSwitch()
                {
                    // Lookup table is converted to a switch expression

                    // switch (Cursor.Current)
                    // {
                    //   case 'a' :
                    //     parse1 instructions
                    //     
                    //     if (parser1.Success)
                    //     {
                    //        success = true;
                    //        value = parse1.Value;
                    //     }
                    // 
                    //     break; // implicit in SwitchCase expression
                    //
                    //   case 'b' :
                    //   ...
                    // }

                    var cases = _map!.ExpectedChars.Where(x => x != OtherSeekableChar).Select(key =>
                    {
                        Expression group = Expression.Empty();

                        var parsers = _map[key];

                        // The list is reversed since the parsers are unwrapped
                        foreach (var parser in parsers!.ToArray().Reverse())
                        {
                            var groupResult = parser.Build(context);

                            group = Expression.Block(
                                groupResult.Variables,
                                Expression.Block(groupResult.Body),
                                Expression.IfThenElse(
                                    groupResult.Success,
                                    Expression.Block(
                                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                                        context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.Assign(result.Value, groupResult.Value)
                                        ),
                                    group
                                    )
                                );
                        }

                        return (Key: (uint)key, Body: group);

                    }).ToArray();

                    // Construct default case body
                    Expression defaultBody = Expression.Empty();

                    if (_otherParsers != null)
                    {
                        foreach (var parser in _otherParsers.ToArray().Reverse())
                        {
                            var defaultCompileResult = parser.Build(context);

                            defaultBody = Expression.Block(
                                defaultCompileResult.Variables,
                                Expression.Block(defaultCompileResult.Body),
                                Expression.IfThenElse(
                                    defaultCompileResult.Success,
                                    Expression.Block(
                                        Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                                        context.DiscardResult
                                        ? Expression.Empty()
                                        : Expression.Assign(result.Value, defaultCompileResult.Value)
                                        ),
                                    defaultBody
                                    )
                                );
                        }
                    }

                    block =
                        Expression.Switch(
                            Expression.Convert(context.Current(), typeof(uint)),
                            defaultBody,
                            cases.Select(c => Expression.SwitchCase(
                                c.Body,
                                Expression.Constant(c.Key)
                            )).ToArray()
                        );
                }
    #pragma warning restore CS8321 // Local function is declared but never used

            }
            else
            {
                // parse1 instructions
                // 
                // if (parser1.Success)
                // {
                //    success = true;
                //    value = parse1.Value;
                // }
                // else
                // {
                //   parse2 instructions
                //   
                //   if (parser2.Success)
                //   {
                //      success = true;
                //      value = parse2.Value
                //   }
                //   
                //   ...
                // }

                foreach (var parser in Parsers.Reverse())
                {
                    var parserCompileResult = parser.Build(context);

                    block = Expression.Block(
                        parserCompileResult.Variables,
                        Expression.Block(parserCompileResult.Body),
                        Expression.IfThenElse(
                            parserCompileResult.Success,
                            Expression.Block(
                                Expression.Assign(result.Success, Expression.Constant(true, typeof(bool))),
                                context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(result.Value, parserCompileResult.Value)
                                ),
                            block
                            )
                        );
                }
            }

            result.Body.Add(block);

            // [if skipwhitespace]
            // if (!success)
            // {
            //    context.Scanner.Cursor.ResetPosition(begin);
            // }

            if (reset != null)
            {
                result.Body.Add(
                    Expression.IfThen(
                        Expression.Not(result.Success),
                        context.ResetPosition(reset)
                        )
                    );
            }

            return result;
        }
    */
}
