# Reuse the last short JSON string within a document

Only matches to the last eligible short token hit; longer tokens bypass the cache. Arrays with repeated values benefit more than objects whose keys and values alternate. Cache state belongs to one parse and never mutates the shared parser graph.

This independent sample uses the existing string/array/object grammar subset. The existing JsonParser is unchanged. Benchmark both modes of the same new parser with Optimize=false/true, including document state creation, arrays and objects, 1/4/256 distinct values, and plain/escaped values. Escaped values already allocate during decoding; these cases expose when token caching or span retention cannot avoid that allocation. The parser graph is built once.

The PR depends only on shared benchmark infrastructure #345 and its separate correctness prerequisites #344 and #346. Each allocation alternative adds differently named sample, test and benchmark files so they can be reviewed independently.

Measurements use net11.0 only, .NET 11 RC1 on ARM64, one launch, three warmups, five measured iterations and 100 ms iteration time. The comparison table reports explicit time and allocation deltas against the unoptimized mode of this same sample.

## Decision and results

Draft: worth retaining only as an opt-in policy for known consecutive plain-token repetition. The repeated plain array improves 10.1% and drops allocations from 23,072 to 10,832 bytes (53%). Escaped values show no allocation saving because decoding already creates their strings; other distributions have mixed timings, including a 14.6% slower object case. Object rows do not show allocation savings, so their faster means must not be interpreted as successful string reuse.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `3d07a9d685f3e4f125cdc5e7431f5340f6b1b19b`.

## Validation

Full Release solution build across supported TFMs; 870 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
