# Phased implementation plan

The route from a documentation-only repository to a shipping editor. Phases land in order, each as its own pull request. Requirement identifiers refer to [`requirements.md`](requirements.md); decision numbers to [`decisions/`](decisions/).

**Status:** Phase 0 landed. Phase 1 next.

## Context

The repository was documentation only: a complete specification (`docs/requirements.md`, 82 `XE-nnn` requirements) and the decisions taken while reviewing it. `0002` settled the stack — .NET 10 and Avalonia — and recorded that scaffolding was unblocked, since everything encoded in a project file was decided.

This plan sequences the work from there to a shipping editor, front-loading the two areas where the requirements are most likely to be wrong: lossless round-tripping (§4) and the dependency and navigation model (§2.2, §2.4).

---

## Phase 0 — Repository and CI scaffolding *(landed)*

Solution layout, build configuration, CI, and the documents above. What follows is what the repository actually contains, so that a later phase can trust it.

**Projects**, in `XsdEditor.slnx`:

| Project | Purpose |
| --- | --- |
| `src/XsdEditor.Core` | Syntax tree, schema model, parser, serialiser, index, commands. **No Avalonia reference** — this is what keeps Phase 1 testable headless. |
| `src/XsdEditor.App` | Avalonia application. The publish properties live here. |
| `src/XsdEditor.Cli` | `xsdedit`: the headless harness CI measures round-trip and timings through, without a display. |
| `tests/XsdEditor.Core.Tests` | xUnit. |

**Build configuration.** `global.json` pins the SDK feature band. `Directory.Build.props` carries the settings that are genuinely shared — nullable, warnings-as-errors, the .NET analysers, the NuGet audit — and deliberately **does not** set `TargetFramework`, which each project declares for itself; the SDK resolves framework references before a value set at that level is visible, and trimming and AOT checks then compare against an empty string. `Directory.Packages.props` carries every version with its licence annotated, plus any transitive pin needed to clear an advisory.

**CI**, on pull request, on `main`, and weekly:

1. `dotnet format --verify-no-changes`
2. Build and test on ubuntu, windows and macos
3. Self-contained, single-file, trimmed publish per RID
4. Supply chain: the gate is `NuGetAudit` in the build; the CI job publishes the report and asserts a clean graph positively, plus a package inventory that is explicitly *not* a licence check

**Carried forward, deliberately.** Lock files and `--locked-mode` (nothing to lock until the dependency set settles); licence-allowlist enforcement and `THIRD-PARTY-NOTICES` generation (deferred to the phase that produces an installer, where the §4d obligation attaches); tightening trim warnings from reported to gating (Phase 2, once the real warning set is known); a benchmark project (Phase 1, with the first timing table).

---

## Phase 1 — Syntax tree, schema model, serialiser, commands, index

The largest and most load-bearing phase. Nothing here needs a UI.

### 1a. Lossless syntax layer

`XE-067`–`XE-069` demand comments, whitespace, and **original character-reference spelling** survive a write, and `XE-031` demands a partial parse of a malformed buffer. A DOM cannot do this; a **Roslyn-style green/red tree over the raw text** can. Every node owns an exact source span including trivia, so serialising an untouched node is a byte copy of its span, and only modified nodes are re-rendered. Lossless preservation stops being a feature to implement and becomes the default behaviour.

**How the source is read is settled by [`0005`](decisions/0005-syntax-layer.md), not by a spike.** `0002` says the model is "constructed from `XmlReader`", and `XmlReader` cannot serve these requirements: it resolves character references before the value is visible, normalises attribute values, reports a start position with no extent, and throws rather than recovering. That is documented behaviour rather than something to measure, so it is recorded as a decision — read with a purpose-built lexer, amending `0002` — and `0005` is **proposed**, since it changes a clause in an accepted record. Phase 1 does not start until it is accepted or rejected.

`0005` also settles a plain concrete syntax tree rather than Roslyn's green/red split: round-trip fidelity needs only full-fidelity nodes with spans and trivia, and the split buys incremental reparse we have not yet measured a need for.

**Ampersand preprocessor** (`XE-070`) escapes raw `&` in annotation text before parsing, which shifts every downstream offset. It therefore emits a **patched buffer plus an offset map**, and the serialiser reverses exactly the escapes it introduced. An `&` already opening a valid reference is untouched — the corpus would fail immediately across 116 references otherwise.

### 1b. Schema model

A semantic layer projected over the syntax tree, each node holding a pointer to its syntax node: `SchemaClosure` → `SchemaDocument` → `GlobalElement` / `ComplexType` / `SimpleType` → `ModelGroup` / `ElementParticle` / `Facet` / `Annotation` / `UnsupportedConstruct` / `GapNode`.

