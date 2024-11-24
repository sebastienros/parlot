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

    public OneOf(Parser<T>[] parsers)
    {
        _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

        // We can't build a lookup table if there is only one parser
        if (_parsers.Length <= 1)
        {
            return;
        }

        // If all parsers are seekable we can build a lookup table
        if (_parsers.All(x => x is ISeekable seekable && seekable.CanSeek))
        {
            var lookupTable = new Dictionary<char, List<Parser<T>>>();

            foreach (var parser in _parsers)
            {
                var expectedChars = (parser as ISeekable)!.ExpectedChars;

                foreach (var c in expectedChars)
                {
                    if (!lookupTable.TryGetValue(c, out var list))
                    {
                        list = new List<Parser<T>>();
                        lookupTable[c] = list;
                    }

                    list.Add(parser);
                }
            }

            if (lookupTable.Count <= 1)
            {
                // If all parsers have the same first char, no need to use a lookup table

                ExpectedChars = lookupTable!.Keys.ToArray();
                CanSeek = true;
                lookupTable = null;
            }

            if (_parsers.All(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                // All parsers can start with white spaces
                SkipWhitespace = true;
            }

            if (_parsers.Any(x => x is ISeekable seekable && seekable.SkipWhitespace))
            {
                // If not all parsers accept a white space, we can't use a lookup table since the order matters

                lookupTable = null;
            }

            if (lookupTable != null)
            {
                CanSeek = true;
                _map = new CharMap<List<Parser<T>>>(lookupTable);
                ExpectedChars = _map.ExpectedChars.ToArray();
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

        if (_map != null)
        {
            if (SkipWhitespace)
            {
                var start = context.Scanner.Cursor.Position;

                context.SkipWhiteSpace();

                var seekableParsers = _map[cursor.Current];

                if (seekableParsers != null)
                {
                    var length = seekableParsers!.Count;

                    for (var i = 0; i < length; i++)
                    {
                        if (seekableParsers[i].Parse(context, ref result))
                        {
                            return true;
                        }
                    }
                }

                context.Scanner.Cursor.ResetPosition(start);
            }
            else
            {
                var seekableParsers = _map[cursor.Current];

                if (seekableParsers != null)
                {
                    var length = seekableParsers!.Count;

                    for (var i = 0; i < length; i++)
                    {
                        if (seekableParsers[i].Parse(context, ref result))
                        {
                            return true;
                        }
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
