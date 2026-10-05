# Parlot Explorer

Parlot Explorer is a local .NET tool for understanding how a source-generated Parlot parser processes
its input. Edit text in Monaco, replay the parse one event at a time, and follow named rules through
nested calls, failures, and backtracking. Inspect the buffer at each step and view the returned object
as a tree or JSON. Assemblies and input stay on your computer; the UI works offline.

![Parlot Explorer showing editable input, named parser calls, replay controls, the buffer, and a structured result](https://raw.githubusercontent.com/sebastienros/parlot/2cb9352656513c058f4579b755f537b400c70305/docs/images/parser-explorer.jpg)

## Install and run

The tool requires the .NET 10 and ASP.NET Core 10 runtimes. The .NET 10 SDK includes both.
For a published package, install it as a global tool:

```sh
dotnet tool install --global Parlot.Explorer --prerelease
parlot-explorer /path/to/Your.Parsers.dll
```

You can also start `parlot-explorer` without a path and browse for an assembly in the UI.
Keep its dependencies beside the assembly, including culture and native-runtime subdirectories.
Private diagnostic entry points are discovered too. Enable **Watch builds** to reload the assembly
when you rebuild it.

The tool opens a local browser UI on macOS and Linux. On Windows, packages built with the optional
WebView2 shell open a standalone window; otherwise the browser is used. The shell requires the .NET 10
Windows Desktop and WebView2 Evergreen runtimes. Use `--no-browser` to print the local URL without
opening a window. Keep the terminal running and press Ctrl+C to stop the server.

For a development build that has not been published, see the
[build and local-package instructions](https://github.com/sebastienros/parlot/blob/main/docs/explorer.md#run-from-this-repository).
Node.js is needed to build the UI from source, not to run an installed package.

## Enable diagnostics in your parser

Use a version of `Parlot.SourceGenerator` that supports the `Diagnostics` option. Add it to the factory
attribute in your build-only `.parlot.cs` file, then rebuild the assembly:

```csharp
using Parlot.Fluent;
using Parlot.SourceGenerator;
using static Parlot.Fluent.Parsers;

public static partial class NumberParser
{
    [GenerateParser(nameof(TryParse), Diagnostics = true)]
    private static Parser<long> Build() => Terms.Integer().Named("integer").Eof();
}
```

Declare the matching entry point in an ordinary `.cs` file:

```csharp
public static partial class NumberParser
{
    public static partial bool TryParse(string text, out long value);
}
```

The consuming application needs no reference to the explorer. Diagnostics are opt-in: generated
parsers without the option contain no tracing calls. Use conditional compilation if only debug builds
should include diagnostics. Both string and `TextReader` entry points are supported; application
configuration parameters can be supplied through the UI's configuration JSON field.

## Explore a parse

- **Edit and run:** select an entry point and change the input. Live mode captures a new parse after
  edits; disable it to run manually.
- **Replay:** use first, previous, play/pause, next, last, or the event slider. Replay uses recorded
  events and does not rerun parser callbacks.
- **Navigate:** Execution shows nested calls in event order, Input shows observed input reach, and
  Callers follows the selected call back toward the root. `Named()` gives generated helpers readable labels.
- **Inspect:** select a call to see its outcome, entry/return positions, observed reach, cursor resets,
  and buffer window. Inspect the returned object as a tree or JSON, or export the capture as JSON.
- **Hide noise:** use Include names or enter comma-separated Exclude patterns such as
  `SkipWS*, Then_*, Sequence_*`. Plain text matches part of a name; `*` matches any text and `?` matches
  one character in a whole-name pattern. Matching ignores case. Hidden parents' children remain visible,
  and filtering preserves all replay events. Clear filters restores the unfiltered view.

## Scope and limits

The explorer traces source-generated parsers, not runtime `Parser<T>` graphs. It displays the actual
generated calls: optimized or skipped grammar branches are not invented. Observed reach is the furthest
cursor offset seen at instrumented boundaries, not an exact mismatch location. Offsets use UTF-16 units.

Inputs are limited to 128,000 UTF-16 units. Recording retains up to 20,000 events and 2,000,000 units of
buffer text. Parsing continues after recording stops, and open calls are marked incomplete. The call
tree virtualizes rows so every retained call is reachable. Results are bounded snapshots, and workers
have a ten-second deadline. See the [full guide](https://github.com/sebastienros/parlot/blob/main/docs/explorer.md)
for dependency loading, snapshot limits, examples, and stress-test measurements.

Load only trusted assemblies: parser code runs locally with your user permissions. Worker processes
provide cleanup and a deadline, not a security sandbox.
