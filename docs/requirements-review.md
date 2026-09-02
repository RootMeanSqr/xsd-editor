# Review of `requirements.md`

A comparison of the baseline specification against an independent draft
written from the same brief. Its purpose is to catch omissions, not to
relitigate decisions: where the baseline and the draft disagree, the baseline
is almost always better reasoned, and the last section records what was
dropped and why.

Findings are ordered by how much they would cost to discover later. Nothing
here blocks the stack decision.

---

## Contradictions and underspecified interactions

### 1. Canvas navigation has no defined entry point except the sidebar — RESOLVED

*Closed. §2.2 now specifies five equivalent re-rooting paths and the object
tree no longer latches a selection; §6 carries the two questions this opened.*

The original finding: §2.2 stated that the visual root was determined **solely**
by sidebar selection, while the Bottom Pane, Undo/Redo Navigation, and the
Dependencies Tree all needed to move the canvas to a node that might not lie
beneath the current root — which scrolling cannot satisfy.

The resolution went further than the finding. Rather than making the other
three paths exceptions to sidebar ownership, the root has **no owning panel**:
all four navigate on equal footing, a fifth path was added (double-clicking a
type name on the canvas), and the object tree was demoted from a selection
model to a filterable list that navigates on click and holds no state.
Single-click still inspects and double-click navigates, so the protection
against accidental re-rooting survives as a gesture distinction rather than a
panel restriction.

Two consequences are now open items in §6: with nothing latched, the canvas
must display its own root, and with five ways in and no way back, canvas
back/forward navigation needs a decision.

### 2. Attribute ordering conflicts with formatting preservation

§4 requires that comments, whitespace, and unrecognised nodes survive
serialisation, and the Implicit Defaults round-trip note is careful enough to
account for a **one-line** diff on the corpus. But **Attribute Ordering** in
the same section reads as a global policy: XSD attributes are serialised in
the order `name`, `type`, `minOccurs`, `maxOccurs`.

Applied to every element on save, that reorders attributes across the whole
file wherever the source used a different order, producing exactly the large
spurious diff §4 rejects elsewhere. Two readings need separating:

- the order in which the editor writes attributes it **adds** to an element,
  and
- whether existing attributes are **normalised** into that order on save.

The first is clearly intended. The second should probably be off by default,
or the corpus should be checked for how consistently it already matches the
default order.

### 3. Deleting a global type has no reference policy

**Rename** (§2.3) is specified with real care: rewrite within the file, warn
on cross-file references, name the file, report the count, never rewrite
across files. **Delete** in the same section gets one clause and says nothing
about the references left behind.

The mirror-image questions all apply — is the user warned, is the reference
count shown, do dangling `type=` and `base=` references become unresolved
placeholders per §2.1? The dependency data needed to answer is already
computed for the Dependencies Tree Pane.

---

## Missing capabilities

### 4. Creating a global element or type from scratch

The only creation paths in the document are **File Creation** (§2.1, a new
empty file) and **Extract Global ComplexType** (§2.3), which requires an
existing element with inline structure to promote. Authoring a new message —
a new global element — or a standalone named type appears nowhere. Deletion of
global types is specified; creation of them is not.

### 5. Autocomplete is assumed but never required

The single mention is in §2.1, as the justification for directive resolution:
"so that autocomplete and validation operate across the full set of referenced
files". The Text View feature list (§2.2) covers syntax highlighting, folding,
search-and-replace, and inline markers — no completion. Either a requirement
is missing from §2.2 or the word should come out of §2.1's rationale.

If it is intended, it is worth scoping: completing element and attribute names
against the schema-for-schemas is a different job from completing `type=` and
`base=` values against the 5,534 named types in the resolved closure. The
second is the one that pays for itself at corpus scale.

### 6. Navigating from a reference to its declaration

The reverse direction is well covered by the Dependencies Tree. The forward
direction is not: from `Type | FooType` on an element card, or from a `type=`
in Text View, to where `FooType` is declared — across files. Design View
partly covers it by rendering the referenced type inline on expansion, which
answers "what is in it" but not "where does it live". Text View has nothing.

