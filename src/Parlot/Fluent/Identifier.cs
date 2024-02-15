using Parlot.Compilation;
using System;
using System.Linq.Expressions;

namespace Parlot.Fluent
{
    public sealed class Identifier : Parser<TextSpan>, ICompilable
    {
        private readonly Func<char, bool> _extraStart;
        private readonly Func<char, bool> _extraPart;

        public Identifier(Func<char, bool> extraStart = null, Func<char, bool> extraPart = null)
        {
            _extraStart = extraStart;
            _extraPart = extraPart;
        }

        public override bool Parse(ParseContext context, ref ParseResult<TextSpan> result)
        {
            context.EnterParser(this);

            var first = context.Scanner.Cursor.Current;

            if (Character.IsIdentifierStart(first) || _extraStart != null && _extraStart(first))
            {
                var start = context.Scanner.Cursor.Offset;

                // At this point we have an identifier, read while it's an identifier part.

                context.Scanner.Cursor.AdvanceNoNewLines(1);

                while (!context.Scanner.Cursor.Eof && (Character.IsIdentifierPart(context.Scanner.Cursor.Current) || (_extraPart != null && _extraPart(context.Scanner.Cursor.Current))))
                {
                    context.Scanner.Cursor.AdvanceNoNewLines(1);
                }

                var end = context.Scanner.Cursor.Offset;

                result.Set(start, end, new TextSpan(context.Scanner.Buffer, start, end - start));
                return true;
            }

            return false;
        }

        public CompilationResult Compile(CompilationContext context)
        {
            var result = new CompilationResult();

            var success = context.DeclareSuccessVariable(result, false);
            var value = context.DeclareValueVariable(result, Expression.Default(typeof(TextSpan)));

            // var first = context.Scanner.Cursor.Current;

            var first = Expression.Parameter(typeof(char), $"first{context.NextNumber}");
            result.Body.Add(Expression.Assign(first, context.Current()));
            result.Variables.Add(first);

            //
            // success = false;
            // Textspan value;
            // 
            // if (Character.IsIdentifierStart(first) [_extraStart != null] || _extraStart(first))
            // {
            //    var start = context.Scanner.Cursor.Offset;
            //
            //    context.Scanner.Cursor.Advance();
            //    
            //    while (!context.Scanner.Cursor.Eof && (Character.IsIdentifierPart(context.Scanner.Cursor.Current) || (_extraPart != null && _extraPart(context.Scanner.Cursor.Current))))
            //    {
            //        context.Scanner.Cursor.Advance();
            //    }
            //    
            //    value = new TextSpan(context.Scanner.Buffer, start, context.Scanner.Cursor.Offset - start);
            //    success = true;
            // }

            var start = Expression.Parameter(typeof(int), $"start{context.NextNumber}");

            var breakLabel = Expression.Label($"break_{context.NextNumber}");

            var block = Expression.Block(
                Expression.IfThen(
                    Expression.OrElse(
                        Expression.Call(typeof(Character).GetMethod(nameof(Character.IsIdentifierStart)), first),
                        _extraStart != null
                            ? Expression.Invoke(Expression.Constant(_extraStart), first)
                            : Expression.Constant(false, typeof(bool))
                            ),
                    Expression.Block(
                        [start],
                        Expression.Assign(start, context.Offset()),
                        context.AdvanceNoNewLine(Expression.Constant(1)),
                        Expression.Loop(
                            Expression.IfThenElse(
                                /* if */ Expression.AndAlso(
                                    Expression.Not(context.Eof()),
                                        Expression.OrElse(
                                            Expression.Call(typeof(Character).GetMethod(nameof(Character.IsIdentifierPart)), context.Current()),
                                            _extraPart != null
                                                ? Expression.Invoke(Expression.Constant(_extraPart), context.Current())
                                                : Expression.Constant(false, typeof(bool))
                                            )
                                    ),
                                /* then */ context.AdvanceNoNewLine(Expression.Constant(1)),
                                /* else */ Expression.Break(breakLabel)
                                ),
                            breakLabel
                            ),
                        context.DiscardResult
                            ? Expression.Empty()
                            : Expression.Assign(value, context.NewTextSpan(context.Buffer(), start, Expression.Subtract(context.Offset(), start))),
                        Expression.Assign(success, Expression.Constant(true, typeof(bool)))
                    )
                )
            );

            result.Body.Add(block);

            return result;
        }
    }
}
