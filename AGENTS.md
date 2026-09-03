# AGENTS.md

Working agreements for AI coding agents and human contributors in this repository. Keep this file
current — it is the first thing an agent reads.

## Project

`xsd-editor` is a platform-independent graphical editor for XML Schema Definition (XSD) files,
optimised for the Venetian Blind schema style. See [`docs/requirements.md`](docs/requirements.md)
for scope and goals.

**The technology stack has not been chosen yet.** Do not scaffold an application, add dependencies,
or create source directories until that decision is recorded in `docs/decisions/`. If a task seems
to require picking a stack, stop and ask.

## Repository layout

| Path        | Contents                                         |
| ----------- | ------------------------------------------------ |
| `docs/`     | Requirements, design notes, and decision records |
| `AGENTS.md` | This file                                        |
| `CLAUDE.md` | Pointer to this file, for Claude Code            |

Source, test, and build directories will be added alongside the stack decision.

## Build, test, lint

None yet. When a stack is chosen, document the exact commands here — an agent should be able to
verify a change without guessing.

## Conventions

- **Branches.** Short-lived, branched from `main`, one topic each. Prefer `docs/…`, `feat/…`,
  `fix/…`, `chore/…` prefixes.
- **Commits.** Imperative subject under ~72 characters, with a body explaining *why* when the
  reason is not obvious from the diff. During an interactive working session, accumulate changes
  and commit once the thread of work is finished — do not commit after every exchange.
- **Pull requests.** Everything lands via PR, including documentation. Open as a draft while work
  is in progress.
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

## Working style

- Ask before making an architectural choice the requirements do not already settle. Marking
  something as an open question is a valid outcome.
- Do not add dependencies without saying why in the PR description. Check the candidate for known
  CVEs first — `XE-081` requires that none ship with a known unfixed vulnerability, and every
  dependency is inside the installed artifact with no runtime update path, so a library added here
  is a library to watch for the life of the product. Prefer the smaller dependency surface.
- Do not merge with an unresolved Critical or High finding from either security check (`XE-081`):
  composition scanning over dependencies, and static analysis over our own code. Medium and Low are
  triaged, not gates. An exception to any of this is recorded in `docs/decisions/`, never left
  implicit.
- Do not invent requirements. If a detail is unspecified, add it to the open questions list rather
  than deciding silently.
