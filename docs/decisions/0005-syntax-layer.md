# 0005 — The syntax layer: our own lexer, and a green/red tree over the raw text

**Status:** accepted — this amends a statement in `0002` **Date:** 2026-09-03, accepted 2026-09-04

## Context

[`0002`](0002-technology-stack.md) settled that the editor holds **its own** schema model rather than the BCL's, because `System.Xml.Schema`'s post-compilation model drops comments, formatting and source attribute order. That decision is not in question here. What is in question is one clause inside it:

> The application builds and holds **its own editable schema model, constructed from `XmlReader`**.

That clause was written to say "not from `XmlSchemaSet`". Read literally it also picks the reader, and the reader it picks cannot meet the requirements it was chosen to serve.

The implementation plan originally recorded this as a spike ("S1: can `XmlReader` plus `IXmlLineInfo` yield exact attribute-value spans?"). It is not a measurement question. The answer follows from the documented behaviour of the API, so calling it a spike would defer a decision that can be taken now — and the rest of Phase 1 is already written on top of the answer.

## What the requirements need from the reader

- **`XE-069`** — character and entity references preserved in their **original source form**. The corpus uses them in 75 of its 144 `xs:pattern` facets, where `&#x20;` inside a character class is chosen deliberately because a literal space is invisible.
- **`XE-067`, `XE-068`** — comments, whitespace and foreign attributes byte-for-byte on write.
- **`XE-031`** — a **best-effort partial render** of a buffer that is not well-formed, with the unparsed regions identified positionally as gap markers.
- **`XE-029`** — Go to Definition must report which token sits under the pointer *and* whether it is inside a `type`, `base` or `ref` **attribute value**.

## Why `XmlReader` cannot serve them

Each of these is documented behaviour, not a limitation to be measured:

| Need | `XmlReader` |
| --- | --- |
| Original spelling of `&#x20;` | Resolves references before the value is visible. `Value` gives the resolved text. |
| Exact attribute-value extent | `IXmlLineInfo` reports a **start position only**. There is no API for a node's or an attribute value's extent. |
| Attribute value verbatim | Performs attribute-value normalisation (§3.3.3) — newlines and tabs collapse to spaces. |
| Partial parse of a malformed buffer | Throws `XmlException` and stops. There is no recovery mode. |

Positions could in principle be recovered by re-scanning the raw buffer from each reported start position — which is writing a lexer anyway, but one that runs twice and has to stay in agreement with a second parser's idea of where it is.

## Decision

**Read the source with a purpose-built XML lexer, and build a lossless concrete syntax tree over the raw text.** Every node accounts for its exact extent of source, trivia included — as a width in the green layer, resolved to an absolute span on descent through the red one (below) — so serialising an unmodified node is a copy of its original bytes, and preservation becomes the default behaviour rather than a feature layered on top. `System.Xml.Schema.XmlSchemaSet` continues to validate and resolve, exactly as `0002` intends.

Scope is what makes this affordable: this is not a general XML processor. No DTDs, no entity declarations, no XInclude — it lexes the well-formed-XML subset that XSD 1.0 uses, and its recovery story is "record a gap and resynchronise at the next plausible tag start", which `XE-031` requires of us regardless of which reader we choose.

### The tree is a full green/red split, not a plain CST

An earlier draft of this record recommended a plain concrete syntax tree — full-fidelity nodes each holding an absolute source span — on the grounds that round-trip fidelity needs nothing more and that the split should be bought only once measurement demanded it. **That is reversed here.** The split is adopted up front, because the two properties it buys are both load-bearing for requirements already accepted, and because it is the one part of the design that cannot be retrofitted cheaply: it decides what every node above it holds.

- **The green layer** is immutable and position-free. A green node stores its **width** — how many characters it and its descendants cover — and never an absolute offset. Nodes are shareable: the same green subtree may appear in many documents and at many versions.
- **The red layer** is a throwaway façade created on descent. It carries the parent link and the absolute position, computed by accumulating the widths of preceding siblings as the walk proceeds, and is discarded when the walk ends.

**What this buys.**

