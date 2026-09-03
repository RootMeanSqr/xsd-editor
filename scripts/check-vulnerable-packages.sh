#!/usr/bin/env bash
#
# Report any dependency carrying a known vulnerability (XE-081).
#
# This is a REPORT, not the gate. The gate is NuGetAudit in Directory.Build.props, which
# fails restore on every machine — see docs/decisions/0004-build-and-security-tooling.md.
# What this adds is a durable artifact and a POSITIVE assertion that the graph is clean.
#
# Run it before proposing a dependency: CONTRIBUTING.md asks every candidate to clear a
# CVE check, and this is that check.
#
# Usage:  scripts/check-vulnerable-packages.sh [solution] [report-path]

set -euo pipefail

solution="${1:-XsdEditor.slnx}"
report="${2:-vulnerable.txt}"

in_actions() { [ "${GITHUB_ACTIONS:-}" = "true" ]; }
fail() { if in_actions; then echo "::error::$*"; else echo "ERROR: $*" >&2; fi; exit 1; }

set +e
dotnet list "$solution" package --vulnerable --include-transitive > "$report" 2>&1
scan_status=$?
set -e

cat "$report"

if [ "$scan_status" -ne 0 ]; then
  fail "The vulnerability report did not run to completion (dotnet list exited \
$scan_status). That is a tooling failure, not a clean result."
fi

# NU1905: the audit source could not be consulted. A scan that did not run must never be
# reported as a scan that found nothing.
if grep -q 'NU1905' "$report"; then
  fail "The audit source could not be consulted (NU1905), so nothing was actually checked."
fi

# Assert the clean state positively. Inferring "no vulnerabilities" from the absence of a
# severity word would report a scan that silently produced no output as a pass, which is
# the one failure mode a security check must not have.
if ! grep -q 'has no vulnerable packages given the current sources' "$report"; then
  fail "Could not positively confirm a clean dependency graph (XE-081). Review $report: \
either a package is affected, or the report is not in the form this check understands."
fi

echo "Confirmed: no known vulnerabilities in the dependency graph."
