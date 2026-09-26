# Releasing: guardrails around irreversible steps

A release is rare, and its procedure is read when a release is prepared. The two rules below are different: they
must hold even when nobody thought about releasing, because the dangerous case is exactly the one where an agent did
not realize it was doing release work.

## 1. An agent never pushes a tag on its own

**Pushing a `v*` tag runs `.github/workflows/nuget-publish.yml`, which publishes the packages, and a published
package cannot be withdrawn** (NuGet can only unlist or deprecate it). This is an irreversible action towards the
outside world and **needs the user's explicit consent** every time.

- Pushing a branch or opening a pull request is reversible and follows the normal workflow.
- Before it is pushed, a tag can be recreated at any time: `git tag -d vX.Y.Z`.
- "Let's release" is not consent to push the tag. Do the preceding steps, then stop and ask.

## 2. Never change tests or source code to make the release build pass

If the clean build or the tests before a release are red, that is a signal. Turning it green by editing the check
defeats the check itself.