- **An edit costs O(depth), not O(file).** Only the nodes on the path from the edit to the root change width; every other green node is reused unchanged, and no sibling's or successor's stored position needs rewriting because none stores one. With absolute offsets, a one-character insertion at the top of the 8.3 MB corpus file invalidates the offset of every node after it. This is the property `XE-030`'s two-way synchronisation needs at corpus scale, and the property that makes `XE-087`'s Design View splice into a large buffer affordable.
- **Structural sharing makes the undo stack cheap.** `XE-043` requires one ordered history across both views, and history is naturally a list of versions. With sharing, a version is the changed spine plus pointers to everything unchanged; without it, it is either a copy of the document or a reverse-diff to be replayed.

**What this costs.** Two node kinds instead of one, and the discipline of keeping them separate for the life of the codebase — the green layer must never learn its absolute position, because the moment one node caches one, sharing is unsound. That is a real, permanent tax, accepted knowingly.

### Two invariants, recorded now because they are expensive to retrofit

**Width is measured in UTF-16 code units.** `src/XsdEditor.Core/SourceSpan.cs` already indexes with `int` into a `ReadOnlySpan<char>`, so this follows from a decision already committed. Not bytes, not Unicode scalar values: a CRLF is width 2, and a non-BMP character in an annotation is width 2 because it is a surrogate pair. Mixing units produces spans that are wrong *only* in files containing non-ASCII — which is the worst failure mode available, since the ASCII tests all pass.

**A node's width equals the sum of its children's widths.** Every character of the source belongs to exactly one node, so whatever trivia rule is chosen — a token owns its leading whitespace, a tag closer owns the trailing newline, or some other consistent assignment — the sum must hold. This, not the presence of spans, is what makes the tree lossless: it is the statement that no character is claimed twice and none is dropped. It is a property test over the corpus from the first commit, not a comment.

### The offset-map interaction, stated because it is where fidelity will actually break

> **Superseded by [`0007`](0007-no-ampersand-preprocessor.md).** The premise below — that `XE-070`'s preprocessor runs before our parse — was inherited from a requirement written when the reader was assumed to be `XmlReader`. This record is what removed that assumption: our lexer treats a raw `&` as ordinary text, so there is no parse failure to prevent. There is now no preprocessor at any stage: a raw ampersand is an error wherever it appears, the editor escapes what it writes, the tree is built over the original file bytes, and round trip is an identity with no offset map in it. The reasoning below still stands as an argument for widths, and would apply again if escaping ever moved back before the parse.

`XE-070`'s ampersand preprocessor escapes raw `&` in annotation text **before** parsing. So spans are measured against a *patched* buffer, not the file on disk, and "serialise an unmodified node by copying its span" is only true with respect to that patched buffer.

The preprocessor therefore emits the patched text **and an edit list**, and serialisation reverses exactly the escapes it introduced. Round-trip is defined as: original bytes → preprocess → parse → serialise → un-preprocess → original bytes. The test asserts against the file on disk, so an error in the offset mapping fails visibly rather than producing a plausible-looking file.

Widths make this interaction smaller than it would otherwise be. Each escape widens exactly the annotation text node that contains it, and the correction propagates only up that node's ancestors — it does not shift the position of anything after it, because nothing after it stores a position. Under absolute offsets, every offset in the remainder of the file would need mapping.

## Consequences

- **`0002` is amended, not superseded.** Its "constructed from `XmlReader`" clause becomes "constructed by our own lexer"; everything else it says about the model and about delegating validation to the BCL stands unchanged.
- **We own an XML lexer.** Roughly the cost of the re-scanning it replaces, and it is the component `XE-031` and `XE-029` need anyway. It is also the single highest-risk piece of Phase 1, which is why the round-trip test runs against the whole reference corpus from the first commit.
- **The width-sum invariant is a test, from the first commit.** It is what turns "lossless" from an intention into something CI can fail on.
- **Nothing above the syntax layer sees green nodes.** The schema model, the index and the command layer address the tree through red nodes, so the sharing discipline stays inside one component rather than becoming a rule every contributor must know.
- **Formatting is a separate concern from fidelity.** The tree records exactly what the source says; what *new* text should look like is `XE-084`–`XE-087` in the requirements, and the formatter that answers it sits above this layer rather than inside it.
