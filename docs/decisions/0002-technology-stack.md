# 0002 — Technology stack: .NET and Avalonia

**Status:** accepted
**Date:** 2026-09-03

## Context

The stack was the last item left open by the requirements, and it is deliberately the last one
taken: the delivery form, the packaging constraint, the no-egress rule, the performance targets and
the licence are all inputs to it rather than outcomes of it.

The binding constraints, all already settled:

- **`XE-073`** — a desktop application on Windows, macOS and Linux. Not web, not mobile.
- **`XE-074`** — a self-contained installable per platform. The end user obtains no runtime, SDK or
  package manager, and fetches nothing at install time or on first run. Linkage is unconstrained.
- **`XE-075`** — no outbound network requests at all, including telemetry and crash reporting.
- **`XE-076`**, **`XE-077`**, **`XE-078`** — 10 MB schemas edited without perceptible lag, with
  virtualised lists and a selectively rendered canvas.
- **`XE-079`**, **`XE-080`** — light and dark theming with immediate switching, and accessibility.
- **`XE-081`** — no dependency with a known unfixed vulnerability, and static analysis over our own
  code, both gating merge.
- [`0001-licensing.md`](0001-licensing.md) — Apache-2.0 outbound, which leaves the field wide.

Three candidates came out of `0001` as licence-viable: **.NET with Avalonia**, **JavaFX on
OpenJDK**, and **Qt**. The choice therefore turns on toolkit fit, packaging maturity and ecosystem,
as that record said it should.

**The decisive consideration is that Design View is a bespoke drawing surface, not a form.** Cards,
joint circles, branch connectors and — since `XE-082` — semantic line styles are all drawn by us.
A toolkit is a good fit here to the extent that custom rendering is a first-class activity rather
than an escape hatch, and to the extent that what we draw looks identical on three platforms,
because line style now carries meaning that a platform-specific renderer could distort.

## Decision

### The platform is .NET with Avalonia, written in C#

- **Custom rendering is first-class.** Avalonia is a retained-mode framework over a single Skia
  backend, so a custom control draws into the same scene graph as everything else, and a dashed
  connector is dashed identically on all three platforms. Qt matches this; JavaFX is close. The tie
  is broken below.
- **One codebase, one renderer, three platforms.** No per-platform UI layer and no per-platform
  drawing differences to reconcile — which matters more here than in a forms application, for the
  reason above.
- **Packaging maturity settles it.** `dotnet publish` produces a self-contained per-platform
  artifact with the runtime embedded — `XE-074` satisfied by the first-party toolchain and no
  third-party bundler. JavaFX reaches the same place through `jlink` and `jpackage` — workable,
  with more moving parts. Qt needs `windeployqt` / `macdeployqt` / `linuxdeployqt` plus the LGPL
  dynamic-linking and relink discipline from `0001`, which is a permanent obligation on every
  release rather than a one-time setup cost.
- **Virtualisation is built in** (`XE-077`), so the object tree, the Text View line list and the
  enumeration editor do not each need a hand-written windowing layer.
- **Theming is built in** (`XE-079`): the Fluent themes ship light and dark variants with runtime
  switching, and control theming is the mechanism we would use for our own surfaces anyway.
- **Licensing is the simplest of the three.** Avalonia and the .NET runtime are both MIT. No LGPL
  relink obligation, and no `NOTICE` propagation beyond what any dependency brings.
- **No egress by default** (`XE-075`). Neither Avalonia nor the .NET runtime makes outbound
  requests on its own; .NET's telemetry is in the *CLI*, at build time, and never reaches the
  artifact. This is to be verified against the shipped build rather than assumed, but nothing in
  the stack has to be switched off to comply.

**Risks accepted, recorded so they are not rediscovered as surprises:**

- **Accessibility (`XE-080`) is the weakest area.** Avalonia exposes automation peers on all three
  platforms, but the depth is below that of a native toolkit, and a custom-drawn canvas is a
  surface whose accessibility we own outright — no toolkit would have given us that for free. This
  needs early prototyping rather than late remediation.
- **Avalonia is a smaller ecosystem** than Qt or WPF, and effectively single-vendor. Third-party
  controls are fewer, which pushes toward building rather than buying — acceptable given `XE-081`
  wants a small dependency surface anyway.
- **Trimming and reflection interact badly**, and the application publishes trimmed. This is a
  discipline on every dependency rather than a one-time cost — see the deployment decision below.

### The schema model is ours; the BCL validates but does not represent

The application builds and holds **its own editable schema model, constructed from `XmlReader`**.
`System.Xml.Schema.XmlSchemaSet` is used **only to validate and to resolve references**, never as
the model the editor mutates.

This is the deeper of the two decisions, and it is forced by the serialisation requirements rather
than chosen for elegance. The BCL's post-compilation object model is lossy: it does not preserve
comments, original formatting, prefix choices, or the source order of attributes. Writing a schema
back out through it reformats the file. That directly contradicts the measured attribute-ordering
rule in §4 — whose entire value is that the reference corpus already conforms, so adoption produces
no normalising diff — and it would make Text View round-tripping impossible to hold.

