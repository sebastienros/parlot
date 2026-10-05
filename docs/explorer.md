# Parser Explorer

Parlot Explorer is a local .NET tool with an offline JavaScript UI built with Preact and Monaco.
It discovers diagnostic source-generated parsers in a local assembly, runs them in disposable worker
processes, and replays their execution. No assembly or input is uploaded to a public service.

## Enable diagnostics

Opt in on the **factory in your `.parlot.cs` file**, then rebuild:

```csharp
[GenerateParser(nameof(TryParse), Diagnostics = true)]
public static Parser<char> Build() =>
    Literals.Char('a').Named("a").SkipAnd(Literals.Char('b').Named("b")).Named("ab")
    .Or(Literals.Char('a').Named("a").SkipAnd(Literals.Char('c').Named("c")).Named("ac"));
```

The corresponding ordinary `.cs` file still declares the same entry point:

```csharp
private static partial bool TryParse(string text, out char value);
```

Private methods are supported. The tool discovers a versioned, assembly-local diagnostic marker;
it does not invoke arbitrary methods that happen to look like parsers. No reference to the tool is
needed by the consumer. Normal generated parsers contain no diagnostic calls or support source
unless a factory opts in. Use conditional compilation around the attribute if only debug builds
should include it.

String and `TextReader` entry points are supported. Configuration parameters appear in the UI;
provide an object with parameter names as JSON properties, for example `{"factor": 3}`. Configuration
types must be deserializable by System.Text.Json. The extra cancellation-token parameter receives a
default token; the worker deadline also stops parsers without cooperative cancellation.

## Run from this repository

Building requires the repository's .NET SDK and Node.js 22.12+ (or 24+). Node is only a build dependency.
The installed tool requires the .NET 10 and ASP.NET Core 10 runtimes.

```sh
# Build the runtime first so the source generator uses the current analyzer dependency.
dotnet build
# Build the UI/tool and the sample assembly with its model and satellite dependency.
dotnet build tools/Parlot.Explorer.slnx

dotnet run --no-build --project tools/Parlot.Explorer/Parlot.Explorer.csproj -- \
  tools/Parlot.Explorer.Sample/bin/Debug/net10.0/Parlot.Explorer.Sample.dll
```

The tool opens the browser on macOS/Linux and on Windows without the optional native shell.
`--no-browser` prints the local session URL without opening it. The address uses an available loopback
port, and the URL fragment carries a per-launch session token. Keep the terminal running; Ctrl+C stops
the server. The UI can browse local directories or accept an absolute assembly path.

Try these sample entry points:

| Parser | Input | What it demonstrates |
| --- | --- | --- |
| `Choice` | `ac` | `ab` fails after consuming `a`, resets to zero, then `ac` succeeds |
| `Choice` | `ax` | All alternatives fail and restore the cursor |
| `Assignment` | `answer = 42;` | Whitespace, a structured result in a separate assembly, and a French satellite resource |
| `Optional` | `a` | A failed optional child followed by a zero-width success |
| `Recursive` | `((a))` | Nested calls to the same rule |
| `Throws` | `a` | A callback exception and frame unwinding |
| `Json` | `{"rows":[{"id":1,"values":[true,null,1.25e-5]}]}` | Recursive objects/arrays, alternatives, named rules, and a structured result |

## Explore and replay

- Edit the source in Monaco. **Live** debounces edits and captures a new parse; turn it off to run manually.
- **Watch builds** polls the assembly output directory every two seconds. A changed build rediscovers
  entry points and captures again when Live is enabled. Selection uses the method signature, not a
  metadata token that might move between builds. Failed loads/captures preserve the previous result.
- Use first/previous/play/next/last or the event slider to replay. Playback never re-executes callbacks.
- **Execution** draws nested calls against event order, not wall-clock time. Collapse calls to simplify
  the tree, filter names, or select a completed row to jump to its return. Active rows and bars jump to entry.
- **Input** draws observed input reach. A failed call may reach offset 1 and return at offset 0.
  Successful zero-width calls remain visible. **Callers** follows the current call outward to the root.
