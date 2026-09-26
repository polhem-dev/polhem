# Pre-commit verification hook

This repository uses a Claude Code `PreToolUse` hook to enforce verification **before** the agent runs `git commit`.
It is declared in `.claude/settings.json` and implemented in `.claude/hooks/pre-commit-verify.sh`.

> **Why the checks are asymmetric, why `--no-incremental`, why it must fail open, and how the cwd for "another
> repository" is determined are all written in the script's file header.** Read that script for the details; this
> file does not copy them.

## What the agent needs to know

1. **A failing clean Release build blocks the commit** (exit 2). Combined with `TreatWarningsAsErrors=true`,
   any warning is a failure. Measured at about 5 seconds.
2. **A change to `PublicAPI.Unshipped.txt` only produces a notice and does not block**, but **you must state your
   compatibility judgement in the commit message or in your reply**. The analyzer catches "not declared"; it cannot
   catch "declared but binary incompatible" (for example, adding an optional parameter to an existing public
   constructor). The purpose of the notice is to turn that judgement from silent into something you must face.
3. **Output from `check-docs-i18n.sh` only produces a notice and does not block.** Committing a source document and
   its translations separately is reasonable; the real gate is the Docs Check in CI. When you see the notice, update
   the translations against the source and `--stamp` them before you push. Do not wait for CI on main to go red.
4. **Do not modify tests or source code to make the hook pass.** That is exactly what this hook exists to prevent.
5. **`git --no-verify` has no effect on it** (that is a flag for git's own hooks). It hooks into Claude Code's tool
   calls, not `.git/hooks/`, so **a commit the user makes directly in their own terminal is not affected**.

## Why a hook and not a written rule

A written rule asks the agent to **comply voluntarily**; a hook is **enforced** by the harness. Both of the following
mistakes came from "the agent did not think to check", so writing them as rules does not work: claiming "build is
clean" based on an incremental build; and turning a public API change green by adding it to
`PublicAPI.Unshipped.txt` without judging binary compatibility.

## Temporarily disabling it

When you need to bypass it (a WIP commit, or the build fails for environmental reasons): comment out the `hooks`
block in `.claude/settings.json` and restart the session, or have the user make that commit in their own terminal.
