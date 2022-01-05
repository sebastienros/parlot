using Parlot.Compilation;
using Parlot.Rewriting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    /// <summary>
    /// OneOf the inner choices when all parsers return the same type.
    /// We then return the actual result of each parser.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public sealed class OneOf<T> : Parser<T>, ICompilable
    {
        internal readonly Parser<T>[] _parsers;
        internal readonly Dictionary<char, Parser<T>> _lookupTable;
        internal readonly bool _skipWhiteSpace;

        public OneOf(Parser<T>[] parsers)
        {
            _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

            // All parsers are seekable
            if (_parsers.All(x => x is ISeekable seekable && seekable.CanSeek))
            {
                _lookupTable = new Dictionary<char, Parser<T>>();

                foreach (var parser in _parsers)
                {
                    var expectedChars = (parser as ISeekable).ExpectedChars;

                    foreach (var c in expectedChars)
                    { 
                        // Ambiguous lookup
                        if (_lookupTable.ContainsKey(c))
                        {
                            _lookupTable = null;
                            break;
                        }

                        _lookupTable.Add(c, parser);
                    }
                }

                // All parsers can start with white spaces
                if (_parsers.All(x => x is ISeekable seekable && seekable.SkipWhitespace))
                {
                    _skipWhiteSpace = true;
                }
                else if (_parsers.Any(x => x is ISeekable seekable && seekable.SkipWhitespace))
                {
                    // If not all parsers accept a white space, we can't use a lookup table since the order matters

                    _lookupTable = null;
                }
            }
        }

        public Parser<T>[] Parsers => _parsers;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            var cursor = context.Scanner.Cursor;

            if (_lookupTable != null)
            {
                if (_skipWhiteSpace)
                {
                    var start = context.Scanner.Cursor.Position;

                    context.SkipWhiteSpace();

                    if (_lookupTable.TryGetValue(cursor.Current, out var seekable) && seekable.Parse(context, ref result))
                    {
                        return true;
                    }

                    context.Scanner.Cursor.ResetPosition(start);
                }
                else
                {
                    if (_lookupTable.TryGetValue(cursor.Current, out var seekable))
                    {
                        return seekable.Parse(context, ref result);
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
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(T)));


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
                    var parserCompileResult = kvp.Value.Build(context);

                    return Expression.SwitchCase(
                            Expression.Block(
                            parserCompileResult.Variables,
                            Expression.Block(parserCompileResult.Body),
                            Expression.IfThen(
                                parserCompileResult.Success,
                                Expression.Block(
                                    Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                    context.DiscardResult
                                    ? Expression.Empty()
                                    : Expression.Assign(value, parserCompileResult.Value)
                                    )
                                )
                            ),
                            Expression.Constant(kvp.Key)
                        );
                }).ToArray();

                SwitchExpression switchExpr =
                    Expression.Switch(
                        context.Current(),
                        Expression.Empty(), // no match => success = false
                        cases
                    );

                if (_skipWhiteSpace)
                {
                    var start = context.DeclarePositionVariable(result);

                    block = Expression.Block(
                        context.ParserSkipWhiteSpace(),
                        switchExpr,
                        Expression.IfThen(
                            Expression.IsFalse(success),
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
                                Expression.Assign(success, Expression.Constant(true, typeof(bool))),
                                context.DiscardResult
                                ? Expression.Empty()
                                : Expression.Assign(value, parserCompileResult.Value)
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
