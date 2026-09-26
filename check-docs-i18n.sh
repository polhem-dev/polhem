#!/usr/bin/env bash
# Translation sync check for the public documents: do the translations under docs/<language>/ keep up with the
# source documents?
#
# Why it exists: "when the source changes, the translation changes too" used to be enforced by nothing. With two
# languages discipline can just about hold; with more it is bound to drift, and since the compiler does not read
# documents and tests do not run them, nobody would notice.
#
# ---------------------------------------------------------------------------------------------
# Configuration: the list of languages and their policies is written only here. Other documents point to this
# file and do not copy it.
# ---------------------------------------------------------------------------------------------
SOURCE_LANG="zh-TW"

# Translation languages and policies, as space-separated "language:policy" pairs. The language switch line lists
# the source language first, then the others in this order.
#   strict   every source document needs a translation; a missing, stale or malformed translation fails the script.
#   partial  only the documents on the required list need a translation; stale translations and missing optional
#            ones are only reported, without failing.
TRANSLATIONS="en:strict"

# Required list of a partial language: the variable is named REQUIRED_<language code with - replaced by _>, and
# its value is a space-separated list of paths relative to docs/<language>/. Example:
#   REQUIRED_ja="README.md getting-started.md"

# The label on the language switch line is always the language's own name (autonym). A new language is added
# both here and to TRANSLATIONS.
autonym() {
  case "$1" in
    en) echo "English" ;;
    zh-TW) echo "繁體中文" ;;
    zh-CN) echo "简体中文" ;;
    ja) echo "日本語" ;;
    *) return 1 ;;
  esac
}
# ---------------------------------------------------------------------------------------------
#
# Translation header: the first line of every translation
#   <!-- source: zh-TW/caching.md blob: <40 hex digits> -->
# - It records the blob hash of the source document (git hash-object), not a commit hash. Changing a source and
#   its translation in the same commit is normal, and a commit hash only exists after the commit; a blob hash
#   depends only on the file content and exists before. So it also does not depend on the depth of the git
#   history and is not affected by a rebase.
# - To see what a translation missed: find the commit with git log --all --find-object=<blob>, then diff to now.
# - Any change to the source (formatting included) marks the translation stale. This is deliberate: a person
#   decides whether it needs retranslating and restamps it; the script does not guess which changes "do not
#   matter".
#
# Checks:
#   1. A folder under docs/ looks like a language code but is not declared above.
#   2. A source document carries a translation header (the direction is reversed).
#   3. A translation has no header, a malformed header, a header pointing to another file, or points to a source
#      that does not exist (an orphan translation).
#   4. Stale: the blob in the header differs from the current blob of the source.
#   5. Missing translation.
#   6. Language switch line: the language links at the start of the first non-empty line under the title must
#      list exactly the other language versions of this document that exist. A link counts as a language link
#      when its target is ../<language code>/, regardless of its label, so a wrong label is caught too.
#      Why the script manages it: every new language changes every document in every language, and documents a
#      partial language has not translated must not be listed (they would be dead links), so every document's
#      switch line differs and hand edits are bound to drift.
#
# Failure policy: checks 1, 2 and 6 always fail. Checks 3-5 fail for a strict language; for a partial language,
# stale translations and missing optional translations are only reported, and everything else is as strict.
#
# Usage:
#   ./check-docs-i18n.sh                           Check. No output and exit 0 when all is well; exit 1 on errors.
#   ./check-docs-i18n.sh --stamp <translation>...  Update the translation header to the source's current blob
#                                                  (adding the header if there is none).
#   ./check-docs-i18n.sh --fix-switch              Rewrite the language switch line of every document from the
#                                                  translations that exist.
#
# IMPORTANT: a stamp declares "this has been checked against the source". The script does not and cannot verify
# the translation; stamping without checking turns this check off.
#
# Scope: tracked files, plus untracked files that are not ignored, the same as check-md-links.sh.
set -uo pipefail
cd "$(dirname "$0")"
export LC_ALL=C

SEP=" · "
fail=0

# NOTE: `read -d ''` instead of `$(cat <<'AWK' ...)`. The regexes below contain unbalanced
# parentheses, which bash 3.2 (the macOS default) mis-parses inside a command substitution.
read -r -d '' AWK_SWITCH <<'AWK'
function lang_link_len(s,    link, target) {
  if (!match(s, /^\[[^]]*\]\([^)]*\)/)) return 0
  link = substr(s, 1, RLENGTH)
  target = link
  sub(/^\[[^]]*\]\(/, "", target)
  sub(/\)$/, "", target)
  if (target ~ /^(\.\.\/)+[a-z][a-z](-[A-Za-z]+)?\//) return length(link)
  return 0
}