- **`ObjectId` is stable across re-parse** — `(file, kind, name)` for globals, owning-element path for inline anonymous types. Expansion markers (`XE-027`), navigation history (`XE-028`), and index keys are all keyed by it, and `XE-027`'s rule that an anonymous type's marker belongs to its owning element falls out for free.
- **Effective vs. written attributes** are distinct properties throughout (`XE-072`, `XE-082`, `XE-049`): the Details Pane needs "is this the default", the renderer needs the effective value, the serialiser needs to omit defaults.
- Constructs outside R1 scope become `UnsupportedConstruct` nodes carrying their name and their verbatim span (`XE-012`, `XE-027`).

### 1c. The index — dependency lookup without scanning

One `SchemaIndex` per resolved closure.

**Keys are `(SymbolSpace, QName)`, never a bare `QName`.** XSD keeps elements and types in *separate symbol spaces*: a global element named `Address` and a global complexType named `Address` are both legal in one schema, and a bare-QName key would silently conflate them. Chameleon includes — a no-namespace schema included into a target namespace — also change a component's effective QName at resolution time, so the key is assigned during resolution rather than read off the source.

**Three maps, not five.** A use of a type as `type="T"` and as `base="T"` differ only in which attribute the reference came from, so one reference map with a kind on each entry replaces two maps and halves the number of invariants to maintain under mutation:

| Map | Serves |
| --- | --- |
| `(SymbolSpace, QName) → Component` | type picker (`XE-027`), uniqueness checks (`XE-035`, `XE-042`), object tree (`XE-048`) |
| `(SymbolSpace, QName) → List<Reference>`, each tagged `TypeOf` / `BaseOf` / `RefTo`, resolved or not | continuous reference check (`XE-056`), rename and delete reporting (`XE-039`, `XE-040`), and — filtered by kind — both child sets of the Dependencies Tree (`XE-050`), the unused-types derivation rule (`XE-022`), and the abstract-extension closure (`XE-021`) |
| `ObjectId → Component` (element → owning type) | the element node's single child (`XE-050`) |

Two consequences worth stating. First, "unresolved" is not a separate structure — it is simply a key in the reference map with no entry in the component map, which is exactly why `XE-056` can require a reference to stop being marked the moment its type is created. Second, expanding a Dependencies Tree node is **O(1) to find the dependants and O(k) to produce them**, k being how many there are — which for a widely used base type in this corpus is large. That is the honest form of the claim, and O(k) is unavoidable when k rows are being drawn; what the index buys is that nothing scans 5,534 types to discover them.

