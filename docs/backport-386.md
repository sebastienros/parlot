# AnyCharBefore delimiter lookup

`AnyCharBefore` only jumps to expected delimiter characters when the delimiter cannot start with another character. Alternatives containing a non-seekable parser are checked at every position.

Behavior change: `AnyCharBefore(Terms.Text("end"))` on `abc   end` now returns `abc`, excluding whitespace consumed by the delimiter, instead of `abc   `. Use a literal delimiter if the whitespace should remain part of the result. Interpreted and compiled parsers follow the same rule.
