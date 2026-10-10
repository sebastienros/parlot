# Bulk cursor advancement for character runs

`Identifier`, `Pattern` and `Scanner.ReadWhile` count matching characters before advancing the cursor. The 1.x backport uses `AdvanceNoNewLines` for runs without line breaks and `Advance` otherwise; it does not add streaming state or a new cursor API. Pattern failures caused by minimum size leave the cursor untouched. Result text and line/column tracking are preserved.

Expression-tree compiled Identifier and Pattern parsers retain their existing implementation. CharRunBenchmarks measures the optimized interpreted parsers.
