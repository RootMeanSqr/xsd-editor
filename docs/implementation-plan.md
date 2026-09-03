# Phased implementation plan

The route from a documentation-only repository to a shipping editor. Phases land in order, each as
its own pull request. Requirement identifiers refer to [`requirements.md`](requirements.md);
decision numbers to [`decisions/`](decisions/).

**Status:** Phase 0 in progress.

## Context

The repository is documentation only. PR #1 squash-merged onto `main` (`80a5e94`), carrying
`docs/requirements.md` (82 `XE-nnn` requirements), `0001-licensing.md` (Apache-2.0), `0002-technology-stack.md`
(.NET 10 + Avalonia, accepted) and `0003-text-view-editor-control.md` (AvaloniaEdit, **proposed**, gated on a
spike). `AGENTS.md` says "nothing is scaffolded yet, but nothing blocks it either" and leaves its Build/test/lint
section empty pending the first code.

So the stack is settled and scaffolding is the next thing owed. This plan sequences the work from an empty
repository to a shipping editor, front-loading the two areas where the requirements are most likely to be wrong:
lossless round-tripping (§4) and the dependency/navigation model (§2.2, §2.4).

**Answered while planning** (fold these into the docs as the phases land):

- **Corpus fixtures** are never committed. CI and local dev read a variable naming local paths or URLs to fetch at
  build time. `XE-075` constrains the *application*, not CI, so a downloading CI job is fine — say so in the ADR
  so it is not later mistaken for a violation.
- **Spike gate G7** (unified undo design) moves into Phase 1, since it dictates how the model records edits.
  G1–G6 stay with Text View in Phase 3.
- **Phase 1 includes the mutation/command layer.** Round-trip alone would not exercise whether the dependency
  index stays correct as the model changes, which is the design risk worth retiring early.

---

## Phase 0 — Repository and CI scaffolding

Everything encoded in a project file is decided (`0002`, Consequences), so publish properties go in from the first
commit rather than being retrofitted.

**Solution layout** (`XsdEditor.sln`):

| Project | Purpose |
| --- | --- |
| `src/XsdEditor.Core` | Syntax tree, schema model, parser, serialiser, index, commands. **No Avalonia reference** — this is what keeps Phase 1 testable headless. |
| `src/XsdEditor.App` | Avalonia application. Publish properties live here. |
| `src/XsdEditor.Cli` | Headless harness: `parse`, `roundtrip`, `validate`, `index`, `report`. CI measures timings through this without a display. |
| `tests/XsdEditor.Core.Tests` | xUnit. |
| `tests/XsdEditor.Benchmarks` | BenchmarkDotNet. |

**Build configuration**

- `global.json` pinning the .NET 10 SDK feature band.
- `Directory.Build.props`: `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`,
  `EnforceCodeStyleInBuild=true`, `InvariantGlobalization=true`, `IsTrimmable=true` on `Core`.
- `Directory.Packages.props` — central package management — plus `packages.lock.json` and
  `RestoreLockedMode=true` in CI. Reproducible restore is what makes the `XE-081` composition scan mean anything.
- `App` publish properties: `SelfContained`, `PublishSingleFile`, `PublishTrimmed`,
  `RuntimeIdentifiers=win-x64;osx-arm64;osx-x64;linux-x64` (`XE-074`).
- `.editorconfig` driving `dotnet format`; extend `.gitignore` with the .NET section it currently defers.

**CI** (`.github/workflows/ci.yml`), on PR and on `main`:

1. `dotnet format --verify-no-changes`
2. `dotnet build -warnaserror` and `dotnet test` on ubuntu/windows/macos
3. `dotnet list package --vulnerable --include-transitive` — **fails on any hit** (`XE-081`)
4. Licence inventory over the NuGet graph, emitting `THIRD-PARTY-NOTICES.md` and failing on a licence outside the
   `0001` allowlist
5. **Corpus job**, gated on the corpus variable being set: fetches the fixtures, runs the round-trip and timing
   suites, uploads the timing table as an artifact. Skips loudly, never silently, when unset.