function split_switch(line,    n) {
  g_links = ""
  g_rest = line
  while ((n = lang_link_len(g_rest)) > 0) {
    g_links = (g_links == "" ? "" : g_links sep) substr(g_rest, 1, n)
    g_rest = substr(g_rest, n + 1)
    if (index(g_rest, sep) != 1) break
    g_rest = substr(g_rest, length(sep) + 1)
  }
}

BEGIN { sep = " · " }

{ lines[NR] = $0 }

END {
  title = 0
  for (i = 1; i <= NR; i++) if (substr(lines[i], 1, 2) == "# ") { title = i; break }
  sw = 0
  if (title) for (i = title + 1; i <= NR; i++) if (lines[i] != "") { sw = i; break }
  links = ""
  rest = ""
  if (sw) { split_switch(lines[sw]); links = g_links; rest = g_rest }

  if (mode == "get") { print links; exit }

  for (i = 1; i <= NR; i++) {
    if (title && i == sw && links != "") {
      out = want
      if (rest != "") out = (out == "" ? rest : out sep rest)
      if (out == "") {
        if (i < NR && lines[i + 1] == "") i++
        continue
      }
      print out
      continue
    }
    if (title && i == sw && want != "" && substr(lines[i], 1, 1) == "[") {
      print want sep lines[i]
      continue
    }
    print lines[i]
    if (title && i == title && want != "" && links == "" && !(sw && substr(lines[sw], 1, 1) == "[")) {
      print ""
      print want
      if (i < NR && lines[i + 1] != "") print ""
    }
  }
}
AWK

error() { printf 'error: %s\n' "$1"; fail=1; }
report() { printf 'report: %s\n' "$1"; }

langs_in_order() {
  local t
  echo "$SOURCE_LANG"
  for t in $TRANSLATIONS; do echo "${t%%:*}"; done
}

policy_of() {
  local t
  for t in $TRANSLATIONS; do
    if [ "${t%%:*}" = "$1" ]; then
      echo "${t#*:}"
      return 0
    fi
  done
  return 1
}

required_for() {
  local var
  var="REQUIRED_$(printf '%s' "$1" | tr '-' '_')"
  eval "printf '%s' \"\${$var:-}\""
}

listed() {
  git -c core.quotePath=false ls-files --cached --others --exclude-standard -- "$@"
}

# Paths relative to docs/<lang>/ of the markdown files in one language folder.
docs_of() {
  listed "docs/$1/*.md" | sed "s|^docs/$1/||" | sort -u
}

expected_switch() {
  local self="$1" rel="$2" prefix="../" rest="$2" out="" l
  while [ "${rest#*/}" != "$rest" ]; do
    prefix="../$prefix"
    rest="${rest#*/}"
  done
  for l in $(langs_in_order); do
    [ "$l" = "$self" ] && continue
    [ -f "docs/$l/$rel" ] || continue
    out="${out:+$out$SEP}[$(autonym "$l")](${prefix}${l}/${rel})"
  done
  printf '%s' "$out"
}

validate_config() {
  local l t
  for l in $(langs_in_order); do
    autonym "$l" > /dev/null || { echo "Configuration error: language $l has no autonym." >&2; exit 2; }
  done
  for t in $TRANSLATIONS; do
    case "${t#*:}" in
      strict | partial) ;;
      *) echo "Configuration error: the policy of $t must be strict or partial." >&2; exit 2 ;;
    esac
  done
}

