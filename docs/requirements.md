# XML Schema Definition (XSD) Graphical Editor Requirements Specification

This document details the functional, non-functional, and design requirements for the platform-independent XSD Graphical Editor.

**Reference corpus.** Requirements are validated against OAC-STD-002 Rev E, UCI Schema v2.5 (`UCI_MessageDefinitions_v2_5_0.xsd`, `UCI_SecurityMarkings_v2_5_0.xsd`, `UCI_Versioning_v2_5_0.xsd`). Where a requirement carries a numeric threshold, the corpus figure that justifies it is cited inline.

---

## 1. Scope & Style Focus

- **Delivery Strategy**: **UCI first, general later.** Release 1 targets the UCI v2.5 reference corpus and derives its scope from it. Later releases broaden to general Venetian Blind XSD editing. Exclusions below are therefore split into two categories — deferred and permanent — and a deferred exclusion must not be designed out of the architecture.
- **Style Focus**: The editor is optimised for the **Venetian Blind** style of XSD construction — globally defined complex and simple types, with global elements referencing those types, keeping nesting clean and reusable. The reference corpus is pure Venetian Blind: 5,534 named types and **zero anonymous type definitions**.
- **XSD Version**: **XSD 1.0 only**, in R1 and beyond. XSD 1.1 constructs (`xs:assert`, `xs:alternative`, open content) are permanently out of scope.
- **Supported constructs (R1)**:
  - Global Elements (messages) — 722 in the corpus
  - Globally named ComplexTypes — 4,607
  - Globally named SimpleTypes — 927
  - `xs:sequence` and `xs:choice` model groups
  - `xs:complexContent` / `xs:extension` derivation
  - `xs:restriction` with facets
  - `<xs:include>` and `<xs:import>` directives
  - **Anonymous (inline) complexTypes**, as a transient authoring state only — see Venetian Blind Conformance below
- **Venetian Blind Conformance**: The finished schema is expected to contain **no anonymous type definitions**. This is a property of the completed artifact, not of the editing session, and it is **an authoring convention the editor does not enforce**. The editor neither blocks nor warns on save, and remaining anonymous types are not reported as validation entries; ensuring the schema is fully promoted is the author's responsibility. The editor explicitly supports sketching structure inline — nested elements, sequences, and choices under an element — and then promoting that structure to a globally named type via Extract Global ComplexType (§2.3). Anonymous types are therefore a supported intermediate state that the editor must create, render, and edit.
- **Deferred features**: **exporting the Design View canvas as an image is R2.** The canvas is viewable only inside the editor in R1, so schema structure cannot be shared as a picture; this is accepted for R1 and scheduled rather than left open.
- **Deferred constructs**: `xs:attribute` declarations and attribute groups; `xs:group`; `xs:union`; `xs:list`; `xs:simpleContent`; mixed content; and the additional defaulted attributes `nillable`, `form`, `block`, and `final` (§2.4). These are absent from the UCI corpus but common in general Venetian Blind schemas. They are excluded from R1 scope only. The document model, Attributes Pane, and serialiser must accommodate them without structural rework.
- **Permanently out of scope**: `substitutionGroup`, `xs:key` / `xs:unique` / `xs:keyref`, `xs:redefine`, and `xs:override`. These conflict with the Venetian Blind style focus rather than merely being unused.
- **Round-trip safety**: Files containing deferred or out-of-scope constructs must open, save without corruption, and remain fully editable in Text View, even where Design View cannot render them. This applies from R1, since general schemas will be opened before general editing is supported. Unsupported constructs render as placeholders per §2.2.

---

## 2. Core Functional Requirements

### 2.1 Workspace & File Management

- **Platform Independence**: The application must run identically on Windows, macOS, and Linux. It is a desktop application, packaged per §5, Delivery Form.
- **Multi-File Handling**: Support opening, editing, and saving multiple XSD files concurrently using a tabbed layout.
- **File Creation**: Support creating a new, empty XSD file with a user-specified target namespace.
- **Directive Resolution**: Read and resolve `<xs:include>` and `<xs:import>` references so that autocomplete and validation operate across the full set of referenced files, not just the active file.
- **Built-in Types Always Resolve**: The XSD built-in datatypes — `xs:string`, `xs:double`, `xs:dateTime`, `xs:ID` and the rest of the 1.0 set, primitive and derived alike — resolve implicitly, in every file, with no `xs:import` or `xs:include` required. They belong to the XML Schema namespace, which a schema is never obliged to import, so a reference to one is never an unresolved reference and never produces a red squiggle or a Bottom Pane entry.
  - They are part of the **resolved closure** for every purpose that closure serves: the type picker offers them (§2.2), validation accepts them (§3), and pasting an element typed by one into another file (§2.3) brings no dependency with it and leaves nothing unresolved.
  - What must still be present is the ordinary **namespace binding**: the reference names a prefix, and that prefix has to be bound to `http://www.w3.org/2001/XMLSchema` in the file. A qualified name whose prefix is not bound to anything is a namespace error (§3), which is a different failure from a type that cannot be found and is reported as such. The conventional `xs` binding is not required — a file binding the schema namespace to some other prefix resolves identically.
  - Built-ins are not workspace objects: they are not listed in the Left Panel object tree (§2.4), are not renamable or deletable (§2.3), and have no definition to navigate to. Expanding an element typed by one renders the terminating node of a derivation chain, per §2.2.2.
- **Location Resolution**: R1 resolves two `schemaLocation` forms — **relative paths** and **absolute local paths**.
  - Relative paths resolve against the directory of the **referencing file**, not the active document or the process working directory. In a chain where A includes B and B includes C by relative path, C resolves relative to B.
  - XML catalogs and network URLs (`http:`, `https:`) are **not supported in R1**. A directive using an unsupported form is reported distinctly from a file that is simply missing, so the user can tell "this editor cannot fetch that" from "that file is not there".
  - Absolute paths are inherently platform-specific and may not resolve when a schema authored on one operating system is opened on another (§2.1, Platform Independence). Such a failure is reported as an unresolvable directive rather than treated as a defect.
  - `schemaLocation` values are never rewritten on save, including when a directive could not be resolved.
