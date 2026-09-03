# Measurements

Recorded numbers, kept under version control so a regression is visible as a diff.

Each phase that measures something writes its table here rather than into a pull request
description, because `XE-076`'s targets and `0003`'s gates are properties that regress silently as
features accumulate — the point is to be able to compare a run against the last one.

| File | Contents |
| --- | --- |
| `phase-1-timings.md` | Parse, serialise, index build and validation timings against the reference corpus |
| `spike-avaloniaedit.md` | The `0003` G1–G7 gate results, which close that decision record |

Runs against the reference corpus are only meaningful with `XSDEDITOR_CORPUS` set; record the
machine specification alongside the numbers, since `0003`'s gates are defined against the lowest
specification we intend to support.
