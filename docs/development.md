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

The UCI v2.5 files are a controlled interface standard and are **not** in this repository. Point `XSDEDITOR_CORPUS` at your own copy:

```bash
export XSDEDITOR_CORPUS=/path/to/UCI_MessageDefinitions_v2_5_0.xsd:/path/to/UCI_SecurityMarkings_v2_5_0.xsd
```

```powershell
$env:XSDEDITOR_CORPUS = 'C:\uci\UCI_MessageDefinitions_v2_5_0.xsd;C:\uci\UCI_SecurityMarkings_v2_5_0.xsd'
```

Entries are separated by the platform path separator (`:` on Linux and macOS, `;` on Windows), and each may be a local path or an `https:` URL.

> **The first entry is the entry point; the rest are its `include`/`import` dependencies.**

Without the variable the build and the whole unit suite still pass — only the corpus round-trip and timing suites skip, loudly. Since those are the acceptance tests for the measured serialisation requirements, run them before proposing a change to the parser, model or serialiser.

## Publishing a self-contained build

What an end user would receive:

```bash
dotnet publish src/XsdEditor.App -c Release -r linux-x64   # or win-x64, osx-arm64, osx-x64
```

Run this before proposing a change that adds a dependency: a library that resolves types by name shows up here rather than in development, which is the failure [`decisions/0002`](decisions/0002-technology-stack.md) warns about. Trim warnings are currently **reported, not gating** — see [`decisions/0004`](decisions/0004-build-and-security-tooling.md) for why, and expect that to tighten in Phase 2.
