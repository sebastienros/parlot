# Pool repeated short JSON strings per document

The pool is created lazily, capped at 256 entries, and bypassed for tokens longer than 32 characters. Unique strings pay hashing and dictionary costs. Returned ordinary string models do not retain the input through the pool.

This independent sample uses the existing string/array/object grammar subset. The existing JsonParser is unchanged. Benchmark both modes of the same new parser with Optimize=false/true, including document state creation, arrays and objects, 1/4/256 distinct values, and plain/escaped values. Escaped values already allocate during decoding; these cases expose when token caching or span retention cannot avoid that allocation. The parser graph is built once.

The PR depends only on shared benchmark infrastructure #345 and its separate correctness prerequisites #344 and #346. Each allocation alternative adds differently named sample, test and benchmark files so they can be reviewed independently.

Measurements use net11.0 only, .NET 11 RC1 on ARM64, one launch, three warmups, five measured iterations and 100 ms iteration time. The comparison table reports explicit time and allocation deltas against the unoptimized mode of this same sample.

## Decision and results

Draft / allocation-only tradeoff: the pool reduces allocations for repeated plain arrays by up to 52%, but is slower in every measured mean. Plain repeated arrays regress 13.2–24.6%; unique plain arrays regress 89.1% and grow from 23,072 to 51,632 allocated bytes. Escaped values already allocate during decoding, so pooling cannot avoid those allocations. Do not adopt this dictionary pool as a default throughput optimization.

See [all benchmark deltas](comparison.md), the CSV/Markdown reports, and [retained statistics](measurements.json). Source revision measured: `427227de24e312f28e9080ead945e4a657c8dab6`.

## Validation

Full Release solution build across supported TFMs; 871 runtime tests on .NET 10; 243 source-generator tests; 38 standalone tests on .NET 8/10. All pass. Benchmarks run only on net11.0.
