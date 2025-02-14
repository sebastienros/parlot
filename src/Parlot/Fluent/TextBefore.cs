using Parlot.Compilation;
using Parlot.Rewriting;
using System;
#if NET8_0_OR_GREATER
using System.Buffers;
#endif

#if NETCOREAPP
using System.Linq;
#endif
using System.Linq.Expressions;
using System.Reflection;

namespace Parlot.Fluent;

public sealed class TextBefore<T> : Parser<TextSpan>, ICompilable
{
    private static readonly MethodInfo _jumpToNextExpectedCharMethod = typeof(TextBefore<T>).GetMethod(nameof(JumpToNextExpectedChar), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Parser<T> _delimiter;
    private readonly bool _canBeEmpty;
    private readonly bool _failOnEof;
    private readonly bool _consumeDelimiter;
    private readonly bool _canJumpToNextExpectedChar;
#if NET8_0_OR_GREATER
    private readonly SearchValues<char>? _expectedSearchValues;
#else
    private readonly char[]? _expectedChars;
#endif
    public TextBefore(Parser<T> delimiter, bool canBeEmpty = false, bool failOnEof = false, bool consumeDelimiter = false)
    {
        _delimiter = delimiter;
        _canBeEmpty = canBeEmpty;
        _failOnEof = failOnEof;
        _consumeDelimiter = consumeDelimiter;

        if (_delimiter is ISeekable seekable && seekable.CanSeek)
        {
#if NET8_0_OR_GREATER
            _expectedSearchValues = SearchValues.Create(seekable.ExpectedChars);
#else
            _expectedChars = seekable.ExpectedChars;
#endif
            _canJumpToNextExpectedChar = true;
        }
    }

    public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
    {
        context.EnterParser(this);

        var start = context.Scanner.Cursor.Position;

        var parsed = new ParseResult<T>();

        if (_canJumpToNextExpectedChar)
        {
#if NET8_0_OR_GREATER
            JumpToNextExpectedChar(context, _expectedSearchValues!);
#else
            JumpToNextExpectedChar(context, _expectedChars!);
#endif
        }

        while (true)
        {
            var previous = context.Scanner.Cursor.Position;

            if (context.Scanner.Cursor.Eof)
            {
                if (_failOnEof)
                {
                    context.Scanner.Cursor.ResetPosition(start);

                    context.ExitParser(this);
                    return false;
                }

                var length = previous - start;

                if (length == 0 && !_canBeEmpty)
                {
                    context.ExitParser(this);
                    return false;
                }

                result.Set(start.Offset, previous.Offset, new TextSpan(context.Scanner.Buffer, start.Offset, length));

                context.ExitParser(this);
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
                    context.ExitParser(this);
                    return false;
                }

                result.Set(start.Offset, previous.Offset, new TextSpan(context.Scanner.Buffer, start.Offset, length));

                context.ExitParser(this);
                return true;
            }

            context.Scanner.Cursor.Advance();
        }
    }

#if NET8_0_OR_GREATER
    private static void JumpToNextExpectedChar(ParseContext context, SearchValues<char> expectedChars)
    {
        var index = context.Scanner.Cursor.Span.IndexOfAny(expectedChars);

        if (index >= 0)
        {
            context.Scanner.Cursor.Advance(index);
        }
    }
#else
    private static void JumpToNextExpectedChar(ParseContext context, char[] expectedChars)
    {
        var indexOfAny = int.MaxValue;
        foreach (var c in expectedChars)
        {
            var index = context.Scanner.Cursor.Span.IndexOf(c);
            if (index >= 0)
            {
                indexOfAny = Math.Min(indexOfAny, index);
            }
        }

        if (indexOfAny < int.MaxValue)
        {
            context.Scanner.Cursor.Advance(indexOfAny);
        }
    }
#endif

    public CompilationResult Compile(CompilationContext context)
    {
        var result = context.CreateCompilationResult<TextSpan>();

        //  var start = context.Scanner.Cursor.Position;
        //
        //  [if _canJumpToNextExpectedChar]
        //      JumpToNextExpectedChar(context, expectedChars);
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
        //              value = new TextSpan(context.Scanner.Buffer, start.Offset, length);
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
        //          value = new TextSpan(context.Scanner.Buffer, start.Offset, length);
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

#if NET8_0_OR_GREATER
        var expectedCharsExpression = Expression.Constant(_expectedSearchValues);
#else
        var expectedCharsExpression = Expression.Constant(_expectedChars);
#endif
        var block = Expression.Block(
            delimiterCompiledResult.Variables.Append(previous).Append(length),
            _canJumpToNextExpectedChar ? Expression.Call(null, _jumpToNextExpectedCharMethod, context.ParseContext, expectedCharsExpression) : Expression.Empty(),
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
                            Expression.Assign(result.Success, Expression.Constant(true)),
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, context.NewTextSpan(context.Buffer(), context.Offset(start), length)),
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
                            Expression.Assign(result.Success, Expression.Constant(true)),
                            context.DiscardResult ? Expression.Empty() : Expression.Assign(result.Value, context.NewTextSpan(context.Buffer(), context.Offset(start), length)),
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

    public override string ToString() => $"TextBefore({_delimiter})";
}
