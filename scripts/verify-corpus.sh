#!/usr/bin/env bash
#
# Fetch the reference corpus named by XSDEDITOR_CORPUS and verify it arrived intact.
#
# The corpus is a controlled interface standard and is never committed
# (docs/decisions/0004-build-and-security-tooling.md), so this is what stands between
# "the fixture is what we think it is" and a round-trip suite silently measuring
# something else. Run it locally the same way CI does.
#
# Usage:  scripts/verify-corpus.sh [output-dir]
#
# XSDEDITOR_CORPUS is a semicolon-separated list of local paths or https URLs. The
# first entry is the entry point; the rest are its include/import dependencies.
# Unset, this exits 0 with a warning: the corpus is optional for a build, and its
# absence must be loud rather than silently green.

set -euo pipefail

out_dir="${1:-corpus}"

# GitHub Actions renders ::error:: and ::warning:: on the run summary. Outside CI they
# would be noise, so the same messages print plainly.
in_actions() { [ "${GITHUB_ACTIONS:-}" = "true" ]; }
warn() { if in_actions; then echo "::warning::$*"; else echo "WARNING: $*" >&2; fi; }
fail() { if in_actions; then echo "::error::$*"; else echo "ERROR: $*" >&2; fi; exit 1; }

if [ -z "${XSDEDITOR_CORPUS:-}" ]; then
  warn "XSDEDITOR_CORPUS is not set — the reference corpus was not fetched or verified. \
The round-trip and timing suites are the acceptance tests for XE-069, XE-071, XE-072 and \
XE-083, and none of them ran."
  exit 0
fi

mkdir -p "$out_dir"

# Semicolon-separated, because a colon cannot separate entries that may be URLs.
# The trailing newline matters: without it `read` returns false on the final entry and
# drops it silently. The count check below is what makes that unable to recur.
entries_file="$(mktemp)"
trap 'rm -f "$entries_file"' EXIT
printf '%s\n' "$XSDEDITOR_CORPUS" | tr ';' '\n' > "$entries_file"

expected="$(grep -c '[^[:space:]]' "$entries_file")"
echo "entries: $expected"

first=1
while IFS= read -r entry; do
  [ -n "$entry" ] || continue
  name="$(basename "$entry")"
  case "$entry" in
    http://*|https://*)
      # --fail so an HTML error page is never mistaken for a schema.
      curl -sS --fail --location --max-time 300 "$entry" -o "$out_dir/$name" \
        || fail "Could not fetch $entry"
      ;;
    *)
      [ -f "$entry" ] || fail "No such corpus file: $entry"
      cp "$entry" "$out_dir/$name"
      ;;
  esac
  if [ "$first" = 1 ]; then echo "entry point: $name"; first=0; fi
done < "$entries_file"

echo "--- fetched ---"
ls -l "$out_dir"

# Count only .xsd files: the script writes checksums.txt into this directory below, so
# counting every file makes a second run against the same directory fail — and fail by
# accusing the contributor of the dropped-entry bug this check exists to catch.
actual="$(find "$out_dir" -maxdepth 1 -type f -name '*.xsd' | wc -l)"
if [ "$actual" -ne "$expected" ]; then
  fail "Expected $expected corpus files but fetched $actual. A dropped entry leaves the \
closure incomplete, and the suites would then measure something other than the corpus."
fi

( cd "$out_dir" && sha256sum ./*.xsd | tee checksums.txt )

# The bytes are the thing under test (XE-067, XE-083). The corpus is wholly CRLF, so a
# bare LF means something normalised the files in transit and every round-trip assertion
# against them would be meaningless.
python3 - "$out_dir" <<'PY_EOF'
import pathlib, sys

bad = []
for f in sorted(pathlib.Path(sys.argv[1]).glob("*.xsd")):
    data = f.read_bytes()
    crlf = data.count(b"\r\n")
    bare_lf = data.count(b"\n") - crlf
    print(f"{f.name}: bytes={len(data)} CRLF={crlf} bare-LF={bare_lf}")
    if bare_lf:
        bad.append((f.name, bare_lf))

for name, count in bad:
    print(f"{name} contains {count} bare LF; the corpus is wholly CRLF, so it was "
          "normalised in transit and any round-trip test against it is meaningless.",
          file=sys.stderr)
sys.exit(1 if bad else 0)
PY_EOF
