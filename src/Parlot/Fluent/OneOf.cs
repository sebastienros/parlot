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
    private readonly Parser<T>[] _parsers;
    internal readonly CharMap<List<Parser<T>>>? _map;
    internal readonly List<Parser<T>>? _otherParsers;

    public OneOf(Parser<T>[] parsers)
    {
        _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

        // We can't build a lookup table if there is only one parser
        if (_parsers.Length <= 1)
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

        if (_parsers.Any(x => x is ISeekable seekable))
        {
            var lookupTable = _parsers
                            .Where(p => p is ISeekable seekable && seekable.CanSeek)
                            .Cast<ISeekable>()
                            .SelectMany(s => s.ExpectedChars.Select(x => (Key: x, Parser: (Parser<T>)s)))
                            .GroupBy(s => s.Key)
                            .ToDictionary(group => group.Key, group => new List<Parser<T>>());

            foreach (var parser in _parsers)
            {
                if (parser is ISeekable seekable && seekable.CanSeek)
                {
                    foreach (var c in seekable.ExpectedChars)
                    {
                        lookupTable[c].Add(parser);
                    }
                }
                else
                {
                    _otherParsers ??= new();
                    _otherParsers.Add(parser);

                    foreach (var entry in lookupTable)
                    {
                        entry.Value.Add(parser);
                    }
                }
            }

            // If only some parser use SkipWhiteSpace, we can't use a lookup table
            // Meaning, All/None is fine, but not Any
            if (_parsers.All(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                SkipWhitespace = true;
            }
            else if (_parsers.Any(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                lookupTable = null;
            }

            var expectedChars = string.Join(",", lookupTable?.Keys.ToArray() ?? []);

            Name = $"OneOf ({string.Join(",", _parsers.Select(x => x.Name))}) on '{expectedChars}'";
            if (lookupTable != null && lookupTable.Count > 0)
            {
                _map = new CharMap<List<Parser<T>>>(lookupTable);

                // This parser is only seekable if there isn't a parser
                // that can't be reached without a lookup. Unless ISeekable
                // had an OtherParsers property too. In which case every "Or" would
                // become a lookup, meaning every grammar would become a set of
                // lookups (switches).

                if (_otherParsers == null)
                {
                    CanSeek = true;
                    ExpectedChars = _map.ExpectedChars;
                }
            }
        }
    }

    public bool CanSeek { get; }

    public char[] ExpectedChars { get; } = [];

    public bool SkipWhitespace { get; }

    public Parser<T>[] Parsers => _parsers;

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
                        return true;
                    }
                }
            }
        }
        else
        {
            var parsers = _parsers;
            var length = parsers.Length;

            for (var i = 0; i < length; i++)
            {
                if (parsers[i].Parse(context, ref result))
                {
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

        return false;
    }

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<T>();

        Expression block = Expression.Empty();

        //if (_map != null) 
        // For now don't use lookup maps for compiled code as there is no fast option in that case.

        if (false)
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

#pragma warning disable CS0162 // Unreachable code detected
            var cases = _map.ExpectedChars.Select(key =>
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
#pragma warning restore CS0162 // Unreachable code detected

            // Creating the switch expression if we need it
            SwitchExpression switchExpr =
                Expression.Switch(
                    Expression.Convert(context.Current(), typeof(uint)),
                    Expression.Empty(), // no match => success = false
                    cases.Select(c => Expression.SwitchCase(
                        c.Body,
                        Expression.Constant(c.Key)
                    )).ToArray()
                );

            // Implement binary tree comparison
            // Still slow with a few elements

            var current = Expression.Variable(typeof(uint), $"current{context.NextNumber}");
            var binarySwitch = Expression.Block(
                [current],
                Expression.Assign(current, Expression.Convert(context.Current(), typeof(uint))),
                BinarySwitch(current, cases)
            );

            static Expression BinarySwitch(Expression num, (uint Key, Expression Body)[] cases)
            {
                if (cases.Length > 3)
                {
                    // Split comparison in two recursive comparisons for each part of the cases

                    var lowerCount = (int)Math.Round((double)cases.Length / 2, MidpointRounding.ToEven);
                    var lowerValues = cases.Take(lowerCount).ToArray();
                    var higherValues = cases.Skip(lowerCount).ToArray();

                    return Expression.IfThenElse(
                        Expression.LessThanOrEqual(num, Expression.Constant(lowerValues[^1].Key)),
                        // This value or lower
                        BinarySwitch(num, lowerValues),
                        // Higher values
                        BinarySwitch(num, higherValues)
                    );
                }
                else if (cases.Length == 1)
                {
                    return Expression.IfThen(
                        Expression.Equal(Expression.Constant(cases[0].Key), num),
                        cases[0].Body
                        );
                }
                else if (cases.Length == 2)
                {
                    return Expression.IfThenElse(
                        Expression.NotEqual(Expression.Constant(cases[0].Key), num),
                        Expression.IfThen(
                            Expression.Equal(Expression.Constant(cases[1].Key), num),
                            cases[1].Body),
                        cases[0].Body);
                }
                else // cases.Length == 3
                {
                    return Expression.IfThenElse(
                        Expression.NotEqual(Expression.Constant(cases[0].Key), num),
                        Expression.IfThenElse(
                            Expression.NotEqual(Expression.Constant(cases[1].Key), num),
                            Expression.IfThen(
                                Expression.Equal(Expression.Constant(cases[2].Key), num),
                                cases[2].Body),
                            cases[1].Body),
                        cases[0].Body);
                }
            }

            // Implement lookup
            // Doesn't work since each method can update the main state with the result (closure issue?)

            var table = Expression.Variable(typeof(CharMap<Action>), $"table{context.NextNumber}");

            var indexerMethodInfo = typeof(CharMap<Action>).GetMethod("get_Item", [typeof(uint)])!;

            context.GlobalVariables.Add(table);
            var action = result.DeclareVariable<Action>($"action{context.NextNumber}");

            var lookupBlock = Expression.Block(
                [current, action, table],
                [
                    Expression.Assign(current, Expression.Convert(context.Current(), typeof(uint))),
                    // Initialize lookup table once
                    Expression.IfThen(
                        Expression.Equal(Expression.Constant(null, typeof(object)), table),
                        Expression.Block([
                            Expression.Assign(table, ExpressionHelper.New<CharMap<Action>>()),
                            ..cases.Select(c => Expression.Call(table, typeof(CharMap<Action>).GetMethod("Set", [typeof(char), typeof(Action)])!, [Expression.Convert(Expression.Constant(c.Key), typeof(char)), Expression.Lambda<Action>(c.Body)]))
                            ]
                        )
                    ),
                    Expression.Assign(action, Expression.Call(table, indexerMethodInfo, [current])),
                    Expression.IfThen(
                        Expression.NotEqual(Expression.Constant(null), action),
                        //ExpressionHelper.ThrowObject(context, current)
                        Expression.Invoke(action)
                        )
                ]
            );

            if (SkipWhitespace)
            {
                var start = context.DeclarePositionVariable(result);

                block = Expression.Block(
                    context.ParserSkipWhiteSpace(),
                    binarySwitch,
                    Expression.IfThen(
                        Expression.IsFalse(result.Success),
                        context.ResetPosition(start))
                    );
            }
            else
            {
                block = binarySwitch;
            }
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

            foreach (var parser in _parsers.Reverse())
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

        return result;
    }
}
