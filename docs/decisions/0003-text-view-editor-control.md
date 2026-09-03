# 0003 — Text View editor control: AvaloniaEdit, subject to a measured spike

**Status:** proposed — the decision is conditional, and this record is closed by the spike below
**Date:** 2026-09-03

## Context

[`0002`](0002-technology-stack.md) settled the platform and left one component unchosen: the
control behind Text View. It is separated out because it is the only stack choice that turns on a
measurement rather than an argument, and because the measurement has not been run.

Text View is not a text box with colours. The requirements ask it for:

- Syntax highlighting, folding, and search with find-and-replace, and deliberately **no**
  autocompletion (`XE-026`).
- Validation markers — a red squiggly underline and a second severity marker — applied and cleared
  per validation pass (`XE-063`, `XE-064`).
- **Go to Definition** on `Ctrl`+click, offered *only* inside a `type`, `base`, or `ref` attribute
  value and nowhere else in the text (`XE-029`). The control must therefore report what token sits
  under the pointer, not merely which character offset.
- A landing highlight that marks a declaration briefly and fades, on a configurable timer
  (`XE-046`, Preferences).
- `Ctrl`+scroll zoom and an independent font size, in the platform's native monospaced face
  (`XE-026`, `XE-046`).
- Virtualised line rendering (`XE-077`) at **8.3 MB and 147,419 lines** with no perceptible lag
  (`XE-076`).
- Undo grouped by word boundary and **unified with Design View's semantic undo stack** (`XE-043`).
- Two-way synchronisation with the document model, and a best-effort partial render when the buffer
  is not well-formed (`XE-030`, `XE-031`).

## Options

**AvaloniaEdit** — MIT, the Avalonia port of AvalonEdit, the SharpDevelop editor that also sits
under ILSpy. It provides a rope-backed document, virtualised line rendering, XSHD-driven
highlighting, folding, a search panel, and the extension points that markers and hit-testing need.
It covers the first six demands above out of the box.

**Build our own.** Rejected without measurement. A credible editor control is caret and selection
modelling, text layout, IME and dead-key handling, bidirectional text, clipboard semantics, undo,
and a document structure that stays fast under edits in the middle of an 8 MB buffer. That is a
product in its own right, and building it would consume the schedule that the actual product needs.

**Monaco or CodeMirror inside a WebView.** Rejected on principle rather than on numbers. It puts a
browser engine inside an artifact that must be self-contained (`XE-074`) and make no network
requests (`XE-075`), against an `XE-081` posture that wants the dependency surface small. Avalonia's
WebView support is also the least mature part of that ecosystem.

So the real question is not *which control* but *whether AvaloniaEdit clears the bar at corpus
scale, and what we give up if it does not*.

## Decision

