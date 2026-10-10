# Cursor restoration on parser failure

`Eof`, `Switch` and `AnyCharBefore` restore the starting cursor when they consume text and then fail, allowing ordered alternatives to retry that text. `Switch` also restores it when its callback returns null. This applies to interpreted and expression-tree compiled parsers.

This backport uses the 1.x delegate-based `Switch`; main's fixed-index selector and compacting-stream cursor overflow changes are not applicable.
