# Parsing streams

Parlot parses a `TextReader` or a `Stream` with the same parsers as a `string`. The parser graph, the built-in
parsers and `ParseContext` callbacks work unchanged: streaming drives the synchronous `Parse` method, it isn't a
second implementation of each parser.

```csharp
using Parlot.Fluent;

// One value, of any size, retaining only the text the parser can still read
using var file = File.OpenRead("document.json");
var value = JsonParser.Json.Parse(file);

// The same, without blocking the caller
await using var input = File.OpenRead("document.json");
var (success, result) = await JsonParser.Json.TryParseAsync(input, cancellationToken: cancellationToken);

// Successive values, for instance NDJSON or log records
await using var records = File.OpenRead("records.ndjson");
var count = await record.ParseManyAsync(records, (item, ct) => store.AddAsync(item, ct), cancellationToken: cancellationToken);
```

There are two modes:

- **Compacting buffer**, for one value: `Parse`/`TryParse(TextReader | Stream)` and `ParseAsync`/`TryParseAsync`.
  The parse runs once over a buffer which is refilled in place, and which only retains the text a parser can still
  move back to. Memory is bounded by the largest token or backtracking region, not by the size of the value.
- **Window driver**, for successive values: `ParseManyAsync`. Each value is parsed from a window of text, and
  parsed again on a larger window when it reached the end of the window.

## Compacting buffer

The cursor reads a window of the input, a `string` whose first char is at the absolute offset `Cursor.BufferStart`.
`Cursor.Offset`, `Cursor.Position` and `ParseResult` offsets are absolute: they are the ones in the whole text, so
positions saved by parsers stay valid when the window is replaced. Lines and columns are the ones of the whole text.

### Tokens

The parsers which read the cursor directly, such as literals, identifiers, numbers, strings or patterns, are *tokens*.
In compacting mode they run through `ParseContext.ParseToken`:

1. the token parses the current window, which isn't final: the `Cursor` records in `HitEnd` that the token read its end;
2. if `HitEnd` is set, the token's result might depend on the text which follows, so the window is refilled and the
   token is parsed again from its start;
3. otherwise its result is the one of a `string` parse, and the parse continues.

Only the token is parsed again, never the value. A token is small compared to the input, so this costs about nothing.
Between tokens the cursor is never at the end of a non-final window: white space is skipped incrementally, refilling as
needed, and a custom `ParseContext.WhiteSpaceParser`, which can parse comments, runs as a token.

### Backtrack floor and pins

A refill drops the text below the *backtrack floor*: the lowest position from which an active parser can read again.
The parsers which move back to read again keep their position with `ParseContext.Pin()`:

| Parser | Pinned text |
|---|---|
| `OneOf` (`Or`, `|`) | The start of the choice while alternatives remain to be tried. With a lookup table (see `ISeekable`), the next char selects the alternatives first, so an exclusive choice, like the JSON values, pins nothing. |
| `Optional`, `ZeroOrOne`, `Else`, `Not`, `WhenFollowedBy`, `WhenNotFollowedBy` | Their start, while their parser runs. |
| `ZeroOrMany`, `OneOrMany`, `Separated` | The start of the current element only: a root array or list streams. |
| `LeftAssociative`, `RightAssociative` | The start of the current operator, which isn't part of the match when no operand follows it. |
| `Unary` | Its start, while its operators are tried. |
| `Capture` | Its start, until the captured text is created. |

Sequences (`And`, `SkipAnd`, `AndSkip`, `Between`, ...) don't pin: they only move back to report a failure, and their
caller pins the position it reads again from, or fails too. A failed parse can therefore leave the cursor on discarded
text, but no parser reads it.

A grammar whose root is a non-exclusive choice pins its start until an alternative succeeds, so it buffers the whole
value. When a parser's match decides the rest of the parse, `Commit()` releases the text before it: no parser can
move back before a committed match (a PEG *cut*). Moving back before a committed match, or to discarded text, throws a
`ParseException`. `Commit()` has no effect when parsing a `string`.

```csharp
// After "let", the statement can't be anything else: don't keep the text for the other alternatives
var let = Terms.Keyword("let").Commit().SkipAnd(assignment);
var statement = let.Or(expression);
```

### Custom parsers

A custom parser which reads the cursor (`Cursor.Current`, `Span`, `Advance`, `Match`, `Scanner` methods, ...) must be a
token. Start its `Parse` method with:

