# xsd-editor

A platform-independent graphical editor for XML Schema Definition (XSD) files, optimised for the
Venetian Blind schema style.

> **Status: pre-alpha.** The specification is complete and the solution is scaffolded; the editor
> does not do anything yet. Work proceeds in phases — see
> [`docs/implementation-plan.md`](docs/implementation-plan.md). The delivery form is settled: a
> packaged desktop application for Windows, macOS, and Linux that installs without the user
> fetching dependencies. It is built with **.NET 10 and Avalonia**.

## Repository layout

```
src/
  XsdEditor.Core   Schema model, parser, serialiser, index, commands (no UI reference)
  XsdEditor.App    The Avalonia desktop application
  XsdEditor.Cli    xsdedit, a headless harness for round-trip and timing runs
tests/
  XsdEditor.Core.Tests   Unit tests
  XsdEditor.Benchmarks   Performance fixtures
docs/            Project documentation (requirements, plan, decisions, measurements)
AGENTS.md        Working agreements for AI coding agents (and humans)
CLAUDE.md        Pointer to AGENTS.md for Claude Code
LICENSE          Apache License 2.0
NOTICE           Attribution notice required by Apache-2.0
CLA.md           Individual Contributor License Agreement
CONTRIBUTING.md  How to contribute, and how to sign the CLA
```

## Where to start

- [`docs/development.md`](docs/development.md) — set up a local development environment.
- [`docs/requirements.md`](docs/requirements.md) — the specification.
- [`docs/implementation-plan.md`](docs/implementation-plan.md) — what is being built, in what order.
- [`AGENTS.md`](AGENTS.md) — conventions to follow when contributing.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) and nothing else.

```bash
dotnet build XsdEditor.slnx
dotnet test  XsdEditor.slnx
dotnet run --project src/XsdEditor.App
```

Full instructions, including the reference corpus and self-contained publishing, are in
[`docs/development.md`](docs/development.md).

## Licence

Apache License 2.0 — see [`LICENSE`](LICENSE). The reasoning, including what it means for
dependency choice, is in [`docs/decisions/0001-licensing.md`](docs/decisions/0001-licensing.md).

## Technology

**.NET 10 and Avalonia**, in C#, published as a self-contained single-file artifact per platform.
The editor holds its own schema model rather than the BCL's, so that comments, formatting and
attribute order survive a round trip. See
[`docs/decisions/0002-technology-stack.md`](docs/decisions/0002-technology-stack.md) for the
reasoning.

## Contributing

Work happens on short-lived branches off `main`, merged via pull request. Run `dotnet format`,
`dotnet build` and `dotnet test` before proposing a change; CI runs all three on Windows, macOS and
Linux. Contributors sign the [CLA](CLA.md) before their first pull request is merged — see
[`CONTRIBUTING.md`](CONTRIBUTING.md).