Forward edges (a type's own dependencies) come from the model rather than the index; the subset closure (`XE-021`) and the unused-types reachability walk (`XE-022`) are whole-closure O(V+E) passes and stay that way.

### 1d. Commands, undo, and index maintenance — the design risk

**First task: measure a full index rebuild on the corpus.** A rebuild over 5,534 types may well be a few milliseconds — comfortably inside a frame — in which case rebuild-on-dirty is correct and incremental maintenance is complexity bought for nothing. This plan insists elsewhere on measuring before gating (`0004` on trim warnings, `0003` on the editor control); it should hold itself to the same rule rather than assuming the clever design.

**If the measurement says rebuild is too slow**, the index is maintained incrementally by the command layer, never by scanning: every mutation reports its edge deltas and the index applies them. Incremental maintenance is easy to get subtly wrong, so it would be verified rather than trusted — a test-only `VerifyAgainstFullRebuild()`, with property-based tests running random command sequences and asserting equivalence after every step. That harness needs a property-testing library (CsCheck or FsCheck), which is a dependency decision under `AGENTS.md` and is taken then rather than assumed now.

Either way the command layer belongs in this phase: it is what `XE-043`'s undo granularity is defined against.

`ICommand` carries: apply, invert, the affected `ObjectId` and view, and a merge rule. This gives `XE-043`'s one-entry-per-semantic-operation, `XE-043`'s word-boundary grouping for text edits, and Undo/Redo Navigation.

**The portable half of spike gate G7 lands here; the control-specific half stays in Phase 3.** `0003`'s G7 asks for one ordered stack across both views *with AvaloniaEdit's `UndoStack` disabled or subordinated* — and in this phase AvaloniaEdit has not been spiked, `0003` is still `proposed`, and its fallback ladder can still replace the control. Writing the AvaloniaEdit-specific design against a control that may not survive Phase 3 would be work done twice.

What belongs here is everything that does not name a control: one ordered command stack owned by the model, `ICommand` with apply, invert, affected `ObjectId` and view, and a merge rule, and `XE-043`'s word-boundary grouping for text edits. Subordinating the editor control's own stack to it remains G7, measured and recorded with G1–G6 in `docs/measurements/spike-avaloniaedit.md`, which is what closes `0003`.

### 1e. Serialiser

Byte-copy of unmodified spans; re-render only where the model changed. Two deliberate deviations, both specified: strict attribute ordering (`XE-071`) applied to **every** element written, and implicit defaults omitted (`XE-072`).

### 1f. Validation, and the orchestration around it

`XmlSchemaSet` over the resolved closure (`XE-052`–`XE-054`), with `XmlSchemaException` positions mapped back to `ObjectId` and syntax span. Cascade suppression (`XE-065`) and the separate continuous name-lookup check (`XE-056`) are implemented here; both are pure model operations. Directive resolution (`XE-016`, `XE-018`) and degradation on an unresolvable one (`XE-019`) come with it.

**The orchestration is designed here too, not left to the phase that displays it.** A validation pass runs on exactly three triggers and never continuously (`XE-055`), runs asynchronously without blocking input (`XE-060`), is cancelled or discarded when the document changes under it (`XE-061`), covers the whole closure each time (`XE-058`), and never gates a save (`XE-057`). Results carry enough provenance for the UI to say they are in flight or stale (`XE-059`, `XE-062`).

These are architectural rather than cosmetic — cancellation has to run through the whole validation path, and `XE-061`'s "stale results must never overwrite newer ones" is a property of the result-publishing API. Retrofitting either onto a synchronous validator in Phase 5 would mean rewriting it, so the API is shaped for them now even though nothing displays the results until Phase 2.

### Phase 1 verification

- **Round-trip is the headline test**: parse → serialise an unmodified document and assert the output is byte-identical, over every fixture. On `UCI_MessageDefinitions_v2_5_0.xsd` the requirements make a falsifiable prediction — **zero** attribute-ordering diff (`XE-071`, measured over 19,377 multi-attribute elements) and **exactly one** line of diff from the single explicit `minOccurs="1"` (`XE-072`). Assert precisely that. Any other diff is a defect in the syntax layer.
- Character references preserved across 144 patterns / 116 references (`XE-069`).
- Purpose-built fixtures for the four §6 verification gaps: anonymous complexTypes, raw ampersands, unresolvable directives, and a from-nothing authoring path.
- Malformed-buffer fixtures producing gap nodes with the rest of the tree intact (`XE-031`).
- Property-based command/index equivalence, as above.
- **Timings**, through `XsdEditor.Cli`, with a `tests/XsdEditor.Benchmarks` project added in this phase — it was deliberately not scaffolded in Phase 0, where it would have measured nothing and pulled in a dependency early. Cold parse of 8.3 MB, serialise, index build, a full validation pass, and index update after a rename. Recorded in `docs/measurements/phase-1-timings.md`, and re-run in a corpus CI job — also added in this phase, alongside the first suite there is to skip — so `XE-076` regressions surface early.

*Exit:* round-trip proven on the corpus with exactly the two predicted diffs; timings recorded; G7 design written; `0002` amended if S1 forced a hand-rolled lexer.

---

## Phase 2 — GUI scaffolding

Mock-ups via the `design` skill first, then the shell: menu bar (`XE-044`), Top Ribbon (`XE-045`), tabs (`XE-014`, `XE-023`), and the four panels. Light/Dark theming (`XE-079`) and preference persistence (`XE-046`) land here because retrofitting theming across custom-drawn surfaces is expensive.

The three read-only panels wire straight onto the Phase 1 index and are what prove it: object tree (`XE-048`, virtualised at ~6,250 entries), Details Pane (`XE-049`), **Dependencies Tree** (`XE-050`, lazily expanded), Bottom Pane (`XE-051`), and the one shared `Navigate(ObjectId)` service behind all three (`XE-047`).

**Two things pulled forward into this phase, against the natural ordering:**

- **A canvas rendering spike.** Design View is why Avalonia was chosen, and `XE-076`/`XE-078` concentrate there. Draw 500 cards, connectors, and joint circles and measure the frame budget *now*, not in Phase 4 when the renderer is written. Cheap here, very expensive to discover later.
- **Accessibility prototyping.** `0002` records a11y as the accepted risk and says it "needs early prototyping rather than late remediation" — so a screen-reader pass over the shell and a custom-drawn control belongs in this phase, not at the end.

*Exit:* a running application that opens the corpus, lists and navigates its objects in Text-less form, switches theme, and persists preferences.

---

## Phase 3 — Text View and the AvaloniaEdit spike

Run gates **G1–G6** against the corpus on at least two platforms including Linux, on the lowest specification intended to be supported (`0003`). G7 is already answered from Phase 1. Record the measurement table and update `0003` to accepted or rejected **in place**, as that record requires.

Then Text View itself: highlighting, folding, search, validation markers (`XE-063`, `XE-064`), Go to Definition (`XE-029`), landing highlight, `Ctrl`+scroll zoom, and two-way sync with the model (`XE-030`), degrading to a partial render when the buffer is malformed (`XE-031`).

If G1–G5 fail, take the fallback ladder in order. If G6 or G7 fail, the control choice reopens — with measurements in hand, per `0003`.

---

## Phase 4 — Design View

The bespoke canvas, and the single largest lump of UI work. Split in two:

- **4a, rendering**: nested encloser boxes, joint circles and right-angled branching, cardinality labels, dashed optionality (`XE-082`), extension-base nesting (§2.2.1), simple-type derivation chains (§2.2.2), annotations, abstract badges, three visually distinct "cannot display" states (`XE-027`), per-object expansion with the ancestry recursion bound, re-rooting via all six paths, and selective rendering (`XE-078`).
- **4b, interaction**: drag and drop (`XE-036`), model group edits (`XE-037`), copy/paste across tabs (`XE-038`), delete and rename (`XE-039`, `XE-040`), annotation editor (`XE-041`), and Extract Global ComplexType (`XE-042`) — each a command from Phase 1, so undo granularity is already settled.

---

## Phase 5 — Whole-schema tools, packaging, release

Create Schema Subset (`XE-021`), Unused Types Report (`XE-022`), external change detection (`XE-024`), Open Recent and session re-open (`XE-044`, `XE-046`), per-platform installers, SBOM and `THIRD-PARTY-NOTICES`, and a **verified `XE-075` audit** of the shipped build — `0002` says egress compliance is to be proven against the artifact rather than assumed.

---

## Verification, end to end

| Level | How |
| --- | --- |
| Unit | `dotnet test` — headless, no display needed, since `Core` has no Avalonia reference |
| Round-trip | Corpus byte-comparison asserting exactly the two predicted diffs |
| Property-based | Random command sequences vs. `VerifyAgainstFullRebuild()` |
| Performance | `XsdEditor.Cli` and BenchmarkDotNet in the corpus CI job, from Phase 1; table committed per phase |
| UI | Avalonia.Headless for view-model and interaction tests; the canvas spike for frame budget |
| Supply chain | `dotnet list package --vulnerable`, licence inventory, static analysis — all merge-gating (`XE-081`) |
| Manual | `dotnet publish` per platform, open the corpus, exercise the phase's features on Windows, macOS and Linux |

Each phase lands as its own PR against `main` (squash-merged, per `AGENTS.md`), with the behaviour change and the documentation update in the same commit.

---

## Requirement coverage

So that a requirement falling through the gaps between phases is visible rather than rediscovered late. Scope and non-goal statements (`XE-001`–`XE-012`) are not listed: they constrain every phase rather than being built in one.

| Phase | Requirements |
| --- | --- |
| 0 | `XE-081` (tooling half) |
| 1 | `XE-016`–`XE-019`, `XE-021`, `XE-022`, `XE-030`, `XE-031`, `XE-038`–`XE-040`, `XE-042`, `XE-043`, `XE-052`–`XE-062`, `XE-065`, `XE-066`, `XE-067`–`XE-072` |
| 2 | `XE-013`, `XE-014`, `XE-015`, `XE-020`, `XE-023`, `XE-024`, `XE-044`–`XE-051`, `XE-064`, `XE-077`, `XE-079`, `XE-080` |
| 3 | `XE-025`, `XE-026`, `XE-028`, `XE-029`, `XE-063` |
| 4 | `XE-027`, `XE-033`–`XE-037`, `XE-041`, `XE-078`, `XE-082` |
| 5 | `XE-073`–`XE-076` |

Four are deliberately spread rather than owned by one phase, and are called out so they are not assumed done:

- **`XE-032` Responsiveness** and **`XE-076` Performance** are properties every phase is measured against, not features. Each phase that adds a surface adds its timing to `measurements/`.
- **`XE-077` Virtualisation** covers three separate lists — the object tree (Phase 2), the Text View line list (Phase 3), and the enumeration editor (Phase 2) — so it is satisfied in pieces rather than at once.
- **`XE-080` Accessibility** is prototyped in Phase 2 on `0002`'s advice, but satisfying it is Phase 4's problem too: a custom-drawn canvas is a surface whose accessibility we own outright.
- **`XE-007`** (canvas image export) is R2 and appears in no phase by design.