```csharp
if (context.IsCompacting)
{
    return context.ParseToken(this, ref result);
}
```

`IsCompacting` is `false` when parsing a `string`, and while a token is parsed. A parser which reads the end of a
non-final window outside of a token throws an `InvalidOperationException` describing this fix. A parser which only
invokes other parsers needs nothing, except a `Pin()` if it moves back to read again:

```csharp
var pin = context.Pin();
// ... parse, ResetPosition(start), parse again ...
context.Unpin(pin);
```

`Pin` returns `-1` and `Unpin(-1)` does nothing when the text doesn't need to be pinned, so this costs a branch when
parsing a `string`. Pins are released in the reverse order. `MovePin` moves a pin forward, for instance to the next
element of a loop.

`Cursor.Buffer` is the current window: index it with `offset - Cursor.BufferStart`, or use `Cursor.GetSpan(start, length)`
and `Cursor.CreateSpan(start, length)`, which take absolute offsets. A `TextSpan` created by `CreateSpan` references
its window string, which remains valid after a refill; its `Offset` is relative to its `Buffer`.

### Asynchronous parsing

The parse is synchronous. `ParseAsync` and `TryParseAsync` read the first `BufferSize` chars asynchronously. When the
input fits in them, it is parsed as a `string` on the calling thread. Otherwise the parse runs on a thread pool thread
whose refills block on `TextReader.ReadAsync`, so a stream which doesn't allow synchronous reads, like an ASP.NET Core
request body, can be parsed. Use `Parse(Stream)` on a thread which can block to avoid the thread pool hop.

## Window driver

`ParseManyAsync` reads text into a contiguous window and parses the window with the regular synchronous parser.
While more text may follow, the window is *non-final*: its `Cursor` has `IsFinal == false`, and records in
`Cursor.HitEnd` that the parse depended on text past the end of the window. This is the model of
`Utf8JsonReader(isFinalBlock)` and of Java's `Matcher.hitEnd()`.

- If the parse ends, successfully or not, **without** `HitEnd`, the result doesn't depend on what follows:
  it is the result of parsing the whole text.
- Otherwise the result is inconclusive. The driver reads more text, so that the window at least doubles,
  and parses the value again from its start.
- When the reader is exhausted, the window is final, `HitEnd` is never set and the parse is exactly the
  parse of a `string`.

`HitEnd` is sticky for an attempt: `ResetPosition` doesn't clear it, so a branch that reached the end of the
window and then backtracked still makes the attempt inconclusive. A `ParseException` thrown while `HitEnd`
is set is inconclusive too, since more text could have avoided it. Only the item which spans the end of the window
is retried, and the doubling bounds the cost of the retries.

### What sets `HitEnd`

Both modes rely on `HitEnd`: the window driver to retry a value, the compacting buffer to retry a token.
`Cursor` sets it centrally, so most parsers need nothing:

- reaching the end of the window, by any `Advance*` method or `ResetPosition`, or when the window is empty;
- `PeekNext` past the end of the window;
- `Match` when the remaining text is a prefix of the expected text;
- `TryGetSpan(minLength, out span)` when fewer characters are available.

A parser which reads to the end of the window, for instance an identifier or a number, has reached the end
and set `HitEnd`. Explicit marks are only needed where a parser fails or stops by *looking* at the end
without advancing to it. The built-in parsers and the source generator emitters do this, for instance for
literal texts, keyword lookups, unterminated strings, and characters sets with a minimum size.

A custom parser which reads `Cursor.Span` or `Cursor.Buffer` directly must call `Cursor.MarkHitEnd()` when its
result depends on the text which follows the window, typically when it fails or stops because the span is too short:

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
combines both. Forgetting it can only produce a wrong result when parsing a stream: compare `Parse(TextReader)`
and `ParseManyAsync` over a reader returning one character at a time with `Parse(string)` to test a parser.

## API

All the methods are on `Parser<T>` and take an optional `StreamParseOptions` and `CancellationToken`.
`Stream` overloads decode the stream with `StreamParseOptions.Encoding` (UTF-8 by default, a byte order
mark takes precedence) and leave it open.

