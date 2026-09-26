#!/usr/bin/env bash
# Pointer checks for committed files. The rules are in .claude/rules/public-docs.md.
#
# Plans and other personal working documents live in local/, which git ignores. Nobody else
# can open them: not a contributor's clone, not CI, not a reader on GitHub. So:
#   (1) no committed file may name a plan file,
#   (2) no committed file may point to a specific file under local/,
#   (3) public documents may not point into .claude/ (agent guidance, not product documentation),
#   (4) public documents should not refer readers to "the plan" in prose.
# (1)-(3) are expected to print nothing and make the script exit 1 when they do.
# (4) has known false positives and is advisory; read each hit.
#
# Each check was added after a batch of references had slipped through for a long time.
# Do not narrow the scope or the file types.
set -uo pipefail
cd "$(dirname "$0")"

MD_ROOTS=(docs/ README.md README.zh-TW.md CHANGELOG.md CHANGELOG.zh-TW.md CONTRIBUTING.md CONTRIBUTING.zh-TW.md src/ samples/ apps/ tools/)

PLAN_FILE_RE='plan-[a-z0-9]+(-[a-z0-9.]+)+\.md'
LOCAL_FILE_RE='(^|[^A-Za-z0-9_.-])local/[A-Za-z0-9_./-]*[A-Za-z0-9_-]\.[A-Za-z]{1,5}([^A-Za-z0-9]|$)'

failed=0

section() {
  printf '\n=== (%s) %s ===\n' "$1" "$2"
  return 0
}

# Prints the hits and records a failure when there are any. Called with the hits as an argument,
# not at the end of a pipeline: a pipeline runs it in a subshell, where setting `failed` is lost.
report() {
  if [[ -n "$1" ]]; then
    printf '%s\n' "$1"
    failed=1
  fi
  return 0
}

# Every tracked file, plus untracked files that are not ignored. local/ is ignored, so it is
# never scanned itself.
repo_files() {
  git ls-files -z --cached --others --exclude-standard
}

# docs/repo-ops/ holds maintainer documents, not public ones. It is still scanned by (1) and (2).
public_md_filter() {
  grep -v "^docs/repo-ops/"
  return 0
}

# A full URL to the old repository is a pointer readers can follow: its plans stay readable
# in the archived jeff377/bee-library. Links to a URL, and bare URLs, are removed before matching.
section 1 "all files — names a plan file (expected empty)"
report "$(repo_files | xargs -0 grep -InE "$PLAN_FILE_RE" 2>/dev/null \
  | sed -E -e 's#\[[^]]*\]\(https?://[^)]*\)##g' -e 's#https?://[^ )>"]*##g' \
  | grep -E "^[^:]+:[0-9]+:.*$PLAN_FILE_RE")"

# Naming the directories (`local/plans/`) to explain the convention is fine; pointing at a file
# inside them is not.
section 2 "all files — points to a file under local/ (expected empty)"
report "$(repo_files | xargs -0 grep -InE "$LOCAL_FILE_RE" 2>/dev/null)"

# CLAUDE.md files are agent guidance themselves, so they may point into .claude/.
section 3 "public markdown — points into .claude/ (expected empty)"
report "$(grep -rn --include="*.md" -e "\.claude/" "${MD_ROOTS[@]}" 2>/dev/null \
  | grep -v "/CLAUDE\.md:" | public_md_filter)"

section 4 "public markdown — refers to a plan in prose (known false positives; read each hit)"
grep -rnE --include="*.md" "見 plan|本 plan|plan (的|內|各)|(see|the|migration|integration) plan" \
  "${MD_ROOTS[@]}" 2>/dev/null | public_md_filter

echo
exit "$failed"
