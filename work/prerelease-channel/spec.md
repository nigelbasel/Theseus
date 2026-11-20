# Spec: pre-releases from the project's primary branch

**Pipeline:** `feature`, project `theseus-bootstrap`. **Branch:** `nigel/prerelease-channel`, off `nigel/theseus`.
**Approved:** Nigel, 2026-11-20 ("yes, go ahead"), after the morning's install found that Check for updates offers
nothing from the primary branch.

## The problem

Releases are published only from pushes to `main`. Under the project branch model (2026-11-19) nothing reaches `main`
until the owner merges the primary's pull request, so the update check cannot offer him the work he must try live
before merging it.

## Acceptance criteria

1. A push to `nigel/theseus` that builds and tests green publishes a GitHub **pre-release** with the same installer
   and checksums as a release from `main`. It is never marked latest.
2. Releases from `main` are unchanged, and `releases/latest` (the default update check and the download link) keeps
   answering with `main`'s newest release.
3. Settings has **Include pre-releases in Check for updates**, off by default and off for a settings file from before
   it.
4. With it on, the update check offers the newest usable release by build stamp, pre-release or not; drafts and
   releases with no installer are skipped. With it off, it behaves as before.
5. The setting is read on every check, so changing it needs no restart. The check still costs one call to GitHub.

## Out of scope

Pre-releases from workstream branches; removing old pre-releases (deleting needs asking); a channel per project.

## Holdout scenarios that apply

None.