- The inspector shows entry/return positions, observed reach, explicit cursor resets, the buffer window,
  and `HitEnd`. Offsets are UTF-16 code-unit offsets, matching .NET strings and Monaco.
- Results can be inspected as a tree or JSON. **Export JSON** saves the capture, original input, buffers,
  and result snapshot. Exported files can contain sensitive input; export is an explicit local action.

Tracing follows the **physical generated call graph**. Named helpers have readable labels; unnamed
helpers use shortened generated names. Optimizations can combine alternatives into a single recognizer
or skip impossible branches entirely. The tool does not synthesize calls for those branches. Observed
reach is the maximum cursor offset seen at instrumented boundaries or immediately before an explicit
reset; it is **not** an exact character mismatch position. Streaming retries performed entirely inside
shared runtime helpers are not individually traced.

This first version diagnoses generated parsers. It does not add tracing to runtime `Parser<T>` graphs,
provide source-level breakpoints, or infer an expected-token error message. It provides the execution
evidence needed to investigate those errors, including the backtracking information discussed in
[issue 173](https://github.com/sebastienros/parlot/issues/173) and
[issue 274](https://github.com/sebastienros/parlot/issues/274).

The trace statistics show **Parse** duration in milliseconds for the recorded invocation, including
diagnostic instrumentation and JIT compilation. It excludes worker startup and result inspection;
the diagnostic capture duration shown with the result also includes result inspection. Filtering and
replaying leave these timings unchanged. This is not a benchmark of an uninstrumented parser.

## Assembly loading and limits

Each discovery/capture copies the assembly's output directory to a temporary directory, preserving
subdirectories, then starts a fresh worker. AssemblyDependencyResolver handles dependencies and native
libraries, with an adjacent-file fallback for managed assemblies and culture subdirectories. The original
build output is not held open by a parser worker. The snapshot is rejected if file lengths/timestamps
change while it is copied. A subsequent capture sees the new binary even when its assembly identity is
unchanged. The displayed build identifier is the module MVID.

Select a dedicated build output directory containing the parser's dependencies. The worker runs on .NET
10; assemblies and native dependencies must be compatible with that runtime and the current OS/CPU.
The tool does not emulate .NET Framework, install missing runtimes, or resolve application-specific
plugin directories outside the output snapshot. Symbolic links in the output tree are skipped.

Limits in this first version:

- 128,000 UTF-16 units of input; 20,000 trace events; 2,000,000 units of retained buffer text.
- An output snapshot of at most 2,000 files / 512 MB.
- A ten-second worker deadline, covering discovery/invocation/result inspection after copying.
- Result snapshots stop at depth 12, 2,000 visited values, 200 collection items, 100 fields/properties,
  and 4,096 characters per string. Cycles and throwing getters are marked. Property getters and collection
  enumerators execute in the worker, so the deadline also covers a stuck result inspector.
- The call tree virtualizes rows: every retained call remains reachable by scrolling. Stepping follows the current call.
  Filtering searches descendants even when their ancestors are collapsed.
- **Exclude patterns** hides noisy rule rows in Execution, Input, and Callers. Separate patterns with commas:
  `SkipWS*, Then_*, Sequence_*`. Plain text matches part of a name; `*` matches any text and `?`
  matches one character in a whole-name pattern. Matches ignore case; other punctuation is literal.
  Excluding a parent promotes its children in the displayed tree, including when that parent was collapsed.
  Filters only affect displayed rows; replay events, capture limits, and parser results are unchanged.
  **Clear filters** restores the unfiltered view.
- Capture limits retain a contiguous prefix. Parsing continues after recording stops, so the result can
  succeed while open trace frames are marked **incomplete**. Late failures beyond the cap are not recorded.

A process boundary provides cleanup and a hard timeout, **not a security sandbox**. Load only trusted
assemblies: parser callbacks, configuration constructors, and result getters have your local user
permissions and may have side effects. Live mode reruns them after edits. The host binds to loopback,
requires a random session token for filesystem/execution APIs, rejects foreign Host/Origin headers,
and serves all UI assets locally.

## Package and Windows shell

```sh
dotnet pack tools/Parlot.Explorer/Parlot.Explorer.csproj -c Release -o artifacts/explorer
dotnet tool install Parlot.Explorer --tool-path artifacts/explorer-tool \
  --source artifacts/explorer --prerelease
artifacts/explorer-tool/parlot-explorer /absolute/path/to/Your.Parsers.dll
```

The optional Windows x64 WinForms/WebView2 shell can be included in the same cross-platform tool package:

```sh
dotnet pack tools/Parlot.Explorer/Parlot.Explorer.csproj -c Release \
  -p:IncludeWindowsShell=true -o artifacts/explorer
```

On Windows, the host launches the bundled shell when present; elsewhere it opens the default browser.
The shell requires the .NET 10 Windows Desktop runtime and the WebView2 Evergreen runtime. If WebView2
initialization fails it opens the same local URL in the browser. The server lifetime remains controlled
by the terminal. This shell has been cross-compiled on macOS; interactive Windows validation is still
required before distribution. The browser path also supports Windows ARM64 without the x64 shell.

The UI bundles Monaco workers with Vite; it uses no CDN, Razor, Blazor, or browser-hosted .NET runtime.
Use `-p:SkipClientBuild=true` only when a current `wwwroot` bundle is already present. The separate tools
solution keeps Node and desktop tooling out of the library's ordinary build.

## Validation

```sh
npm test --prefix tools/Parlot.Explorer/Client
python3 tools/Parlot.Explorer/verify.py
```

`verify.py` requires Debug builds of the tool, sample, and source generator. It starts a temporary server
and tests discovery of private methods, backtracking, optional/recursive/throwing parsers, external
model and French satellite loading, authentication, recompilation with the same assembly identity,
a blocked worker's hard deadline, and host recovery. It leaves the repository's sample sources unchanged.
Generator/standalone tests cover opt-in emission, balanced exception unwinding, zero-width matches,
packed recognizers with local functions, configuration, reader entry points, and event limits.

### Long-trace validation

```sh
python3 tools/Parlot.Explorer/stress.py
node tools/Parlot.Explorer/Client/stress.mjs artifacts/explorer-stress
```

The recursive JSON-style sample exercises objects, arrays, escaped strings, numbers, booleans, null,
whitespace, alternatives, and EOF. It is a diagnostic fixture, not a JSON conformance implementation.
The harness checks returned node counts against Python's JSON parser, balanced calls, valid truncated
prefixes, a late failure, 80 nested arrays (419 generated call levels), and the 128,000-unit input limit.
It saves inputs, captures, and measurements under the ignored `artifacts/explorer-stress` directory.

An October 2026 macOS run measured:

| Input | Events retained | Calls | Worker capture | Whole request | Recording |
| --- | ---: | ---: | ---: | ---: | --- |
| 50 rows / 7,606 units | 17,090 | 8,545 | 11 ms | 159 ms | Complete |
| 700 rows / 109,131 units | 20,000 | 10,013 | 19 ms | 171 ms | Capped; parse succeeded |
| Late failure / 7,621 units | 17,112 | 8,552 | 8 ms | 150 ms | Complete |
| 80 nested arrays / 183 units | 1,864 | 932 | 7 ms | 103 ms | Complete |

The largest response was about 2.36 MB, with one shared input snapshot. Worker timing includes result
inspection; whole-request timing also includes snapshotting, process startup, and serialization.
These single-run numbers are illustrative, not throughput benchmarks.

The replay index shares completed subtrees and uses range maxima to preserve input reach after resets.
On the 20,000-event capture, the Node replay/flatten benchmark's late-step p95 fell from about 9 ms to
under 1 ms; it excludes browser layout/paint. The tree mounts at most 32 rows at the current 520px viewport,
instead of hiding everything beyond call 600. Replay tests also exercise 10,000 nested synthetic calls,
backward seeks, collapsed-name searches, and incomplete frames. Input sizes above 128,000 and full
recordings beyond 20,000 events remain outside the current supported limits.
