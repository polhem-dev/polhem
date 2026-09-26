#!/usr/bin/env bash
# Checks that no <c>identifier</c> in XML doc prose points to a type that no longer exists.
# (The rule is in the XML documentation comments section of .claude/rules/code-style.md.)
#
# Why this script exists: `<see cref>` is guarded by the compiler (CS1574 + TreatWarningsAsErrors fails the
# build), but **`<c>Foo</c>` in prose has no protection at all**. After a type is renamed or deleted, the `<c>`
# points to nothing, and the XML doc ships in the NuGet package's .xml file straight into the consumer's
# IntelliSense. A real miss: the doc of `CacheInfo.Initialize` pointed to `CacheBootstrapper`, a type that
# disappeared when the static facade was removed, and it survived until the repository-wide inventory on 2026-08-15.
#
# Preferred fix: use `<see cref>` wherever possible so the compiler takes over. This script only guards the places
# that really must use `<c>`: types from external packages, SQL keywords, deliberate mentions of removed types, and
# **upward cross-assembly references** (for example Polhem.ObjectCaching mentioning AddPolhemFramework in
# Polhem.Hosting: the dependency points the other way, so cref cannot resolve it).
#
# Expected output: the OK line only. For each other line, decide whether it really points to nothing or is a
# new exception that belongs in the allowlist.
set -uo pipefail
cd "$(dirname "$0")"

# Exclusion patterns for find, shared by every search below.
readonly BIN_PATH_GLOB='*/bin/*'
readonly OBJ_PATH_GLOB='*/obj/*'

# Identifiers deliberately not checked. Every addition **must name its category**, otherwise this list turns
# into a mute button.
ALLOWLIST=(
  # --- External package / BCL types (not declared in this solution) ---
  AsyncLocal CoCreateInstance InternalsVisibleTo ToolStripMenuItem
  FileBufferingReadStream
  FormatterNotRegisteredException TypelessFormatter IXmlSerializable ReadXmlDiffgram
  # --- Deliberate mentions of removed types (the prose says used to / which is gone / the former) ---
  SafeTypelessFormatter ItemsForSerialization NumberFormatPresets
  GetIndexsCommandText
  CreateSerializer
  # --- Hypothetical types in a forward-looking suggestion (not implemented; the prose says "abstract this via ...") ---
  IDescriptionSyncCommandBuilder
  # --- Placeholders in documentation, not real type names ---
  Cxxx SaveX IXxxRepository G
  # --- SQL keywords / data dictionary objects / column names ---
  ALL_TABLES ALL_TAB_COMMENTS ALL_COL_COMMENTS ANDEC DECAN ANSI_QUOTES
  DO_SUM QUANTITY SIZE SQL_MODE USERNAME
  LOCALTIMESTAMP NO_BACKSLASH_ESCAPES
)

is_allowed() {
  local needle="$1"
  for item in "${ALLOWLIST[@]}"; do
    [[ "$item" == "$needle" ]] && return 0
  done
  return 1
}

# First, block what would silently blind every grep below: NUL bytes in source files.
#
# grep treats a file containing NUL as binary, so every grep over it comes back empty. It does not report an
# error; it **skips silently**. This really happened: `$"{lang}\x00{ns}"` in SnapshotLanguageService.cs had the
# NUL written as a raw byte instead of the `\0` escape sequence, so the file's 5,432 bytes of source contributed
# 0 bytes to the corpus below, and its own <c> tags were never checked. Only the framework health check on
# 2026-09-04 found it; no mechanism said anything in between.
#
# `\0` behaves identically (a C# escape sequence that compiles to the same bytes), so there is no legitimate
# exception. The test compares the file with its `tr -d '\000'` output: a shell variable cannot hold NUL, so a
# grep pattern cannot find it.
NUL_HITS=$(
  find src tests tools apps samples -name '*.cs' \
       -not -path "$BIN_PATH_GLOB" -not -path "$OBJ_PATH_GLOB" 2>/dev/null \
  | while IFS= read -r f; do
      tr -d '\000' < "$f" | cmp -s - "$f" || echo "$f"
    done
)
if [[ -n "$NUL_HITS" ]]; then
  echo "Source files contain NUL bytes (grep treats the whole file as binary and skips it silently; use the \\0 escape sequence instead):"
  echo "$NUL_HITS" | sed 's/^/    /'
  exit 1