Validation is a different problem, and there we delegate. Reimplementing XSD 1.0 validation would
be a large, subtle, permanently maintained body of code with a mature and well-tested
implementation sitting in the framework. So the model is ours and the verdict is the BCL's.

### The target is .NET 10 LTS, published self-contained, single-file and trimmed, under the JIT

.NET 10 is the current LTS, supported into November 2028. .NET 9 left support in May 2026, so this
is barely a choice; it is recorded so the floor is explicit.

Framework-dependent deployment is excluded outright by `XE-074`. That leaves JIT against NativeAOT,
and **the JIT is chosen**. NativeAOT would buy roughly a second of startup and a somewhat smaller
artifact. What it costs is not the switch but the standing obligation behind it: the compiler must
know every reachable code path at build time, so no dependency may ever use `Reflection.Emit`, no
assembly may be loaded at runtime, diagnostics degrade, and each platform's build needs its own
native toolchain with no cross-compilation. That is a constraint on every dependency decision for
the life of the product, accepted before we know which dependencies we need.

**The AOT-safety discipline is adopted anyway**, which is the point of taking these two together:
compiled XAML bindings rather than `ReflectionBinding`, no `Reflection.Emit`, no dynamic assembly
loading. Trimming already demands most of it, and it is what we would want for `XE-076` regardless.
Holding to it keeps NativeAOT a one-property change if startup time later turns out to matter,
rather than a rewrite. Trimming itself is the reversible half: if some dependency misbehaves under
it, we ship a larger installer and lose nothing else.

One consequence is worth stating because it is otherwise invisible: **a runtime plugin or extension
model stays possible.** NativeAOT would have foreclosed it permanently. Nothing in the requirements
asks for one, but the JIT choice does not spend that option.

### The MVVM layer is `CommunityToolkit.Mvvm`

MIT, one package, Microsoft-maintained, source-generator based with no runtime reflection — so it
is trim-clean and would stay AOT-clean under the discipline above.

The alternative worth taking seriously was ReactiveUI, because this application's state is a
derived-collections problem more than an asynchronous one: one large schema model, with the object
tree, dependency tree and validation pane all derived from it and a selection that must stay
coherent across them. ReactiveUI is good at exactly that, but it arrives with `System.Reactive`,
usually `DynamicData`, reflection in places, and an idiom every contributor has to learn — the
largest dependency surface of the three options, against an `XE-081` that wants the smallest.

So the derived-collection problem is deferred rather than pre-solved. **If it becomes real,
`DynamicData` is added on its own** — it is MIT and usable without ReactiveUI, so the part that
solves the problem can be bought without the framework around it. Plain `INotifyPropertyChanged`
was rejected in the other direction: at this model's size it means hand-rolling the change
propagation both alternatives already provide.

### Still to be chosen

**Text View needs a virtualised code editor control** (`XE-077`). AvaloniaEdit is the presumptive
answer, conditional on a measured spike — the criteria and the fallback ladder are in
[`0003-text-view-editor-control.md`](0003-text-view-editor-control.md). This does not block
scaffolding.

## Consequences

- **Scaffolding is unblocked.** Everything encoded in the project file is now decided, so the
  solution, the application project and the test project can be created, with the publish
  properties present from the first commit rather than retrofitted. `AGENTS.md` is updated
  accordingly, and the build and test commands go in there as soon as there is something to run.
- **`XE-081` gets concrete tooling.** Composition scanning has a first-party answer in
  `dotnet list package --vulnerable` over the transitive graph; static analysis has Roslyn
  analyzers in the build plus SonarQube, whose C# support is mature. Neither is adopted here — the
  tools are named so the requirement is no longer stack-dependent.
- **The third-party notices file** required by `0001` is generated from the NuGet graph, which is
  the same inventory the composition scan and the SBOM read.
- **The dependency-licence check in `AGENTS.md` is easier to satisfy than expected.** The
  NuGet ecosystem is overwhelmingly MIT and Apache-2.0; the LGPL discipline that record preserved
  is unlikely to be exercised.
- **Every future dependency carries a third question.** Alongside the CVE and licence checks in
  `AGENTS.md`, a candidate must be trim-safe, and AOT-safe if the discipline above is to hold. A
  library that reflects over types by name is a library that fails in the installer and not in
  development, which is the worst place to discover it.

## What this gives up

Native look and feel. An Avalonia application draws its own controls rather than hosting the
platform's, so it will not be pixel-identical to a native application on any of the three
targets. That is accepted: the product is dominated by a custom canvas that would never have looked
native anyway, and `XE-079`'s requirement is a coherent theme, not platform mimicry. The one place
it is not free is fonts — the requirements already call for the platform's native monospaced and UI
faces, and those must be resolved from the system rather than shipped.