- **Unresolvable Directives**: A file whose `include` or `import` targets cannot be resolved **still opens and remains fully editable**. The editor degrades rather than refusing.
  - The unresolvable directive is flagged in place, in both Text and Design Views, and reported in the Bottom Pane.
  - Type references that cannot be resolved because of the missing file render as **unresolved placeholders**, marked with a **red squiggly underline** on the reference text in both Text View and on the Design View element card. The reference is displayed, cannot be expanded, and the referencing element remains editable. Hovering or selecting the marked reference reports the reason and the missing file.
  - The red squiggly underline is the general marker for unresolved and invalid references, applied consistently wherever such a reference appears rather than only at the directive site. The squiggle's waveform carries the meaning independently of its colour, satisfying §5's requirement that information not depend on colour alone; the specific red must still meet contrast in both Light and Dark themes.
  - The unresolvable directive and all references depending on it are preserved verbatim on save. A missing file never causes its references to be rewritten or dropped.
  - If the missing file later becomes available, resolution is re-attempted on the next validation pass (§3) without requiring the document to be reopened.
- **Namespace Handling**: Namespace declarations and prefixes are **modifiable**, not merely preserved. The editor reads and resolves them, and supports restructuring them from the Attributes Pane.
  - The **target namespace** may be set at file creation (§2.1, File Creation) and changed at any time thereafter.
  - Prefixes may be **declared, renamed, and removed**. Renaming a prefix rewrites every qualified name bound to it within the file, in the same single operation and single undo entry as a type rename (§2.3).
  - Prefix conflicts across imported schemas are reported and resolvable by rebinding one of the conflicting prefixes.
  - **Cross-file consequences follow the rename policy (§2.3).** A prefix binding is local to its file, so renaming one affects nothing else. Changing a file's target namespace does affect every file that imports it: the editor warns, names the referencing files, and reports the count, but does **not** rewrite them.
  - A prefix that is declared but unused is preserved on save rather than removed. The editor does not tidy namespace declarations it did not touch.
  - Prefix restructuring also remains available in Text View, since that view is unrestricted.
- **Save Model**:
  - Explicit save only. **No autosave and no crash-recovery journal** — both are deliberate v1 non-goals.
  - Each tab displays a dirty-state marker whenever it holds unsaved changes.
  - Closing a dirty tab, or quitting with any dirty tab open, prompts the user to save, discard, or cancel.
- **External Change Detection**: If a file open in the editor is modified on disk by another process, the editor prompts the user to either reload from disk or keep the in-editor version.

### 2.2 Dual-View Editing Mode

- **Single-Pane View Switching**: The main window switches between **Text View** and **Design View** via a toggle control in the top toolbar. Only one view is rendered at a time, maximising screen space.
- **Text View**:
  - Full text/XML representation of the active XSD file.
  - Syntax highlighting, line folding, search-and-replace, and inline validation markers.
