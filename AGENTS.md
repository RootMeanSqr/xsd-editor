# AGENTS.md

Working agreements for AI coding agents and human contributors in this repository. Keep this file current — it is the first thing an agent reads.

## Project

`xsd-editor` is a platform-independent graphical editor for XML Schema Definition (XSD) files, optimised for the Venetian Blind schema style. See [`docs/requirements.md`](docs/requirements.md) for scope and goals.

**The stack is .NET 10 with Avalonia**, in C#, recorded in [`docs/decisions/0002-technology-stack.md`](docs/decisions/0002-technology-stack.md). Published self-contained, single-file and trimmed, under the JIT. `CommunityToolkit.Mvvm` is the MVVM layer.

Work proceeds in phases; the route from here to a shipping editor is [`docs/implementation-plan.md`](docs/implementation-plan.md). Read it before starting a piece of work, so that a change lands in the phase it belongs to.

## Repository layout

| Path        | Contents                                         |
| ----------- | ------------------------------------------------ |
| `docs/`     | Requirements, the plan, design notes, and decision records |
| `src/XsdEditor.Core` | Syntax tree, schema model, parser, serialiser, index, commands. **No Avalonia reference** |
| `src/XsdEditor.App`  | The Avalonia application. The publish properties live here |
| `src/XsdEditor.Cli`  | `xsdedit`, the headless harness CI measures through |
| `tests/XsdEditor.Core.Tests` | xUnit tests over the core                |
| `scripts/` | Checks a contributor may want to run, called by CI rather than duplicated in it |
| `.github/` | Workflows, Dependabot, and any helper that only makes sense inside CI |
| `AGENTS.md` | This file                                        |
| `CLAUDE.md` | Pointer to this file, for Claude Code            |
| `LICENSE`   | Apache-2.0, the project's outbound licence       |
| `NOTICE`    | Attribution notice required by Apache-2.0 §4d    |
| `CLA.md`    | Individual Contributor License Agreement         |
| `CONTRIBUTING.md` | How work lands, and how to sign the CLA    |

`XsdEditor.Core` deliberately carries no Avalonia reference. That is what keeps the parser, model and serialiser testable headless, and it is a property to preserve rather than an accident.

## Build, test, lint

Run from the repository root. The solution is `XsdEditor.slnx`.

```bash
dotnet restore XsdEditor.slnx
dotnet build   XsdEditor.slnx                    # warnings are errors
dotnet test    XsdEditor.slnx
dotnet format  XsdEditor.slnx                    # CI runs --verify-no-changes
dotnet run --project src/XsdEditor.App           # the editor
dotnet run --project src/XsdEditor.Cli -- --help # the headless harness
```

`dotnet build` treats warnings as errors and runs the .NET analysers, so the build *is* the static-analysis gate `XE-081` asks for — a finding fails locally exactly as it does in CI.

**The reference corpus is not in the repository** and is located through `XSDEDITOR_CORPUS`: a list of paths or URLs separated by a **semicolon on every platform** (a colon cannot separate entries that may be URLs), of which **the first is the entry point and the rest are its `include`/`import` dependencies** ([`docs/decisions/0004-build-and-security-tooling.md`](docs/decisions/0004-build-and-security-tooling.md)). Without it the build and the unit suite pass, and only the corpus round-trip and timing suites skip — loudly. They are the acceptance tests for `XE-069`, `XE-071`, `XE-072` and `XE-083`, so run them before proposing a change to the parser, model or serialiser.

**CI logic lives in scripts, not in the workflow.** Anything past a couple of lines, or with
control flow, goes in a file that CI calls: bash embedded in a YAML block scalar cannot be
shellchecked, cannot be run locally, and has to be extracted before it can be tested at all. The
split is by audience — `scripts/` for anything a contributor could plausibly want to run
(`verify-corpus.sh`, `check-vulnerable-packages.sh`), `.github/scripts/` for helpers that only
make sense inside a workflow. Scripts detect `GITHUB_ACTIONS` and emit `::error::` annotations
there, plain messages otherwise, so the same file serves both.

Full setup instructions are in [`docs/development.md`](docs/development.md).

## Conventions

