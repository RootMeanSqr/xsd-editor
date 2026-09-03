# Setting up a development environment

Everything here is developer-side tooling, which `XE-074` leaves unconstrained. The constraint
it does impose — that an *end user* fetches no runtime, SDK or package manager — is satisfied by
the publish settings in `src/XsdEditor.App/XsdEditor.App.csproj` and verified by CI.

## Prerequisites

| | |
| --- | --- |
| **.NET SDK** | 10.0.100 or a later 10.0 feature band. `global.json` pins it, so a mismatched SDK fails fast rather than building differently. |
| **Git** | Any recent version. |
| **An editor** | Visual Studio 2026, JetBrains Rider, or VS Code with the C# Dev Kit. All three read `.editorconfig`, which is the whole of the house style. |

Install the SDK from <https://dotnet.microsoft.com/download/dotnet/10.0>, or with your platform's
package manager. Verify:

```bash
dotnet --version   # expect 10.0.1xx
```

Nothing else is needed. There is no external database, service, or container.

## Build, test, run

From the repository root:

```bash
dotnet restore XsdEditor.slnx
dotnet build   XsdEditor.slnx
dotnet test    XsdEditor.slnx

dotnet run --project src/XsdEditor.App     # the editor
dotnet run --project src/XsdEditor.Cli -- --help   # the headless harness
```

Format before you push — CI runs the same command with `--verify-no-changes` and fails on a diff:

```bash
dotnet format XsdEditor.slnx
```

`dotnet build` treats warnings as errors, so an analyser finding fails locally exactly as it does
in CI. That is deliberate (`XE-081`): the build is the static-analysis gate.

## The reference corpus

The UCI v2.5 files are a controlled interface standard and are **not** in this repository
([`decisions/0004`](decisions/0004-build-and-security-tooling.md)). Point `XSDEDITOR_CORPUS` at a
local copy:

```bash
export XSDEDITOR_CORPUS=/path/to/uci/v2.5     # Linux, macOS
$env:XSDEDITOR_CORPUS = 'C:\path\to\uci\v2.5' # Windows PowerShell
```

Without it the build and the whole unit suite still pass; only the corpus-dependent round-trip
and timing suites skip, and they say so loudly. Those are the acceptance tests for `XE-069`,
`XE-071` and `XE-072`, so a change to the parser or the serialiser should be run against the
corpus before it is proposed.

## Publishing a self-contained build

What an end user would receive:

```bash
dotnet publish src/XsdEditor.App -c Release -r linux-x64   # or win-x64, osx-arm64, osx-x64
```

Self-contained, single-file and trimmed, per `XE-074` and
[`decisions/0002`](decisions/0002-technology-stack.md). Trim warnings are errors in CI, so run
this before proposing a change that adds a dependency — a library that resolves types by name
fails here rather than in development, which is the failure `0002` warns about.

## Benchmarks

```bash
dotnet run --project tests/XsdEditor.Benchmarks -c Release
```

Release configuration only; BenchmarkDotNet refuses a Debug build, and rightly.

## Before opening a pull request

- `dotnet format XsdEditor.slnx` leaves no diff
- `dotnet build XsdEditor.slnx` and `dotnet test XsdEditor.slnx` are clean
- Corpus suites have been run if you touched the parser, model or serialiser
- A new dependency has passed the CVE, licence and trim-safety checks in
  [`../CONTRIBUTING.md`](../CONTRIBUTING.md), and the PR description says why it is needed
- Behaviour and the documentation describing it are in the same commit (`AGENTS.md`)
