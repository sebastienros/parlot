# Parlot Security Threat Model

**Status:** Current baseline  
**Version:** 1.1  
**Last updated:** August 17, 2026  
**Implementation reference:** [PR #324](https://github.com/sebastienros/parlot/pull/324)

## Purpose

This document identifies security-relevant assets, trust boundaries, threats, controls, and residual risks for
the Parlot runtime, Roslyn source generator, and NuGet release process. It complements the practical
[security guidance](security.md); consuming applications still require their own threat models.

## Executive summary

Parlot executes in two security-sensitive environments:

1. Runtime parsers execute inside consuming applications and may process hostile text.
2. Source-generated parsers are constructed inside the compiler host, where parser factories and selected
   generators execute with build-process privileges.

The principal runtime risk is denial of service through pathological input, recursive grammars, or unbounded
result construction. The principal build-time risk is execution of untrusted factories, analyzers, or
generators in an environment containing source, credentials, or network access. The release process can
publish executable packages used by downstream builds.

PR #324 implemented the repository-controlled priority mitigations: zero-progress repetition termination,
bounded and contained `IncludeFiles`, redacted path diagnostics, safe generated locations and identifiers,
build-time trust guidance, hardened GitHub Actions, release validation, and package provenance attestations.

## Scope

### In scope

- Runtime scanning, parser execution, recursion, repetition, cancellation, semantic callbacks, diagnostics,
  and attacker-controlled text.
- Compile-time parser-factory execution, `IncludeFiles`, `IncludeGenerators`, emitted C#, and diagnostics.
- NuGet package contents, dependencies, GitHub Actions, release tags, publication, and provenance.

### Out of scope

- Consumer vulnerabilities not caused by Parlot behavior or guidance.
- Authorization, interpretation, query execution, or other domain logic built on parsed results.
- Security defects in GitHub, NuGet, Roslyn, the .NET SDK, or operating systems.

## Trust boundaries

| ID | Boundary | Security concern |
|---|---|---|
| TB1 | Untrusted text to runtime process | CPU, memory, stack, result growth, diagnostic disclosure |
| TB2 | Consumer repository to compiler host | Build-time code execution and access to credentials or files |
| TB3 | `IncludeFiles` patterns to project filesystem | Traversal, symlink escape, excessive reads, path disclosure |
| TB4 | Generator dependencies to compiler host | Execution of malicious or compromised analyzer code |
| TB5 | Parser callbacks to host application | Arbitrary trusted code, ambient state, secrets, and side effects |
| TB6 | Repository and actions to CI runner | Workflow tampering, secret exposure, dependency compromise |
| TB7 | Release artifacts to NuGet | Package tampering, spoofing, or unauthorized publication |

## Security objectives

- **Availability:** Parsing terminates or honors cancellation within an application-defined budget.
- **State integrity:** Failed parsing restores cursor state.
- **Result integrity:** Runtime and generated parsers produce equivalent results.
- **Build confidentiality:** Generation does not expose unrelated files, paths, or credentials.
- **Build integrity:** Included files and emitted source correspond only to authorized project inputs.
- **Publisher integrity:** Only authorized workflows and maintainers publish official packages.
- **Traceability:** Releases can be tied to source revisions and build workflows.

## Threat register

| ID | Threat | Risk | Implemented controls | Residual action |
|---|---|---|---|---|
| T1 | Pathological input or grammar consumes excessive CPU. | High | Cooperative cancellation, recursion limits, seekable parsing, adversarial tests. | Consumers must set deadlines and isolate hostile workloads. |
| T2 | Recursive or zero-width cycles exhaust CPU or stack. | Medium | Parser-position loop detection, recursion limits, and zero-progress termination in runtime and generated repetition. | Custom parser and callback code can still fail to terminate. |
| T3 | Large input or results exhaust memory. | High | Span-based scanning limits copies; guidance requires input and result bounds. | Consumers must define safe input, AST, collection, and decoded-value limits. |
| T4 | Failure-path cursor corruption changes branch behavior. | Medium | Cursor-restoration contract and adversarial failure-path tests. | Custom parsers must preserve the contract. |
| T5 | A parser factory executes arbitrary code in the compiler host. | High | Explicit build-time execution and isolated-build guidance. | Build only trusted code or use disposable isolation. |
| T6 | `IncludeFiles` reads secrets or unrelated files. | Low | Project-root containment; absolute path, traversal, symlink, and non-C# rejection; file, size, and traversal limits; redacted `PARLOT016`-`PARLOT020` diagnostics. | Trusted source can intentionally include sensitive in-root C#; use narrow patterns. |
| T7 | `IncludeGenerators` loads compromised analyzer code. | High | Documentation treats generators and transitive dependencies as executable build dependencies. | Pin, review, and use trusted package sources. |
| T8 | Grammar-derived text corrupts generated C#. | Low | Safe line directives, collision-resistant identifiers, centralized escaping, and adversarial generation tests. | Custom `ISourceable` implementations must emit safe source. |
| T9 | Diagnostics disclose local paths or source. | Low | Include diagnostics identify entry numbers rather than sensitive paths; generated line handling is hardened. | Other compiler diagnostics may expose repository information. |
| T10 | A compromised workflow, tag, account, or key publishes a malicious package. | High | Least privilege, immutable action pins, release concurrency, strict SemVer tags, main-ancestry validation, both test suites, and provenance. | Protect tags, require release reviewers, and adopt NuGet Trusted Publishing. |
| T11 | A lookalike package is mistaken for an official release. | Low | Official NuGet identity, HTTPS, strong-name identity, and provenance attestations. | Consumers should verify package source and provenance. |
| T12 | Scanner boundary defects cause crashes or incorrect results. | Low | Multi-target builds, platform CI, and adversarial boundary tests. | Continue coverage for distinct downlevel implementations. |

## Implemented controls

### Runtime

- `ParseContext` supports cancellation and configurable recursion depth.
- Parser re-entry at the same position is detected.
- Repetition combinators stop when a successful child consumes no input.
- Tests cover cursor restoration, cancellation, scanner boundaries, and repetition termination.

### Source generation

- Parser factories and `IncludeGenerators` are documented as trusted executable build inputs.
- `IncludeFiles` is contained to the MSBuild project root.
- Absolute paths, root escape, symbolic links, non-C# targets, excessive traversal, excessive file count,
  oversized files, and excessive total content are rejected.
- Rejected include diagnostics do not echo sensitive paths.
- Generated line directives and identifiers are hardened against injection and collisions.

### CI and release

- Workflows use least-privilege permissions and immutable action SHAs.
- Release checkout does not persist credentials.
- Tags must use canonical SemVer and point to a commit contained in `origin/main`.
- Release builds run runtime and source-generator tests.
- NuGet packages receive GitHub build-provenance attestations before publication.

## Required consumer controls

Consumers should:

- reject input over an application-specific limit before creating a `Scanner`;
- use cancellation deadlines and `ParseContext.MaxRecursionDepth`;
- bound repeated elements, AST depth, decoded strings, and collection growth;
- treat semantic actions, custom parsers, and `ParseContext` hooks as arbitrary trusted code;
- redact input, source excerpts, tokens, and paths from user-visible errors;
- build untrusted repositories only in disposable workers without credentials or unnecessary network access;
- review and pin analyzers, generators, package sources, and transitive build dependencies; and
- isolate high-risk parsing with CPU, memory, time, filesystem, and network restrictions.

## External administrative actions

1. Protect release tags so only authorized maintainers or automation can create them.
2. Require reviewer approval through a protected GitHub release environment.
3. Configure NuGet Trusted Publishing and remove the long-lived `NUGET_API_KEY`.
4. Periodically review action pins, repository permissions, branch protections, and release administrators.

## Residual risk

Parlot cannot guarantee bounded execution for every consumer-defined grammar, callback, result type, and
input. Cancellation is cooperative. Custom parser and source-emission code can violate library contracts.
Compiler analyzers run with compiler-host privileges. Strong-name signing is not package provenance, and
API-key publishing remains a credential risk until Trusted Publishing is configured.

## Review cadence

Review this model at least annually and whenever Parlot adds a parser execution mechanism, filesystem or
network access, source-generation inclusion capability, unsafe scanner optimization, build-time executable
dependency, distinct target-framework implementation, package-content change, or release-workflow change.

| Area | Owner |
|---|---|
| Runtime parser and scanner risks | Parlot maintainers |
| Source-generator risks | Source-generator maintainers |
| Release and credential risks | Repository administrators |
| Input, result, semantic, and process limits | Consuming application owners |

