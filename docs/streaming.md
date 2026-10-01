# Parsing streams

Parlot parses a `TextReader` or a `Stream` with the same parsers as a `string`. The parser graph,
the source-generated parsers, custom parsers and `ParseContext` callbacks all work unchanged: streaming
is a driver around the synchronous `Parse` method, not a second implementation of each parser.

```csharp
using Parlot.Fluent;

// One value
await using var file = File.OpenRead("document.json");
var (success, value) = await JsonParser.Json.TryParseAsync(file, cancellationToken: cancellationToken);

// Successive values, for instance NDJSON or log records
await using var input = File.OpenRead("records.ndjson");
var count = await record.ParseManyAsync(input, (item, ct) => store.AddAsync(item, ct), cancellationToken: cancellationToken);
```

## How it works

The driver reads text into a contiguous window and parses the window with the regular synchronous parser.
While more text may follow, the window is *non-final*: its `Cursor` has `IsFinal == false`, and records in
`Cursor.HitEnd` that the parse depended on text past the end of the window. This is the model of
`Utf8JsonReader(isFinalBlock)` and of Java's `Matcher.hitEnd()`.

- If the parse ends, successfully or not, **without** `HitEnd`, the result doesn't depend on what follows:
  it is the result of parsing the whole text.
- Otherwise the result is inconclusive. The driver reads more text, so that the window at least doubles,
  and parses again from the start of the value.
- When the reader is exhausted, the window is final, `HitEnd` is never set and the parse is exactly the
  parse of a `string`.

`HitEnd` is sticky for an attempt: `ResetPosition` doesn't clear it, so a branch that reached the end of the
window and then backtracked still makes the attempt inconclusive. A `ParseException` thrown while `HitEnd`
is set is inconclusive too, since more text could have avoided it.

The cost of the retries is bounded by the geometric growth. A single value grows the window four times
per attempt, so the wasted work is at most about 4/3 of a parse of the whole value. Items of
`ParseManyAsync` double it, and only the item which spans the end of the window is retried.

### What sets `HitEnd`

`Cursor` sets it centrally, so most parsers need nothing:

- reaching the end of the window, by any `Advance*` method or `ResetPosition`, or when the window is empty;
- `PeekNext` past the end of the window;
- `Match` when the remaining text is a prefix of the expected text;
- `TryGetSpan(minLength, out span)` when fewer characters are available.

A parser which reads to the end of the window, for instance an identifier or a number, has reached the end
and set `HitEnd`. Explicit marks are only needed where a parser fails or stops by *looking* at the end
without advancing to it. The built-in parsers and the source generator emitters do this, for instance for
literal texts, keyword lookups, unterminated strings, and characters sets with a minimum size.

### Custom parsers

A custom parser which only uses `Cursor` and `Scanner` methods works as is. A parser which reads
`Cursor.Span` or `Cursor.Buffer` directly must call `Cursor.MarkHitEnd()` when its result depends on the
text which follows the window, typically when it fails or stops because the span is too short:

```csharp
var span = context.Scanner.Cursor.Span;

if (span.Length < 4)
{
    // More text could make this parser succeed
    context.Scanner.Cursor.MarkHitEnd();
    return false;
}
```

`MarkHitEnd` does nothing on a final window, so it costs nothing when parsing a `string`. `TryGetSpan`
combines both. Forgetting it can only produce a wrong result when parsing a stream: compare
`TryParseAsync` over a reader returning one character at a time with `Parse(string)` to test a parser.

## API

All the methods are on `Parser<T>` and take an optional `StreamParseOptions` and `CancellationToken`.
`Stream` overloads decode the stream with `StreamParseOptions.Encoding` (UTF-8 by default, a byte order
mark takes precedence) and leave it open.

| Method | Description |
|---|---|
| `TryParseAsync(TextReader \| Stream)` | Parses one value, returns `(bool Success, T? Value)`. A `ParseException` returns `false`. |
| `ParseAsync(TextReader \| Stream)` | Parses one value, returns the value or `default`. A `ParseException` is propagated. |
| `ParseManyAsync(TextReader \| Stream, onItem)` | Parses successive values, invokes `onItem` (`Action<T>` or `Func<T, CancellationToken, ValueTask>`) for each and returns their count. |
| `ParseManyAsync(TextReader \| Stream, separator, onItem)` | Same, with a separator parser between the values. A trailing separator is accepted. |
| `ParseManyAsync(TextReader \| Stream, char delimiter, onItem)` | Parses each text delimited by `delimiter`, for instance each line, as a value. |
| `ParseManyAsync(...)` without `onItem` | Returns an `IAsyncEnumerable<T>` (.NET 8 and later). |

`StreamParseOptions`:

| Option | Default | Description |
|---|---|---|
| `BufferSize` | 4096 | Minimum number of characters read before parsing, and minimum growth. |
| `MaxBufferedCharacters` | `int.MaxValue` | Maximum characters retained for a value (each item for `ParseManyAsync`). Exceeding it throws a `ParseException`. Set it for untrusted input. |
| `Encoding` | UTF-8 | Encoding of a `Stream`. |
| `SkipWhiteSpace` | `true` | Whether `ParseManyAsync` skips white space before each item, separator and the end. |
| `ContextFactory` | `null` | Creates the `ParseContext` of a window, for instance to set `WhiteSpaceParser` or use a derived context. It must pass the cancellation token. |

### Single values

