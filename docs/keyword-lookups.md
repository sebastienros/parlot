# Keyword lookup investigation

## Decision

Use a generated length/discriminator tree for sufficiently large, compatible keyword-only
`OneOf`/`Or` choices. Keep the runtime `CharMap` lookup, small generated choices, and other
parser types unchanged. No new public parser or byte-input API is introduced.

The automatic specialization accepts 8-256 ordinal, nonempty ASCII-letter keywords, each
at most 64 characters, with at most 4096 characters in the vocabulary. Nested choices are
flattened when their whitespace behavior agrees. These are conservative generation/code-size
bounds, not a universal performance crossover.

For example, this build-only grammar needs no special API:

```csharp
OneOf(
    Terms.Keyword("if"), Terms.Keyword("else"), Terms.Keyword("while"), Terms.Keyword("return"),
    Terms.Keyword("int"), Terms.Keyword("internal"), Terms.Keyword("interface"), Terms.Keyword("class"))
```

### Why not replace every lookup?

`Keyword` checks for an **ASCII-letter boundary**, not an identifier boundary:
`Keyword("if")` accepts the prefix of `if1`, `if_`, and `if\u00e9`. Choices are ordered, not
longest-match. For example, `Keyword("if1").Or(Keyword("if12"))` returns `if1` for `if12`.
Similarly, `Keyword("Accept")` may match the prefix of `Accept-Encoding`.

Consequently, scanning letters-or-digits, or treating arbitrary keyword lists as exact
tokens, would change existing behavior. Ordinary `Text` choices also accept prefixes and
cannot be replaced this way. Mixed parsers can have callbacks and other observable effects.
The existing paths remain responsible for those cases.

ASCII folding is not a replacement for `OrdinalIgnoreCase` or culture-sensitive comparison.
Those keyword choices retain their existing implementation, including `returnMatchedText`.
The internal recognizer supports ASCII folding and byte spans for comparative experiments,
but those modes are **not** wired into the public keyword parser.

## Generation

`KeywordChoiceSource` scans the next ASCII-letter run without advancing the cursor. It stops
after the vocabulary's maximum length plus one, so a very long unknown identifier does not
introduce unbounded scanning. It then calls a static local recognizer emitted by
`KnownStringLookup`. A successful match returns canonical text and advances the cursor once;
failure restores any skipped whitespace. `Capture`, custom whitespace, prefix parsing,
and composition continue to use the existing generated parser contracts.

The recognizer:

1. Groups candidates by exact length and removes duplicate spellings, retaining the first result.
2. Verifies chunks shared by every remaining candidate once, before branching.
3. Scores unproven discriminator positions by the smallest largest partition, then the
   greatest number of distinct partitions. Ties prefer wider loads, then lower offsets.
4. Dispatches on a loaded value and records precisely which positions that equality proved.
5. Verifies only the remaining positions at each leaf.

Supported load widths are 1/2/4 UTF-16 code units and 1/2/4/8 bytes. Nodes with more than
32 candidates use single-character/byte dispatch to avoid a large sparse wide-integer
switch; smaller nodes consider all widths. Leaf and shared-chunk comparisons still use
wide loads. The benchmarks can force either discriminator strategy.

Hoisting common comparisons was essential, not merely a cleanup. The initial implementation
repeated the 12-character prefix in every leaf of the 128-token example. JIT inspection
showed approximately 52-53 KB of native code, large stack frames, and non-inlined span helper
calls. Hoisting reduced the narrow recognizer to about 2.6 KB and changed recognition from
roughly 150 ns to 2-3 ns. Adding more aggressive inlining would not address that duplication.

### Safety, endianness, and folding

All loads use `MemoryMarshal.Read` over explicitly sliced spans. The enclosing exact-length
case proves each offset/width is valid; there are no speculative or out-of-range reads.
No unsafe compilation option is needed for the recognizer.