- **Design View**:
  - A canvas-based node-graph representation of the XSD structure.
  - **Canvas Root**: The canvas renders a single object — an Element, ComplexType, or SimpleType — as its visual root, focused and centred. Until the user first navigates, the canvas is empty.
  - **Re-rooting**: The root changes only by explicit navigation, and six paths perform it: an entry in the Left Panel object tree (§2.4); a Bottom Pane validation entry (§2.4); an entry in the Dependencies Tree (§2.4); Undo/Redo Navigation (§2.3); creating a global object from the canvas (§2.3, Create Global Object); and a **double-click on the `Base Type` row in a ComplexType box header** (§2.2.1). The six are equivalent. No panel owns the root, and none is privileged over the others.
    - The base type row is the **only** canvas label that re-roots. Double-clicking an element card's `Type` row edits the type instead (see **Changing an Element's Type** below); no other type name on the canvas navigates.
    - A base type carrying an unresolved reference (§2.1) cannot be re-rooted to. The attempt reports the missing file and leaves the canvas as it stands, rather than clearing it.
  - **Selection Does Not Re-root**: A single click on a node inside the canvas selects it, highlights it with a thick outline, and loads its properties into the Attributes Pane, without changing the root. Single-click inspects, double-click navigates. This distinction — not ownership of the root by one panel — is what prevents unintended re-rooting during canvas interaction.
  - **The Object Tree Does Not Latch**: The Left Panel (§2.4) is a filterable listing of the file's objects, not a selection model. It holds no persistent selection, marks no current item, and is unaffected by re-rooting performed from any other path. Clicking an entry navigates the canvas; that is its only role.
    - Because no panel now indicates the current root, **the canvas must display it itself** — the root object's name and kind, shown persistently and independently of pan and zoom position. With no latched sidebar entry there is otherwise nothing that answers "what am I looking at" once the user has panned away from the root node.
  - **Changing an Element's Type**: Double-clicking the `Type | <typeName>` row on an element card opens a type picker and, on confirmation, rewrites the element's `type` attribute to the chosen type.
    - The picker offers the named ComplexTypes and SimpleTypes available in the resolved `include`/`import` closure (§2.1), together with the built-in datatypes, which are always available (§2.1, Built-in Types Always Resolve). It must be filterable rather than a plain list: the corpus offers 5,534 named types.
    - **A name outside the closure is accepted.** The user may type a type that does not yet exist — an author often knows what they intend to create or import next — and the resulting reference renders as unresolved per §2.1, with the red squiggly underline and a Bottom Pane entry, until the type appears. This follows the same degrade-rather-than-refuse posture as an unresolvable directive: the editor reports the gap, it does not prevent it.
    - **Design View refreshes on confirmation.** The element's subtree is re-rendered against the new type, following that type's own expansion markers (§2.2, Expansion State). Nothing is discarded — the previous type's markers survive, since they belong to that type and not to this element — and the canvas root is unaffected.
    - Where the element's previous type was an inline anonymous complexType (§1), assigning a named type removes that inline definition. The change is one semantic operation and therefore one undo entry (§2.3), which restores the inline structure.
  - **Nested Bounding-Box Enclosers**: ComplexTypes render as enclosing bounding boxes. All child elements and nested structures are positioned **inside the bounds of their parent type box**. Expanding a nested element renders its referenced ComplexType as a nested encloser box, recursively, to unbounded depth.
  - **Joint Circle Connectors & Right-Angled Branching**: Model groups are represented on connection lines by a small circular joint node. The stem from the parent leads into the joint circle, which splits into right-angled branch connectors linking to each child card. Joint circles, stems, branches, and child cards must remain exactly middle-aligned under all sizing changes and nested expansions.
    - **Glyphs**: Sequence and choice are distinguished by pictorial icons rather than letters, following the convention established by existing XSD design tools. Sequence and choice must be distinguishable by icon shape alone, without relying on colour or on a text label.
    - Because the glyphs are not self-explanatory, the joint node exposes its model group kind on hover or selection, and the context menu that converts between sequence and choice (§2.3) names the current kind explicitly.
  - **Element Card Layout**: Each element card shows the element name on an upper row and a `Type | <typeName>` row beneath it.
  - **Cardinality Annotation**: An occurrence-range label (`0..∞`, `0..17`, `1..4`) is displayed to the left of an element card whenever the effective range differs from the implied default of `1..1`. Default cardinality is not labelled.
  - **Universal Expansion Control**: **Every** element card displays an expand/collapse control, regardless of whether its type is a ComplexType, a named SimpleType, or a built-in primitive. What expansion renders is defined in §2.2.1 and §2.2.2.
  - **Expansion State**: Expanded/collapsed is a property of the **object**, held per tab, not a property of a position on the canvas. Each tab keeps a marker for every object it has rendered; the schema file is never written to record it, and the state does not outlive the tab.
    - Because the marker belongs to the object, it is **shared by every occurrence of that object**. Expanding a type while traversing one branch means that type is expanded wherever else it appears, and remains expanded when the canvas is re-rooted onto it. This is what makes Back and Forward (§2.2, Navigation History) return the user to the structure they left rather than to a collapsed root.
    - An occurrence renders expanded only if its own marker is expanded **and** every ancestor between it and the current root is also expanded. A collapsed parent hides its subtree regardless of the markers within it.
    - An object never touched by the user defaults to collapsed.
    - An inline anonymous complexType (§1) has no identity to share, so its marker is keyed to the element that owns it.
    - **Re-rooting always expands the root, in the render only.** Re-rooting onto a type shows the immediate child elements of that type and of its base type (§2.2.1), whatever the stored markers say. The override establishes what the user sees on arrival and is **never written to the markers**. Writing it would mean that merely visiting a type marked it expanded, and since markers are shared across occurrences, browsing types one at a time would progressively mark the whole schema expanded until every view opened fully expanded. Deeper levels follow their own markers.
    - **Only an explicit expand/collapse writes a marker.** Clicking the control on a card records the new state for that object. Navigation never does — not re-rooting, not Back or Forward (§2.2, Navigation History), not a Bottom Pane or Dependencies Tree entry. Collapsing a root that arrived expanded by the override supersedes it for the current render and records that object as collapsed.
    - **Recursion bound**: before expanding an element, the renderer compares its type against the types already present in that element's **ancestry within the current render** — the chain of enclosing type boxes from the displayed root down to its parent, base-type boxes (§2.2.1) included. If the type is already in that chain, expansion stops there.
      - The stopped element renders with a **`…` indicator** in place of its expanded content, showing that structure continues but repeats. Hovering or selecting it names the repeating type and where in the ancestry it occurs.
      - The bound is on the render, not the marker. The type keeps whatever expansion marker it has, and expands normally wherever it occurs outside its own ancestry.
      - The check is against the ancestry rather than the type alone, so it also catches mutual recursion (`A` containing `B` containing `A`), and so a type that merely appears twice in unrelated branches is unaffected.
      - Without this bound, per-object expansion state would expand a recursive type without limit: its marker being expanded would apply to the occurrence inside itself, and to that occurrence's own inner occurrence, indefinitely.
  - **Anonymous Type Rendering**: An element whose type is an inline anonymous complexType renders its content as a nested bounding box in the normal way, but with no type name in the box header and no `Type` row on the element card. The box carries a visual marker distinguishing it from a named global type. Because conformance is unenforced (§1), this marker is the **only** signal that structure remains to be promoted, so it must be readily noticeable at normal zoom rather than a subtle styling difference.
  - **Unsupported Construct Rendering**: Constructs outside R1 scope (§1) — whether deferred or permanently excluded — render as **opaque placeholder nodes** in Design View, positioned in document order where the construct occurs.
    - The placeholder names the construct (e.g. `xs:group`, `xs:union`) and any identifying attribute, so the user knows what occupies that position without switching to Text View.
    - Placeholders are read-only on the canvas. The construct remains fully editable in Text View and is preserved verbatim on save (§1, Round-trip safety).
    - **A placeholder is not an error state and must not be styled as one.** Design View now has three distinct "cannot display this" conditions that carry different meanings and must be visually distinguishable: an unsupported-construct placeholder (valid schema, outside editor scope); an unresolved reference (§2.1, red squiggly underline, genuine error); and a gap marker (§2.2, unparsed region of a malformed buffer). Only the second indicates something wrong with the schema.
  - **Abstract Type Indication**: Types declared `abstract="true"` render with a distinguishing badge or italic label on the type box. 70 such types exist in the corpus.
  - Support visual editing of element names, types, and annotations.
