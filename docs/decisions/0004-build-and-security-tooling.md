# 0004 — Build, corpus access, and security tooling

**Status:** accepted **Date:** 2026-09-03

## Context

[`0002`](0002-technology-stack.md) settled the stack and recorded that scaffolding was unblocked, naming `dotnet list package --vulnerable` and "Roslyn analyzers in the build plus SonarQube" as the concrete answer to `XE-081` — but adopting neither. `XE-081` requires both checks to run *in the build* and to gate merge, so they have to be chosen before the first code lands rather than after.

Three questions came with the first commit, and none of them belongs in a commit message.

## Decision

### The reference corpus is never committed; `XSDEDITOR_CORPUS` locates it

The UCI v2.5 files are a controlled interface standard, and this is a public Apache-2.0 repository. `XSDEDITOR_CORPUS` names the files instead. A developer points it at a local checkout; CI reads it from a repository variable.

**Format.** A list of entries separated by a **semicolon on every platform**. Each entry is either a local path or an `https:` URL, and the two may be mixed.

> **The first entry is the entry point. The rest are its `include`/`import` dependencies.**

That ordering is the whole contract, and it exists because the unit of work is the resolved closure (`XE-016`), not a file: the suites need to know which document to open before they can resolve anything from it. Entries after the first are made available for resolution and are not opened directly.

The separator is a semicolon rather than the platform path separator, which an earlier draft of this record specified. A colon cannot separate a list whose entries may be URLs, because `https:` contains one — and a rule that works for local paths and silently mangles remote ones is worse than either alone.

Where an entry is a URL it is fetched to a temporary directory first, and `schemaLocation` resolution then proceeds against local paths exactly as `XE-018` specifies. Network URLs are not resolvable by the editor in R1, and nothing about this fixture mechanism changes that.

**Fetched files are opaque bytes.** They are never decoded, re-encoded, or line-ending normalised in transit, because their exact bytes are what the round-trip suites assert against (`XE-067`, `XE-083`). The corpus is wholly CRLF, so a fetch path that helpfully normalised would turn every round-trip test into a false pass on one platform and a false failure on another. CI verifies each file against a recorded SHA-256, which catches exactly that.

**The corpus is pinned to a commit, not tracked.** Its pin and per-file checksums are recorded in [`../measurements/corpus-figures.md`](../measurements/corpus-figures.md). Tracking the upstream default branch would mean an edit to the standard arriving as an unexplained parser-test failure; pinned, a round-trip diff is always our regression. Moving the pin is a reviewed commit that updates those checksums in the same change. The URLs live only in the repository variable, so there is one operative copy to update.

### Licence checking stays manual; the CI step is an inventory and is named as one

`0001` restricts which licences may ship and Apache-2.0 §4d obliges us to carry each dependency's `NOTICE` into the installer. Neither is automated here. `dotnet list package` reports **no licence data at all**, so the CI step that runs it is an inventory, named "Package inventory (informational)" so that it cannot be mistaken for a gate. A step named for a compliance obligation that performs no check is worse than no step, because it reads green forever.

Enforcement is therefore by hand for now, against the licence annotated beside every entry in `Directory.Packages.props`, which is a short list and is reviewed when it changes. Generating `THIRD-PARTY-NOTICES` and failing on a licence outside the allowlist needs a tool this project has not chosen, and it is deferred to the phase that produces an installer — the point at which the §4d obligation actually attaches.

### Static analysis is Roslyn analyzers in the build, with SonarQube deferred

`TreatWarningsAsErrors`, `EnableNETAnalyzers` and `AnalysisLevel=latest-recommended` are set in `Directory.Build.props`, so analyser findings fail the build on every machine and not only in CI. That satisfies "static analysis over our own code, in the build, gating merge" from the first commit onward, with no hosted service and no third-party dependency.

**SonarQube is deferred, not rejected.** It earns its place on a body of code that exists; adopting a hosted analysis service against five nearly empty projects would buy nothing and would put a build-time network dependency in place before there is anything to analyse. It is revisited once Phase 1 has landed, and this record is amended then.

`XE-081`'s split remains intact either way: composition scanning over dependencies, and static analysis over our own code, both gating merge, with Medium and Low triaged rather than gating.

### Dependencies are centrally versioned and lock-filed

`Directory.Packages.props` carries every version, annotated with its licence, because Apache-2.0 §4d obliges us to carry each dependency's `NOTICE` into the installer and the same inventory feeds the SBOM and the composition scan. `RestorePackagesWithLockFile` makes restore reproducible; CI moves to `--locked-mode` once the first lock files are committed.

**The vulnerability gate is `NuGetAudit` in the build, not a grep in CI.** `XE-081` asks for composition scanning "in the build, not as a periodic audit", and NuGet's own audit is exactly that: `NU1901`–`NU1904` are promoted to errors, so restore fails on any advisory on every machine rather than only where CI happens to inspect a report. `NU1905` — the audit source could not be consulted — is promoted with them, because a scan that did not run must fail rather than pass quietly. The CI step remains, to publish the report as an artifact, and asserts the clean result positively rather than inferring it from the absence of a severity word.

**Transitive pinning is enabled, and is how a vulnerable indirect dependency is answered.** `XE-081` forbids shipping a dependency with a known unfixed vulnerability, but most of the dependency graph is not ours to choose — it arrives through Avalonia. Central transitive pinning lets a `PackageVersion` entry alone lift such a package to a patched release without waiting for the parent to update, which is the "update" response `AGENTS.md` names first. Pins live in their own labelled group with the advisory and the reasoning recorded beside each, since a pin with no stated cause is one a later contributor deletes.

This is not hypothetical: the very first CI run found `Tmds.DBus.Protocol` 0.21.2 reaching the artifact through Avalonia's Linux backend with a High-severity advisory, and it is pinned to 0.21.3. Worth recording that the gate paid for itself before there was any product code.

## Consequences

- A contributor with no corpus access can still build, test, and land a change. They cannot verify the measured requirements, and CI tells them so rather than passing quietly.
- The `publish` CI job builds self-contained, single-file and trimmed on all three platforms, so a dependency that reflects by name surfaces in CI rather than in an installer — the failure mode `0002` singled out as the worst place to find it. Trim warnings are **reported rather than gating** for now: Avalonia's own trim-annotation coverage has not been measured, and a gate set before that measurement would fail on a third party's warnings — `Avalonia.DesignerSupport` raises `IL2104` on the very first publish, which is exactly that case. Holding this position takes an explicit `WarningsNotAsErrors` entry in the application project, because the repository-wide `TreatWarningsAsErrors` reaches the trimmer too; without it the build would have gated on trim warnings while this record said it did not. Tightening it back to an error is a Phase 2 item, once the real warning set is known.
- SonarQube's absence is a recorded, dated decision with a revisit point, which is what `AGENTS.md` asks for in place of an implicit exception.
