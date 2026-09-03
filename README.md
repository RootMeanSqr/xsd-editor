# xsd-editor

A platform-independent graphical editor for XML Schema Definition (XSD) files, optimised for the
Venetian Blind schema style.

> **Status: pre-alpha.** No code yet. The project is still in the requirements phase — the
> technology stack has not been chosen, so the repository layout below is deliberately minimal and
> will grow once that decision is made. The delivery form is settled: a packaged desktop
> application for Windows, macOS, and Linux that installs without the user fetching dependencies.

## Repository layout

```
docs/            Project documentation (requirements, decisions, design notes)
AGENTS.md        Working agreements for AI coding agents (and humans)
CLAUDE.md        Pointer to AGENTS.md for Claude Code
LICENSE          Apache License 2.0
NOTICE           Attribution notice required by Apache-2.0
CLA.md           Individual Contributor License Agreement
CONTRIBUTING.md  How to contribute, and how to sign the CLA
```

## Where to start

- [`docs/requirements.md`](docs/requirements.md) — the specification.
- [`AGENTS.md`](AGENTS.md) — conventions to follow when contributing.

## Licence

Apache License 2.0 — see [`LICENSE`](LICENSE). The reasoning, including what it means for
dependency choice, is in [`docs/decisions/0001-licensing.md`](docs/decisions/0001-licensing.md).

## Contributing

Work happens on short-lived branches off `main`, merged via pull request. Until a stack is chosen
there is no build, test, or lint step to run. Contributors sign the [CLA](CLA.md) before their
first pull request is merged — see [`CONTRIBUTING.md`](CONTRIBUTING.md).