- **Navigation History**: The editor maintains a **back/forward history of navigation**, spanning both views, reached from toolbar controls and platform-conventional keyboard shortcuts.
  - Scope is **per tab**, matching Undo/Redo (§2.3). Each open file carries its own history.
  - The history is **unified across views**, in the same way the undo stack is. An entry records a location and the view it was in — a canvas root in Design View, a position in Text View. Moving back or forward to an entry belonging to the other view switches to that view, so the user is never returned to a location they cannot see. Where consecutive entries share a view, no switch occurs.
  - Design View entries are pushed by all six re-rooting paths (§2.2, Re-rooting).
  - Text View entries are pushed by **discrete jumps only** — a Bottom Pane entry (§2.4), a search or find-and-replace result, a go-to-line, and the landing position when a view switch or a history move arrives in Text View. Ordinary scrolling, caret movement, and typing do not push entries; a history that recorded them would be unusable.
  - **Navigation history is not Undo/Redo.** It moves the user without changing the document, and Undo/Redo changes the document without being reachable from it. They are separate stacks with separate controls. Where they touch: an undo or redo that relocates the user (§2.3, Undo/Redo Navigation) pushes a navigation entry like any other move, so Back returns to where the user was before the undo — while the edit itself is reversed only by Redo.
  - Forward entries are discarded when the user navigates somewhere new after going back, per normal convention.
- **Immediate Synchronisation**: Changes in Design View are written to the in-memory document model immediately; edits in Text View update the visual graph.
- **Malformed Buffer Handling**: When the Text View buffer is not well-formed, Design View performs a **best-effort partial render** rather than clearing the canvas or blocking the view switch.
  - The parser recovers as far as it can and renders every structure it was able to resolve. Switching views is never blocked on well-formedness.
  - Regions that could not be parsed are shown as explicit gap markers on the canvas, positioned where the unparsed content sits in document order. Unparsed content is never silently omitted — an absence must be visible as an absence.
  - A persistent banner indicates that the graph is partial and names the parse failure, with navigation to the offending location in Text View.
  - The recovered model is a **render-only projection**. It never replaces the authoritative document model, and the unparsed source text is retained verbatim for serialisation.
  - **Editing While Partial**: Successfully parsed subtrees remain fully editable. Gap markers and any structure whose boundaries could not be resolved are read-only, and reject edit operations with an explanation rather than failing silently.
    - The canvas must visually distinguish editable regions from locked ones, so the user can tell what is safe to work on without attempting an edit to find out.
    - **Document-scope operations are blocked entirely while the buffer is malformed.** Extract Global ComplexType (§2.3) writes a new type definition under the root schema, and renaming a global type (§2.3) rewrites references across the whole file — neither can be performed safely when the document's global scope has not been fully parsed, regardless of whether the originating subtree parsed cleanly.
    - Insertions adjacent to a gap marker are rejected, since the correct document-order position cannot be determined.
- **Responsiveness**: Parsing, validation, and serialisation must not block user input. The UI must remain interactive throughout, with a rendering target of 60 fps during canvas interaction.

#### 2.2.1 Rendering Type Derivation by Extension

Derivation by extension is the dominant structural pattern in the corpus — 3,646 `xs:extension` declarations inside 4,052 `xs:complexContent` blocks — so inherited content must be visible on the canvas rather than hidden behind the Attributes Pane.

- An expanded ComplexType box displays a **`Base Type | <baseTypeName>`** row in its header, mirroring the `Type` row on element cards.
- The base type renders as **its own encloser box nested inside** the derived type's box, labelled **`<baseTypeName> (extension base)`**, positioned first within the derived box.
- The base box owns its own model-group joint circle and child element cards.
- The derived type's own children hang from a **second, separate joint circle**, positioned after the base box. This ordering reflects XSD semantics, in which extension content appends after base content.
- Rendering recurses where the base type is itself derived by extension, producing nested `(extension base)` boxes to arbitrary depth.

#### 2.2.2 Rendering SimpleType Derivation

- Expanding an element whose type is a SimpleType or built-in primitive renders that type's **derivation chain** as a horizontal series of nodes, linked by plain connectors. No joint circle is drawn, as no model group is involved.
- The chain runs from the named SimpleType through each intermediate restriction base to the terminating built-in primitive (e.g. `UUID` → `uci:UniversallyUniqueIdentifierType` → `xs:string`).
- Each node in the chain carries its own expand/collapse control, allowing the chain to be revealed one link at a time.
- **Facets are not drawn on the canvas.** Selecting any node in the chain loads its facets into the Attributes Pane (§2.4).
- **Visual Type Language**: ComplexTypes render as rectangular encloser boxes; SimpleTypes and primitives render as rounded, pill-shaped nodes with a distinct type glyph. The two must be distinguishable at a glance.

### 2.3 Interactive Graph Manipulation & Canvas Actions

