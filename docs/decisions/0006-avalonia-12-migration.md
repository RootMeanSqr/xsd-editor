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

- **The published artifact starts on `linux-x64`. That was checked by hand, and CI still cannot check it.** Every job passes on a mismatched Avalonia graph, so a green run proves the solution restores, builds and publishes trimmed on four RIDs — not that the application starts. On Linux x86-64 (Ubuntu 24.04, glibc 2.39) with .NET SDK 10.0.400, `dotnet restore`, `dotnet build` (0 warnings under `TreatWarningsAsErrors`), `dotnet test` (5 passed, 0 failed, 0 skipped) and `dotnet format --verify-no-changes` all pass. `dotnet list XsdEditor.slnx package --include-transitive` resolves `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent` and `Avalonia.FreeDesktop` to 12.1.2 and `Tmds.DBus.Protocol` to 0.94.1, with no transitive pins; neither NuGet's audit at restore nor `dotnet list package --vulnerable --include-transitive` reports an advisory. `dotnet publish src/XsdEditor.App/XsdEditor.App.csproj -c Release -r linux-x64` then produced the 36 MB single-file trimmed `xsd-editor`, and **that binary was run directly — not `dotnet run`** — under Xvfb at 1280×1024×24 with no window manager. It mapped a viewable 1280×800 top-level window titled `XSD Editor` with `WM_CLASS` `xsd-editor`, drew the centred `TextBlock` as anti-aliased text on the Fluent light background, and wrote nothing to stdout or stderr. That is the evidence that matters here, because it exercises what trimming actually threatens: the X11 backend, the Skia render pass, HarfBuzz shaping and system font resolution, and `AvaloniaXamlLoader` over `App.axaml` and `MainWindow.axaml`.
- **`win-x64`, `osx-arm64` and `osx-x64` remain unverified.** They publish; nothing was started on them. Nor was anything exercised past first paint even on `linux-x64` — no interaction, no window manager, and a virtual display rather than a real one. A green CI run on an Avalonia change is still weaker evidence than it looks, and stays so until Phase 1 adds tests and Phase 2 adds bindings.
- **Avalonia 12 introduces no new trim warnings.** The `linux-x64` publish reports exactly one, `IL2104` for `Avalonia.DesignerSupport` — the warning `XsdEditor.App.csproj` already names. Publishing `main` the same way (Avalonia 11.3.0 with the `Tmds.DBus.Protocol` 0.21.3 pin) reports that same single warning, so the set is unchanged across the major rather than merely assumed to be. `ILLinkTreatWarningsAsErrors` is deliberately `false` (`0004`), so these report rather than gate; the measurement `0004`'s Phase 2 tightening item wants now exists.
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
