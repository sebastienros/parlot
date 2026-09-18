# Windows x64 vectorization follow-up

Run `.github/workflows/windows-vector-benchmarks.yml` on one Windows 2025 x64 hosted runner. Its branch-specific push trigger permits the initial investigation without merging a workflow into main. The manual trigger is available once the workflow exists on the default branch.

All variants start at the same checkout: main `2961dca01eed79242d0cba36e8d67cf8976b7651` with the common benchmark harness. SDK 11.0.100-rc.1.26425.128 is pinned in each worktree. The only measured TFM is net11.0. BenchmarkDotNet 0.16.0-preview.1 recognizes that runtime; default shipping TFMs remain unchanged. JSON input generation has seed 42 in every variant.

Variants run serially on the same VM, with no parallel builds/tests during measurement:

| Variant | Production changes | Cases |
|---|---|---:|
| main | None | 64 |
| combined | PRs #347, #349 and #355 | 18 README Parlot cases |
| quoted-simd | Closed PR #348 only | 18 README + 12 quoted-string cases |
| whitespace | Closed PR #351 only | 18 README + 10 whitespace cases |

Main includes the union of candidate cases plus 24 scan-kernel cases comparing Scalar, SearchValues, VectorMask and NarrowedVectorMask. This rechecks the earlier portable-vector results on x64 as well as the closed PRs. No grammar/result-model experiments are included. Patches contain only each candidate's production changes and its correctness tests, never the old benchmark results. The combined patch is exactly the same as the local Mac comparison.

Each variant gets a Release multi-target build, runtime tests on net10.0, and standalone generated-parser tests on net10.0 before timing. Each benchmark uses the original screening settings: one launch, three warmups, five 100 ms measured iterations. These are shorter than the later Mac README-only comparison (two launches, ten 250 ms iterations); compare main/candidate deltas within each host and retain the error intervals. Close differences require longer confirmation before reopening a rejected candidate.

The workflow saves CPU/OS/SDK details, BenchmarkDotNet's runtime/SIMD information, full logs, raw JSON/CSV/Markdown reports, a source/filter manifest, and comparison tables as an artifact. It also writes the comparison to the Actions job summary. CPU model and available AVX features are detected from the actual runner, not assumed from the OS label. Hosted VM noise and a single CPU model limit generalization.

Source PR heads used to form the patches:

- #347: `89c21bb` (`perf/cursor-vector-advance`)
- #349: `51bf5ba` (`perf/string-decode-bulk-copy`)
- #355: `2ae5f5b` (`perf/cursor-local-state`)
- #348: `3c0dd86` (`perf/quoted-string-simd`)
- #351: `099a0d2` (`perf/whitespace-vector-prefix`)

Reports can be regenerated from downloaded artifacts:

```sh
python scripts/windows-benchmarks/run.py --report-only --output <artifact-directory>
```

The public Windows SDK is newer than the local Mac SDK (`11.0.100-rc.1.26413.103`, unavailable at the public Windows download URL). Thus cross-host differences also include a runtime/compiler build change; conclusions use within-runner baseline/candidate ratios rather than raw Mac/Windows times.