Constants use little-endian lane order, independently of the machine running the generator.
Generated code tests `BitConverter.IsLittleEndian`; the JIT folds this platform constant.
The other branch reverses byte order for byte input, or **UTF-16 lane order** for char input,
without reversing the bytes within each character. Tests exercise simulated big-endian
loads as well as native loads, including checked arithmetic.

ASCII folding clears bit `0x20` only at alphabetic positions. Other positions retain all
bits (`0xFF` for bytes, `0xFFFF` for chars). Each switch uses a common mask across its
candidates; incompatible masks fall back to verified leaf comparisons. Non-ASCII input
cannot match by losing its high bits. Byte/folding vocabularies themselves must be ASCII.

Generation uses allocations and LINQ; recognition does not. The benchmark project links
the same internal emitter source, rather than maintaining a second implementation.

## Measurements

Measured on an Apple M4 Pro, macOS 15.8, .NET 10.0.11 Arm64, BenchmarkDotNet 0.15.8.
Tiered compilation was disabled for the controlled comparisons. Measurements are local
microbenchmarks, not an x64, .NET 8, or application-level throughput claim.

### Exact-token recognition

These benchmarks receive already delimited input: a fixed 64-element corpus containing
25% hits, first-character misses, last-character misses, and overlong tokens. Uppercase
hits are included for case-insensitive runs. Vocabulary analysis, dynamic compilation,
UTF-8 encoding, and dictionary construction are outside the measured operation.

Representative final means, in ns/token; all methods allocate **0 B/token**:

| Vocabulary | Mode | Char equality chain | Wide char tree | Narrow char tree | Dictionary | FrozenDictionary | FNV hash + verification |
|---|---|---:|---:|---:|---:|---:|---:|
| 4 keywords | Ordinal | 2.50 | 2.36 | 2.32 | 6.81 | 3.85 | 3.78 |
| 27 headers | Ordinal | 3.09 | 2.81 | 2.77 | 7.81 | 6.58 | 6.92 |
| 77 keywords | Ordinal | 7.37 | 3.48 | 5.17 | 7.26 | 6.00 | 5.97 |
| 128 shared-prefix tokens | Ordinal | 27.58 | 2.82 | 2.17 | 7.33 | 6.03 | 8.08 |
| 27 headers | Ignore ASCII case | 3.24 | 2.86 | 2.80 | 9.05 | 9.05 | 9.41 |
| 77 keywords | Ignore ASCII case | 7.07 | 3.57 | 5.12 | 7.74 | 6.17 | 7.79 |
| 128 shared-prefix tokens | Ignore ASCII case | 31.27 | 3.23 | 2.71 | 10.45 | 7.97 | 12.74 |

The char equality baseline uses constant span comparisons, partitioned by length.
On these ASCII inputs its case-insensitive `OrdinalIgnoreCase` comparisons agree with
ASCII folding; non-ASCII equivalence is deliberately not assumed.
Dictionary measurements start with existing strings: they do not include allocating or
decoding a token. The separate `ScanAndFrozen` experiment uses a span alternate lookup.

Byte comparisons tell a similar story:

| Vocabulary | Mode | Byte equality chain | Byte tree |
|---|---|---:|---:|
| 4 keywords | Ordinal | 2.37 | 2.28 |
| 27 headers | Ordinal | 3.21 | 2.86 |
| 77 keywords | Ordinal | 14.18 | 3.47 |
| 128 shared-prefix tokens | Ordinal | 141.49 | 2.85 |
| 27 headers | Ignore ASCII case | 7.42 | 2.81 |
| 77 keywords | Ignore ASCII case | 27.53 | 3.47 |
| 128 shared-prefix tokens | Ignore ASCII case | 440.77 | 2.99 |

The byte baseline uses `SequenceEqual` or `Ascii.EqualsIgnoreCase` against UTF-8 literals.
Large equality chains can also exhaust the JIT's inlining budget. These results motivate
generated exact protocol-token recognition, but do not establish a need for a new public
Parlot byte parser. No decoding/materialization occurs in either byte benchmark.

### Complete generated parsing