Static analysis beyond Roslyn analyzers (SonarQube is *named* but not adopted in `0002`) is chosen in Phase 0 and
recorded as **`docs/decisions/0004-build-and-security-tooling.md`**, covering the scanner, the corpus-fetch
mechanism and its `XE-075` relationship, and the notices/SBOM generation.

**Docs.** Fill in `AGENTS.md` → Build, test, lint with the exact commands and the solution path — the file
explicitly promises this as soon as there is something to run.

*Exit:* a green three-platform CI, `dotnet publish` producing a self-contained artifact on each, and an empty but
running Avalonia window.

---

## Phase 1 — Syntax tree, schema model, serialiser, commands, index

The largest and most load-bearing phase. Nothing here needs a UI.

### 1a. Lossless syntax layer

`XE-067`–`XE-069` demand comments, whitespace, and **original character-reference spelling** survive a write, and
`XE-031` demands a partial parse of a malformed buffer. A DOM cannot do this; a **Roslyn-style green/red tree over
the raw text** can. Every node owns an exact source span including trivia, so serialising an untouched node is a
byte copy of its span, and only modified nodes are re-rendered. Lossless preservation stops being a feature to
implement and becomes the default behaviour.

**Open construction question, to settle first.** `0002` says the model is "constructed from `XmlReader`".
`XmlReader` resolves character references and normalises attribute values, and it throws rather than recovering on
malformed input — both against what this layer needs. Spike it (**S1, first task of the phase**): if `XmlReader`
plus `IXmlLineInfo` can be made to yield exact attribute-value spans against the raw buffer, use it; if not, write
a small hand-rolled XML lexer for the syntax layer and keep `XmlReader`/`XmlSchemaSet` strictly for validation.
The second outcome is a deviation from `0002` and gets an amendment to that record, not a silent change.

**Ampersand preprocessor** (`XE-070`) escapes raw `&` in annotation text before parsing, which shifts every
downstream offset. It therefore emits a **patched buffer plus an offset map**, and the serialiser reverses exactly
the escapes it introduced. An `&` already opening a valid reference is untouched — the corpus would fail
immediately across 116 references otherwise.

### 1b. Schema model

A semantic layer projected over the syntax tree, each node holding a pointer to its syntax node:
`SchemaClosure` → `SchemaDocument` → `GlobalElement` / `ComplexType` / `SimpleType` → `ModelGroup` /
`ElementParticle` / `Facet` / `Annotation` / `UnsupportedConstruct` / `GapNode`.

- **`ObjectId` is stable across re-parse** — `(file, kind, name)` for globals, owning-element path for inline
  anonymous types. Expansion markers (`XE-027`), navigation history (`XE-028`), and index keys are all keyed by
  it, and `XE-027`'s rule that an anonymous type's marker belongs to its owning element falls out for free.
- **Effective vs. written attributes** are distinct properties throughout (`XE-072`, `XE-082`, `XE-049`): the
  Details Pane needs "is this the default", the renderer needs the effective value, the serialiser needs to omit
  defaults.
- Constructs outside R1 scope become `UnsupportedConstruct` nodes carrying their name and their verbatim span
  (`XE-012`, `XE-027`).

### 1c. The index — O(1) dependency lookup

One `SchemaIndex` per resolved closure. Five maps, each earning its place against a named requirement:

| Map | Serves |
| --- | --- |
| `QName → ITypeDefinition` | type picker (`XE-027`), uniqueness checks (`XE-035`, `XE-042`), object tree (`XE-048`) |
| `QName → List<Reference>` (**all** references, resolved or not) | continuous reference check (`XE-056`), rename/delete reporting (`XE-039`, `XE-040`) |
| `QName → List<ElementDecl>` (`type="T"`) | Dependencies Tree children (`XE-050`) |
| `QName → List<ComplexType>` (`base="T"`) | Dependencies Tree children; unused-types derivation rule (`XE-022`); abstract-extension closure (`XE-021`) |
| `ObjectId → ITypeDefinition` (element → owning type) | the element node's single child (`XE-050`) |