- **Branches.** Short-lived, branched from `main`, one topic each. Prefer `docs/…`, `feat/…`, `fix/…`, `chore/…` prefixes.
- **Commits.** Imperative subject under ~72 characters, with a body explaining *why* when the reason is not obvious from the diff. During an interactive working session, accumulate changes and commit once the thread of work is finished — do not commit after every exchange.
- **Pull requests.** Everything lands via PR, including documentation. Open as a draft while work is in progress. A contributor signs [`CLA.md`](CLA.md) before their first PR is merged.
- **Squash merges only.** A PR lands on `main` as a single commit. Merge commits and rebase merges are disabled at the repository level (Settings → General → Pull Requests), so `main` reads as one commit per change rather than as the working history of a branch. Write the PR title and description to be the commit message they become.
- **No Claude session links.** Commit messages and pull request descriptions must not carry `Claude-Session:` trailers or `claude.ai/code/session_…` URLs. They resolve only for the account that created them, so in a public repository they are noise in the permanent record. Co-authorship trailers are fine; the session link is not.
- **Markdown.** No line-width rule, and **paragraphs are written as a single line** rather than hard wrapped. Renderers and diff viewers soft-wrap, so a hard wrap buys nothing and costs a reflow every time a sentence is edited — and it makes a one-word change show up as a multi-line diff. Tables, code blocks and list items keep their own line structure. Files not otherwise being edited are left alone rather than reflowed for its own sake: decisions `0001`–`0003` are still hard wrapped for that reason. `docs/requirements.md` already writes one line per paragraph.
- **Documentation.** A change to behaviour and the docs describing it belong in the same commit.
- **Requirement IDs.** Requirements in `docs/requirements.md` carry stable `XE-nnn` identifiers. Cite them in commits, tests, and issues rather than section numbers. A new requirement takes the next unused number; an identifier is never reused or reassigned, even after its requirement is removed.

## Decisions

Choices that are expensive to reverse — the stack, the parser, the persistence model, the licence — get a short record in `docs/decisions/` (one file per decision: context, the decision, the consequences). Open questions live in `docs/requirements.md` until they are answered.

Five are recorded so far: **Apache-2.0** in [`docs/decisions/0001-licensing.md`](docs/decisions/0001-licensing.md), **.NET with Avalonia** in [`docs/decisions/0002-technology-stack.md`](docs/decisions/0002-technology-stack.md), and **AvaloniaEdit for Text View** in [`docs/decisions/0003-text-view-editor-control.md`](docs/decisions/0003-text-view-editor-control.md), **build, corpus access and security tooling** in [`docs/decisions/0004-build-and-security-tooling.md`](docs/decisions/0004-build-and-security-tooling.md), and **the syntax layer** in [`docs/decisions/0005-syntax-layer.md`](docs/decisions/0005-syntax-layer.md).

One is still `proposed` and blocks work: `0003` is conditional on a measured spike, so do not begin Text View implementation without running it. `0005` is accepted — it amends `0002`'s clause on how source text is read, and settles the syntax layer as our own lexer over a full green/red tree, so Phase 1 is unblocked.

## Working style

- Ask before making an architectural choice the requirements do not already settle. Marking something as an open question is a valid outcome.
- Do not add dependencies without saying why in the PR description. Check the candidate for known CVEs first — `XE-081` requires that none ship with a known unfixed vulnerability, and every dependency is inside the installed artifact with no runtime update path, so a library added here is a library to watch for the life of the product. Prefer the smaller dependency surface.
- Check that the candidate is **trim-safe** as well. The application publishes trimmed, and we hold to AOT-safe practice so that NativeAOT stays reachable (`0002`): a library that resolves types or members by name through reflection fails in the installed artifact and not in development. Prefer source-generator-based libraries over reflection-based ones.
- Check the candidate's **licence** too, in the same pass. Anything shipped to users must permit distribution inside an Apache-2.0 product: MIT, BSD, ISC, Apache-2.0, MPL-2.0, and GPLv2-with-Classpath-Exception are fine; GPL and AGPL are not. LGPL is permitted only where the library is linked dynamically from inside the artifact and a relink path is preserved. Build and test tooling is unconstrained, since it does not reach the artifact. Record the licence of every shipped dependency — Apache-2.0 §4d obliges us to carry its `NOTICE` contents into each installer, and the same inventory feeds the SBOM and the `XE-081` composition scan.
- Do not merge with an unresolved Critical or High finding from either security check (`XE-081`): composition scanning over dependencies, and static analysis over our own code. Medium and Low are triaged, not gates. An exception to any of this is recorded in `docs/decisions/`, never left implicit.
- Do not invent requirements. If a detail is unspecified, add it to the open questions list rather than deciding silently.
