# 0007 — No ampersand preprocessor: strict everywhere, escaped on input

**Status:** accepted
**Date:** 2026-09-04
**Amends:** [`0005`](0005-syntax-layer.md) — supersedes its offset-map section
**Rewrites:** `XE-070` · **Adds:** `XE-088`

## Context

`XE-070` originally required a preprocessor that escaped **raw, unescaped** ampersands in annotation and documentation text to `&amp;`, and stated its purpose as "preventing parser failures on non-conforming schemas". [`0005`](0005-syntax-layer.md) took that at its word and placed the preprocessor before our own parse, recording the consequences under "the offset-map interaction, stated because it is where fidelity will actually break": spans measured against a patched buffer rather than the file on disk, an edit list emitted alongside the patched text, and round trip defined as original bytes → preprocess → parse → serialise → un-preprocess → original bytes.

Implementing the syntax layer showed the premise had already gone. `0005` replaced `XmlReader` with our own lexer, and that lexer has no failure mode to prevent: a raw `&` is lexed as an ordinary character of a text token. Escaping before the parse prevents a failure that cannot occur.

That left the question of what the leniency was *for*. A first pass kept it, tolerating a raw `&` inside `xs:annotation`, `xs:documentation` and `xs:appinfo` and reporting it everywhere else. Two things argued against keeping it. **The reference corpus contains 119 ampersands and not one is raw** — every occurrence already opens a valid reference — so the case the tolerance existed for is absent from the only real-world sample we have. And a reader that is lenient where the format is not does the user no favours: it accepts a file that every other tool, `XmlSchemaSet` included, will reject, and defers the discovery to whoever receives it.

## Decision

**There is no preprocessor, at any stage. A raw ampersand is a well-formedness error wherever it appears, and the editor escapes what it writes so that it never creates one.**

### The parser is strict, with no exception for annotation text

A raw `&` — one opening no valid character or entity reference — is reported as `RawAmpersand` in element text and in attribute values alike, inside an annotation or anywhere else. A numeric reference whose syntax is well formed but whose code point XML forbids is reported separately as `InvalidCharacterReference`: it is not repairable by escaping, so it is not the same finding.

The rule is **if a conforming reader rejects it, so do we**, and that is a test rather than a claim — `XmlReaderParityTests` runs the same buffers through `XmlReader` and through our parser and asserts the two agree.

Being strict costs the user nothing they had. Parsing still recovers rather than throwing (`XE-031`), so the file still opens and renders, and a failing result still never gates a save (`XE-057`). The editor reports; it does not refuse.

### The escaping moves to the editor's write path — `XE-088`

Any text the editor writes into the document has `&` escaped to `&amp;`, and the escape is reversed on display. The user types and reads `R&D`; the file holds `R&amp;D`. It is stated as a property of the write path rather than per-field, so a field added in a later phase inherits it instead of having to remember it.

What the editor is *preserving* rather than writing is untouched: an unedited annotation is copied verbatim (`XE-067`), raw ampersand and all, and reported rather than silently repaired. Rewriting it on save would edit a document the user did not ask to change.

### `XE-069` is unaffected

Character references are preserved in their original spelling and are never escaped, resolved or normalised. `&#x20;` in a `xs:pattern` facet stays six characters. This is the requirement the preprocessor was always forbidden to touch, and nothing here changes it.

## Consequences

- **`0005`'s "offset-map interaction" section no longer describes the system**, and is marked superseded rather than deleted: its reasoning — that widths make a correction propagate up one node's ancestors instead of shifting every offset after it — remains the argument for widths, and would apply again if escaping ever moved back before the parse.
- **The tree is built over the original file bytes and round trip is an identity.** No patched buffer, no offset map, nowhere in the fidelity path. "Serialising an unmodified node copies its span" is now true of the file on disk with no qualification, so the round-trip test asserts against the bytes a user can see in another editor.
- **A non-conforming schema now reports an error on open** where the first pass accepted it silently. That is the intended change and the visible one. It still opens, renders and saves.
- **The local-name matching this record previously flagged for the schema model to tighten is gone with the leniency.** Deciding whether an element was annotation text meant matching `documentation` on local name alone, because namespace resolution belongs to the schema model and the syntax layer sees only prefixes. That approximation, and the follow-up to remove it, both disappear.
- **Phase 1f owes nothing here.** It was going to decide whether to build the preprocessor; there is nothing left to decide, and no work before it is blocked.
- **This came out of building the thing, not out of reviewing the plan.** `0005` reasoned correctly from `XE-070`'s stated purpose; that purpose was written when the reader was assumed to be `XmlReader`, and `0005` is itself what removed the assumption. It is an argument for taking foundations early, while the record and the code are close enough together for the contradiction to be visible.
