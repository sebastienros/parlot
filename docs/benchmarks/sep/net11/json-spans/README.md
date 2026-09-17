# Retain JSON token spans instead of materializing strings

Returned JsonRetainedString and JsonRetainedObject models deliberately retain source buffers. This changes the sample result representation and retention behavior; it is an explicit opt-in sample, not a change to the existing JSON parser.

This independent sample uses the existing string/array/object grammar subset. The existing JsonParser is unchanged. Benchmark both modes of the same new parser with Optimize=false/true, including document state creation, arrays and objects, 1/4/256 distinct values, and plain/escaped values. Escaped values already allocate during decoding; these cases expose when token caching or span retention cannot avoid that allocation. The parser graph is built once.

The PR depends only on shared benchmark infrastructure #345 and its separate correctness prerequisites #344 and #346. Each allocation alternative adds differently named sample, test and benchmark files so they can be reviewed independently.

Measurements use net11.0 only, .NET 11 RC1 on ARM64, one launch, three warmups, five measured iterations and 100 ms iteration time. The comparison table reports explicit time and allocation deltas against the unoptimized mode of this same sample.

## Decision and results

Draft: useful as an explicit input-ownership policy for mostly plain values, not a drop-in result-model change. The repeated plain array improves 13.4% and drops allocation from 23,064 to 12,824 bytes (44%). Escaped arrays allocate 2,048 bytes more because decoding already creates strings and span-backed nodes are larger. Some other timings are noisy. Retained input lifetime is not measured by the allocation counter: unescaped spans keep their source buffer alive for as long as the result needs it.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `fc2027f498cb51f49f2fee6a74d2bfaba8946cf3`.

## Validation

Full Release solution build across supported TFMs; 870 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
