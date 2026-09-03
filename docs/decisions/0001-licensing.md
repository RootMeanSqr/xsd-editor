# 0001 — Licensing: Apache-2.0, with revenue from maintenance and support

**Status:** accepted
**Date:** 2026-09-03

## Context

The project should be open source, and it should be able to sustain paid work — principally the
ongoing effort of tracking security findings, closing vulnerabilities, and shipping maintained
builds. The initial framing was "free for personal use, paid for enterprise", which forced a
choice before anything else could be decided.

That framing is not open source. The OSI definition forbids discrimination against fields of
endeavour, so any licence conditioning use on who the user is — an individual rather than a
company — is source-available rather than open source. Both are legitimate; they are not the same
thing, and enterprise procurement reviewers know the difference.

Two routes were considered seriously.

**BUSL-1.1 with GPLv3 as the Change License.** Source public and forkable; an Additional Use Grant
written to permit personal use; everything else needs a commercial licence; each release converts
to GPLv3 after a fixed period. This delivers the personal/enterprise boundary directly, but it is
not open source during the restricted period, and it constrains dependencies more tightly: every
shipped component must be compatible both with proprietary distribution *and* with GPLv3
relicensing at the Change Date.

**Genuine dual licensing under GPLv3 plus a commercial licence** was rejected earlier in the
analysis. Copyleft only engages on distribution, and an enterprise running a desktop editor
internally distributes nothing — so the GPL gives essentially no commercial leverage here. AGPL
does not help either, because there is no network service to trigger it.

The decisive observation is that the stated motivation — keeping up with security findings — is a
**support proposition, not a licensing one**. It is sold by shipping maintained builds, SBOMs, and
a response commitment. No licence produces that value; a restrictive one only adds the ability to
refuse, at the cost of the open source label and of a narrower dependency pool.

## Decision

**The project is licensed Apache-2.0.** Revenue, where it is pursued, comes from maintenance and
support — maintained builds, security response, SBOMs, and indemnity — not from a use restriction.

Apache-2.0 rather than MIT, for four reasons that all matter to the intended buyer:

- **An express patent grant** (§3), with termination on patent litigation. MIT is silent on
  patents; the implied grant has never been well tested. This is the difference enterprise legal
  review actually asks about.
- **An explicit trademark reservation** (§6). The substance is the same under MIT, but stated
  rather than inferred.
- **A contribution clause** (§5), which sets the default terms for inbound contributions.
- **§9**, which lets a redistributor offer warranty, support, or indemnity on its own behalf
  without binding the original authors. That is precisely the shape of the support offering.

MIT's advantages — brevity and GPLv2 compatibility — do not outweigh these. GPLv2 compatibility is
only relevant to a route not taken.

**Trademark, not licence, is the practical lever.** The project name is what distinguishes the
maintained build from a fork, and it should be treated as the asset it is.

**Contributor licensing: an individual CLA is required**, recorded in [`CLA.md`](../../CLA.md) and
adapted from the Apache ICLA v2.2.

Apache-2.0 §5 makes inbound contributions Apache-2.0 by default, so strictly speaking a CLA is not
needed to operate under this decision. It is adopted anyway for one reason: the **right to
sublicense**. Without it, relicensing later — a commercial edition, or a move to different terms —
would require the permission of every past contributor, which stops being achievable after a
handful of them. Adopting a CLA before the first outside contribution costs almost nothing;
retrofitting one is the part that does not work.

This is a hedge, not a plan. Apache-2.0 remains the intended end state. A corporate CLA is not
written yet and is only needed when a contributor's employer holds rights in their work.

## Consequences

### On dependency choice

The constraint is now single-phase rather than two-phase: shipped dependencies must permit
distribution inside a product licensed Apache-2.0.

- **Safe:** MIT, BSD-2/3-Clause, ISC, Apache-2.0, MPL-2.0, GPLv2-with-Classpath-Exception.
- **Excluded from shipped code:** GPL and AGPL, which would force the whole application to
  copyleft.
- **Permitted with discipline:** LGPL — including Qt and GTK. The LGPL allows distribution under
  other terms where the user can relink, which means shipping the library dynamically and keeping
  a source offer or object files available. See `XE-074` below: this does not conflict with the
  packaging requirement.
- **No longer a concern:** EPL-2.0 and CDDL. These are GPL-incompatible and would have been traps
  under the BUSL route, where a GPLv3 Change Date would eventually have collided with them. Under
  Apache-2.0 they are ordinary weak-copyleft licences and can ship. This is a real widening of the
  field on the JVM, where the Eclipse and legacy Jakarta components sit.
- **Build and test tooling is unconstrained**, since it does not reach the distributed artifact.

### On packaging

Apache-2.0 dependencies bring an obligation that MIT ones do not: **§4d requires their `NOTICE`
contents to be propagated**. This is an obligation on the *installable artifact*, not merely on a
file in the repository. Each installer must therefore carry a third-party notices file covering
every bundled component. The `NOTICE` file at the repository root points at it.

That file falls out of the same composition scan `XE-081` already requires for CVE detection and
the SBOM, so the two should share machinery rather than being built twice.

### On the requirements document

`XE-074` (Packaging) was clarified alongside this decision to state that **how the artifact is
assembled is unconstrained** — static linking, dynamic linking against libraries shipped inside
the artifact, and an embedded runtime are all acceptable. The requirement was always about what
the *user* must acquire, never about linkage; without that said explicitly, "self-contained" reads
as "statically linked" and appears to rule out LGPL components that it does not in fact rule out.

### On the stack decision

Licensing no longer narrows the field much. .NET with Avalonia (MIT throughout), JavaFX on OpenJDK
(GPLv2-with-Classpath-Exception, whose exception permits linking regardless of outbound terms), and
Qt (LGPLv3, with the dynamic-linking discipline above) are all viable. The stack should be chosen
on toolkit fit, ecosystem, and packaging maturity — see `XE-073`, `XE-074`, `XE-075`, and
`XE-081` — rather than on licence compatibility.

### On the future licence/activation check

The activation check listed under §1, Future features remains available: Apache-2.0 does not
prevent distributing a separately licensed commercial edition, and open core stays open as an
option if it is ever wanted. Under `XE-075` (No Network Egress) any such check must be **offline** —
a signed licence file verified against an embedded public key — which needs a signature primitive
but no HTTP client.

### What this gives up

Nothing prevents an enterprise from using the editor without paying. That is the accepted cost of
the open source label, and it is the trade this decision makes deliberately: the paid offering has
to be worth buying on its merits rather than by being the only lawful option.