| Method | Description |
|---|---|
| `Parse(TextReader \| Stream)` | Parses one value through the compacting buffer, returns the value or `default`. A `ParseException` is propagated. |
| `TryParse(TextReader \| Stream, out T value[, out ParseError? error])` | Same, returns whether the text matched. A `ParseException` or a cancellation returns `false` with the error. |
| `TryParseAsync(TextReader \| Stream)` | Parses one value through the compacting buffer, returns `(bool Success, T? Value)`. A `ParseException` returns `false`. |
| `ParseAsync(TextReader \| Stream)` | Same, returns the value or `default`. A `ParseException` is propagated. |
| `ParseManyAsync(TextReader \| Stream, onItem)` | Parses successive values, invokes `onItem` (`Action<T>` or `Func<T, CancellationToken, ValueTask>`) for each and returns their count. |
| `ParseManyAsync(TextReader \| Stream, separator, onItem)` | Same, with a separator parser between the values. A trailing separator is accepted. |
| `ParseManyAsync(TextReader \| Stream, char delimiter, onItem)` | Parses each text delimited by `delimiter`, for instance each line, as a value. |
| `ParseManyAsync(...)` without `onItem` | Returns an `IAsyncEnumerable<T>` (.NET 8 and later). |

`StreamParseOptions`:

| Option | Default | Description |
|---|---|---|
| `BufferSize` | 4096 | Number of characters read at once, and before parsing. A refill reads at least as many characters as it retains. |
| `MaxBufferedCharacters` | `int.MaxValue` | Maximum characters buffered: the retained text of the compacting buffer, or the window of a value for `ParseManyAsync`. Exceeding it throws a `ParseException` at the start of the token or value which doesn't fit. Set it for untrusted input. |
| `Encoding` | UTF-8 | Encoding of a `Stream`. |
| `SkipWhiteSpace` | `true` | Whether `ParseManyAsync` skips white space before each item, separator and the end. |
| `ContextFactory` | `null` | Creates the `ParseContext` of the parse (of each window for `ParseManyAsync`), for instance to set `WhiteSpaceParser` or use a derived context. It must use the provided scanner and pass the cancellation token. |

### Single values

The single value methods return the result of `Parse`/`TryParse` on the whole text, but stop reading as soon as
the result is complete: the text which follows the value is not necessarily read, like for `Parse(string)` which
doesn't require the end of the text. Use `.Eof()` to reject trailing text.

An input which fits in the first `BufferSize` chars, a `StringReader` in the asynchronous methods, and a seekable
`Stream` whose remaining length is within `MaxBufferedCharacters` in `TryParseAsync`, are parsed as a `string`.

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
  In compacting mode `Cursor.Offset` and `ParseResult` offsets are absolute too. With `ParseManyAsync` they are
  relative to the window. A `TextSpan` references the window string it was parsed from (which it keeps alive), and
  its `Offset` is relative to that string. Call `ToString()` to keep only its text.
- **Callbacks.** In compacting mode, callbacks invoked while parsing, such as `Then`, `When` or `ParseContext` hooks,
  run once, like for a `string`, except within custom tokens, which can be parsed again. With `ParseManyAsync` they can
  run more than once for the same text when an attempt is retried: keep them free of side effects, or use the item
  callback, which runs once per committed value.
- **Contexts.** In compacting mode a single `ParseContext` parses the whole input. `ParseManyAsync` creates one for
  each window and shares it between the items of the same window.
- **Cancellation.** The token is passed to the reads and to the `ParseContext`, and throws an
  `OperationCanceledException`.

## Limitations

- Parsers with unbounded lookahead, such as `AnyCharBefore` or a `Capture` of a large structure, and choices which
  aren't exclusive, retain their text until they complete. Use `Commit()` where a match decides the parse.
- A conservative `HitEnd`, for instance a token which reached the end of the window to decide that it failed, costs a
  token retry (a value retry with `ParseManyAsync`), never correctness.
- The compacting buffer supports inputs up to `int.MaxValue` characters, since offsets are `int`.
- To fill a window, the readers read until the target size or the end of the input. With interactive sources, use
  the delimited overload or a small `BufferSize`.
- Generated parsers only parse strings. Their code embeds the same `Cursor` and sets `HitEnd` the same way, but
  compiles it with `PARLOT_STANDALONE`, which fixes `BufferStart` to `0` and removes the window replacement.

## Performance

Parsing a `string` is unchanged: the compacting checks share a byte of `ParseContext` with the parser hooks,
`HitEnd` is only set on the paths that reach the end of the text, and generated parsers compile the compacting code out.

The benchmarks below (`StreamingBenchmarks`) parse JSON objects with the sample grammar, from a `TextReader`
which isn't a `StringReader` and from a non-seekable stream, to avoid the fast paths. `Document` parses an
array of `Count` objects, `Lines` parses one object per line.

