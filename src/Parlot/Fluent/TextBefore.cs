﻿namespace Parlot.Fluent
{
    using Compilation;
    using System;
    using System.Linq;
    using System.Linq.Expressions;

    public sealed class TextBefore<T, TParseContext, TChar> : Parser<BufferSpan<TChar>, TParseContext, TChar>, ICompilable<TParseContext, TChar>
    where TParseContext : ParseContextWithScanner<TChar>
    where TChar : IEquatable<TChar>, IConvertible
    {
        private readonly Parser<T, TParseContext> _delimiter;
        private readonly bool _canBeEmpty;
        private readonly bool _failOnEof;
        private readonly bool _consumeDelimiter;

        public TextBefore(Parser<T, TParseContext> delimiter, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
        {
            _delimiter = delimiter;
            _canBeEmpty = canBeEmpty;
            _failOnEof = failOnEof;
            _consumeDelimiter = consumeDelimiter;
        }

        public override bool Parse(TParseContext context, ref ParseResult<BufferSpan<TChar>> result)
        {
            context.EnterParser(this);

            var start = context.Scanner.Cursor.Position;

            var parsed = new ParseResult<T>();

            while (true)
            {
                var previous = context.Scanner.Cursor.Position;

                if (context.Scanner.Cursor.Eof)
                {
                    if (_failOnEof)
                    {
                        context.Scanner.Cursor.ResetPosition(start);
                        return false;
                    }

                    var length = previous - start;

                    if (length == 0 && !_canBeEmpty)
                    {
                        return false;
                    }

                    result.Set(start.Offset, previous.Offset, context.Scanner.Buffer.SubBuffer(start.Offset, length));
                    return true;
                }

                var delimiterFound = _delimiter.Parse(context, ref parsed);

                if (delimiterFound)
                {
                    var length = previous - start;

                    if (!_consumeDelimiter)
                    {
                        context.Scanner.Cursor.ResetPosition(previous);
                    }

                    if (length == 0 && !_canBeEmpty)
                    {
                        return false;
                    }

                    result.Set(start.Offset, previous.Offset, context.Scanner.Buffer.SubBuffer(start.Offset, length));
                    return true;
                }

                context.Scanner.Cursor.Advance();
            }
        }

        public CompilationResult Compile(CompilationContext<TParseContext, TChar> context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(BufferSpan<char>)));

            //  var start = context.Scanner.Cursor.Position;
            //  
            //  while (true)
            //  {
            //      var previous = context.Scanner.Cursor.Position;
            //  
            //      if (context.Scanner.Cursor.Eof)
            //      {
            //          [if _failOnEof]
            //          {
            //              context.Scanner.Cursor.ResetPosition(start);
            //              return false;
            //          }
            //          [else]
            //          {
            //              var length = previous - start;
            //  
            //              [if !_canBeEmpty]
            //              if (length == 0)
            //              {
            //                  break;
            //              }
            //  
            //              success = true;
            //              value = new BufferSpan<char>(context.Scanner.Buffer, start.Offset, length);
            //              break;
            //          }
            //      }
            //  
            //      delimiter instructions
            //  
            //      if (delimiter.success)
            //      {
            //          var length = previous - start;
            //  
            //          [if !_consumeDelimiter]
            //          {
            //              context.Scanner.Cursor.ResetPosition(previous);
            //          }
            //  
            //          [if !_canBeEmpty]
            //          if (length == 0)
            //          {
            //              break;
            //          }
            //  
            //          success = true;
            //          value = new BufferSpan<char>(context.Scanner.Buffer, start.Offset, length);
            //          break;
            //      }
            //  
            //      context.Scanner.Cursor.Advance();
            //  }

            var delimiterCompiledResult = _delimiter.Build(context);

            var breakLabel = Expression.Label($"break_{context.NextNumber}");
            var previous = Expression.Parameter(typeof(TextPosition), $"previous_{context.NextNumber}");
            var length = Expression.Parameter(typeof(int), $"length_{context.NextNumber}");
            var start = context.DeclarePositionVariable(result);

            var block = Expression.Block(
                delimiterCompiledResult.Variables.Append(previous).Append(length),
                Expression.Loop(
                    Expression.Block(
                        Expression.Assign(previous, context.Position()),
                        Expression.IfThen(
                            context.Eof(),
                            _failOnEof
                            ? Expression.Block(
                                context.ResetPosition(start),
                                Expression.Break(breakLabel)
                                )
                            : Expression.Block(
                                Expression.Assign(length, Expression.Subtract(context.Offset(previous), context.Offset(start))),
                                _canBeEmpty
                                ? Expression.Empty()
                                : Expression.IfThen(Expression.Equal(length, Expression.Constant(0)), Expression.Break(breakLabel)),
                                Expression.Assign(success, Expression.Constant(true)),
                                Expression.Assign(value, context.SubBufferSpan(context.Offset(start), length)),
                                Expression.Break(breakLabel)
                                )
                            ),

                        Expression.Block(delimiterCompiledResult.Body),

                        Expression.IfThen(
                            delimiterCompiledResult.Success,
                            Expression.Block(
                                Expression.Assign(length, Expression.Subtract(context.Offset(previous), context.Offset(start))),
                                _consumeDelimiter
                                ? Expression.Empty()
                                : context.ResetPosition(previous),
                                _canBeEmpty
                                ? Expression.Empty()
                                : Expression.IfThen(Expression.Equal(length, Expression.Constant(0)), Expression.Break(breakLabel)),
                                Expression.Assign(success, Expression.Constant(true)),
                                Expression.Assign(value, context.SubBufferSpan(context.Offset(start), length)),
                                Expression.Break(breakLabel)
                                )
                            ),
                        context.Advance()
                        ),
                    breakLabel)
                );

            result.Body.Add(block);

            return result;
        }
    }
}
