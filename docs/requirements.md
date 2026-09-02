# Requirements — xsd-editor

**Status:** draft. Written to give the project a shape to argue with, not to
freeze anything. Every section marked _(assumption)_ is a guess that needs your
confirmation, and the [Open questions](#open-questions) list at the end
collects the decisions still to be made. Answering those is the next step;
choosing a technology stack comes after, not before.

---

## 1. Problem

XSD files are the contract between systems that exchange XML, and they are
edited far more often than they are written from scratch. The tools available
to do that split badly:

- **Plain text editors** show the file exactly as it is, but give no help with
  the structure. Following a type reference across an `xs:import` means
  grepping, and a broken schema is only discovered when something downstream
  fails to validate.
- **Heavyweight IDEs and commercial suites** understand schemas well, but are
  large, often licensed per seat, and treat the schema as one view inside a
  much bigger product.

The gap is a focused editor that understands XSD semantics — types,
references, namespaces, and the imports that tie files together — while
keeping the file itself readable and diff-friendly.

## 2. Goals

1. Open, navigate, and understand an existing schema, including multi-file
   schemas connected by `xs:include` and `xs:import`.
2. Make structural edits (add or change elements, attributes, types,
   cardinality, documentation) without hand-writing XML.
3. Report schema errors as they occur, at the location that caused them, in
   language that says how to fix them.
4. Preserve everything the editor does not deliberately change: formatting,
   comment placement, attribute order, entity usage. A round trip through the
   editor with no edits must produce a byte-identical file.

## 3. Non-goals

Deliberately out of scope for the first release, and listed so scope creep is
visible when it happens:

- Authoring or editing XML instance documents beyond validating them against
  the open schema.
- Editing other schema languages (RELAX NG, Schematron, DTD, JSON Schema).
- Code generation (XSD to classes/bindings) and schema-to-schema mapping.
- Real-time multi-user collaborative editing.
- Visual/graphical schema diagrams as the primary editing surface. _(A
  read-only structural view is in scope; a full drag-and-drop diagram editor is
  not — see the open questions.)_

## 4. Users _(assumption)_

- **Integration developers** who maintain schemas describing message formats
  between systems, and mostly make small, careful changes to large existing
  files.
- **Data and standards analysts** who read schemas more than they write them,
  and need to answer "what does this field mean and where is it used".

The primary user is assumed to be comfortable with XML but not to have the full
XSD specification memorised. The editor should make the schema's structure
legible to that person rather than assuming expertise.

## 5. Functional requirements

Requirements are labelled `FR-n` for reference in issues and PRs. Priority is
**MUST** (first release), **SHOULD** (first release if affordable), or **LATER**.

### Opening and navigating

| ID    | Requirement                                                                                                          | Priority |
| ----- | -------------------------------------------------------------------------------------------------------------------- | -------- |
| FR-1  | Open a `.xsd` file and display both its source text and a navigable tree of its declarations.                          | MUST     |
| FR-2  | Resolve `xs:include`, `xs:import`, and `xs:redefine`/`xs:override` and present the referenced files as part of one schema set. | MUST     |
| FR-3  | Jump from a type or element reference to its declaration, across files.                                                | MUST     |
| FR-4  | Show all usages of a named type, element, attribute, or group ("find references").                                     | SHOULD   |
| FR-5  | Search declarations by name, with namespace-aware matching.                                                            | SHOULD   |
| FR-6  | Show the effective content model of a complex type, including inherited particles from `xs:extension`/`xs:restriction`.| SHOULD   |
| FR-7  | Handle schemas of at least a few thousand declarations across dozens of files without noticeable lag. _(assumption — see open questions)_ | SHOULD |

### Editing

| ID    | Requirement                                                                                                          | Priority |
| ----- | -------------------------------------------------------------------------------------------------------------------- | -------- |
| FR-8  | Edit the schema as text, with XSD-aware completion for element names, attributes, type references, and facets.          | MUST     |
| FR-9  | Add, rename, and delete declarations (element, attribute, complexType, simpleType, group, attributeGroup) through the structural view. | MUST |
| FR-10 | Edit cardinality (`minOccurs`/`maxOccurs`), nillability, default and fixed values, and `use` on attributes.             | MUST     |
| FR-11 | Edit `xs:annotation`/`xs:documentation` content as prose, not raw markup.                                              | SHOULD   |
| FR-12 | Rename a declaration and update every reference to it across the schema set in one operation.                          | SHOULD   |
| FR-13 | Edit simple type facets (`enumeration`, `pattern`, `minLength`, numeric bounds, …) with facet-appropriate input.        | SHOULD   |
| FR-14 | Manage namespace prefix declarations and the target namespace.                                                        | SHOULD   |
| FR-15 | Undo and redo every operation, including multi-file ones such as a rename.                                            | MUST     |
| FR-16 | Extract an inline anonymous type into a named global type, and inline a named type back. _(refactoring)_               | LATER    |

### Validating

| ID    | Requirement                                                                                                          | Priority |
| ----- | -------------------------------------------------------------------------------------------------------------------- | -------- |
| FR-17 | Check the schema against the XSD specification and report violations with file, line, and column.                      | MUST     |
| FR-18 | Report unresolved references and unreachable/unused global declarations as distinct diagnostics.                       | MUST     |
| FR-19 | Validate an XML instance document against the open schema and report failures against the schema location responsible. | SHOULD   |
| FR-20 | Re-validate incrementally as the user types, without blocking editing.                                                | SHOULD   |
| FR-21 | Generate a skeleton XML instance document from the schema, for eyeballing what it describes.                           | LATER    |
| FR-22 | Diff two versions of a schema semantically (what changed for consumers), not just textually.                           | LATER    |

### Files

| ID    | Requirement                                                                                                          | Priority |
| ----- | -------------------------------------------------------------------------------------------------------------------- | -------- |
| FR-23 | Save changes back to the original files, preserving untouched formatting byte-for-byte (see goal 4).                   | MUST     |
| FR-24 | Warn on save when a file has changed on disk since it was opened.                                                     | MUST     |
| FR-25 | Never modify a file the user did not edit, even when a multi-file operation touches it — changes are explicit and reviewable. | MUST |

## 6. Non-functional requirements

| ID    | Requirement                                                                                                       |
| ----- | ----------------------------------------------------------------------------------------------------------------- |
| NFR-1 | **Fidelity.** Open-and-save with no edits is byte-identical. This constrains the parser: a plain DOM round trip will not satisfy it, so the schema model must retain source positions and trivia. |
| NFR-2 | **Responsiveness.** Typing stays smooth while validation runs; validation and reference resolution happen off the interactive path. |
| NFR-3 | **Offline.** Everything works with no network access. Remote `schemaLocation` URLs are resolved only if the user allows it, and are cached locally. |
| NFR-4 | **Privacy.** Schemas are frequently confidential. No telemetry, no uploading of file contents anywhere, by default and preferably at all. |
| NFR-5 | **Portability.** Runs on Linux, macOS, and Windows. _(assumption)_ |
| NFR-6 | **Diff-friendly output.** Generated markup follows the surrounding file's existing conventions so that commits show only the semantic change. |
| NFR-7 | **Accessibility.** Keyboard-operable throughout; the structural view is usable with a screen reader. |

## 7. Standards scope _(assumption)_

- XSD 1.0 support is required.
- XSD 1.1 (`xs:assert`, `xs:alternative`, open content, conditional type
  assignment) is desirable but may be deferred; the model should not be
  designed in a way that makes it impossible to add.
- Well-known related vocabularies (`xml:base`, `xml:lang`, `xsi:*`) are
  recognised, not editable as schema.

## 8. Success criteria

The first release is good enough when a user can:

1. Open a real multi-file schema they did not write and answer "where is this
   type defined and who uses it" faster than with grep.
2. Add an optional element to an existing complex type, save, and see a commit
   diff containing only that change.
3. Be told about a broken reference while typing, rather than after running an
   external validator.

## 9. Open questions

These block or shape the stack decision, so they are worth settling first. My
recommendation is given where I have one, but these are yours to make.

1. **Delivery form.** Desktop application, web application, VS Code extension,
   or a language server plus a thin client? This is the single biggest fork in
   the road and drives almost everything else. _(A language server with an
   editor-agnostic core is the most reusable, and the most work up front.)_
2. **Primary editing surface.** Is the text the source of truth with structure
   as a view onto it, or is the structure primary with text generated from it?
   NFR-1 pushes strongly toward text-as-truth.
3. **Parser.** Adopt an existing XML parser that preserves trivia, or write a
   lossless syntax layer? Related: is there an existing XSD object model worth
   building on in the candidate stack?
4. **Scale target.** What is the largest schema that must feel fast? FR-7 is a
   guess. A real example file from your own work would be worth more than the
   number.
5. **Single file or workspace?** Does the editor open one schema and follow its
   imports, or does it index a whole directory or repository?
6. **Graphical view.** Read-only structure view only, or eventually a diagram
   people edit directly? Affects how much the model must support layout.
7. **Instance documents.** Is validating XML against the schema (FR-19) part of
   the first release, or a follow-up?
8. **Users.** Section 4 is invented. Who is this actually for — you, a team, or
   a public release?
9. **Licence.** Not chosen. Determines whether GPL-licensed libraries are
   available and whether this is public at all.

---

_Once these are answered, the next artefacts are a stack decision record in
`docs/decisions/` and a first milestone cut from the MUST rows above._
