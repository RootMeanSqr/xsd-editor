# 0006 — Avalonia 12

**Status:** accepted
**Date:** 2026-09-04
**Amends:** [`0002`](0002-technology-stack.md) — the Avalonia version, not the choice of Avalonia

## Context

Phase 0 pinned Avalonia at 11.3.0. That version was written by hand into
`Directory.Packages.props` from recollection rather than resolved from the feed, and it was already
a major version behind when it was committed: 12.1.2 was the current stable release on the day the
scaffolding landed. The same is true of three other pins, all corrected by the Dependabot run that
surfaced this one. The repository had no lock file and no check comparing a pin against the feed, so
nothing caught it.

Dependabot then raised the bump twice and got it wrong both times, in a way worth recording because
the failure is silent. It opened one pull request moving `Avalonia` alone, superseded it with a
second that added `Avalonia.Themes.Fluent`, and left `Avalonia.Desktop` on 11.3.0 in both, though
`Avalonia.Desktop` 12.1.2 exists on the feed. **Every CI check passed on that split.**
`CentralPackageTransitivePinningEnabled` resolves the mismatch without complaint, and
`XsdEditor.App` is a single window with no bindings, so no job in the suite could have failed. Both
pull requests were closed.

## Decision

### The three Avalonia packages move together to 12.1.2

`Avalonia`, `Avalonia.Desktop` and `Avalonia.Themes.Fluent`. The family ships in lockstep and a
partial move is a configuration nobody upstream tests.

### The `Tmds.DBus.Protocol` pin is removed, not carried forward

`0004` recorded pinning it to 0.21.3, because Avalonia 11.3.0's `Avalonia.FreeDesktop` depended on
0.21.2, which carries `GHSA-xrw6-gwf8-vvr9` (High). Avalonia 12.1.2's `Avalonia.FreeDesktop`
depends on `[0.94.1, )`, and the advisory's affected ranges are `(,0.21.3)` and `[0.22.0,0.92.0)`.
0.94.1 falls outside both, so the parent has moved past the advisory and `Directory.Packages.props`
says a pin is removed at exactly that point.

Carrying it forward would have been worse than redundant. 0.21.3 is **below** Avalonia 12's floor
of 0.94.1, so with transitive pinning enabled the pin would have forced a downgrade against a
dependency's own minimum. There are now no transitive pins at all.

Note what this implies about the split-family pull requests: they passed CI partly *because* they
were split. Leaving `Avalonia.Desktop` on 11.3.0 kept `Avalonia.FreeDesktop` on 11.3.0 and the
0.21.3 pin consistent with it. A complete bump would have surfaced the pin conflict at restore. The
incomplete change was the quieter one.

### Now, before Phase 2

Phase 1 is `XsdEditor.Core`, which carries no Avalonia reference, so this is invisible to it and the
two proceed in parallel. The reasons to take it now rather than later:

- `XsdEditor.App` is five files. Every day of Phase 2 makes this migration larger, permanently.
- Phase 2 is the first phase that writes Avalonia API surface. Migrating after it means rewriting
  code written against 11.
- Phase 3 forces it regardless. `0003` commits to AvaloniaEdit for Text View, and
  `Avalonia.AvaloniaEdit` 12.0.0 declares `Avalonia >= 12.0.0`; the 11.x line cannot satisfy it.
  Staying on 11.3.0 would run `0003`'s spike against a stack already committed to being left.

## Consequences

- **This change is not verified to run, and CI cannot verify it.** Every job passes on a mismatched
  Avalonia graph today, so a green run proves the solution restores, builds and publishes trimmed on
  four RIDs — not that the application starts. Nor was it checked by hand: the environment this was
  written in has no .NET SDK and its network policy refuses Microsoft's build host, so nothing here
  was built or launched locally. **Someone must start the published artifact on at least one
  platform before this record can be trusted as more than a resolved dependency graph.** The gap
  closes on its own as Phase 1 adds tests and Phase 2 adds bindings; until then a green CI run on an
  Avalonia change is weaker evidence than it looks.
- The application code needed no changes. It uses `AppBuilder.Configure`, `UsePlatformDetect`,
  `StartWithClassicDesktopLifetime`, `AvaloniaXamlLoader`, `IClassicDesktopStyleApplicationLifetime`
  and `<FluentTheme />` — all stable across the major. A larger surface would have made this a
  bigger record, which is the argument for doing it at five files.
- `0004`'s pinning paragraph now carries a dated note saying the condition it answered no longer
  holds. The reasoning stands; the pin does not.
- **Grouping is not enforcement.** `.github/dependabot.yml` now groups `Avalonia` and `Avalonia.*`,
  for security updates as well as version updates, so the family is the default unit of a pull
  request. A group still opens with whatever subset Dependabot resolves, and a dropped member is
  dropped silently. Until a check asserts one version across every `Avalonia*` `PackageVersion`,
  this invariant is a review responsibility rather than a build one. That check is the obvious
  follow-up and is not in this change.
- The wider lesson is the one the version table makes: a pin written from recollection is a pin
  nobody resolved. Introducing a dependency should mean asking the feed what the current version
  is, and lock files with a locked restore — already on Phase 0's carried-forward list — would make
  the drift visible rather than leaving it to a weekly bot.
