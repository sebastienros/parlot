# Security guidance

Parlot parses data in the caller's process and source-generates parsers in the compiler process. Applications
and build pipelines must set resource and trust boundaries appropriate for the data and code they process.
See the [threat model](threat-model.md) for the system boundaries, threat register, implemented controls, and
residual risks behind this guidance.

## Source generation and build-time code execution

`[GenerateParser]` executes the annotated parser factory at build time to construct its parser graph. The
factory runs inside the compiler host with that process's filesystem, network, environment-variable, and
credential access. Treat parser factories and every method they call as trusted executable build code.

`[IncludeGenerators]` loads and executes selected analyzer assemblies before Parlot emits the temporary
compilation. Enabling it is equivalent to trusting those packages and their transitive dependencies as
executable build dependencies. Pin and review generator dependencies and do not use `IncludeGenerators` to
run code from an untrusted repository or package source.

Build an untrusted repository only in a disposable, isolated worker or container:

- do not mount developer home directories, SSH agents, package-publishing credentials, or cloud credential
  stores;
- provide read-only source and package caches where possible, disable unnecessary network access, and discard
  the worker after the build;
- do not expose unrelated CI secrets to pull-request builds or compiler processes; and
- review project files, targets, analyzers, generators, and parser factories before running restore or build.

Source-generator diagnostics can contain compiler messages and source file names. Do not publish unrestricted
build logs from sensitive repositories.

## IncludeFiles containment

`[IncludeFiles]` is for C# sources needed by the temporary parser-factory compilation. Paths are relative to
the source file containing the parser factory, but their normalized targets must remain within the MSBuild
project root. Parlot rejects:

- absolute paths and parent traversal that escapes the project root;
- symbolic links in any included path;
- patterns not ending in `.cs`;
- more than 256 files, files larger than 1 MiB, total included content larger than 8 MiB, or glob traversal
  beyond 10,000 entries.

Rejected entries produce `PARLOT016` through `PARLOT020`. Diagnostics identify the attribute entry number
instead of disclosing its original or resolved path. Prefer exact paths such as `Ast/Nodes.cs`; when a glob is
necessary, anchor it to the narrowest directory, such as `Ast/Generated/*.cs`, instead of `**/*.cs`.

## Processing untrusted input

Parlot does not impose an application-wide input or result limit. Consumers should enforce all of the
following before accepting hostile input:

- reject inputs over an application-specific byte or character limit before creating a `Scanner`;
- pass a `CancellationToken` with a deadline and set `ParseContext.MaxRecursionDepth` for recursive grammars;
- bound repeated elements, AST depth, decoded string lengths, and other result growth in the grammar or
  semantic actions;
- avoid returning raw input, file paths, tokens, or source excerpts in user-visible errors and logs; map
  diagnostics to a redacted application error at trust boundaries; and
- run high-risk parsing in a separate process with CPU, memory, time, filesystem, and network restrictions.

Repetition combinators stop when a successful child parser consumes no input, preventing empty-match loops.
Cancellation is cooperative and checked regularly at parser entry; it is not a replacement for process-level
resource limits in hostile multi-tenant scenarios.

Semantic actions (`Then`, predicates, custom parsers, and custom `ParseContext` hooks) execute arbitrary
application code. Use only trusted actions, avoid ambient secrets and side effects, and apply the same output
and cancellation limits to code called from them.
