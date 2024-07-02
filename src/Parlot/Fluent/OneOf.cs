using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>, ICompilable, ISeekable
    {
        internal readonly Parser<T>[] _parsers;
        internal readonly ParsersDictionary<T>? _map;

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

                    lookupTable = null;
                }
                else if (_parsers.All(x => x is ISeekable seekable && seekable.SkipWhitespace))
                {
                    // All parsers can start with white spaces
                    SkipWhitespace = true;
                }
                else if (_parsers.Any(x => x is ISeekable seekable && seekable.SkipWhitespace))
                {
                    // If not all parsers accept a white space, we can't use a lookup table since the order matters

                    lookupTable = null;
                }

                if (lookupTable != null)
                {
                    CanSeek = true;
                    _map = new ParsersDictionary<T>(lookupTable);
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

            if (_map != null)
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

                var binarySwitch = BinarySwitch(Expression.Convert(context.Current(), typeof(uint)), cases);

                static Expression BinarySwitch(Expression num, (uint Key, Expression Body)[] cases)
                {
                    if (cases.Length > 3)
                    {
                        // Split comparison in two recursive comparisons for each part of the cases

                        var targetIndex = cases.Length / 2;
                        var lowerValues = cases.Take(targetIndex).ToArray();
                        var higherValues = cases.Skip(targetIndex + 1).ToArray();

                        return Expression.IfThenElse(
                            Expression.LessThanOrEqual(Expression.Constant(cases[targetIndex].Key), num),
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
}