check_translation() {
  local lang="$1" rel="$2" first="$3" file="docs/$1/$2" policy expected_src src blob current msg
  policy=$(policy_of "$lang")
  expected_src="$SOURCE_LANG/$rel"
  case "$first" in
    "<!-- source: "*" blob: "*" -->") ;;
    *)
      error "$file: the translation header is missing or malformed. After translating against the source, stamp it with ./check-docs-i18n.sh --stamp $file."
      return
      ;;
  esac
  src=${first#"<!-- source: "}
  src=${src%%" blob: "*}
  blob=${first##*" blob: "}
  blob=${blob%" -->"}
  if ! printf '%s' "$blob" | grep -Eq '^[0-9a-f]{40}$'; then
    error "$file: the blob in the header is not 40 hex digits."
    return
  fi
  if [ "$src" != "$expected_src" ]; then
    error "$file: the header points to $src; it should be $expected_src."
    return
  fi
  if [ ! -f "docs/$src" ]; then
    error "$file: the source docs/$src does not exist (orphan translation)."
    return
  fi
  current=$(git hash-object "docs/$src")
  if [ "$blob" != "$current" ]; then
    msg="$file: stale, docs/$src changed after the stamp. Update it against the source and restamp with --stamp; find the stamped version with git log --all --find-object=$blob."
    if [ "$policy" = strict ]; then error "$msg"; else report "$msg"; fi
  fi
}

check() {
  local declared d l t lang policy required rel file want got first msg
  declared=" $(langs_in_order | tr '\n' ' ') "
  for d in $(listed docs | awk -F/ 'NF >= 3 { print $2 }' | sort -u); do
    printf '%s' "$d" | grep -Eq '^[a-z]{2}(-[A-Za-z]+)?$' || continue
    case "$declared" in
      *" $d "*) ;;
      *) error "docs/$d/ looks like a language folder, but the header of check-docs-i18n.sh does not declare this language." ;;
    esac
  done

  for l in $(langs_in_order); do
    for rel in $(docs_of "$l"); do
      file="docs/$l/$rel"
      want=$(expected_switch "$l" "$rel")
      got=$(awk -v mode=get "$AWK_SWITCH" "$file")
      if [ "$want" != "$got" ]; then
        error "$file: the language switch line should be \"${want:-(none)}\" but is \"${got:-(none)}\". Fix: ./check-docs-i18n.sh --fix-switch"
      fi
      first=$(head -n 1 "$file")
      if [ "$l" = "$SOURCE_LANG" ]; then
        case "$first" in
          "<!-- source:"*) error "$file: a source document must not carry a translation header (is the direction reversed?)." ;;
        esac
      else
        check_translation "$l" "$rel" "$first"
      fi
    done
  done

  for t in $TRANSLATIONS; do
    lang=${t%%:*}
    policy=${t#*:}
    required=" $(required_for "$lang") "
    for rel in $(docs_of "$SOURCE_LANG"); do
      [ -f "docs/$lang/$rel" ] && continue
      msg="docs/$lang/$rel: missing translation (source docs/$SOURCE_LANG/$rel)."
      if [ "$policy" = strict ]; then
        error "$msg"
      else
        case "$required" in
          *" $rel "*) error "$msg It is on the required list." ;;
          *) report "$msg" ;;
        esac
      fi
    done
  done
}

stamp() {
  local path lang rel src blob tmp
  if [ "$#" -eq 0 ]; then
    echo "Usage: ./check-docs-i18n.sh --stamp <translation path>..." >&2
    exit 2
  fi
  for path in "$@"; do
    path=${path#./}
    case "$path" in
      docs/*/*.md) ;;
      *) echo "Not a .md under docs/<language>/: $path" >&2; exit 2 ;;
    esac
    lang=${path#docs/}
    lang=${lang%%/*}
    rel=${path#docs/"$lang"/}
    if ! policy_of "$lang" > /dev/null; then
      echo "$path: $lang is not a declared translation language; source documents are not stamped." >&2
      exit 2
    fi
    [ -f "$path" ] || { echo "$path does not exist." >&2; exit 2; }
    src="docs/$SOURCE_LANG/$rel"
    [ -f "$src" ] || { echo "$path: source $src not found." >&2; exit 2; }
    blob=$(git hash-object "$src")
    tmp=$(mktemp)
    awk -v header="<!-- source: $SOURCE_LANG/$rel blob: $blob -->" \
      'NR == 1 { print header; if (index($0, "<!-- source:") == 1) next } { print }' "$path" > "$tmp"
    cat "$tmp" > "$path"
    rm -f "$tmp"
    echo "Stamped: $path <- $src"
  done
}

fix_switch() {
  local l rel file want tmp
  for l in $(langs_in_order); do
    for rel in $(docs_of "$l"); do
      file="docs/$l/$rel"
      want=$(expected_switch "$l" "$rel")
      tmp=$(mktemp)
      awk -v mode=fix -v want="$want" "$AWK_SWITCH" "$file" > "$tmp"
      if ! cmp -s "$tmp" "$file"; then
        cat "$tmp" > "$file"
        echo "Updated language switch line: $file"
      fi
      rm -f "$tmp"
    done
  done
}

validate_config
case "${1:-}" in
  "") check; exit "$fail" ;;
  --stamp) shift; stamp "$@" ;;
  --fix-switch) fix_switch ;;
  *) echo "Usage: ./check-docs-i18n.sh [--stamp <translation path>... | --fix-switch]" >&2; exit 2 ;;
esac
