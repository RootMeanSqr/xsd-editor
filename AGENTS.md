# AGENTS.md

Working agreements for AI coding agents and human contributors in this
repository. Keep this file current — it is the first thing an agent reads.

## Project

`xsd-editor` is an editor for XML Schema Definition (XSD) files. See
[`docs/requirements.md`](docs/requirements.md) for scope and goals.

**The technology stack has not been chosen yet.** Do not scaffold an
application, add dependencies, or create source directories until that decision
is recorded in `docs/decisions/`. If a task seems to require picking a stack,
stop and ask.

## Repository layout

| Path        | Contents                                                   |
| ----------- | ---------------------------------------------------------- |
| `docs/`     | Requirements, design notes, and decision records           |
| `AGENTS.md` | This file                                                  |
| `CLAUDE.md` | Pointer to this file, for Claude Code                      |

Source, test, and build directories will be added alongside the stack decision.

## Build, test, lint

None yet. When a stack is chosen, document the exact commands here — an agent
should be able to verify a change without guessing.

## Conventions

- **Branches.** Short-lived, branched from `main`, one topic each. Prefer
  `docs/…`, `feat/…`, `fix/…`, `chore/…` prefixes.
- **Commits.** Imperative subject under ~72 characters, with a body explaining
  *why* when the reason is not obvious from the diff.
- **Pull requests.** Everything lands via PR, including documentation. Open as
  a draft while work is in progress.
- **Markdown.** Wrap prose at roughly 80 columns so diffs stay reviewable.
- **Documentation.** A change to behaviour and the docs describing it belong in
  the same commit.

## Decisions

Choices that are expensive to reverse — the stack, the parser, the persistence
model, the licence — get a short record in `docs/decisions/` (one file per
decision: context, the decision, the consequences). Open questions live in
`docs/requirements.md` until they are answered.

## Working style

- Ask before making an architectural choice the requirements do not already
  settle. Marking something as an open question is a valid outcome.
- Do not add dependencies without saying why in the PR description.
- Do not invent requirements. If a detail is unspecified, add it to the open
  questions list rather than deciding silently.
