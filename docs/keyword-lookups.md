# Keyword lookup investigation

## Decision

Use a generated length/discriminator tree for sufficiently large, compatible keyword-only
`OneOf`/`Or` choices. Ordinal text-only choices use a separate ordered-prefix recognizer.
Keep the runtime `CharMap` lookup, small generated choices, and mixed or unsupported
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
The separate text recognizer described below handles ordinal text choices without a
boundary requirement. Other unsupported keyword and mixed choices retain the existing paths.

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

## Applying the FancyEnum / AsciiHash findings

[FancyEnum's investigation](https://htmlcsstoimage.com/blog/fancy-enum-generator) distinguishes
three techniques: small direct comparisons, length-partitioned span switches, and integer
dispatch over packed bytes. Its [implementation notes](https://github.com/htmlcsstoimage/FancyEnumGenerator/blob/main/docs/how-it-works.md)
also explain why large switches are separated into helpers: span temporaries can make a
large method's prologue expensive even on an immediate miss.

[AsciiHash](https://github.com/StackExchange/StackExchange.Redis/blob/main/eng/StackExchange.Redis.Build/AsciiHash.md)
is a discriminator, not permission to accept a truncated token. A matching packed prefix
must still be accompanied by the length and complete content verification. Parlot's input
is UTF-16 rather than UTF-8: its packed loads retain all 16 bits of each character, and
recognition proves every position. Narrowing characters to bytes would introduce false
matches for non-ASCII input. No dependency on either generator is added.

The benchmark compares a length-partitioned span switch, the existing adaptive packed
tree, the same tree separated into length helpers, and an FNV hash with full verification.
Span switches and hashing are comparison strategies, not automatic replacements for
every lookup. A hash must inspect the whole input; an informative packed discriminator
can reject it earlier.

### Bounded keyword length helpers

Length splitting is enabled only for more than 32 distinct spellings, more than 1024
total UTF-16 characters, and more than one distinct length. Eligible buckets with more
than four candidates get a static helper; the other buckets share a budget of 32
inlined candidates before also moving into helpers. The parent length switch proves
the helpers' load bounds. No helper is forced to inline.

These are conservative code-footprint limits, not universal JIT thresholds. Splitting
every vocabulary slightly regressed the 77-keyword and 128-token single-length
experiments, while substantially improving the larger multi-length vocabulary.
Moderate and single-length sets therefore retain their original generated tree.

### Ordered text prefixes

`TextChoiceSource` extends packed lookup to homogeneous ordinal `Text` choices using the
same vocabulary bounds as the keyword specialization. Unicode, punctuation, NUL, surrogate
code units, and newlines are allowed. Whitespace behavior must agree across flattened
choices. Case-insensitive and culture-sensitive comparisons, empty literals, callbacks,
and mixed parser kinds keep their original emitters.

Unlike keywords, text matches a prefix and needs no delimiter. At each node the emitter:

1. Records the first complete candidate as the fallback and drops candidates after it.
2. Groups earlier candidates by their shortest remaining prefix.
3. Checks the available input length before slicing and uses the existing packed recognizer
   to dispatch on those prefixes.
4. Continues matching the suffix in a small static helper, returning the recorded fallback
   if the longer, earlier alternatives fail.

Only recognized prefixes reach suffix helpers. Therefore a fallback is returned only
after its entire text has been verified, and a longer match never supersedes an earlier
shorter alternative. For `Text("if1"), Text("if"), Text("if12")`, input `if12` returns
`if1`, while `ifx` returns `if`. For `Text("if"), Text("if1")`, both return `if`.
Canonical strings are returned without allocating substrings.

The cursor advances once on success, using the same precomputed `AdvanceBy` line/column
deltas as `TextLiteral.GenerateSource`. Newline metadata is computed during generation,
not by scanning the matched text during parsing. On failure, no text has been consumed,
and any skipped whitespace is restored. Capture, discarded results, custom whitespace,
and later sequence failure continue to compose with the existing generated contracts.

## Measurements

The tables in this section record the original keyword investigation. The additional
article-driven comparisons below use the current adaptive emitter and ordered text path.

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

Refreshed on 2026-10-02 on the same Apple M4 Pro/.NET 10.0.11 environment,
SDK 11.0.100-rc.1.26425.128, using normal runtime tiering and BenchmarkDotNet `ShortRun`
without command-line iteration or warmup overrides:

| Declarations | Input | Before | After | Time reduction | Allocated, both |
|---|---|---:|---:|---:|---:|
| 8 | Valid | 1.774 us | 1.595 us | 10.1% | 1.41 KB |
| 128 | Valid | 29.155 us | 26.597 us | 8.8% | 19.32 KB |
| 8 | Unknown type in final declaration | 1.582 us | 1.415 us | 10.6% | 1.34 KB |
| 128 | Unknown type in final declaration | 29.703 us | 26.223 us | 11.7% | 19.24 KB |
| 8 | Invalid expression in final declaration | 1.673 us | 1.492 us | 10.8% | 1.38 KB |
| 128 | Invalid expression in final declaration | 28.975 us | 26.399 us | 8.9% | 19.28 KB |

The 99.9% confidence intervals for valid files are 1.774 +/- 0.0471 us versus
1.595 +/- 0.0199 us (eight declarations), and 29.155 +/- 3.2016 us versus
26.597 +/- 1.9845 us (128 declarations). The longer-input intervals overlap:
the mean reductions are indicative, not a precise speedup guarantee.

The valid-file means show approximately **9-10% lower total parsing time**, not
the larger isolated-token speedup. Expression parsing and object construction account
for the unaffected work. Allocations are unchanged. These results do not extend to the
existing SQL sample: its case-insensitive keywords are ineligible for this optimization.

Reproduce this comparison with:

```bash
dotnet build -c Release
dotnet run --no-build --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- \
  --filter '*KeywordSourceBenchmarks*' --job short
```

## Article-driven measurements

Measured on Apple M4 Pro, macOS 15.8.1, .NET 10.0.11 Arm64, BenchmarkDotNet 0.15.8,
using SDK 11.0.100-rc.1.26413.103. Tiered compilation was disabled for both strategies
and complete entry points. Each benchmark used one launch, three warmups, and seven
250 ms measurement iterations. These are local controlled comparisons, not x64,
.NET 8, or whole-application throughput claims.

The initial strategy experiment used exact ordinal input and the same 64-element,
25%-hit delimited corpus as the earlier recognizer benchmarks. Means in ns/token,
with **0 B/token** allocated:

| Vocabulary | Unsplit packed tree | Length-partitioned span switch | FNV hash + verification |
|---|---:|---:|---:|
| 4 keywords | 2.06 | 2.11 | 3.53 |
| 27 headers | 2.57 | 2.90 | 6.56 |
| 77 keywords | 3.16 | 4.39 | 6.11 |
| 128 shared-prefix tokens | 2.22 | 3.19 | 7.29 |
| 200 multi-length tokens | 8.57 | 3.03 | 9.88 |

This supports retaining packed dispatch, rather than adopting span switches or general
hashing everywhere. The large recognizer did benefit from splitting its packed tree.
After applying the conservative helper policy, the confirmation run measured:

| 200-token recognizer | Before | After | Time reduction | Allocated, both |
|---|---:|---:|---:|---:|
| Ordinal | 8.448 ns | 3.177 ns | 62.4% | 0 B |
| Ignore ASCII case, experimental | 8.522 ns | 3.113 ns | 63.5% | 0 B |

The ordinal 99.9% confidence intervals are 8.448 +/- 0.197 ns versus
3.177 +/- 0.093 ns. The experimental folded result is not a claim about public
case-insensitive keyword parsing, which remains on its original path. Other benchmark
vocabularies emit identical unsplit code under the final policy.

`TextParserBenchmarks` compares the old first-character `OneOf` dispatch against the
new ordered packed-prefix path **in the same build**. Its `BaselineText` hides only
the concrete text parser type, forwarding the original seek information and exactly
the original emitted text body without adding a generated wrapper call.

Setup checks success and canonical result against the runtime parser for every input.
Hits have a trailing suffix; misses include first-character and late-character changes
and truncation. Misses that would legally match an earlier shorter text are excluded,
and the mixed corpus contains 25% hits. Generation and setup are outside measurement.
The public generated entry point, context allocation, recognition, and cursor advancement
are all inside it.

Final means in ns/call; both versions retain **192 B/call**, the existing context
allocation rather than a token-matching allocation:

| Vocabulary | Input | Original generated lookup | Packed-prefix lookup | Time reduction |
|---|---|---:|---:|---:|
| 27 headers | Hit | 24.72 | 20.83 | 15.7% |
| 27 headers | Miss | 24.02 | 18.90 | 21.3% |
| 27 headers | Mixed | 26.18 | 19.81 | 24.3% |
| 77 language literals | Hit | 25.25 | 21.34 | 15.5% |
| 77 language literals | Miss | 24.33 | 21.21 | 12.8% |
| 77 language literals | Mixed | 27.01 | 21.46 | 20.5% |
| 128 shared-prefix literals | Hit | 167.42 | 20.18 | 87.9% |
| 128 shared-prefix literals | Miss | 162.39 | 20.46 | 87.4% |
| 128 shared-prefix literals | Mixed | 196.08 | 21.17 | 89.2% |

The mixed-header 99.9% confidence intervals are 26.18 +/- 0.446 ns versus
19.81 +/- 0.306 ns; mixed language literals are 27.01 +/- 0.605 ns versus
21.46 +/- 0.374 ns. The shared-prefix case is deliberately adversarial, not an
estimate of a typical grammar's overall speedup.

Reproduce the final before/after comparisons and the switch/hash experiments with:

```bash
dotnet build -c Release
DOTNET_TieredCompilation=0 dotnet run --no-build \
  --project test/Parlot.Benchmarks/Parlot.Benchmarks.csproj -c Release -- \
  --filter '*TextParserBenchmarks*' \
    '*KeywordLookupBenchmarks.CharDecisionTree*' \
    '*KeywordLookupBenchmarks.CharLengthHelpers*' \
    '*KeywordLookupBenchmarks.CharSpanSwitch*' \
    '*KeywordLookupBenchmarks.HashDispatch*' \
  --warmupCount 3 --iterationCount 7 --iterationTime 250 --launchCount 1
```

For `IgnoreCase=true`, `CharSpanSwitch` uses the original comparison-chain fallback,
not an ordinal span switch. The switch strategy table above uses only the ordinal rows.

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

Text coverage additionally checks ordered overlapping prefixes, canonical string identity,
Unicode and surrogate code units, NUL, truncation, newline metadata, discarded results,
and the complete text-parser benchmark corpora. Large keyword consumers exercise both
many-candidate length buckets and many small buckets that exhaust the inline budget.
Standalone text grammars execute on .NET 8 and .NET 10 and compile for .NET Framework
4.7.2 and .NET Standard 2.0.
