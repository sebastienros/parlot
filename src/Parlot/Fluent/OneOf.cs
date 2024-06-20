using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Collections.Frozen;
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
        internal readonly FrozenDictionary<char, List<Parser<T>>>? _lookupTable;

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
                    _lookupTable = lookupTable.ToFrozenDictionary();

                    CanSeek = true;
                    ExpectedChars = _lookupTable.Keys.ToArray();
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

            if (_lookupTable != null)
            {
                if (SkipWhitespace)
                {
                    var start = context.Scanner.Cursor.Position;

                    context.SkipWhiteSpace();

                    if (_lookupTable.TryGetValue(cursor.Current, out var seekableParsers))
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

                    context.Scanner.Cursor.ResetPosition(start);
                }
                else
                {
                    if (_lookupTable.TryGetValue(cursor.Current, out var seekableParsers))
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

            if (_lookupTable != null)
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

                var cases = _lookupTable.Select(kvp =>
                {
                    Expression group = Expression.Empty();

                    // The list is reversed since the parsers are unwrapped
                    foreach (var parser in kvp.Value.ToArray().Reverse())
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

                    return Expression.SwitchCase(
                            group,
                            Expression.Constant(kvp.Key)
                        );
                }).ToArray();

                SwitchExpression switchExpr =
                    Expression.Switch(
                        context.Current(),
                        Expression.Empty(), // no match => success = false
                        cases
                    );

                if (SkipWhitespace)
                {
                    var start = context.DeclarePositionVariable(result);

                    block = Expression.Block(
                        context.ParserSkipWhiteSpace(),
                        switchExpr,
                        Expression.IfThen(
                            Expression.IsFalse(result.Success),
                            context.ResetPosition(start))
                        );
                }
                else
                {
                    block = Expression.Block(
                        switchExpr
                    );
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
