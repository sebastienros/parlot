# Sequential OneOf parsing

`OneOf` iterates its stored parser array directly when no character lookup map is available. The public `Parsers` property and ordered-choice behavior are unchanged. `OneOfSequentialBenchmarks` covers non-seekable alternatives and mixed whitespace handling. This backport preserves the 1.x lookup-group construction and does not require #378.
