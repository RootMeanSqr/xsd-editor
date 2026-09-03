# Reference corpus: pinned location and verified figures

The corpus is the UCI v2.5 schema set from OAC-STD-002 Rev E. It is **not committed** — it is a controlled interface standard and this is a public repository ([`../decisions/0004-build-and-security-tooling.md`](../decisions/0004-build-and-security-tooling.md)) — so this file records what it is, where it is, and what it should measure, which is what makes a change to it detectable.

## Pin

Source: `gitlab.com/open-arsenal/uci/standard`, path `03_OAC-STD-002_RevE_UCI_Schema_v2_5`, pinned at commit **`6d5af178bcd3b18b3a102bede101dea0af9a0ad5`** (2026-03-02).

The pin is deliberate. Tracking `main` would mean an upstream edit to the standard arriving as an unexplained round-trip failure in our parser tests; pinned, any diff is our regression. Bumping it is a reviewed commit that updates the checksums below in the same change.

| File | Bytes | Lines | SHA-256 |
| --- | ---: | ---: | --- |
| `UCI_Versioning_v2_5_0.xsd` (entry point) | 2,131 | 32 | `644f9c776c4e50c144b758e5daf83f069dc8ddc40a507e25cb033d5e65459b2e` |
| `UCI_MessageDefinitions_v2_5_0.xsd` | 8,287,146 | 147,419 | `ac9430499e1107371345e04430895c8c9f18578c1a6b022958ca43ae8aa7bf27` |
| `UCI_SecurityMarkings_v2_5_0.xsd` | 343,221 | 8,588 | `4a8056f1503234d423a0c2495844ab65387febced62bea58b87d4f343e9e7a0b` |

The fetch URLs themselves live in the `XSDEDITOR_CORPUS` repository variable and are deliberately not duplicated here: one operative copy means one place to update when the pin moves. What this file records is what the variable should resolve *to* — the pin and the checksums — which is what makes a wrong or drifted value detectable rather than silent.

## Closure shape

`UCI_Versioning` `xs:import`s `UCI_MessageDefinitions` (a *different* target namespace), which `xs:include`s `UCI_SecurityMarkings` (the *same* target namespace). Nothing references Versioning, so it is the unique root: rooting the closure there reaches all three files and exercises both directive kinds, which is why it is the entry point rather than the much larger MessageDefinitions.

## Encoding

**All three files are wholly CRLF**, tab-indented, with no byte-order mark. Not one bare LF appears anywhere in the set. This is load-bearing for `XE-067` and `XE-083`: a serialiser emitting `\n` produces a diff on every one of 147,419 lines, and — the trap — a serialiser hard-coding `\r\n` passes every corpus test while still being wrong. LF and mixed-ending fixtures are therefore purpose-built (§6).

## Figures

Measured by regular expression over the three files, which is **not** a parser: nesting, comments and CDATA are invisible to it. Phase 1 re-derives these through the real model, and the difference between the two is itself worth looking at.

Exact, because a test can fail on them:

| Figure | Value | Why it must stay exact |
| --- | ---: | --- |
| Lines in `UCI_MessageDefinitions` | 147,419 | `0003`'s G1–G5 gates are defined against this file |
| Bytes in `UCI_MessageDefinitions` | 8,287,146 | as above; ~8.3 MB against `XE-076`'s 10 MB target |
| Elements carrying 2+ attributes, all conforming to the default order | 19,377 | `XE-071`'s falsifiable claim that adoption produces no normalising diff |
| Explicit `minOccurs="1"` | 1 | `XE-072` predicts exactly one line of diff on save |
| Anonymous complexTypes | 0 | `XE-006`'s purity claim; why Extract Global has no corpus coverage |
| Raw unescaped ampersands | 0 | why `XE-070`'s escape path has no corpus coverage |
| Bare LF line endings | 0 | why `XE-083`'s LF path has no corpus coverage |
| `xs:pattern` facets | 144 | denominator of the character-reference measurement |
| Patterns containing character references | 75 | `XE-069`'s justification |

Magnitudes, deliberately approximate — nothing depends on the digits, and a precise number nobody can re-derive rots:

| Figure | Approximately |
| --- | ---: |
| Named complexTypes | 4,600 |
| Named simpleTypes | 950 |
| Named types, total | 5,550 |
| Top-level elements | 722 |
| `abstract="true"` types | 70 |
| `xs:complexContent` blocks | 2,000 |
| `xs:extension` declarations | 2,000 |
| `xs:enumeration` values | 7,750 |
| Foreign-namespace (`uci:`) attributes | 6,280 |
| Numeric `maxOccurs` values | 400 |

### Corrections to earlier figures

Two figures previously in `requirements.md` were artifacts of counting tags rather than elements, and are corrected above:

- **`xs:complexContent` "4,052"** is exactly 2 × 2,026 — every block counted once opening and once closing.
- **`xs:extension` "3,646"** is the same artifact: 2,026 opening tags plus 1,620 closing ones, the difference being the 406 that are self-closing.

**"5,534 named types"** was `UCI_MessageDefinitions` alone (4,607 + 927, which reproduces exactly). Across all three files it is 5,558. The figure was right about the file it measured and wrong about the set it named.

None of the load-bearing figures was affected — all nine reproduce exactly — which is the argument for keeping precision only where a test spends it.
