using BenchmarkDotNet.Attributes;
using Parlot.Fluent;
using System;
using static Parlot.Fluent.Parsers;

namespace Parlot.Benchmarks;

// | Method          | Count | Mean      | Error     | StdDev   |
// |---------------- |------ |----------:|----------:|---------:|
// | NotSeekable     | 2     |  34.18 ns |  5.883 ns | 0.322 ns |
// | MixedWhiteSpace | 2     |  36.34 ns |  9.553 ns | 0.524 ns |
// | NotSeekable     | 4     |  45.31 ns |  4.425 ns | 0.243 ns |
// | MixedWhiteSpace | 4     |  60.05 ns | 19.775 ns | 1.084 ns |
// | NotSeekable     | 8     |  73.36 ns | 16.250 ns | 0.891 ns |
// | MixedWhiteSpace | 8     | 112.68 ns | 86.086 ns | 4.719 ns |

// Measures OneOf.Parse when no lookup map can be built, in which case the alternatives are tried in order:
// none of them declares its first chars, or only some of them skip white spaces. The last alternative is the
// one matching. The contexts are reused and the cursor moved back before each parse, such that the allocations
// of a parse are not part of the result.

[MemoryDiagnoser, ShortRunJob]
public class OneOfSequentialBenchmarks
{
    private Parser<TextSpan> _notSeekable;
    private Parser<string> _mixedWhiteSpace;
    private ParseContext _notSeekableContext;
    private ParseContext _mixedWhiteSpaceContext;

    [Params(2, 4, 8)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var patterns = new Parser<TextSpan>[Count];
        var texts = new Parser<string>[Count];

        for (var i = 0; i < Count; i++)
        {
            var c = (char)('a' + i);

            patterns[i] = Literals.Pattern(x => x == c);

            // A single alternative skipping white spaces prevents the lookup
            texts[i] = i == 0 ? Terms.Text(c.ToString()) : Literals.Text(c.ToString());
        }

        var last = ((char)('a' + Count - 1)).ToString();

        _notSeekable = OneOf(patterns);
        _mixedWhiteSpace = OneOf(texts);
        _notSeekableContext = new ParseContext(new Scanner(last));
        _mixedWhiteSpaceContext = new ParseContext(new Scanner(last));

        if (((OneOf<TextSpan>)_notSeekable).CanSeek || ((OneOf<string>)_mixedWhiteSpace).CanSeek)
        {
            throw new InvalidOperationException("The parsers are expected not to use a lookup map.");
        }

        if (NotSeekable().ToString() != last || MixedWhiteSpace() != last)
        {
            throw new InvalidOperationException("Incorrect OneOf parsing.");
        }
    }

    [Benchmark(Baseline = true)]
    public TextSpan NotSeekable()
    {
        var result = new ParseResult<TextSpan>();

        _notSeekableContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _notSeekable.Parse(_notSeekableContext, ref result);

        return result.Value;
    }

    [Benchmark]
    public string MixedWhiteSpace()
    {
        var result = new ParseResult<string>();

        _mixedWhiteSpaceContext.Scanner.Cursor.ResetPosition(TextPosition.Start);
        _mixedWhiteSpace.Parse(_mixedWhiteSpaceContext, ref result);

        return result.Value;
    }
}
