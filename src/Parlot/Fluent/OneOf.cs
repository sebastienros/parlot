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
        private readonly Parser<T>[] _parsers;
        private readonly Dictionary<char, Parser<T>> _nonWhiteSpacelookupTable;
        private readonly Dictionary<char, Parser<T>> _whiteSpaceLookupTable;
        private readonly bool _hasLookupTable;

        public OneOf(Parser<T>[] parsers)
        {
            _parsers = parsers ?? throw new ArgumentNullException(nameof(parsers));

            _nonWhiteSpacelookupTable = new Dictionary<char, Parser<T>>();
            _whiteSpaceLookupTable = new Dictionary<char, Parser<T>>();

            foreach (var parser in _parsers)
            {
                // Not seekable ?
                if (parser is not ISeekable seekable || !seekable.CanSeek)
                {
                    _nonWhiteSpacelookupTable = null;
                    _whiteSpaceLookupTable = null;
                    break;
                }

                var table = seekable.SkipWhitespace
                    ? _whiteSpaceLookupTable
                    : _nonWhiteSpacelookupTable
                    ;

                // Ambiguity ?
                if (table.ContainsKey(seekable.ExpectedChar))
                {
                    // TODO: this can be rewritten in a separate OneOf<T> to isolate the ambiguous choices

                    _nonWhiteSpacelookupTable = null;
                    _whiteSpaceLookupTable = null;
                    break;
                }

                table.Add(seekable.ExpectedChar, parser);
            }

            _hasLookupTable = _nonWhiteSpacelookupTable != null || _whiteSpaceLookupTable != null;
        }

        public Parser<T>[] Parsers => _parsers;

        public override bool Parse(ParseContext context, ref ParseResult<T> result)
        {
            context.EnterParser(this);

            if (_hasLookupTable)
            {
                if (_nonWhiteSpacelookupTable != null)
                {
                    if (_nonWhiteSpacelookupTable.TryGetValue(context.Scanner.Cursor.Current, out var seekable))
                    {
                        return seekable.Parse(context, ref result);
                    }
                }

                if (_whiteSpaceLookupTable != null)
                {
                    context.SkipWhiteSpace();

                    if (_whiteSpaceLookupTable.TryGetValue(context.Scanner.Cursor.Current, out var seekable))
                    {
                        return seekable.Parse(context, ref result);
                    }
                }
            }
            else
            {
                var parsers = _parsers;

                for (var i = 0; i < parsers.Length; i++)
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


            Expression block = Expression.Empty();

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

            result.Body.Add(block);

            return result;
        }
    }
}
