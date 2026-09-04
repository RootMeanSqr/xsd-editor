# Setting up a development environment

Everything here is developer-side tooling, which `XE-074` leaves unconstrained. The constraint it does impose — that an *end user* fetches no runtime, SDK or package manager — is satisfied by the publish settings in `src/XsdEditor.App/XsdEditor.App.csproj` and verified by CI.

This file owns **installation, environment, and how to build**. The corpus rationale is in [`decisions/0004`](decisions/0004-build-and-security-tooling.md) and the pull-request checklist in [`../CONTRIBUTING.md`](../CONTRIBUTING.md); neither is repeated here. [`../AGENTS.md`](../AGENTS.md) carries the same command list as a working reference — it is the one thing deliberately in both places, since an agent reads that file and not this one.

## Prerequisites

| | |
| --- | --- |
| **.NET SDK** | 10.0.100 or a later 10.0 feature band. `global.json` pins it, so a mismatched SDK fails fast rather than building differently. |
| **Git** | Any recent version. |
| **An editor** | Visual Studio 2026, JetBrains Rider, or VS Code with the C# Dev Kit. All three read `.editorconfig`, which is the whole of the house style. |

Install the SDK from <https://dotnet.microsoft.com/download/dotnet/10.0>, or with your platform's package manager, then check it resolves:

```bash
dotnet --version   # expect 10.0.1xx
```

Nothing else is needed: no database, no service, no container.

## Debug build

The ordinary loop, and what CI runs on all three platforms before it publishes anything. Debug is the default configuration, so none of these needs a flag:

```bash
dotnet build XsdEditor.slnx                       # warnings are errors
dotnet test  XsdEditor.slnx
dotnet run --project src/XsdEditor.App            # the editor
dotnet run --project src/XsdEditor.Cli -- --help  # the headless harness
dotnet format XsdEditor.slnx                      # CI runs --verify-no-changes
```

It builds incrementally, keeps full debugging information, and runs against the SDK's shared runtime, so a rebuild is seconds rather than a relink of the whole framework. Nothing in `XSDEDITOR_CORPUS`, and no runtime identifier, is needed to build or to run the unit suite.

The publish properties in `src/XsdEditor.App/XsdEditor.App.csproj` — self-contained, single-file, trimmed — apply on **publish only** and do not affect this. That is exactly why the release build below is a separate step rather than a slower version of this one.

## The reference corpus

The UCI v2.5 files are a controlled interface standard and are **not** in this repository. `XSDEDITOR_CORPUS` names them: a **semicolon-separated** list, each entry a local path or an `https:` URL, of which **the first is the entry point and the rest are its `include`/`import` dependencies**.

```bash
export XSDEDITOR_CORPUS='/path/UCI_Versioning_v2_5_0.xsd;/path/UCI_MessageDefinitions_v2_5_0.xsd;/path/UCI_SecurityMarkings_v2_5_0.xsd'
```

```powershell
$env:XSDEDITOR_CORPUS = 'C:\uci\UCI_Versioning_v2_5_0.xsd;C:\uci\UCI_MessageDefinitions_v2_5_0.xsd;C:\uci\UCI_SecurityMarkings_v2_5_0.xsd'
```

`UCI_Versioning` is the entry point despite being the smallest file: it is the only one nothing else references, so the closure rooted there reaches all three. CI reads the same variable, holding the pinned upstream URLs; which commit those point at, and what each file should check-sum to, is recorded in [`measurements/corpus-figures.md`](measurements/corpus-figures.md).

**The corpus is wholly CRLF.** If you keep a local copy in a Git repository, make sure it is not line-ending normalised on checkout, or every round-trip test will fail against it for a reason that has nothing to do with your change.

Verify a local copy the same way CI does — same script, so a green run here means a green run
there:

```bash
scripts/verify-corpus.sh
```

It fetches or copies each entry, checks the count, records SHA-256s, and fails if anything arrived
line-ending normalised.

Without the variable the build and the whole unit suite still pass — only the corpus round-trip and timing suites skip, loudly. Since those are the acceptance tests for the measured serialisation requirements, run them before proposing a change to the parser, model or serialiser.

## Before adding a dependency

`CONTRIBUTING.md` asks every candidate to clear a CVE check. That check is a script, and CI runs
the same one:

```bash
scripts/check-vulnerable-packages.sh
```

The gate itself is `NuGetAudit`, which already fails `dotnet restore` on an affected package; this
produces the report and positively confirms a clean graph.

## Release build

**A check, not the way you build.** It produces what an end user would receive — self-contained, single-file and trimmed (`XE-074`) — and it relinks the runtime for the named platform, so it is far slower than a Debug build and harder to debug. Nothing in day-to-day work needs it.

```bash
dotnet publish src/XsdEditor.App -c Release -r linux-x64   # or win-x64, osx-arm64, osx-x64
```

**Run it when the answer could differ from Debug**, which is a narrower set than it sounds:

- **Before proposing a change that adds a dependency.** Trimming runs only here, so a library that resolves types or members by name builds and runs perfectly in Debug and fails in the installed artifact — the failure [`decisions/0002`](decisions/0002-technology-stack.md) warns about, and the reason `AGENTS.md` asks every candidate for a trim-safety check.
- **When verifying what ships**: the `XE-075` no-egress audit and the packaging claims are properties of this output, not of `bin/Debug`.

Trim warnings are currently **reported, not gating** — see [`decisions/0004`](decisions/0004-build-and-security-tooling.md) for why, and expect that to tighten in Phase 2. CI publishes all four runtime identifiers on every pull request, so a trim failure surfaces there even if you do not run this locally.