`KeywordParserBenchmarks` compares the new parser with the original first-character
dispatch **in the same build**. `BaselineKeyword` hides the concrete parser type from
specialization but forwards its seek information and emits exactly the original keyword
body: it introduces no extra generated helper call.

Hit and miss corpora are checked against the runtime parser at setup. Misses include
immediate, early, same-length late, and overlong failures; the mixed corpus is 25% hits.
Every operation includes the public generated entry point, token scanning/recognition,
cursor advancement, and the existing parsing-context allocation.

Final head-to-head means in ns/call (three warmups, seven 250 ms measurement iterations):

| Vocabulary | Input | Original generated lookup | Current generated lookup | Current/original |
|---|---|---:|---:|---:|
| 4 keywords | Hit | 25.38 | 25.69 | 1.01 |
| 4 keywords | Miss | 22.98 | 23.12 | 1.01 |
| 4 keywords | Mixed | 23.82 | 23.53 | 0.99 |
| 16 keywords | Hit | 33.09 | 25.15 | 0.76 |
| 16 keywords | Miss | 31.42 | 22.63 | 0.72 |
| 16 keywords | Mixed | 37.07 | 22.55 | 0.61 |
| 77 keywords | Hit | 34.11 | 24.67 | 0.72 |
| 77 keywords | Miss | 31.78 | 21.86 | 0.69 |
| 77 keywords | Mixed | 36.32 | 24.12 | 0.66 |
| 128 shared-prefix tokens | Hit | 243.50 | 32.86 | 0.13 |
| 128 shared-prefix tokens | Miss | 246.20 | 22.49 | 0.09 |
| 128 shared-prefix tokens | Mixed | 299.78 | 26.11 | 0.09 |

Small choices showed little consistent benefit and are left on the old path. The
16- and 77-keyword vocabularies showed clear improvements; the shared-prefix vocabulary
benefited most. Both generated variants retain the existing **192 B/call** allocation.
This is not a claim that the complete generated entry point is allocation-free.

The automatic floor of eight candidates is conservative; the measured contrast between
four and sixteen candidates is not evidence of an exact, vocabulary-independent crossover.
The upper limits prevent an unbounded generated method. Larger vocabularies and different
CPUs should be measured before expanding these limits.

### Whole source grammar

`KeywordSourceBenchmarks` measures a complete, synthetic C#-like declaration grammar,
not isolated token recognition. It has 16 type keywords and eight modifier keywords,
plus identifiers, integer literals, parentheses, arithmetic precedence, semicolons,
whitespace, repetition, and an EOF check. For example:

```csharp
public static int integer = (10 + 2) * 3 - 1;
byte internalValue = 4 + 5 * 6;
```

This is a syntax subset, not a C# compiler: it evaluates numeric initializers but does
not type-check them. Parsing returns a list of declaration records containing type,
name, modifier count, and initializer value. Identifier strings, declaration objects,
and result-list allocations are included in the measurement.

Both entry points use the same `BuildSource` grammar. The before variant substitutes
the existing `BaselineKeyword` emitter for the two keyword choices, retaining the old
first-character dispatch without introducing extra generated calls. The after variant
uses ordinary `Terms.Keyword`; inspection confirms its two choices use `MatchKeyword`.
All other grammar construction is shared. Setup checks every result record against
independently calculated expected values, and verifies that invalid files fail without
returning partial results.

On the same Apple M4 Pro/.NET 10.0.11 environment, using normal runtime tiering, two
launches, five warmups, and twelve 500 ms measurement iterations:

| Declarations | Input | Before | After | Time reduction | Allocated, both |
|---|---|---:|---:|---:|---:|
| 8 | Valid | 1.805 us | 1.598 us | 11.5% | 1.43 KB |
| 128 | Valid | 29.259 us | 26.237 us | 10.3% | 19.34 KB |
| 8 | Unknown type in final declaration | 1.636 us | 1.448 us | 11.5% | 1.35 KB |
| 128 | Unknown type in final declaration | 28.952 us | 25.899 us | 10.5% | 19.26 KB |
| 8 | Invalid expression in final declaration | 1.682 us | 1.519 us | 9.7% | 1.39 KB |
| 128 | Invalid expression in final declaration | 28.622 us | 25.887 us | 9.6% | 19.30 KB |

