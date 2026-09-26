#!/usr/bin/env bash
#
# PreToolUse hook: verify the tree before any `git commit` reaches the shell.
#
# Three checks, deliberately asymmetric:
#   1. Clean Release build  -> BLOCKS the commit (exit 2). `--no-incremental` is the
#      whole point: an incremental build can hide a warning that a full build reports,
#      which is how a "build is clean" claim has been wrong before.
#   2. `PublicAPI.Unshipped.txt` diff -> WARNS only. The PublicApiAnalyzers package is
#      enabled repo-wide, so an *undeclared* API change is already a build error and is
#      caught by check 1. The gap it cannot see is a change that was declared in
#      Unshipped.txt — that turns the build green while the break is still real.
#   3. `check-docs-i18n.sh` output -> WARNS only. A source edit and its translation may
#      legitimately land in separate commits, so blocking here would be wrong; the Docs
#      Check workflow is the gate. The value is hearing about it before a push turns CI
#      red, rather than after.
#
# WARNING: this hook must fail open. Anything it cannot parse, locate, or run exits 0
# and lets the commit through. A verification hook that wedges the repo is worse than
# one that occasionally misses.
#
set -uo pipefail

SOLUTION="Polhem.slnx"

payload=$(cat 2>/dev/null) || exit 0

# Intercept only git commit. Matching the raw payload rather than a parsed field keeps
# this working without a JSON parser; a stray match (a commit string inside an echo)
# costs one build and nothing else.
printf '%s' "$payload" | grep -Eq '(^|[;&|"[:space:]])git[[:space:]]+commit' || exit 0

# The hook process starts in the Claude Code project directory, not in the directory the
# intercepted command will run in. Resolving the repository from the hook's own cwd
# therefore always landed on *this* repository, so a command like
# `cd ../other-repo && git commit ...` built this solution and blocked a commit this hook
# has no say over. Recover the intended directory from the last `cd` in the command.
target_dir=$PWD
cd_arg=$(printf '%s' "$payload" \
    | grep -oE '(^|[;&|"[:space:]])cd[[:space:]]+[^[:space:];&|"]+' \
    | tail -1 \
    | sed -E 's/^(.*[^[:alnum:]_])?cd[[:space:]]+//' 2>/dev/null)
if [[ -n "$cd_arg" ]]; then
    # The payload carries the command text verbatim, so a leading ~ is still literal.
    case "$cd_arg" in
        "~") cd_arg=$HOME ;;
        "~/"*) cd_arg="$HOME/${cd_arg#\~/}" ;;
        # Any other form is already a usable path, absolute or relative to the hook cwd.
        *) ;;
    esac
    [[ -d "$cd_arg" ]] && target_dir=$cd_arg
fi

repo_root=$(git -C "$target_dir" rev-parse --show-toplevel 2>/dev/null) || exit 0
cd "$repo_root" 2>/dev/null || exit 0

# Guards the case where the agent is committing in some other repository — the plugin
# repo, a sample clone — where this solution does not exist and this hook has no say.
[[ -f "$SOLUTION" ]] || exit 0

command -v dotnet >/dev/null 2>&1 || exit 0

# ---------------------------------------------------------------------------
# Check 1 — full, non-incremental Release build. Blocking.
# ---------------------------------------------------------------------------
build_log=$(mktemp -t polhem-precommit-build) || exit 0
trap 'rm -f "$build_log"' EXIT

if ! dotnet build "$SOLUTION" --configuration Release --no-incremental -v q -nologo \
        >"$build_log" 2>&1; then
    {
        echo "COMMIT BLOCKED: the clean Release build failed."
        echo
        grep -Ei "error|warning" "$build_log" | head -30
        echo
        echo "Note: this repository sets TreatWarningsAsErrors=true, so a warning is a failure."
        echo "This was a full --no-incremental build; existing obj/ output did not affect it."
        echo "Fix the build, then commit again."
    } >&2
    exit 2
fi

# ---------------------------------------------------------------------------
# Check 2 — public API surface changes. Advisory only.
# ---------------------------------------------------------------------------
notice=""

api_files=$(git diff HEAD --name-only -- '*PublicAPI.Unshipped.txt' 2>/dev/null)
if [[ -n "$api_files" ]]; then
    api_diff=$(git diff HEAD -- '*PublicAPI.Unshipped.txt' 2>/dev/null \
               | grep -E '^[+-][^+-]' | head -40)

    notice=$(printf '%s\n' \
        "PublicAPI.Unshipped.txt changed (not blocked; the compatibility needs an explicit judgement):" \
        "" \
        "$api_diff" \
        "" \
        "The analyzer only guarantees that a change is declared, not that it is compatible. Adding an" \
        "optional parameter to an existing public member, or changing a signature or a type, is still" \
        "binary-incompatible even when the declaration turns the build green." \
        "State the compatibility judgement in the commit message or in your reply.")
fi

# ---------------------------------------------------------------------------
# Check 3 — translation sync of docs/<lang>/. Advisory only.
# ---------------------------------------------------------------------------
if [[ -x ./check-docs-i18n.sh ]]; then
    i18n_out=$(./check-docs-i18n.sh 2>&1 | head -30)
    if [[ -n "$i18n_out" ]]; then
        i18n_notice=$(printf '%s\n' \
            "The translation sync check reported something (not blocked; after a push, the Docs Check" \
            "workflow fails on the items marked as errors):" \
            "" \
            "$i18n_out" \
            "" \
            "A source and its translation may be committed separately but must be pushed together." \
            "Each message says how to fix it.")
        notice="${notice:+$notice$'\n\n'}$i18n_notice"
    fi
fi

[[ -n "$notice" ]] || exit 0

printf '%s\n' "$notice" >&2

# stderr is not reliably surfaced on a non-blocking exit, so also emit the notice as a
# systemMessage. python3 builds the JSON to keep the embedded diff correctly escaped;
# if it is unavailable the stderr copy above still stands.
if command -v python3 >/dev/null 2>&1; then
    NOTICE="$notice" python3 -c \
        'import json, os; print(json.dumps({"systemMessage": os.environ["NOTICE"]}))' \
        2>/dev/null || true
fi

exit 0