- **Create Global Object**: Right-clicking **empty canvas space** in Design View offers creating a new global **ComplexType**, **SimpleType**, or **top-level Element**.
  - The user is prompted for a name, validated for uniqueness against the full resolved `include`/`import` closure (§2.1) on the same rule as Extract Global ComplexType (§2.3) — `xs:include` shares a target namespace, so a locally unique name can still collide across files. A collision is rejected inline and re-prompted.
  - The new object is written to the root schema of the active file and the **canvas re-roots onto it** (§2.2, Re-rooting), so the user is placed in the thing they just made rather than having to find it.
  - It is created empty: a ComplexType with no model group yet, a SimpleType with no base or facets yet, an Element with no type yet. Populating it uses the ordinary editing paths — an element with no type carries an unresolved reference until one is assigned (§2.2, Changing an Element's Type).
  - This is the counterpart to **Delete** below, and the only path to a global object that does not begin with existing structure. Extract Global ComplexType (§2.3) promotes structure that already exists; this creates from nothing.
- **Drag & Drop**: Elements and model groups are repositioned by dragging them directly on the canvas. Two moves are supported, and they are the same gesture distinguished only by where the drop lands.
  - **Reorder within a group**: dropping between two siblings of the item's own parent model group moves it to that position. An insertion indicator shows where it will land before the drop is committed.
  - **Move to another type**: dropping onto a different type box moves the item out of its source group and into the target's model group. The move is a genuine relocation — the item is removed from the source.
  - **Dropping on a group rather than between siblings appends.** Where the drop lands on a model group or type box as a whole — anywhere in it that is not an insertion point between two existing children — the item is placed **last** among that group's children. Appending is the only position a whole-group drop can mean; an item that belongs somewhere else is then dragged into place, which is the reorder case above.
  - **The reachable set is what the canvas is already rendering.** A drop target must be an **expanded** box in the current render of the active tab. Drag and drop never re-roots, never expands anything, and never spans tabs. Moving something into a collapsed group means expanding that group first, as a separate deliberate act; moving it to a type that is not in the current render uses **Copy & Paste** (§2.3), which is also the way to *copy* rather than move — dragging has no copy variant, so the gesture has one meaning.
    - **Auto-scroll**: a target that is rendered but outside the viewport is reachable. Dragging toward a canvas edge scrolls the canvas in that direction, at a rate that increases with proximity to the edge, and stops when the pointer moves away or the drop is committed. This moves the viewport only; it changes neither the root nor any expansion state.
    - No spring-loading. Hovering a dragged item over a collapsed box does **not** expand it. Auto-expansion would put structure on screen that the user did not ask for, and would make where an item lands depend on how long the pointer lingered.
  - **Order is preserved as authored, whether or not it is significant.** Reordering is allowed inside `xs:choice` and `xs:all`, where child order carries no meaning in XSD, as well as inside `xs:sequence`, where it does. In the first case the edit changes only the document text; the editor does not prevent it, reorder it back, or warn, because the author's chosen reading order is worth keeping.
  - **Invalid drops are refused** rather than corrected, with the pointer showing a no-drop cue and no document change: dropping an item onto itself or into its own subtree; dropping onto a **collapsed** box, which shows no children and therefore offers no position; dropping into a SimpleType box; and dropping onto a recursion-bound `…` indicator (§2.2), which stands for structure that is not rendered.
  - **Dropping into a base type box** (§2.2.1) edits the base type itself, not the derived type being viewed, and so changes every type derived from it. This is accepted without a prompt or a report: the base type box is a real type rather than a read-only echo, and editing what is on screen is what the canvas is for.
  - A completed drag is one semantic operation and therefore **one undo entry** (§2.3, Undo/Redo); undoing restores both the original parent and the original position within it. An abandoned drag — released outside any valid target, or cancelled with `Esc` — writes nothing.
- **Model Group Edits**: Support adding child elements, adding sequence/choice groups, and converting a model group between choice and sequence. Reachable from context menus on the parent ComplexType box, the joint circle icon, and local element cards.
- **Copy & Paste**:
  - Copy elements or entire nested model groups, recursively including their children.
  - Paste copied items as children of a selected element or group.
  - **The clipboard spans tabs.** Content copied in one file may be pasted into any other open file.
  - Where a pasted element's type is not present in the destination's resolved `include`/`import` closure (§2.1) — which built-in datatypes always are (§2.1) — the paste still succeeds and the reference renders as unresolved per §2.1. The paste is never refused for a missing type.
  - **Pasting into a different file prompts to bring dependencies with it.** The editor computes the types the pasted subtree depends on, transitively, and offers to copy those the destination lacks.
    - Where a dependency name already exists in the destination, the user is prompted per name to **overwrite** the existing definition or **keep** it and let the pasted content reference it. Same-name types are not assumed to be the same type.
    - Declining the dependency copy leaves the references unresolved, which is a legitimate outcome — the author may intend to add an `import` instead.
    - The paste and any accompanying dependency copy are one semantic operation and therefore one undo entry (§2.3, Undo/Redo).
- **Delete**: Support deleting an element, a model group (with its subtree), or a global type definition.
  - **Deleting a global type is permitted even where it is still referenced.** References to it become unresolved and render per §2.1 — red squiggly underline, Bottom Pane entry — rather than being rewritten or deleted alongside it. Deletion never edits a reference.
  - Before deleting a referenced type the editor reports how many references exist and in which files, so the user knows the size of what they are breaking. The report informs; it does not block.
  - This is deliberately asymmetric with **Rename** below, which does rewrite references. Renaming preserves the author's intent that the references keep pointing at the same type; deleting does not, and inventing a replacement target would be a guess.
- **Rename**:
  - Renaming a global type updates the declaration and rewrites all `type=` and `base=` references **within the same file only**.
  - If the renamed type is also referenced by another currently open file, the editor warns the user, names the referencing file, and reports the reference count. It does **not** rewrite cross-file references. Cross-file coupling in the corpus is low but non-zero: 3 types are referenced across files at 31 sites.
- **Right-Click Element Shortcuts**:
  - **Toggle Optional**: Toggles `minOccurs="0"`. When cleared, the `minOccurs` attribute is removed (1 is the implied default).
  - **Toggle Unbounded**: Toggles `maxOccurs="unbounded"`. Enabling it on an element that already carries a numeric `maxOccurs` **overwrites** that value. Disabling it returns the element to `maxOccurs` 1, which is the implied default and is therefore **removed from the document** rather than written explicitly; the Attributes Pane shows it as a muted `1`. The corpus contains 388 numeric `maxOccurs` values, so the overwrite path is reachable in practice; the original numeric value is recoverable only via undo.
  - **Set Numeric maxOccurs**: Bounded numeric occurrence values are entered through the Attributes Pane, not the context menu toggle.
  - **Extract Global ComplexType**: Converts an element's inline anonymous complexType hierarchy into a globally defined `<xs:complexType>` under the root schema. The user is prompted for a name, defaulting to `<elementName>Type`, and the element's `type` attribute is rewritten to reference the new global type.
    - **This is a primary authoring path, not a repair tool.** The intended workflow is to mock up structure inline — adding elements, sequences, and choices directly under an element — and then extract that structure into a named global type once its shape has settled. It is required in R1 and is exercised by authoring, not by the reference corpus.
    - **Recursive Extraction**: Where the extracted subtree contains further nested anonymous complexTypes, all of them are promoted to named global types in a single operation.
      - **No type is ever created silently.** The user must explicitly confirm a name for every type the operation will create, including nested ones. There is no bulk-accept, no auto-naming, and no path that writes a type the user has not seen and approved.
      - Prompts are presented outermost-first, following the reading order of the canvas.
      - Sequential prompting is deliberate. A single consolidated dialog listing all pending names was considered and rejected; naming each type in canvas order keeps the user's attention on one type at a time. The accepted cost is that a deep extraction produces a corresponding number of prompts, and that cancelling late discards the naming work done so far.
      - The operation is **atomic**: cancelling at any point abandons the entire extraction and leaves the document unchanged. No partial promotion is written.
      - The whole extraction is a single semantic operation and therefore a single undo entry (§2.3, Undo/Redo).
    - **Naming and Collision**: Each name field is prefilled with `<elementName>Type` as a starting suggestion only; the prefill does not constitute confirmation. Names are validated for uniqueness against the **full resolved `include`/`import` closure**, not the active file alone, since `xs:include` shares a target namespace and a locally unique name can still collide across files. A collision is rejected inline and re-prompted, with the offending name retained in the field for editing. Extraction cannot proceed on a duplicate name.
    - The inverse operation (inlining a global type back to anonymous) is not required.
- **Undo / Redo**:
  - Scope is **per tab**; each open file maintains one independent stack.
  - The stack is **unified across both views**. Design View operations and Text View edits share a single ordered history, and it survives switching views. An operation performed in one view can be undone from the other.
  - Granularity for Design View actions is **one entry per semantic operation**. A recursive Extract Global (§2.3) is a single entry regardless of how many types it creates.
  - Text View edits are not semantic operations and are **grouped by word boundary**: a run of typing forms a single undo entry, closed by a space, tab, or newline. Undo therefore reverses a word at a time rather than a character at a time.
    - Deletions group under the same rule, so a run of backspacing is reversed as one entry.
    - Discrete operations — paste, find-and-replace, block indent — always form their own entries and never merge into an adjacent typing run.
    - Boundary-based grouping is chosen over an idle-time interval deliberately: it is deterministic and therefore testable, whereas timer-based coalescing produces different results for the same input depending on typing speed.
  - **Undo/Redo Navigation**: Undo and redo return the user to the view and location where the affected edit was made. This is distinct from Navigation History (§2.2), which moves the user without changing the document; see that requirement for how the two interact. If an edit was made in Text View and the user has since switched to Design View, undoing that edit switches back to Text View and scrolls to the affected line; the same applies in reverse for Design View operations, which recentre the canvas on the affected node. An edit is never reversed off-screen or in a view the user cannot see.
    - Where consecutive undo entries belong to the same view, the view is not switched repeatedly — the switch occurs only when the target view differs from the active one.
  - No history is discarded on a view switch. The unified stack removes the invalidation problem that separate per-view histories would otherwise create, in which undoing in one view could reach a state inconsistent with edits made in the other.
  - Redo is cleared when a new operation is performed after an undo, per normal convention.

### 2.4 Panel Layout

- **Left Panel — XSD Objects Tree**:
  - Groups: Elements (messages), ComplexTypes, SimpleTypes.
  - Search bar supporting wildcard filters (`*` for any string, `?` for any single character).
  - Clicking an entry re-roots the canvas on that object (§2.2). The tree holds no persistent selection and marks no current item.
  - Holds approximately 6,250 objects for the reference corpus and is subject to the virtualisation requirement in §5.
- **Right Panel — Attributes Pane**:
  - View and edit type properties, base types (extensions), and annotations.
  - View and edit the file's target namespace and its prefix declarations (§2.1, Namespace Handling).
  - **Complete Attribute Display**: Every attribute applicable to the selected object is listed, including those absent from the source document because they hold their implied default. The R1 defaulted set is exactly three: `minOccurs` (default `1`), `maxOccurs` (default `1`), and `abstract` (default `false`). `nillable`, `form`, `block`, and `final` are deferred per §1 — the pane must be able to take additional defaulted attributes without redesign.
  - **Default-Value Styling**: An attribute displaying its implied default renders its value in a muted style with its label in normal weight. An attribute holding a non-default value renders its value in the normal foreground style with its **label in bold**, marking it as explicitly specified rather than inherited from the default. The pair of cues lets a user distinguish effective value from provenance at a glance, without consulting the source text.
  - **Editing a Default**: Editing a muted default to a non-default value writes the attribute to the document, unmutes the value, and bolds the label. Returning it to the default value removes the attribute and reverts both cues.
  - Display foreign-namespace attributes (e.g. `uci:version`) as **read-only** metadata rows. The corpus carries 6,281 such attributes.
  - **Facet Editor** for SimpleTypes, supporting the full XSD 1.0 facet set: `length`, `minLength`, `maxLength`, `pattern`, `enumeration`, `whiteSpace`, `maxInclusive`, `maxExclusive`, `minInclusive`, `minExclusive`, `totalDigits`, `fractionDigits`.
    - The pane lists every facet applicable to the selected type's base, including those currently unset, which display an empty value.
    - Supports adding, deleting, and reordering facets.
    - Corpus usage: enumeration 7,766; pattern 144; length 68; maxLength 61; minLength 58; maxInclusive 49; minInclusive 25; whiteSpace 2; minExclusive 1.
  - **Correction to prior revision**: `minValue` and `maxValue` were listed as facets in an earlier draft. They are not XSD facets and have been removed.
- **Dependencies Tree Pane**:
  - Right-click any type to display a reverse dependency tree showing, recursively, the elements and types that use it.
  - Clicking an entry re-roots the canvas on that element or type (§2.2), so a dependency chain can be followed by successive clicks.
- **Bottom Pane — Validation Messages**:
  - Lists validation errors and warnings. Clicking an entry navigates to the corresponding element in both views — scrolling to the line in Text View, and re-rooting the canvas on it in Design View (§2.2).

---

## 3. Validation

- **Scope**: Full **XSD 1.0 schema validity** checking, not merely well-formedness and type-reference resolution.
- **No suppression rules.** Foreign-namespace attributes such as `uci:version` are legal under the XSD schema-for-schemas, which permits attributes from other namespaces on schema components via a `##other` wildcard. They validate cleanly without an import and require no special handling.
- **Cross-file resolution**: Validation operates across the resolved `include`/`import` closure, not the active file alone.
- **Triggers**: A validation pass runs on exactly three events:
  - **File load**, once the document and its `include`/`import` closure have been read.
  - **Save**, initiated after the file has been written to disk.
  - **Explicit user command**, available from the menu and the action bar.

  Validation is **not continuous**. No pass is triggered by editing, by switching views, or on a debounce timer.
- **Save Is Never Blocked**: A failing validation result does not prevent saving. The user may save a schema with any number of validation errors, and the editor does not prompt for confirmation on save. Validation reports; it does not gate.
- **Pass Granularity**: Each pass covers the full resolved `include`/`import` closure. Incremental or subtree-scoped revalidation is not required, as the infrequent trigger set makes a full pass affordable even at the §5 performance target.
- **Load-Time Behaviour**: The editor becomes interactive as soon as the document is parsed and rendered, without waiting for the load-time validation pass to complete. The file can be read, navigated, and edited while the pass runs.
  - A progress indicator is displayed for the duration of the pass, showing that validation is under way and that the Bottom Pane is not yet populated.
  - The indicator is required on load specifically, where the pass runs unprompted and its absence would otherwise be indistinguishable from a schema with no errors.
- **Background Execution**: Validation runs asynchronously and must never block user input. Editing, navigation, view switching, and saving remain fully available while a pass is in flight. Results are published to the Bottom Pane and to inline markers when the pass completes.
- **Superseded Passes**: If the document changes while a pass is running, that pass is cancelled or its results discarded. Stale results must never overwrite newer ones.
- **Result Staleness Indication**: Because validation is trigger-driven rather than continuous, displayed results can be arbitrarily out of date relative to the current buffer. The Bottom Pane must indicate both when a pass is in flight and when displayed results predate subsequent edits.
- **Marker Currency**: Text View inline validation markers (§2.2) reflect the most recent completed pass only. They do not update as the user types and are subject to the same staleness indication.
- **Inline Marker Vocabulary**: Severity is conveyed by two distinct inline markers, applied in Text View and on Design View element cards alike:
  - **Errors** — red squiggly (wave) underline.
  - **Warnings** — amber dashed underline.

  The two differ in both stroke pattern and colour, so severity remains distinguishable without colour perception, independently satisfying §5's requirement that information not rely on colour alone. Both colours must meet contrast in Light and Dark themes.
- **Cascade Suppression**: Where a single root cause produces many derived failures — most commonly an unresolvable `include` or `import` (§2.1) — the Bottom Pane reports the root cause once and summarises the dependent failures as a count, rather than listing an entry per affected reference. Inline markers still appear at every affected site. Without this rule, one missing file would fill the pane with cascading entries and bury unrelated errors.
- **Unresolvable directives**: See §2.1.

---

## 4. Formatting & Serialisation

- **Comment Preservation**: The serialiser must preserve original XML comments, whitespace formatting, namespace declarations, and unrecognised XML nodes.
- **Foreign Attribute Preservation**: Attributes in non-XSD namespaces are preserved byte-for-byte on write.
- **Character Reference Preservation**: Numeric character references (`&#x20;`, `&#x7E;`, `&#nn;`) and named entity references (`&amp;`, `&lt;`, `&quot;`) are preserved in their **original source form** on write. They are not resolved to literal characters and re-serialised.
  - This matters most in `xs:pattern` facets, where the escaped form is chosen deliberately for legibility — a literal space inside a character class such as `[a-zA-Z0-9&#x20;\-_]{1,20}` is invisible and easily corrupted. The reference corpus uses character references in **75 of its 144 patterns**.
  - Resolving and re-serialising would be semantically equivalent for a regex but would produce a large spurious diff and degrade the source's readability. Neither is acceptable.
  - Preservation applies wherever references occur, not only in patterns.
- **Ampersand Leniency**: A preprocessor escapes **raw, unescaped** ampersands inside annotation and documentation fields to `&amp;` before parsing, preventing parser failures on non-conforming schemas.
  - The preprocessor must escape only ampersands that do not already begin a valid character or entity reference. An ampersand introducing `&#x20;`, `&#nn;`, `&amp;`, `&lt;`, `&gt;`, `&quot;`, or `&apos;` is left untouched.
  - Escaping these would produce `&amp;#x20;` and corrupt the content — a failure the reference corpus would exhibit immediately across 116 character references.
  - The rule is scoped to annotation and documentation text. It does not apply to `xs:pattern` values or other attribute content.
- **Attribute Ordering**: XSD attributes are serialised in a **fixed order, configurable in Preferences**. The shipped default is `name`, `type`, `minOccurs`, `maxOccurs`; a user may reorder it, and the configured order then applies uniformly. Foreign-namespace attributes are written after all XSD attributes, preserving their relative source order.
  - **Ordering is strict and overrides preservation.** It applies to every element written on save, not only to elements the editor has modified. Where the source order differs from the configured order, it is normalised. Attribute order is therefore the one aspect of the original formatting the editor deliberately does not preserve; comments, whitespace, character references, and foreign attributes are unaffected by this rule and continue to round-trip verbatim.
  - **Consequence**: the first save of a file whose attribute order differs from the configured order produces a diff at every affected element. This is intended behaviour, and is the cost of a single canonical order. A team adopting the editor on an existing schema should expect one normalising commit, and is best served by making it a commit of its own rather than mixing it with a substantive change.
- **Implicit Defaults Rule**: Attributes holding their implied default value are never written to the document. This covers the three defaulted attributes in scope: `minOccurs` (`1`), `maxOccurs` (`1`), and `abstract` (`false`). The editor treats the default as the effective value for display and editing purposes (§2.4) while omitting it from serialised output.
  - **Round-trip note**: The corpus contains one explicit `minOccurs="1"`. Applying this rule removes it on save, producing a one-line diff against the original file. This is intended behaviour, not a defect. It is separate from, and smaller than, any diff produced by the strict ordering rule above.

---

## 5. Non-Functional & Quality Attributes

- **Delivery Form**: A **desktop application**, installed and run locally on the user's own machine. Not a web application, not a browser-hosted tool, and not mobile. This is settled, and it is the primary input to the technology-stack decision rather than an outcome of it.
- **Packaging**: Distributed as a **self-contained installable artifact per platform**. Installing or running the editor must not require the end user to obtain a runtime, SDK, interpreter, or package manager, nor to fetch anything over the network at install time or on first run. Whatever the application needs is inside the artifact.
  - Developer-side dependency management is unconstrained. Contributors may use whatever toolchain, package manager, and lockfile the chosen stack brings; the constraint applies to what reaches the end user.
  - A runtime may be **embedded** in the artifact, but never **required** of the user.
  - This is a constraint on the stack, not on the feature set: a stack that cannot produce a single self-contained installable for Windows, macOS, and Linux is disqualified, however well it suits the rest of the specification.
  - It also reinforces §2.1's offline posture. An editor that fetches dependencies on first run would not work in the environments this schema corpus lives in.
- **Performance Target**: The editor must open, render, edit, and save schemas of at least **10 MB** without perceptible lag or UI freezes. The reference corpus main file is 8.3 MB / 147,419 lines, giving roughly 20% headroom against this target.
- **Virtualisation**: Rendering must be virtualised wherever collection size scales with schema size — specifically the Text View line list, the left panel object tree (~6,250 entries), and the Attributes Pane enumeration editor (individual lists run to hundreds of values; 7,766 enumerations corpus-wide). Enumeration lists are uncapped.
- **Selective Rendering**: Design View renders only the expanded subtree beneath the current canvas root (§2.2), not the full schema graph. Because expansion state is per object and therefore shared across occurrences (§2.2, Expansion State), a single re-root can bring a large subtree into view at once; the render must stay within the §5 performance target when it does.
- **Theming**: Full Light and Dark mode support with a user override toggle. All surfaces — text editor, canvas, backgrounds, and input controls — update immediately on change.
- **Accessibility (a11y)**:
  - Colour contrast conforming to WCAG 2.1 AA.
  - Visible keyboard focus indicators on all fields and controls.
  - Keyboard shortcut navigation for common workspace operations.

---

## 6. Open Items

Not requirements. Nothing here blocks implementation, and no confirmations remain outstanding. What is left is one design-phase decision, one measurement, and a set of scope questions raised in review that have not yet been answered either way.

1. **Muted value and marker colour tokens — design phase.** §2.4 styles defaulted attribute values muted; §3 defines red squiggly and amber dashed underlines. In each case the information is carried by a non-colour cue as well (bold label, stroke pattern), so §5's colour-independence requirement is met. What remains is verifying each specific token against 4.5:1 contrast in both Light and Dark themes.
2. **Attribute-order conformance of the corpus — measurable.** §4 makes attribute ordering strict, so the size of the first normalising save depends on how much of the corpus already matches the default order. Counting the elements whose attribute order differs would turn the "expect one normalising commit" advice in §4 into a figure, and would confirm whether `name`, `type`, `minOccurs`, `maxOccurs` is the right default in the first place.

### Scope questions

These came out of a review of this specification against an independent draft. None is a defect in what is written; each is something the document is silent on. They are listed so the silence is deliberate rather than accidental — answering any of them may add a requirement, or may be closed as out of scope.

1. **Autocomplete is assumed but never required.** §2.1 uses "the resolved closure is what autocomplete offers" as part of its rationale, but no section actually requires an autocomplete affordance, in either view. Either §2.2 should require it and say what it completes over, or the rationale in §2.1 should stop leaning on it.
2. **Go-to-definition.** Navigation is specified in the outward direction — from a use to the type it names (§2.2.1, §2.2). The inward direction is covered only by the Dependencies Tree (§2.4). Whether a direct "who references this?" jump exists as its own affordance is unstated.
3. **`default` and `fixed` on elements.** §2.4 styles defaulted attribute values muted, so attribute defaults are handled. The element-level `default` and `fixed` attributes are never mentioned — neither as displayed, nor as editable, nor as excluded.
4. **Instance-document validation.** §3 covers schema validity. Validating an XML instance against the open schema is not required, and is not listed as a non-goal either; it is simply absent. It is a large feature and worth an explicit yes or no.
5. **Unused-type reporting.** The Dependencies Tree (§2.4) answers "what does this depend on" per object. Whether the editor reports globally unreferenced types across the resolved closure — useful on a 5,534-type corpus — is unstated.
6. **Product-level non-goals.** §1 excludes XSD 1.1 and defers canvas image export to R2. Other plausible expectations — schema generation from instance documents, XSLT or code generation, diffing two schemas, version control integration — are neither promised nor excluded.
7. **Who the users are.** The document specifies behaviour without stating the audience. Whether the reader is a schema author fluent in XSD or an analyst who is not changes the weight of several decisions, the Text View's prominence among them.
8. **Telemetry and network egress.** §5 settles packaging but says nothing about whether the application makes network requests at all. Given the reference corpus is a controlled interface standard, a commitment either way is worth recording before the stack is chosen — it constrains crash reporting, analytics, and update checking, all of which are ordinarily added by default.
9. **Stable requirement identifiers.** Requirements are currently addressed by section number and bolded label. Section numbers move as the document changes, which makes test cases and commit messages that cite them go stale. Whether to adopt stable IDs is a documentation decision, not a product one, but it gets more expensive the longer it waits.

### Verification gaps

These are not decisions but coverage notes for test planning. Four R1 requirements cannot be verified against the reference corpus, because the corpus contains no instance of what they handle:

| Requirement | Section | Corpus coverage |
|---|---|---|
| Extract Global ComplexType | §2.3 | 0 anonymous complexTypes |
| Create Global Object | §2.3 | authoring path, not a corpus feature |
| Ampersand preprocessor (escape path) | §4 | 0 raw ampersands |
| Unresolvable directive handling | §2.1 | all references resolve |

Each needs purpose-built test fixtures. Note that character reference preservation (§4) is the opposite case — heavily exercised by the corpus, with 116 references across 75 of 144 patterns.
