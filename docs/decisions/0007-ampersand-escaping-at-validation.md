# 0007 — Ampersand escaping happens at validation, not before parsing

**Status:** accepted
**Date:** 2026-09-04
**Amends:** [`0005`](0005-syntax-layer.md) — where the preprocessor sits, not whether it exists
**Refines:** `XE-070`

## Context

`XE-070` requires a preprocessor that escapes **raw, unescaped** ampersands in annotation and documentation text to `&amp;`, and states its purpose as "preventing parser failures on non-conforming schemas". [`0005`](0005-syntax-layer.md) took that at its word and placed the preprocessor before our own parse, with three consequences it recorded under "the offset-map interaction, stated because it is where fidelity will actually break": spans are measured against a patched buffer rather than the file on disk, the preprocessor emits an edit list alongside the patched text, and round trip is defined as original bytes → preprocess → parse → serialise → un-preprocess → original bytes.

Implementing the syntax layer showed that the premise no longer holds for our parser. `0005` replaced `XmlReader` with our own lexer, and that lexer has no failure mode to prevent: a raw `&` is lexed as an ordinary character of a text token, exactly like any other character. Nothing in the parse path rejects it, so escaping before the parse prevents a failure that cannot occur.

**The requirement is not obsolete, though, and it is worth being precise about why.** A raw `&` is a *well-formedness* error, not a validation-semantics one. `System.Xml.Schema.XmlSchemaSet` reads through `XmlReader`, which throws on it before any question of schema validity is reached — so a non-conforming schema does not validate badly, it fails to be read at all. That is a different thing from `XE-069`'s character references: `&#x20;` and `&amp;` are already well-formed, the preprocessor is explicitly forbidden from touching them, and no reader has ever had a problem with them.

## Decision

**The preprocessor moves out of the parse path and runs only where text is handed to `XmlSchemaSet`.**

- **The syntax tree is built over the original file bytes.** Spans index the file on disk, not a patched derivative of it.
- **Round trip becomes an identity**: original bytes → parse → serialise → original bytes. There is no preprocess or un-preprocess step in it, and no offset map anywhere in the fidelity path.
- **The patched buffer and its edit list live inside validation** ([`1f`](../implementation-plan.md)), which is the only consumer that needs them. `XmlSchemaException` positions are mapped back to original offsets through that edit list, the same way they would have been mapped through the global one.

`XE-070`'s substance is unchanged: only ampersands that do not already begin a valid character or entity reference are escaped, and the rule stays scoped to annotation and documentation text rather than `xs:pattern` values or other attribute content. What changes is when it runs and what it is measured against.

### The syntax layer tolerates a raw ampersand in annotation text, and reports it everywhere else

The parse path no longer escapes anything, but it is still the layer that decides what to *say* about a raw `&`, and `XE-070`'s scope answers that directly. Inside `xs:annotation`, `xs:documentation` and `xs:appinfo` — and anything nested below them, since `xs:documentation` takes arbitrary markup — a raw `&` is accepted silently. Anywhere else it is reported as `RawAmpersand`, one diagnostic per occurrence with a span covering the ampersand alone, so a caret can be put on it.

Attribute content stays strict **even inside an annotation subtree**, because `XE-070` says in terms that the rule does not reach it. So `<xs:documentation><b title="R&D">` reports, while `<xs:documentation>R&D</xs:documentation>` does not.

Two consequences of `0005`'s scope fall out of this. There is no DTD and so no entity declaration to consult, which makes `&notdeclared;` a raw ampersand rather than a reference we cannot resolve. And an `&` inside a CDATA section is neither: nothing in there is markup, so there is nothing to escape and nothing to report.

**A reference to a code point XML forbids is a separate diagnostic, and the leniency does not cover it.** `&#0;`, `&#xD800;` and `&#x110000;` are well formed as syntax and rejected by every conforming reader — `XmlReader` raises "is an invalid character" on each — so they are reported as `InvalidCharacterReference` wherever they appear, annotation text included. The distinction is what an escaping pass could repair: a raw `&` becomes `&amp;` and the document is readable, whereas a reference naming a surrogate or a control character is not rescuable by anything downstream. The accepted set is XML 1.0's `Char` production, and the boundaries either side of it are tests rather than a comment.

The rule the syntax layer follows here is simply **if a conforming reader would reject it, we say so** — checked against `XmlReader` directly rather than reasoned about, for raw ampersands, undeclared entities and forbidden code points alike.

**The element test is on local name, not on a resolved namespace**, because namespace resolution belongs to the schema model and the syntax layer sees only prefixes. That makes it slightly generous — a foreign element that happens to be called `documentation` gets the same tolerance — which is the right way round: the cost is a raw ampersand going unreported, never a valid document being called malformed. The schema model can tighten it once it knows the namespaces.

## Consequences

- **`0005`'s "offset-map interaction" section no longer describes the system.** It is superseded by this record rather than deleted, because the reasoning in it — that widths make the correction propagate up one node's ancestors instead of shifting every offset after it — remains the argument for widths, and would apply again if escaping ever moved back before the parse.
- **The fidelity claim gets stronger and simpler.** "Serialising an unmodified node copies its span" is now true of the file on disk with no qualification, so the round-trip test asserts against the bytes a user can see in another editor. Under `0005` it was true only with respect to a buffer that never existed on disk, and an error in the offset mapping could produce a plausible-looking file.
- **Nothing in the syntax layer needs the preprocessor to exist yet.** It is `1f`'s to build, and no work before then is blocked on it. What the syntax layer does carry is the diagnostic, which is the half of `XE-070` the editor needs in order to open a non-conforming file and say what is wrong with it.
- **Whether we need the escaping at all is still open, but the corpus no longer argues for it.** Tolerating a raw ampersand in annotation text is settled and implemented; what `1f` decides is whether to *rewrite* it before handing the text to `XmlSchemaSet`.

  Without a preprocessor, a schema containing a raw `&` parses, renders and edits normally — our lexer is lenient — and simply reports that it could not be validated. That is honest about a non-conforming file rather than silently validating a document the user does not have; the cost is that such a schema can never be validated at all. **The reference corpus contains 119 ampersands and not one of them is raw**: every occurrence already opens a valid character or entity reference, across all three files (115 in `UCI_MessageDefinitions`, 4 in `UCI_SecurityMarkings`, 0 in `UCI_Versioning`). So the corpus exercises `XE-069`, which the preprocessor is forbidden to touch, and provides no instance of the case `XE-070` exists for. `1f` still decides, but it decides knowing that the motivating input is absent from the only real-world sample we have, and that a preprocessor built for it would ship untested by the corpus.
- **This came out of building the thing, not out of reviewing the plan.** `0005` reasoned correctly from `XE-070`'s stated purpose; the purpose was written when the reader was assumed to be `XmlReader`, and `0005` itself is what removed that assumption. It is a good argument for taking migrations and foundations early, while the record and the code are still close enough together for the contradiction to be visible.