The 99.9% confidence intervals for valid files are 1.805 +/- 0.0146 us versus
1.598 +/- 0.0039 us (eight declarations), and 29.259 +/- 0.1516 us versus
26.237 +/- 0.0564 us (128 declarations). An earlier, shorter run showed 8-9% lower
valid-file parsing times, but its long-input failure cases were noisy; the table above
uses the longer confirmation run rather than drawing conclusions from that noise.

The application-shaped gain is approximately **10-11% lower total parsing time**, not
the larger isolated-token speedup. Expression parsing and object construction account
for the unaffected work. Allocations are unchanged. These results do not extend to the
existing SQL sample: its case-insensitive keywords are ineligible for this optimization.

Reproduce this comparison with:

```bash
dotnet build -c Release
dotnet run --no-build --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- \
  --filter '*KeywordSourceBenchmarks*' --job short \
  --warmupCount 5 --iterationCount 12 --launchCount 2 --iterationTime 500
```

## Generated shapes

The following abbreviated shapes illustrate the emitter; actual output uses packed constants
and endian-aware loads rather than the descriptive `Read`/`Verify` helpers shown here.

HTTP-style headers share content before dispatching:

```csharp
case 16:
    if (Read8(input, 0) != Packed("Content-")) return Unknown;
    switch (Read8(input, 8))
    {
        case Packed("Encoding"): return ContentEncoding;
        case Packed("Language"): return ContentLanguage;
        case Packed("Location"): return ContentLocation;
    }
    break;
```

Language keywords first exclude unrelated lengths, then choose informative positions:

```csharp
case 5:
    switch (Read4Chars(input, bestOffset))
    {
        // Each branch verifies only the one remaining unproven character.
        // The result is the canonical keyword, not a new substring.
    }
    break;
```

For `commonprefixaa` through the 128 generated suffix combinations, a narrow tree keeps
the repeated prefix out of the leaves:

```csharp
case 14:
    if (!VerifyCommonPrefix(input)) return Unknown;
    switch (input[13])
    {
        case 'a':
            switch (input[12])
            {
                case 'a': return FirstToken;
                // Remaining valid prefix/suffix combinations...
            }
            break;
        // Remaining suffix letters...
    }
    break;
```

These are examples of exact recognition, not a promise to change `Keyword("Accept")`
into an HTTP-header parser.

## Reproduction and coverage

```bash
dotnet build -c Release

DOTNET_TieredCompilation=0 dotnet run --no-build \
  --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- \
  --filter '*KeywordLookupBenchmarks*' \
  --warmupCount 3 --iterationCount 5 --iterationTime 200

DOTNET_TieredCompilation=0 dotnet run --no-build \
  --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- \
  --filter '*KeywordParserBenchmarks.Generated*' \
  --warmupCount 3 --iterationCount 7 --iterationTime 250
```

The complete parser benchmark also includes runtime `OneOf`, scan-plus-tree, and
scan-plus-FrozenDictionary methods. Those isolate different costs; their raw ns/op must
not be confused with equivalent complete entry-point measurements.

Inspect parser output under `test/Parlot.Benchmarks/obj/GeneratedFiles`.
For native recognizer code, run the `KeywordLookupTests` test host with
`DOTNET_JitDisasm='Recognizer:Narrow'` and `DOTNET_TieredCompilation=0`.
Branch hardware counters were not collected.

Tests cover every known token, every position mutated through byte values, casing,
punctuation, shorter/longer inputs, duplicate spellings and overlapping masks, random
UTF-16/byte input, and simulated big-endian execution. Standalone consumers additionally
cover keyword boundaries, nested `Or`, whitespace rollback, capture, custom whitespace,
checked arithmetic, and absence of a runtime Parlot dependency.