Measured on Apple M-series (Arm64), .NET 10, BenchmarkDotNet `ShortRun`. Ratios are relative to `ReadToEnd` + `Parse`.
`ReadToEnd` doesn't copy the text here, it returns the benchmark's string.

| Method | Count | Mean | Ratio | Allocated | Alloc ratio |
|---|---:|---:|---:|---:|---:|
| Document, `ReadToEnd` + `Parse` | 1000 | 386.0 us | 1.00 | 757.8 KB | 1.00 |
| Document, `Parse(TextReader)` | 1000 | 549.7 us | 1.42 | 869.1 KB | 1.15 |
| Document, `TryParseAsync(TextReader)` | 1000 | 547.8 us | 1.42 | 870.1 KB | 1.15 |
| Document, `Stream` `ReadToEndAsync` + `Parse` | 1000 | 461.7 us | 1.20 | 997.4 KB | 1.32 |
| Document, `Parse(Stream)` | 1000 | 499.0 us | 1.29 | 881.5 KB | 1.16 |
| Document, `ReadToEnd` + `Parse` | 10000 | 8.00 ms | 1.00 | 7.49 MB | 1.00 |
| Document, `Parse(TextReader)` | 10000 | 10.27 ms | 1.28 | 8.60 MB | 1.15 |
| Document, `TryParseAsync(TextReader)` | 10000 | 10.26 ms | 1.28 | 8.60 MB | 1.15 |
| Document, `Stream` `ReadToEndAsync` + `Parse` | 10000 | 8.07 ms | 1.01 | 9.69 MB | 1.29 |
| Document, `Parse(Stream)` | 10000 | 9.56 ms | 1.20 | 8.61 MB | 1.15 |
| Document, `ReadToEnd` + `Parse` | 100000 | 92.9 ms | 1.00 | 74.4 MB | 1.00 |
| Document, `Parse(TextReader)` | 100000 | 117.7 ms | 1.27 | 85.7 MB | 1.15 |
| Document, `TryParseAsync(TextReader)` | 100000 | 117.6 ms | 1.27 | 85.7 MB | 1.15 |
| Document, `Stream` `ReadToEndAsync` + `Parse` | 100000 | 93.5 ms | 1.01 | 96.7 MB | 1.30 |
| Document, `Parse(Stream)` | 100000 | 118.0 ms | 1.27 | 85.7 MB | 1.15 |
| Lines, `ReadLine` + `Parse` | 100000 | 36.9 ms | 1.00 | 116.3 MB | 1.00 |
| Lines, `ParseManyAsync(TextReader)` | 100000 | 34.3 ms | 0.93 | 83.9 MB | 0.72 |
| Lines, `ParseManyAsync(TextReader, '\n')` | 100000 | 40.9 ms | 1.11 | 116.3 MB | 1.00 |
| Lines, `ParseManyAsync(Stream)` | 100000 | 33.7 ms | 0.91 | 83.9 MB | 0.72 |

The cost of a single value is a constant ratio of reading the text first, whatever its size, and the buffer stays flat.
Peak buffered characters for the same documents:

| Count | Document | `BufferSize` 4096 (default) | `BufferSize` 64 |
|---:|---:|---:|---:|
| 1000 | 56,295 | 4,163 | 142 |
| 10000 | 573,763 | 4,167 | 142 |
| 100000 | 5,838,891 | 4,169 | 142 |

The extra time is the per-token bookkeeping and the window strings: each refill allocates a new window, about the size of
the text in total, which the `TextSpan`s of the tokens reference. Read the text first when it's small or wanted anyway,
and stream it when it's large, or when it isn't needed after the parse. `ParseManyAsync` is as fast as reading lines,
with memory bounded by the largest value.

Before the compacting buffer, single values were parsed by the window driver, which retries the whole value on a
larger window. That cost grew with the value, and buffered all of it:

| Method | Count | Window driver | Compacting buffer |
|---|---:|---:|---:|
| `TryParseAsync(TextReader)` time ratio | 1000 | 1.51 | 1.42 |
| `TryParseAsync(TextReader)` time ratio | 10000 | 1.35 | 1.28 |
| `TryParseAsync(TextReader)` time ratio | 100000 | 1.93 | 1.27 |
| `TryParseAsync(TextReader)` alloc ratio | 100000 | 2.26 | 1.15 |
| Peak buffered characters | 100000 | 16,777,216 (window) | 4,169 |
