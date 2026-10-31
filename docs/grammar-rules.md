# Grammar rules

Parlot looks for patterns that make a grammar slower than it could be, or that keep it from parsing what it seems to.
Each pattern is a rule with an identifier:

| Rule | Category | Pattern |
|---|---|---|
| [PARLOT101](#parlot101) | Performance | A choice mixes alternatives that skip white space and alternatives that don't |
| [PARLOT102](#parlot102) | Performance | A choice alternative is tried whatever the next char is |
| [PARLOT103](#parlot103) | Performance | Choice alternatives start with the same parser |
| [PARLOT104](#parlot104) | Performance | A `Deferred` parser keeps its loop detection |
| [PARLOT111](#parlot111) | Correctness | Left recursion |
| [PARLOT112](#parlot112) | Correctness | Choice alternatives after one that never fails |
| [PARLOT113](#parlot113) | Correctness | A choice alternative shadowed by an earlier one |
| [PARLOT201](#parlot201) | Performance, from a trace | A rule parsed again at the same position |
| [PARLOT202](#parlot202) | Performance, from a trace | Input read again after backtracking |

The rules up to PARLOT113 read the grammar. PARLOT201 and PARLOT202 read what one parse did, in
[Parser Explorer](explorer.md).

## Where the rules are reported

**The source generator** analyzes each grammar it generates, and reports what it finds as `Info` diagnostics on the
factory, with a link to this page. They never fail a build, `TreatWarningsAsErrors` included. Use the usual compiler
options to change that, for instance in the project file:

```xml
<PropertyGroup>
  <!-- Fail the build on the correctness rules -->
  <WarningsAsErrors>$(WarningsAsErrors);PARLOT111;PARLOT112;PARLOT113</WarningsAsErrors>
  <!-- Never report this rule -->
  <NoWarn>$(NoWarn);PARLOT103</NoWarn>
</PropertyGroup>
```

The factories run during builds, not during the design-time passes of an editor, so the diagnostics appear after a build.

**Parser Explorer** shows them in its Suggestions panel, for a factory that opts into diagnostics with
`[GenerateParser(..., Diagnostics = true)]`. The panel also shows PARLOT201 and PARLOT202 for the input being parsed,
with a button that moves the replay to the parser concerned.

**At runtime**, `GrammarAnalyzer.Analyze` returns what it finds in a grammar that isn't generated. A test is a good
place for it, since the analysis takes much longer than a parse:

```csharp
using Parlot.Analysis;

[Fact]
public void GrammarParsesWhatItSeemsTo()
{
    var findings = GrammarAnalyzer.Analyze(MyGrammar.Expression);

    Assert.DoesNotContain(findings, static finding => finding.Rule.Category == GrammarRuleCategory.Correctness);
}
```

Each `GrammarFinding` has the `Rule` it found (`Id`, `Title`, `Category`, `HelpLink`), the `Parser` where it found it,
like the choice whose alternatives are concerned, and a `Message`. A message names the parsers with their `Named()`
name, and the others with the call that built them, like `Terms.Text("<")`, which makes named grammars easier to fix.
`GrammarRule.All` lists the rules.

### How the rules are chosen

- A correctness rule only reports what it can prove from Parlot's parser types. When a parser of another type, or a
  callback, could change the conclusion, it reports nothing.
- A performance rule reports a cost that `GrammarRulesBenchmarks` measures. The numbers below are from a single
  `ShortRunJob` on an Apple M4 Pro with .NET 10.0.11; compare them with each other rather than with other machines.
- When a grammar can be made faster without changing what it parses, Parlot does it when the grammar is built, and no
  rule reports it. The rules are about the changes only the author of a grammar can make.

## PARLOT101

**A choice mixes alternatives that skip white space and alternatives that don't.**

A choice (`Or`, `OneOf`) builds a lookup table from the chars each alternative can start with, and only tries the
alternatives that can start with the next char. When all its alternatives skip white space, it skips it once, before
reading the next char. When only some of them do, the next char isn't the same for all of them, and the choice has no
lookup table: it tries every alternative, one after the other, whatever the next char is. A parser that doesn't tell
whether it skips white space counts as one that doesn't.

```csharp
// Reported: Literals.Text("b") doesn't skip white space
var keyword = Terms.Text("a").Or(Literals.Text("b")).Or(Terms.Text("c"));

// Every alternative skips white space
var keyword = Terms.Text("a").Or(Terms.Text("b")).Or(Terms.Text("c"));

// Or none of them, and the choice is wrapped instead
var keyword = SkipWhiteSpace(Literals.Text("a").Or(Literals.Text("b")).Or(Literals.Text("c")));
```

An alternative that can't tell which chars it starts with, like `ZeroOrOne(sign).SkipAnd(number)`, can declare them,
and that it skips white space, with `Lookup(skipWhiteSpace: true, ...)`.

Most parsers copy the hints of the parser they start with when they are built. A parser that starts with a `Deferred`
before its `Parser` is set keeps the hints of a `Deferred` that has none, and the message says so. Build it after
setting the parser when the grammar allows it, or wrap it in `Lookup`.

| Benchmark | Mean |
|---|---:|
| `MixedWhiteSpace`, 8 alternatives, the last one matches | 120.8 ns |
| `SameWhiteSpace` | 19.6 ns |

## PARLOT102

**A choice alternative is tried whatever the next char is.**

An alternative that doesn't tell which chars it can start with is added to every group of the lookup table, and tried
for the chars that no other alternative starts with. The choice then tells the choices that contain it that it can
start with any char. The message names the parser that keeps the alternative from telling:

- `Pattern` tests chars with a predicate. When the predicate accepts a set of chars, `AnyOf`, `NoneOf` and `Identifier`
  tell them, and are vectorized on .NET 8 and later.
- `Scan` without start chars: pass the chars a token can start with as its second argument.
- An alternative that starts with a parser that can succeed without reading anything, like `ZeroOrOne` or `Optional`.
- A parser type that doesn't implement `ISeekable`.

Any of them can be wrapped in `Lookup` with the chars it can start with. Alternatives that can succeed without reading
anything are not reported: they are a fallback by design, or [PARLOT112](#parlot112) reports them.

```csharp
// Reported: Terms.Pattern(...) is tried for every char
var token = Terms.Pattern(static c => c is >= 'a' and <= 'z').Or(Terms.Text("(").Then(static x => new TextSpan(x)));

// The choice only tries the alternative for a letter
var token = Terms.AnyOf("abcdefghijklmnopqrstuvwxyz").Or(Terms.Text("(").Then(static x => new TextSpan(x)));
```

| Benchmark | Mean |
|---|---:|
| `UnseekableAlternative`, `Pattern` first among 5 alternatives | 13.2 ns |
| `SeekableAlternative` | 11.6 ns |

The cost grows with the cost of the alternative failing, which a `Pattern` keeps low.

## PARLOT103

**Choice alternatives start with the same parser.**

When an alternative fails after a parser succeeded, the next alternative that starts with the same parser parses it
again, at the same position.

```csharp
var identifier = Terms.Identifier();

// Reported: the identifier is parsed again when no '+' follows it
var term = identifier.And(Terms.Char('+')).Or(identifier.And(Terms.Char('-')));

// The identifier is parsed once
var term = identifier.And(Terms.Char('+').Or(Terms.Char('-')));
```

For operators, `LeftAssociative`, `RightAssociative` and operator tables (`Operators()`) parse each operand once.

The rule compares parser instances: two calls to `Terms.Identifier()` build two parsers, which it doesn't take for the
same one. It doesn't report a shared fixed text either (`Char`, `Text`, `Keyword`), which costs as little to read
again as to look up.

| Benchmark | Mean |
|---|---:|
| `SharedLeadingParser`, the second alternative matches | 31.0 ns |
| `FactoredLeadingParser` | 16.0 ns |

## PARLOT104

**A `Deferred` parser keeps its loop detection.**

A `Deferred` that can be invoked again at the position where it is already active looks for itself in the active
parsers each time it is invoked, see [Left recursion](writing.md#left-recursion). The first parse of a grammar proves
which ones can't, and these skip the check. The proof can't read a parser type that isn't Parlot's: when a `Deferred`
can invoke one before reading anything, it keeps the check.

```csharp
// Reported: Custom<T> holds a parser that Parlot can't analyze
expression.Parser = new Custom<long>(Between(Terms.Char('('), expression, Terms.Char(')')).Or(Terms.Integer()));
```

Invoke the custom parser after the grammar has read something, or, when the grammar has no left recursion, disable the
loop detection with `disableLoopDetection: true` on the `ParseContext`. A generated grammar only contains Parlot's
parsers, so the source generator never reports this rule.

| Benchmark | Mean |
|---|---:|
| `LoopDetection`, 8 nested parentheses | 132.1 ns |
| `LoopDetectionElided` | 87.6 ns |

## PARLOT111

**Left recursion.**

A `Deferred` parser that can invoke itself before reading anything fails where it is invoked again, instead of
recursing forever, so the alternatives that start with it never match:

```csharp
// Reported: 'expression' -> 'expression'. "1+2" parses as 1 and leaves "+2"
expression.Parser = expression.AndSkip(Terms.Char('+')).And(Terms.Integer()).Then(static x => x.Item1 + x.Item2)
    .Or(Terms.Integer());

// A loop instead of the recursion
expression.Parser = Terms.Integer().LeftAssociative((Terms.Char('+'), static (a, b) => a + b));
```

The message lists the parsers of the cycle. Name the `Deferred` parsers with `Named()` to tell them apart.

## PARLOT112

**Choice alternatives after one that never fails.**

`Optional`, `ZeroOrOne`, `ZeroOrMany`, `Always` and `Else` always succeed, and so does a sequence of them. The
alternatives after one of them are never tried. `ElseError` reports an error instead of failing, with the same result.

```csharp
// Reported: Terms.Text("b") is never tried, "b" parses as "none"
var value = Terms.Text("a").Else("none").Or(Terms.Text("b"));

// The fallback comes last
var value = Terms.Text("a").Or(Terms.Text("b")).Else("none");
```

## PARLOT113

**A choice alternative shadowed by an earlier one.**

A choice returns the first alternative that matches. An alternative never matches when an earlier one matches
whenever it would:

- the earlier alternative is a text, or a char, that starts the texts of the later one, like `"<"` before `"<="`;
- the later alternative starts with the parser that is the earlier alternative, like `identifier` before
  `identifier.And(arguments)`.

```csharp
// Reported: "<=" never matches, "<=" parses as "<"
var comparison = Terms.Text("<").Or(Terms.Text("<="));

// The longer text first
var comparison = Terms.Text("<=").Or(Terms.Text("<"));
```

The rule also applies to the operators of `LeftAssociative`, `RightAssociative`, `Unary` and operator tables, which
are tried in order: with `"<"` first, `a <= b` parses `a`, and the operator is rolled back when no operand follows `<`.
A `Keyword` isn't shadowed by a shorter one, since it fails when a letter follows it: `Keyword("in")` doesn't match
`"int"`.

## PARLOT201

**A rule parsed again at the same position.** *(Parser Explorer, for the input being parsed)*

The rules that matched a text that an earlier call of the same rule had already matched, at the same position. It is
what [PARLOT103](#parlot103) predicts, measured on an input: the same parser instance or the same name. A call inside
a call that is itself parsed again is counted with it, not twice.

## PARLOT202

**Input read again after backtracking.** *(Parser Explorer, for the input being parsed)*

The chars that parsers read and then gave back by moving the cursor back, in total and for each rule that moved it,
compared with the size of the input. Backtracking is how a choice tries its next alternative, so some of it is
expected. A total that is a multiple of the input points to the choices to look at, with
[PARLOT101](#parlot101), [PARLOT102](#parlot102) and [PARLOT103](#parlot103).

The trace shows the generated parser, which can combine alternatives or skip the ones that can't match, so both rules
count the calls that were made, not the ones a grammar describes.

## Limitations

- The analysis reads the parsers through their fields. Without dynamic code (native AOT), it may not reach every parser,
  and every parser type that isn't Parlot's keeps its loop detection.
- The callbacks of a grammar (`Then`, `When`, `If`, `Select`...) are not analyzed: a rule that would depend on what a
  callback returns reports nothing.
- `If` and `Select` choose a parser when they parse: the rules don't follow them to the parser they start with.
