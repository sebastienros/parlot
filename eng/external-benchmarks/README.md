# External benchmarks

Measures how a change to Parlot affects libraries that use it, by building them from source against any Parlot
checkout and comparing the results with another checkout.

| Library | Parlot path exercised | Benchmarks |
|---|---|---|
| [Fluid](https://github.com/sebastienros/fluid) | Runtime (Fluent) grammar, custom `ParseContext`, loop detection on | `FluidParse`: a 216 B and a 1.6 KB template from Fluid's benchmarks, the latter repeated 20 times (32 KB), and ten short templates |
| [NCalc](https://github.com/ncalc/ncalc) | `Parlot.SourceGenerator` (shipping parser) and the same grammar compiled against the runtime, as NCalc's own benchmarks do | `NCalcParse.Generated` and `NCalcParse.Fluent` for four expressions |

## Usage

```bash
# Clone the libraries at pinned commits and reference $(ParlotRoot) instead of the Parlot packages
eng/external-benchmarks/setup.sh

# Build against each checkout, check the outputs match the first one, run the benchmarks and compare them
eng/external-benchmarks/run.sh main=/path/to/parlot-main lazy=/path/to/parlot-lazy

# Parlot's own benchmarks (test/Parlot.Benchmarks), same rounds and comparison
eng/external-benchmarks/run-internal.sh main=/path/to/parlot-main lazy=/path/to/parlot-lazy
```

Checkouts are typically git worktrees: `git worktree add ../parlot-lazy perf/lazy-line-column`.

- The variants differ only by the Parlot assembly (and, for NCalc's generated parser, the generator and its embedded
  runtime), so the harness runs BenchmarkDotNet in-process from each variant's output folder.
- `run.sh --validate` output (`artifacts/validate-<variant>.txt`) records what the libraries produce: Fluid renders
  and error messages with their line and column, NCalc expression trees and errors for both parsers. Every variant
  is compared with the first one before the benchmarks run.
- The validation output also counts the parsers that run again at an offset where they already ran during the same
  parse, which bounds what memoization could save.
- Each variant runs `ROUNDS` times (2 by default), in reverse order every other round. `compare.py` reports the mean
  of the per-round ratios of the medians to the baseline, so a drift of the machine between rounds cancels out,
  with the range of the ratios as a noise indicator.
- `BENCH_WARMUP` and `BENCH_ITERATIONS` override the warmup and measured iteration counts (6 and 20).
- A variant can use patched clones of the libraries, to measure a change that needs their cooperation:

  ```bash
  EXTERNAL_REPOS=$PWD/eng/external-benchmarks/.repos-offset \
  APPLY_PATCHES=eng/external-benchmarks/patches/fluid-string-literal-offset.patch eng/external-benchmarks/setup.sh
  eng/external-benchmarks/run.sh main=../parlot lazy=../parlot-lazy lazy-offset=../parlot-lazy,eng/external-benchmarks/.repos-offset
  ```

  `patches/fluid-string-literal-offset.patch` makes Fluid's string literal parser save the cursor's offset rather
  than its `Position`, which costs a line count once line and column are computed lazily.
