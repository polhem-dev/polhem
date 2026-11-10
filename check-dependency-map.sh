#!/usr/bin/env bash
# The dependency map must match the project references in src/.
#
# Why: the compiler does not read documents, so a diagram drifts as soon as a reference changes, and nothing else
# notices. The architecture document's diagrams are what readers and agents take as the allowed edges.
#
# What it checks, in docs/<lang>/architecture/dependency-map.md for every language:
# - The edges of the area diagrams, between the `dependency-map:areas` comments, are exactly the references of the
#   csproj files.
# - Every project is declared inside exactly one area's subgraph.
# - The edges of the overview diagram, between the `dependency-map:overview` comments, are exactly the area pairs that
#   those references cross, so the overview is derived from the same facts.
# - The projects each overview box lists are exactly the projects of that area. A box names them without `Polhem.`, and
#   a name starting with a dot extends the last full name before it (`Web.Blazor, .Mud` is `Web.Blazor.Mud`).
#
# Left out: `Polhem.Analyzers`, which only orders the build and never reaches a consumer, and references that leave
# src/, such as the `Polhem.JsonRpc.*` packages. The external package table in the document is not checked.
#
# Known limit: a csproj reference is matched as text, so a reference added by a shared props or targets file is not seen.
set -euo pipefail
cd "$(dirname "$0")"

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

git ls-files 'src/*/*.csproj' | while read -r file; do
    basename "$file" .csproj
done | grep -v '^Polhem\.Analyzers$' | sort > "$work/projects"

git ls-files 'src/*/*.csproj' | while read -r file; do
    name=$(basename "$file" .csproj)
    { grep -oE 'ProjectReference Include="[^"]+"' "$file" || true; } \
        | sed -E 's/.*[\\/]([^\\/]+)\.csproj"/\1/' \
        | while read -r target; do
            if grep -qxF "$target" "$work/projects" && [[ "$name" != "Polhem.Analyzers" ]]; then
                echo "$name -> $target"
            fi
        done
done | sort > "$work/actual"

failed=0

report() {
    local doc=$1 problem=$2 lines=$3
    echo "$doc: $problem"
    sed 's/^/  /' "$lines"
    failed=1
}

for doc in docs/*/architecture/dependency-map.md; do
    awk -v edges="$work/edges" -v members="$work/members" -v overview="$work/overview" -v listed="$work/listed" \
        -v errors="$work/errors" '
        function fail(message) { print "line " FNR ": " message > errors }
        /<!-- dependency-map:areas:start -->/ { section = "areas"; next }
        /<!-- dependency-map:areas:end -->/ { section = ""; next }
        /<!-- dependency-map:overview:start -->/ { section = "overview"; next }
        /<!-- dependency-map:overview:end -->/ { section = ""; next }
        section == "" { next }
        /^```mermaid/ { delete label; area = ""; next }
        section == "overview" && match($0, /^ *[A-Za-z]+ --> [A-Za-z]+ *$/) { print $1, $3 > overview; next }
        section == "overview" && match($0, /^ *[A-Za-z]+\["<b>/) {
            id = $0; sub(/^ */, "", id); sub(/\[.*/, "", id)
            text = $0; sub(/^[^"]*"/, "", text); sub(/".*/, "", text)
            sub(/^.*<\/b>/, "", text); gsub(/<br\/>/, " ", text)
            n = split(text, parts, ",")
            base = ""
            for (i = 1; i <= n; i++) {
                part = parts[i]; gsub(/^ +| +$/, "", part)
                if (part == "") continue
                if (substr(part, 1, 1) == ".") name = base part
                else { name = part; base = part }
                print "Polhem." name, id > listed
            }
            next
        }
        section != "areas" { next }
        /^ *subgraph / { area = $2; next }
        /^ *end *$/ { area = ""; next }
        match($0, /^ *[A-Za-z]+\["Polhem[^"]*"\]/) {
            line = substr($0, RSTART, RLENGTH)
            sub(/^ */, "", line)
            id = line; sub(/\[.*/, "", id)
            name = line; sub(/^[^"]*"/, "", name); sub(/".*/, "", name)
            label[id] = name
            if (area != "") print name, area > members
            next
        }
        /^ *[A-Za-z]+ --> [A-Za-z]+ *$/ {
            if (!($1 in label)) fail("no node declared for " $1)
            else if (!($3 in label)) fail("no node declared for " $3)
            else print label[$1] " -> " label[$3] > edges
        }
    ' "$doc" </dev/null

    touch "$work/edges" "$work/members" "$work/overview" "$work/listed" "$work/errors"
    sort -o "$work/edges" "$work/edges"
    sort -o "$work/members" "$work/members"
    sort -u -o "$work/overview" "$work/overview"
    sort -o "$work/listed" "$work/listed"

    if [[ -s "$work/errors" ]]; then report "$doc" "the diagrams cannot be read" "$work/errors"; fi

    comm -23 "$work/actual" "$work/edges" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "in the csproj files but not in the area diagrams" "$work/diff"; fi
    comm -13 "$work/actual" "$work/edges" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "in the area diagrams but not in the csproj files" "$work/diff"; fi

    cut -d' ' -f1 "$work/members" | sort > "$work/declared"
    comm -23 "$work/projects" "$work/declared" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "projects in no area" "$work/diff"; fi
    comm -13 "$work/projects" "$work/declared" | sort -u > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "projects in an area that are not in src/" "$work/diff"; fi
    uniq -d "$work/declared" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "projects in more than one area" "$work/diff"; fi

    awk 'NR == FNR { area[$1] = $2; next }
         (area[$1] != "" && area[$3] != "" && area[$1] != area[$3]) { print area[$1], area[$3] }' \
        "$work/members" "$work/actual" | sort -u > "$work/crossings"
    comm -23 "$work/crossings" "$work/overview" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "area pairs the references cross but the overview does not show" "$work/diff"; fi
    comm -13 "$work/crossings" "$work/overview" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "area pairs in the overview that no reference crosses" "$work/diff"; fi

    comm -23 "$work/members" "$work/listed" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "projects of an area that its overview box does not list" "$work/diff"; fi
    comm -13 "$work/members" "$work/listed" > "$work/diff"
    if [[ -s "$work/diff" ]]; then report "$doc" "projects an overview box lists that are not in its area" "$work/diff"; fi

    rm -f "$work/edges" "$work/members" "$work/overview" "$work/listed" "$work/errors"
done

if [[ "$failed" -ne 0 ]]; then
    echo "Update the diagrams in docs/en/architecture/dependency-map.md and its translations to match the csproj files."
    exit 1
fi