fi

# The corpus: every **non-XML-doc** line in the solution. Including tests/ and other unpublished folders is
# deliberate: this script only asks "does this name still exist", not "which layer is it in".
CODE=$(mktemp)
trap 'rm -f "$CODE"' EXIT
find src tests tools apps samples -name '*.cs' \
     -not -path "$BIN_PATH_GLOB" -not -path "$OBJ_PATH_GLOB" -print0 2>/dev/null \
  | xargs -0 grep -hv '^[[:space:]]*///' > "$CODE"

status=0
while IFS= read -r id; do
  is_allowed "$id" && continue
  grep -qw -- "$id" "$CODE" && continue
  status=1
  echo "Points to a type that does not exist: <c>${id}</c>"
  grep -rn --include='*.cs' --exclude-dir=bin --exclude-dir=obj "<c>${id}</c>" src \
    | sed 's/^/    /'
done < <(
  find src -name '*.cs' -not -path "$BIN_PATH_GLOB" -not -path "$OBJ_PATH_GLOB" -print0 \
    | xargs -0 grep -ho '<c>[A-Z][A-Za-z0-9_]*</c>' \
    | sed 's|<c>\(.*\)</c>|\1|' | sort -u
)

# ---------------------------------------------------------------------------
# Reverse check: <c> used where <see cref> should be.
#
# The check above guards "does what <c> points to still exist", but the header says "**use cref wherever
# possible**", and nothing used to enforce that sentence. In the inventory on 2026-09-04, src/ alone had 239
# <c> tags pointing to types in this solution that cref could resolve, and the number was rising (245 in the
# previous baseline, 259 that time).
#
# The criterion is "a type declared in the same project". Downward cross-assembly references can use cref too,
# but need the fully qualified name; this script does not follow that layer, to avoid false positives.
# File-name forms (Foo.xml / Bar.razor) should use <c> anyway and are excluded first.
#
# The fix for a hit: change it to <see cref="..."/> (the plain name in the same namespace; the fully qualified
# name in a different namespace, and **do not add a using just for a cref**, which triggers IDE0005). Only if it
# really cannot resolve does it go into the allowlist below, with the reason.
CREF_ALLOWLIST=(
  # --- Ambiguous names: several fully qualified candidates, and picking one could mislead ---
  WhereBuilder
)

is_cref_allowed() {
  local id="${1%%.*}"
  for a in "${CREF_ALLOWLIST[@]}"; do [[ "$id" == "$a" ]] && return 0; done
  return 1
}

cref_status=0
while IFS= read -r line; do
  file="${line%%:*}"; id="${line##*:}"
  is_cref_allowed "$id" && continue
  cref_status=1
  echo "Uses <c> where <see cref> should be used: ${id}  (${file})"
done < <(
  for f in $(find src -name '*.cs' -not -path "$BIN_PATH_GLOB" -not -path "$OBJ_PATH_GLOB"); do
    proj=$(echo "$f" | cut -d/ -f2)
    grep -h '^[[:space:]]*///' "$f" | grep -o '<c>[A-Za-z_][A-Za-z0-9_.]*</c>' \
      | sed 's|<c>\(.*\)</c>|\1|' \
      | grep -vE '\.(xml|json|md|cs|txt|props|targets|csproj|editorconfig|razor|axaml)$' \
      | while IFS= read -r id; do
          root="${id%%.*}"
          if grep -rqE "\b(class|interface|struct|enum|record)[[:space:]]+${root}\b" \
               "src/${proj}" --include='*.cs' --exclude-dir=bin --exclude-dir=obj 2>/dev/null; then
            echo "${f}:${id}"
          fi
        done
  done | sort -u
)

if [[ $status -eq 0 && $cref_status -eq 0 ]]; then
  echo "OK: no <c> in the XML doc prose of src/ points to nothing, and none should be a cref."
fi
[[ $status -ne 0 || $cref_status -ne 0 ]] && exit 1
exit 0
