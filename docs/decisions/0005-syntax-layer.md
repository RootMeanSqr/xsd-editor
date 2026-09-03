# 0005 — The syntax layer: a lossless concrete syntax tree over the raw text

**Status:** proposed — this changes a statement in `0002` and needs a decision before Phase 1 begins **Date:** 2026-09-03

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

**Read the source with a purpose-built XML lexer, and build a lossless concrete syntax tree over the raw text.** Every node owns its exact source span including trivia, so serialising an unmodified node is a copy of its original bytes, and preservation becomes the default behaviour rather than a feature layered on top. `System.Xml.Schema.XmlSchemaSet` continues to validate and resolve, exactly as `0002` intends.

Scope is what makes this affordable: this is not a general XML processor. No DTDs, no entity declarations, no XInclude — it lexes the well-formed-XML subset that XSD 1.0 uses, and its recovery story is "record a gap and resynchronise at the next plausible tag start", which `XE-031` requires of us regardless of which reader we choose.

**A plain concrete syntax tree, not Roslyn's green/red split.** The plan previously said green/red. Round-trip fidelity needs only full-fidelity nodes with spans and trivia; the green/red split buys structural sharing across versions and cheap incremental reparse, which are properties `XE-030`'s two-way synchronisation *may* eventually want. Adopting that machinery now would be paying for a performance property before measuring that we need it, which is the mistake `0004` avoided over trim warnings and `0003` avoided over the editor control. The node design keeps spans and parent links so the split can be introduced later without rewriting the model above it.

### The offset-map interaction, stated because it is where fidelity will actually break

`XE-070`'s ampersand preprocessor escapes raw `&` in annotation text **before** parsing. So spans are measured against a *patched* buffer, not the file on disk, and "serialise an unmodified node by copying its span" is only true with respect to that patched buffer.

The preprocessor therefore emits the patched text **and an edit list**, and serialisation reverses exactly the escapes it introduced. Round-trip is defined as: original bytes → preprocess → parse → serialise → un-preprocess → original bytes. The test asserts against the file on disk, so an error in the offset mapping fails visibly rather than producing a plausible-looking file.

## Consequences

- **`0002` is amended, not superseded.** Its "constructed from `XmlReader`" clause becomes "constructed by our own lexer"; everything else it says about the model and about delegating validation to the BCL stands unchanged.
- **We own an XML lexer.** Roughly the cost of the re-scanning it replaces, and it is the component `XE-031` and `XE-029` need anyway. It is also the single highest-risk piece of Phase 1, which is why the round-trip test runs against the whole reference corpus from the first commit.
- **The green/red option stays open** and is taken only if measurement asks for it.
- If this record is **rejected**, §1a of the implementation plan and everything built on it need rewriting before Phase 1 starts, which is why it is raised now rather than during it.
