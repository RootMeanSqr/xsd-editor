# AGENTS.md

Working agreements for AI coding agents and human contributors in this repository. Keep this file
current — it is the first thing an agent reads.

## Project

`xsd-editor` is a platform-independent graphical editor for XML Schema Definition (XSD) files,
optimised for the Venetian Blind schema style. See [`docs/requirements.md`](docs/requirements.md)
for scope and goals.

**The stack is .NET 10 with Avalonia**, in C#, recorded in
[`docs/decisions/0002-technology-stack.md`](docs/decisions/0002-technology-stack.md). Published
self-contained, single-file and trimmed, under the JIT. `CommunityToolkit.Mvvm` is the MVVM layer.
Nothing is scaffolded yet, but nothing blocks it either.

## Repository layout

| Path        | Contents                                         |
| ----------- | ------------------------------------------------ |
| `docs/`     | Requirements, design notes, and decision records |
| `AGENTS.md` | This file                                        |
| `CLAUDE.md` | Pointer to this file, for Claude Code            |
| `LICENSE`   | Apache-2.0, the project's outbound licence       |
| `NOTICE`    | Attribution notice required by Apache-2.0 §4d    |
| `CLA.md`    | Individual Contributor License Agreement         |
| `CONTRIBUTING.md` | How work lands, and how to sign the CLA    |

Source, test, and build directories will be added when the first code lands.

## Build, test, lint

None yet — nothing is scaffolded. The commands will be the ordinary .NET ones (`dotnet build`,
`dotnet test`, `dotnet format`); document them exactly, with the solution path, as soon as there is
something to run. An agent should be able to verify a change without guessing.

## Conventions

- **Branches.** Short-lived, branched from `main`, one topic each. Prefer `docs/…`, `feat/…`,
  `fix/…`, `chore/…` prefixes.
- **Commits.** Imperative subject under ~72 characters, with a body explaining *why* when the
  reason is not obvious from the diff. During an interactive working session, accumulate changes
  and commit once the thread of work is finished — do not commit after every exchange.
- **Pull requests.** Everything lands via PR, including documentation. Open as a draft while work
  is in progress. A contributor signs [`CLA.md`](CLA.md) before their first PR is merged.
- **Squash merges only.** A PR lands on `main` as a single commit. Merge commits and rebase merges
  are disabled at the repository level (Settings → General → Pull Requests), so `main` reads as one
  commit per change rather than as the working history of a branch. Write the PR title and
  description to be the commit message they become.
- **No Claude session links.** Commit messages and pull request descriptions must not carry
  `Claude-Session:` trailers or `claude.ai/code/session_…` URLs. They resolve only for the account
  that created them, so in a public repository they are noise in the permanent record. Co-authorship
  trailers are fine; the session link is not.
- **Markdown.** Wrap prose at roughly 99 columns so diffs stay reviewable in a side-by-side view.
  Nothing enforces it, and nothing external requires it — GitHub soft-wraps rendered Markdown, so
  the width matters only in the diff view. `docs/requirements.md` is an accepted exception: it is
  maintained unwrapped elsewhere and reflowing it here would conflict on every import.
- **Documentation.** A change to behaviour and the docs describing it belong in the same commit.
- **Requirement IDs.** Requirements in `docs/requirements.md` carry stable `XE-nnn` identifiers. Cite
  them in commits, tests, and issues rather than section numbers. A new requirement takes the next
  unused number; an identifier is never reused or reassigned, even after its requirement is removed.

## Decisions

Choices that are expensive to reverse — the stack, the parser, the persistence model, the
licence — get a short record in `docs/decisions/` (one file per decision: context, the decision,
the consequences). Open questions live in `docs/requirements.md` until they are answered.

Two are recorded so far: **Apache-2.0** in
[`docs/decisions/0001-licensing.md`](docs/decisions/0001-licensing.md), and **.NET with Avalonia**
in [`docs/decisions/0002-technology-stack.md`](docs/decisions/0002-technology-stack.md).

## Working style

- Ask before making an architectural choice the requirements do not already settle. Marking
  something as an open question is a valid outcome.
- Do not add dependencies without saying why in the PR description. Check the candidate for known
  CVEs first — `XE-081` requires that none ship with a known unfixed vulnerability, and every
  dependency is inside the installed artifact with no runtime update path, so a library added here
  is a library to watch for the life of the product. Prefer the smaller dependency surface.
- Check that the candidate is **trim-safe** as well. The application publishes trimmed, and we hold
  to AOT-safe practice so that NativeAOT stays reachable (`0002`): a library that resolves types or
  members by name through reflection fails in the installed artifact and not in development. Prefer
  source-generator-based libraries over reflection-based ones.
- Check the candidate's **licence** too, in the same pass. Anything shipped to users must permit
  distribution inside an Apache-2.0 product: MIT, BSD, ISC, Apache-2.0, MPL-2.0, and
  GPLv2-with-Classpath-Exception are fine; GPL and AGPL are not. LGPL is permitted only where the
  library is linked dynamically from inside the artifact and a relink path is preserved. Build and
  test tooling is unconstrained, since it does not reach the artifact. Record the licence of every
  shipped dependency — Apache-2.0 §4d obliges us to carry its `NOTICE` contents into each
  installer, and the same inventory feeds the SBOM and the `XE-081` composition scan.
- Do not merge with an unresolved Critical or High finding from either security check (`XE-081`):
  composition scanning over dependencies, and static analysis over our own code. Medium and Low are
  triaged, not gates. An exception to any of this is recorded in `docs/decisions/`, never left
  implicit.
- Do not invent requirements. If a detail is unspecified, add it to the open questions list rather
  than deciding silently.
