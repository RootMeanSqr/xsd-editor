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

# dotnet list reports per project, so an affected project and a clean one both appear in
# the same report. Assert the affected form is absent BEFORE reading the clean one:
# grep -q on the clean line alone matches a clean project and passes while another is
# affected, and dotnet list exits 0 either way.
if grep -q 'has the following vulnerable packages' "$report"; then
  fail "A project has vulnerable packages (XE-081). Review $report."
fi

# Then assert the clean state positively, once per project. Inferring "no vulnerabilities"
# from the absence of a severity word would report a scan that silently produced no output
# as a pass, which is the one failure mode a security check must not have.
clean_count="$(grep -c 'has no vulnerable packages given the current sources' "$report" || true)"
project_count="$(grep -c '<Project ' "$solution" || true)"

if [ "$clean_count" -eq 0 ]; then
  fail "Could not positively confirm a clean dependency graph (XE-081). Review $report: \
the report is not in the form this check understands."
fi

if [ "$clean_count" -ne "$project_count" ]; then
  fail "Confirmed $clean_count clean project(s) but the solution has $project_count. \
A project that reported neither clean nor affected was not scanned, which is not a pass."
fi

echo "Confirmed: no known vulnerabilities in the dependency graph."
