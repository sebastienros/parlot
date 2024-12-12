using FastExpressionCompiler;
using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent;

/// <summary>
/// OneOf the inner choices when all parsers return the same type.
/// We then return the actual result of each parser.
/// </summary>
/// <typeparam name="T"></typeparam>
public sealed class OneOf<T> : Parser<T>, ICompilable, ISeekable
{
    // Used as a lookup for OneOf<T> to find other OneOf<T> parsers that could
    // be invoked when there is no match.

    public const char OtherSeekableChar = '\0';
    internal readonly CharMap<List<Parser<T>>>? _map;
    internal readonly List<Parser<T>>? _otherParsers;

    private readonly CharMap<Func<ParseContext, ValueTuple<bool, T>>> _lambdaMap = new();
    private Func<ParseContext, ValueTuple<bool, T>>? _lambdaOtherParsers;

    public OneOf(Parser<T>[] parsers)
    {
        Parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

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
                    // If the parser is a Seekable<T> skip it and add directly the concrete parser
                    var decoratedParser = (parser as Seekable<T>)?.Parser ?? parser;

                    foreach (var c in seekable.ExpectedChars)
                    {
                        if (c != OtherSeekableChar)
                        {
                            lookupTable[c].Add(decoratedParser);
                        }
                        else
                        {
                            _otherParsers ??= [];
                            _otherParsers.Add(decoratedParser);
                        }
                    }
                }
                else
                {
                    _otherParsers ??= [];
                    _otherParsers.Add(parser);

                    foreach (var entry in lookupTable)
                    {
                        entry.Value.Add(parser);
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

            Name = $"OneOf ({string.Join(",", Parsers.Select(x => x.Name))}) on '{expectedChars}'";
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

    public IReadOnlyList<Parser<T>> Parsers { get; }

    public override bool Parse(ParseContext context, ref ParseResult<T> result)
    {
        context.EnterParser(this);

        var cursor = context.Scanner.Cursor;

        var start = context.Scanner.Cursor.Position;

        if (SkipWhitespace)
        {
            context.SkipWhiteSpace();
        }

        if (_map != null)
        {
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
}