Two consequences worth stating. First, "unresolved" is not a separate structure — it is simply a key in the
reference map with no entry in the definition map, which is exactly why `XE-056` can require a reference to stop
being marked the moment its type is created. Second, `XE-050`'s tree is then O(1) per expanded node, which is what
lets it be built lazily as that requirement demands.

Forward edges (a type's own dependencies) come from the model rather than the index; the subset closure
(`XE-021`) and the unused-types reachability walk (`XE-022`) are whole-closure O(V+E) passes and stay that way.

### 1d. Commands, undo, and index maintenance — the design risk

**The index is maintained incrementally by the command layer, never rebuilt by scanning.** Every mutation is a
command that reports its edge deltas, and the index applies them. Rebuilding on a dirty flag was considered and
rejected: a rename touching 31 sites would rebuild a 5,534-type index, and `XE-056` runs on every edit.

Incremental maintenance is easy to get subtly wrong, so it is verified rather than trusted: the index exposes a
test-only `VerifyAgainstFullRebuild()`, and **property-based tests run random command sequences and assert index
equivalence after every step**. That check is the reason to build the command layer in this phase rather than
alongside the GUI.

`ICommand` carries: apply, invert, the affected `ObjectId` and view, and a merge rule. This gives `XE-043`'s
one-entry-per-semantic-operation, `XE-043`'s word-boundary grouping for text edits, and Undo/Redo Navigation.

**Spike gate G7 lands here**: a written design showing one ordered stack across both views with AvaloniaEdit's
`UndoStack` disabled or subordinated. Written against this command layer, it either validates the shape or forces
it now — which is the whole point of pulling it forward.

### 1e. Serialiser

Byte-copy of unmodified spans; re-render only where the model changed. Two deliberate deviations, both specified:
strict attribute ordering (`XE-071`) applied to **every** element written, and implicit defaults omitted
(`XE-072`).

### 1f. Validation

`XmlSchemaSet` over the resolved closure (`XE-052`–`XE-054`), with `XmlSchemaException` positions mapped back to
`ObjectId` and syntax span. Cascade suppression (`XE-065`) and the separate continuous name-lookup check
(`XE-056`) are implemented here; both are pure model operations. Directive resolution (`XE-016`, `XE-018`) and
degradation on an unresolvable one (`XE-019`) come with it.

### Phase 1 verification

- **Round-trip is the headline test**: parse → serialise an unmodified document and assert the output is
  byte-identical, over every fixture. On `UCI_MessageDefinitions_v2_5_0.xsd` the requirements make a falsifiable
  prediction — **zero** attribute-ordering diff (`XE-071`, measured over 19,377 multi-attribute elements) and
  **exactly one** line of diff from the single explicit `minOccurs="1"` (`XE-072`). Assert precisely that. Any
  other diff is a defect in the syntax layer.
- Character references preserved across 144 patterns / 116 references (`XE-069`).
- Purpose-built fixtures for the four §6 verification gaps: anonymous complexTypes, raw ampersands, unresolvable
  directives, and a from-nothing authoring path.
- Malformed-buffer fixtures producing gap nodes with the rest of the tree intact (`XE-031`).
- Property-based command/index equivalence, as above.
- **Timings** via `XsdEditor.Cli` + BenchmarkDotNet: cold parse of 8.3 MB, serialise, index build, full validation
  pass, and incremental index update after a rename. Recorded in
  `docs/measurements/phase-1-timings.md` and re-run in the corpus CI job so `XE-076` regressions surface early.

*Exit:* round-trip proven on the corpus with exactly the two predicted diffs; timings recorded; G7 design written;
`0002` amended if S1 forced a hand-rolled lexer.

---

## Phase 2 — GUI scaffolding

Mock-ups via the `design` skill first, then the shell: menu bar (`XE-044`), Top Ribbon (`XE-045`), tabs
(`XE-014`, `XE-023`), and the four panels. Light/Dark theming (`XE-079`) and preference persistence (`XE-046`)
land here because retrofitting theming across custom-drawn surfaces is expensive.

The three read-only panels wire straight onto the Phase 1 index and are what prove it: object tree (`XE-048`,
virtualised at ~6,250 entries), Details Pane (`XE-049`), **Dependencies Tree** (`XE-050`, lazily expanded), Bottom
Pane (`XE-051`), and the one shared `Navigate(ObjectId)` service behind all three (`XE-047`).

**Two things pulled forward into this phase, against the natural ordering:**

- **A canvas rendering spike.** Design View is why Avalonia was chosen, and `XE-076`/`XE-078` concentrate there.
  Draw 500 cards, connectors, and joint circles and measure the frame budget *now*, not in Phase 4 when the
  renderer is written. Cheap here, very expensive to discover later.
- **Accessibility prototyping.** `0002` records a11y as the accepted risk and says it "needs early prototyping
  rather than late remediation" — so a screen-reader pass over the shell and a custom-drawn control belongs in
  this phase, not at the end.

*Exit:* a running application that opens the corpus, lists and navigates its objects in Text-less form, switches
theme, and persists preferences.

---

## Phase 3 — Text View and the AvaloniaEdit spike

Run gates **G1–G6** against the corpus on at least two platforms including Linux, on the lowest specification
intended to be supported (`0003`). G7 is already answered from Phase 1. Record the measurement table and update
`0003` to accepted or rejected **in place**, as that record requires.

Then Text View itself: highlighting, folding, search, validation markers (`XE-063`, `XE-064`), Go to Definition
(`XE-029`), landing highlight, `Ctrl`+scroll zoom, and two-way sync with the model (`XE-030`), degrading to a
partial render when the buffer is malformed (`XE-031`).

If G1–G5 fail, take the fallback ladder in order. If G6 or G7 fail, the control choice reopens — with
measurements in hand, per `0003`.

---

## Phase 4 — Design View

The bespoke canvas, and the single largest lump of UI work. Split in two:

- **4a, rendering**: nested encloser boxes, joint circles and right-angled branching, cardinality labels, dashed
  optionality (`XE-082`), extension-base nesting (§2.2.1), simple-type derivation chains (§2.2.2), annotations,
  abstract badges, three visually distinct "cannot display" states (`XE-027`), per-object expansion with the
  ancestry recursion bound, re-rooting via all six paths, and selective rendering (`XE-078`).
- **4b, interaction**: drag and drop (`XE-036`), model group edits (`XE-037`), copy/paste across tabs (`XE-038`),
  delete and rename (`XE-039`, `XE-040`), annotation editor (`XE-041`), and Extract Global ComplexType
  (`XE-042`) — each a command from Phase 1, so undo granularity is already settled.

---

## Phase 5 — Whole-schema tools, packaging, release

Create Schema Subset (`XE-021`), Unused Types Report (`XE-022`), external change detection (`XE-024`), Open
Recent and session re-open (`XE-044`, `XE-046`), per-platform installers, SBOM and `THIRD-PARTY-NOTICES`, and a
**verified `XE-075` audit** of the shipped build — `0002` says egress compliance is to be proven against the
artifact rather than assumed.

---

## Verification, end to end

| Level | How |
| --- | --- |
| Unit | `dotnet test` — headless, no display needed, since `Core` has no Avalonia reference |
| Round-trip | Corpus byte-comparison asserting exactly the two predicted diffs |
| Property-based | Random command sequences vs. `VerifyAgainstFullRebuild()` |
| Performance | `XsdEditor.Cli` + BenchmarkDotNet in the corpus CI job, table committed per phase |
| UI | Avalonia.Headless for view-model and interaction tests; the canvas spike for frame budget |
| Supply chain | `dotnet list package --vulnerable`, licence inventory, static analysis — all merge-gating (`XE-081`) |
| Manual | `dotnet publish` per platform, open the corpus, exercise the phase's features on Windows, macOS and Linux |

Each phase lands as its own PR against `main` (squash-merged, per `AGENTS.md`), with the behaviour change and the
documentation update in the same commit.
