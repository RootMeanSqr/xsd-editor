# 0004 — Build, corpus access, and security tooling

**Status:** accepted
**Date:** 2026-09-03

## Context

[`0002`](0002-technology-stack.md) settled the stack and recorded that scaffolding was
unblocked, naming `dotnet list package --vulnerable` and "Roslyn analyzers in the build plus
SonarQube" as the concrete answer to `XE-081` — but adopting neither. `XE-081` requires both
checks to run *in the build* and to gate merge, so they have to be chosen before the first
code lands rather than after.

Three questions came with the first commit, and none of them belongs in a commit message.

## Decision

### The reference corpus is never committed; `XSDEDITOR_CORPUS` locates it

The UCI v2.5 files are a controlled interface standard, and this is a public Apache-2.0
repository. `XSDEDITOR_CORPUS` names either local paths or URLs to fetch at test time. A
developer points it at a local checkout; CI reads it from a repository variable.

**Corpus-dependent suites skip loudly when it is unset, and never silently.** They are the
acceptance tests for the measured requirements — `XE-069`'s character references, `XE-071`'s
zero-diff attribute ordering, and `XE-072`'s single-line defaults diff — so a green run that
quietly skipped them would misrepresent what was verified.

The repository carries small purpose-built fixtures instead, including for the four
requirements §6 records as having no corpus coverage at all.

**This does not weaken `XE-075`.** That requirement constrains the *application*, which makes
no outbound request of any kind. CI is developer-side tooling, explicitly unconstrained by
`XE-074`'s second bullet. Recorded here so that a CI job which downloads schemas is not later
mistaken for a violation of a rule it was never under.

### Static analysis is Roslyn analyzers in the build, with SonarQube deferred

`TreatWarningsAsErrors`, `EnableNETAnalyzers` and `AnalysisLevel=latest-recommended` are set
in `Directory.Build.props`, so analyser findings fail the build on every machine and not only
in CI. That satisfies "static analysis over our own code, in the build, gating merge" from the
first commit onward, with no hosted service and no third-party dependency.

**SonarQube is deferred, not rejected.** It earns its place on a body of code that exists;
adopting a hosted analysis service against five nearly empty projects would buy nothing and
would put a build-time network dependency in place before there is anything to analyse. It is
revisited once Phase 1 has landed, and this record is amended then.

`XE-081`'s split remains intact either way: composition scanning over dependencies, and static
analysis over our own code, both gating merge, with Medium and Low triaged rather than gating.

### Dependencies are centrally versioned and lock-filed

`Directory.Packages.props` carries every version, annotated with its licence, because
Apache-2.0 §4d obliges us to carry each dependency's `NOTICE` into the installer and the same
inventory feeds the SBOM and the composition scan. `RestorePackagesWithLockFile` makes restore
reproducible; CI moves to `--locked-mode` once the first lock files are committed.

## Consequences

- A contributor with no corpus access can still build, test, and land a change. They cannot
  verify the measured requirements, and CI tells them so rather than passing quietly.
- The `publish` CI job builds self-contained, single-file and trimmed on all three platforms,
  so a dependency that reflects by name surfaces in CI rather than in an installer — the failure
  mode `0002` singled out as the worst place to find it. Trim warnings are **reported rather than
  gating** for now: Avalonia's own trim-annotation coverage has not been measured, and a gate set
  before that measurement would fail on a third party's warnings. Tightening it to `-warnaserror`
  is a Phase 2 item, once the real warning set is known.
- SonarQube's absence is a recorded, dated decision with a revisit point, which is what
  `AGENTS.md` asks for in place of an implicit exception.
