# Contributing

Thanks for your interest. The specification is complete and the stack is settled (.NET 10 and Avalonia, [`docs/decisions/0002-technology-stack.md`](docs/decisions/0002-technology-stack.md)). Work now proceeds in phases: [`docs/implementation-plan.md`](docs/implementation-plan.md) says what is being built and in what order, and it is the best place to find something that needs doing.

To set up a development environment, see [`docs/development.md`](docs/development.md).

## Before your first pull request: sign the CLA

Every contributor signs the [Individual Contributor License Agreement](CLA.md) before their first pull request is merged. It takes a minute and it only has to happen once.

The short version: the project is Apache-2.0, and Apache-2.0 §5 already covers contributions. The CLA asks for one thing on top — the right to sublicense — so the project can be relicensed later without having to find every past contributor. You keep all rights in your own work. The reasoning is at the bottom of [`CLA.md`](CLA.md).

**To sign:** comment on your pull request with

```
I have read the CLA document and I hereby sign the CLA.
```

<!-- Once CLA Assistant (https://cla-assistant.io) is wired up, it will post this prompt on each
     pull request automatically and record the signature. Until then, signatures are recorded by
     the maintainer against the pull request. -->

If you are contributing on behalf of an employer that holds rights in your work, say so on the pull request — a corporate agreement is needed as well as your individual one.

## How work lands

- Short-lived branches off `main`, one topic each. Prefer `docs/…`, `feat/…`, `fix/…`, `chore/…` prefixes.
- Everything lands via pull request, including documentation. Open it as a draft while it is in progress.
- Commit subjects are imperative and under about 72 characters, with a body explaining *why* when the diff does not make it obvious.
- A change to behaviour and the documentation describing it belong in the same commit.

[`AGENTS.md`](AGENTS.md) holds the full working agreements, and applies to human contributors and AI coding agents alike. Read it before starting.

## Adding a dependency

Two checks, both required, before a dependency is proposed — see `XE-081` in the requirements and the working-style section of [`AGENTS.md`](AGENTS.md):

- **Known CVEs.** Nothing ships with a known unfixed vulnerability. Every dependency lives inside the installed artifact with no runtime update path, so anything added here is something to watch for the life of the product.
- **Licence.** It must permit distribution inside an Apache-2.0 product. MIT, BSD, ISC, Apache-2.0, MPL-2.0, and GPLv2-with-Classpath-Exception are fine. GPL and AGPL are not. LGPL is acceptable only where the library is linked dynamically from inside the artifact with a relink path preserved.

Say why the dependency is needed in the pull request description, and prefer the smaller dependency surface.