### 7. Copy and paste across tabs

§2.3 specifies recursive copy and paste but not whether the clipboard spans
tabs. In a multi-file tabbed editor it presumably does, which raises the
question the single-file case avoids: a pasted subtree may reference types
that are not in the destination file's `include`/`import` closure. Paste and
let them render as unresolved references (§2.1), refuse, or offer to add the
directive — any is defensible, but the destination-closure check is not
mentioned.

### 8. `default` and `fixed` on element declarations

Not supported, not deferred, not excluded — simply absent. They are ordinary
XSD 1.0 element attributes and most likely belong in §1's deferred list beside
`nillable`, so the Attributes Pane accounts for them.

### 9. Instance-document validation

Validating a sample XML file against the open schema appears nowhere, in scope
or in exclusions. It is a common adjacency for a schema editor and the most
likely first "why can't it…" question after release. Worth an explicit
exclusion rather than silence, because it affects library selection: a
validating parser is a different dependency from a schema validator.

### 10. Unused global type reporting

With 4,607 complexTypes, types that nothing references accumulate — especially
in a schema maintained across revisions of a standard. §3 covers unresolvable
references (a reference with no type); the converse (a type with no reference)
is not mentioned. The dependency graph that answers it is already required.

---

## Document-level

### 11. No product-level non-goals

§1's exclusions are thorough but entirely construct-level: which XSD features
are in R1. Absent are the product-level exclusions stakeholders actually ask
for — code generation from schemas, other schema languages, semantic schema
diff, collaborative editing, exporting or printing the canvas as an image.
Canvas export is the one worth deciding rather than defaulting: for a
diagram-first editor it is a frequent request, and "the diagram is only
viewable inside the tool" is a real constraint on how people share schema
structure in review meetings.

### 12. No statement of who the users are

The document never says who operates the editor — schema authors extending
UCI, integrators reading it to build against it, or reviewers checking
conformance. This is genuinely load-bearing here, because §2.2's
selection-rooted canvas and §2.4's dependency tree serve readers, while
Extract Global and the model-group edits serve authors. Knowing which comes
first would settle several UX priorities.

### 13. No telemetry or network-egress statement

The corpus is a defence interface standard. §2.1 declines network
`schemaLocation` resolution for R1 scope reasons, which implies offline
operation without committing to it. An explicit "no telemetry, and no file
content leaves the machine" is cheap to state now and constrains the stack
later — crash reporters, analytics SDKs, and cloud-backed editor components
all become disqualifying rather than merely undesirable. In this domain it is
also the kind of thing that gets asked before deployment is approved.

### 14. Stable requirement identifiers

The document references itself by section, and §2.3 alone carries roughly ten
distinct requirements. Numbered IDs (`FR-12`) would make issues, commits, and
the verification-gaps table point at single requirements rather than sections.
A mechanical change, easiest applied before the document is referenced from
anywhere else.

---

## From the draft, deliberately not carried over

Recorded so the comparison is complete.

- **Byte-identical round-tripping as an absolute rule.** The draft made it a
  MUST. §4 plus the Implicit Defaults Rule is the better position: it
  preserves comments, whitespace, foreign attributes, and character references
  — the things a reviewer would notice — while accepting a known one-line diff
  in exchange for a consistent document model. The baseline's version is
  reasoned; the draft's was a slogan.
- **XSD 1.1 support.** The draft kept it as a possible later addition. The
  baseline excludes it permanently, which is a coherent choice given the style
  focus.
- **`xs:redefine` / `xs:override` resolution.** In the draft's FR-2, excluded
  permanently here.
- **Skeleton instance generation and semantic schema diff.** Both were LATER
  in the draft. Neither is mentioned in the baseline; both are reasonable
  permanent non-goals, listed here only so the decision is recorded.
- **Scale target.** The draft guessed "a few thousand declarations". The
  corpus figures replace the guess entirely.