`TryParseAsync` and `ParseAsync` return the result of `TryParse`/`Parse` on the whole text, but stop reading
as soon as the result is conclusive: the text which follows the value is not necessarily read, like for
`Parse(string)` which doesn't require the end of the text. Use `.Eof()` to reject trailing text.

The whole value is buffered. A `StringReader`, and a seekable `Stream` whose remaining length is within
`MaxBufferedCharacters`, are read to the end and parsed once.

### Successive values

`ParseManyAsync` parses a value, invokes the item callback, drops the text of the value and continues
with the next one, so memory is bounded by the largest value, not by the input. The text must only
contain values (and separators and white space). A value which doesn't match throws a `ParseException`,
and a value matching an empty text throws an `InvalidOperationException` to avoid an infinite loop.

The delimited overload reads until it finds the delimiter and parses each delimited text on its own. The
parser must match the whole text, except white space when `SkipWhiteSpace` is enabled, and empty or white
space only texts are skipped. Use it for interactive sources, such as a terminal or a socket, where the
other overloads could wait for more text than the next value needs before parsing it.

## Semantics

- **Positions.** Lines and columns, and the positions of `ParseException`, are those of the whole text.
  `TextSpan.Offset`, `ParseResult` offsets and `Cursor.Offset` are relative to the window: a `TextSpan`
  references the window string it was parsed from (which it keeps alive), not the whole text. For single
  values, the window always starts at the beginning of the text, so all offsets are absolute.
- **Callbacks.** Callbacks invoked while parsing, such as `Then`, `When` or `ParseContext` hooks, can run
  more than once for the same text when an attempt is retried. Keep them free of side effects, or use the
  `ParseManyAsync` item callback, which runs once per committed value.
- **Contexts.** A `ParseContext` is created for each window. `ParseManyAsync` shares it between the items of
  the same window. State that must outlive a window belongs elsewhere.
- **Cancellation.** The token is passed to the reads and to the `ParseContext`, and throws an
  `OperationCanceledException`.

## Limitations

- A single value is buffered entirely, and a value of `ParseManyAsync` too.
- Parsers with unbounded lookahead, such as `AnyCharBefore` or a `Capture` of a large structure, make the
  window grow until they find their end.
- A conservative `HitEnd`, for instance a `OneOf` alternative that reached the end before another one
  succeeded, costs a retry, never correctness.
- To grow the window, the driver reads until the target size or the end of the input. With interactive
  sources, use the delimited overload or a small `BufferSize`.
- Generated parsers have no asynchronous entry point yet. Their synchronous code embeds the same `Cursor`
  and sets `HitEnd` the same way.

## Performance

Parsing a `string` is unchanged: `HitEnd` is only set on the paths that reach the end of the text.

The benchmarks below (`StreamingBenchmarks`) parse JSON objects with the sample grammar, from a `TextReader`
which isn't a `StringReader` and from a non-seekable stream, to avoid the fast paths. `Document` parses an
array of `Count` objects, `Lines` parses one object per line.

Measured on Apple M-series (Arm64), .NET 10, BenchmarkDotNet `ShortRun`. Ratios are relative to the first row
of each group.

| Method | Count | Mean | Ratio | Allocated |
|---|---:|---:|---:|---:|
| Document, `ReadToEnd` + `Parse` | 10 | 3.35 us | 1.00 | 7.84 KB |
| Document, `TryParseAsync(TextReader)` | 10 | 3.50 us | 1.04 | 9.05 KB |
| Document, `Stream` `ReadToEndAsync` + `Parse` | 10 | 3.68 us | 1.10 | 13.05 KB |
| Document, `TryParseAsync(Stream)` | 10 | 3.91 us | 1.17 | 21.36 KB |
| Document, `ReadToEnd` + `Parse` | 1000 | 368.2 us | 1.00 | 757.8 KB |
| Document, `TryParseAsync(TextReader)` | 1000 | 546.9 us | 1.49 | 1192.2 KB |
| Document, `Stream` `ReadToEndAsync` + `Parse` | 1000 | 428.4 us | 1.16 | 997.4 KB |
| Document, `TryParseAsync(Stream)` | 1000 | 512.8 us | 1.39 | 1204.5 KB |
| Lines, `ReadLine` + `Parse` | 10 | 3.44 us | 1.00 | 11.52 KB |
| Lines, `ParseManyAsync(TextReader)` | 10 | 3.35 us | 0.98 | 8.77 KB |
| Lines, `ParseManyAsync(TextReader, '\n')` | 10 | 3.66 us | 1.06 | 11.86 KB |
| Lines, `ParseManyAsync(Stream)` | 10 | 3.74 us | 1.09 | 21.07 KB |
| Lines, `ReadLine` + `Parse` | 1000 | 355.8 us | 1.00 | 1201.8 KB |
| Lines, `ParseManyAsync(TextReader)` | 1000 | 331.6 us | 0.93 | 855.9 KB |
| Lines, `ParseManyAsync(TextReader, '\n')` | 1000 | 374.3 us | 1.05 | 1202.1 KB |
| Lines, `ParseManyAsync(Stream)` | 1000 | 339.8 us | 0.95 | 868.2 KB |

A large single value costs up to about 1.5 times reading the text first, because of the inconclusive attempts.
Use `TryParseAsync` to avoid waiting for, or buffering, text which follows the value, and read the text first
when the whole input is wanted anyway. `ParseManyAsync` is as fast as reading lines, with memory bounded by
the largest value.