**Adopt AvaloniaEdit, conditional on the spike below.** It is the only candidate that is both
licence-clean (MIT, so it ships under `0002`'s rules with an ordinary `NOTICE` entry) and mature
enough to avoid building an editor.

Two risks stop this being unconditional, and they are different in kind.

**The performance risk is empirical.** AvalonEdit's document is built for large files, but XML
highlighting is a stateful span machine: to paint any line, the highlighter must know the span stack
at the top of it. Jumping to the end of an 8 MB file is therefore not free, and neither is
invalidating after an edit near the top. The expectation is that it holds; the expectation is not
evidence, and 147,419 lines is past where this is routinely exercised.

**The undo risk is architectural.** `XE-043` requires **one ordered history across both views** — a
Design View operation and a run of typing share a stack, and undo returns the user to the view where
the edit happened. AvaloniaEdit ships its own `UndoStack` on its document. Either that stack is
disabled and ours drives text edits directly, or ours subordinates it and interleaves. This reaches
into how every edit in the application is recorded, so it is settled in the spike rather than
discovered during implementation.

Accessibility (`XE-080`) is a real concern and is *not* a discriminator: any custom-drawn editing
surface exposes little to a screen reader, and building our own would not improve it.

## Go / no-go criteria

Measured against `UCI_MessageDefinitions_v2_5_0.xsd` — 8.3 MB, 147,419 lines — with XML
highlighting active, on the **lowest specification we intend to support**, recorded with the result.
Run on at least two of the three target platforms, including Linux, because the Skia backends differ.

The thresholds derive from `XE-076`'s "no perceptible lag": roughly 100 ms is where a response stops
feeling immediate, and 16.7 ms is the frame budget at 60 Hz.

| # | Gate | Threshold |
| - | ---- | --------- |
| **G1** | Cold open | First painted, highlighted viewport ≤ **2 s**; fully interactive ≤ **3 s** |
| **G2** | Scrolling | Continuous scroll and jump-to-end sustain ≥ **55 fps**, with **no frame over 100 ms** |
| **G3** | Typing | After an edit near the top of the file: median keystroke-to-paint ≤ **30 ms**, 99th percentile ≤ **100 ms**, over a run of at least 200 keystrokes |
| **G4** | Memory | Working set with the corpus open ≤ **600 MB**, with no unbounded growth over a 10-minute scroll-and-edit session |
| **G5** | Markers | 500 squiggly markers applied and cleared in ≤ **100 ms**, without disturbing scroll position or selection |
| **G6** | Hit-testing | `Ctrl`+click resolves the token under the pointer *and* its classification — whether it sits inside a `type`, `base`, or `ref` value. Demonstrated, not timed |
| **G7** | Undo | A written design showing one ordered stack across both views, with AvaloniaEdit's `UndoStack` either disabled or subordinated, and word-boundary grouping preserved. A design gate, not a number |

**Go** is all seven. G1–G5 failing sends us to the fallback ladder. **G6 or G7 failing is
disqualifying** and is the only route to reopening the choice of control, because both are
structural: a control that cannot say what token is under the pointer cannot serve `XE-029`, and one
whose undo cannot be subordinated cannot serve `XE-043`.

## Fallback ladder

Taken in order. Each rung costs more than the one above it, and the ladder is deliberately arranged
so that the cheap rungs are invisible to the user and the expensive ones are visible only on large
files. **The file size is fixed; the feature set is negotiable.** That is the trade this ladder
makes, and it is why replacing the control sits at the bottom rather than the top.

1. **Bound the highlighter.** Highlight the viewport plus a fixed lookback, resynchronising the span
   stack from periodically cached checkpoints rather than from the start of the file. Invisible to
   the user, changes no requirement, and is the first thing to try because it attacks the specific
   mechanism G1–G3 would have failed on.
2. **Drop folding.** The most expensive structural feature and the most redundant one here: the left
   panel object tree already provides structural navigation over the same document, so folding is a
   convenience rather than a capability. **Amends `XE-026`**, which currently names folding.
3. **Simplify highlighting to a stateless lexical scheme.** Colour tags, attribute names, values and
   comments from a tokeniser that carries no cross-line span state. XSD is regular enough that the
   visible loss is small — mainly multi-line comments and CDATA — and it removes the whole class of
   cost behind G1–G3. Invisible in most files; a slight fidelity loss in a few.
4. **Large-file mode: highlighting off above a threshold**, with a banner saying so and a manual
   override. Well-precedented in editors that handle generated files. Degrades only the files that
   need it, and only in colour. **Amends `XE-026`.**
5. **Replace the control.** Only after 1–4, and only for a G6 or G7 failure, since those cannot be
   bought back by dropping features. If it comes to this, the option is not "build an editor" but
   re-examining the field with the spike's measurements in hand.

**Not on the ladder, at any rung:** validation markers (`XE-063`, `XE-064`), Go to Definition
(`XE-029`), search and find-and-replace (`XE-026`), unified undo (`XE-043`), and virtualisation
(`XE-077`). Each is load-bearing for requirements outside Text View, so none is available as
performance currency.

## Consequences

- **The spike is scheduled before Text View implementation begins**, and its output is a
  measurement table against G1–G7 plus the undo design. This record is then updated to accepted or
  rejected with those numbers in it, rather than being superseded by a new one.
- **The corpus file becomes a permanent performance fixture.** Whatever the spike measures should be
  re-measured in CI or in a periodic manual pass, because G1–G5 are exactly the properties that
  regress silently as features accumulate.
- **AvaloniaEdit is a shipped dependency** under `0002`'s rules: MIT, so its `NOTICE` obligations are
  nil beyond attribution, and it enters the `XE-081` composition scan and the SBOM like any other.
  It is also a large body of third-party code to track for the life of the product — the trade
  accepted in place of writing an editor ourselves.
- **Rungs 2 and 4 amend `XE-026`.** Writing them down now means the requirement is changed
  deliberately, with the measurement that forced it recorded, rather than quietly narrowed during
  implementation.
