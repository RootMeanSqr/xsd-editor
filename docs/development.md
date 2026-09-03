# Setting up a development environment

Everything here is developer-side tooling, which `XE-074` leaves unconstrained. The constraint it does impose — that an *end user* fetches no runtime, SDK or package manager — is satisfied by the publish settings in `src/XsdEditor.App/XsdEditor.App.csproj` and verified by CI.

This file owns **installation and environment**. The command list lives in [`../AGENTS.md`](../AGENTS.md), the corpus rationale in [`decisions/0004`](decisions/0004-build-and-security-tooling.md), and the pull-request checklist in [`../CONTRIBUTING.md`](../CONTRIBUTING.md); none of them is repeated here.

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

Nothing else is needed: no database, no service, no container. Build and test commands are in [`../AGENTS.md`](../AGENTS.md).

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

## Publishing a self-contained build

What an end user would receive:

```bash
dotnet publish src/XsdEditor.App -c Release -r linux-x64   # or win-x64, osx-arm64, osx-x64
```

Run this before proposing a change that adds a dependency: a library that resolves types by name shows up here rather than in development, which is the failure [`decisions/0002`](decisions/0002-technology-stack.md) warns about. Trim warnings are currently **reported, not gating** — see [`decisions/0004`](decisions/0004-build-and-security-tooling.md) for why, and expect that to tighten in Phase 2.
