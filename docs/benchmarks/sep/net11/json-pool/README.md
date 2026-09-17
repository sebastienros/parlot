# Pool repeated short JSON strings per document

The pool is created lazily, capped at 256 entries, and bypassed for tokens longer than 32 characters. Unique strings pay hashing and dictionary costs. Returned ordinary string models do not retain the input through the pool.

This independent sample uses the existing string/array/object grammar subset. The existing JsonParser is unchanged. Benchmark both modes of the same new parser with Optimize=false/true, including document state creation, arrays and objects, and 1/4/256 distinct values. The parser graph is built once.

The PR depends only on shared benchmark infrastructure #345 and its separate correctness prerequisites #344 and #346. Each allocation alternative adds differently named sample, test and benchmark files so they can be reviewed independently.

Measurements use net11.0 only, .NET 11 RC1 on ARM64, one launch, three warmups, five measured iterations and 100 ms iteration time. The comparison table reports explicit time and allocation deltas against the unoptimized mode of this same sample.
