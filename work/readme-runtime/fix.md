# readme-runtime: fix

Pipeline: `bug` (method kit v0), stage **fix**, autonomy L0. Card git-22, branch `main`, repo Theseus.
Started on Nigel's acceptance of the reproduce stage ("yes, go ahead with the fix").

## Cause

The projects were moved to .NET 11 (`global.json` pins a .NET 11 SDK, and every project targets `net11.0`),
and the README's Requirements section was not updated with them. Nothing tied the two together, so the
mismatch could not be caught.

## Change

`README.md:27`: ".NET 10 Desktop Runtime" and its link to `dotnet/10.0` become ".NET 11 Desktop Runtime" and
`dotnet/11.0`. One line.

## Why this addresses the cause and not only the symptom

The text was the symptom. The cause was that nothing checked it. The reproduction test now does: the next
change of target framework fails the build until the README follows it.

## Result

`TheRuntimeTheReadmeAsksFor_IsTheOneTheAppTargets` passes. The test is byte-for-byte unchanged since the
commit in which it failed (`git diff 1f1d73c -- tests/Theseus.Tests/Repository/ReadmeTests.cs` is empty).

## Not checked here (for the verifier)

- That `https://dotnet.microsoft.com/download/dotnet/11.0` exists and offers a Desktop Runtime.
- That an app built with the pinned release-candidate SDK starts on the runtime that page offers.
- The full suite. Only the reproduction test was run in this stage.

## Record

| Stage | Started | Finished | Notes |
|---|---|---|---|
| fix | after 11:35:06 (the reproduce commit) | the fix commit's time | Passing run logged at 11:35:56. The exact start (Nigel's acceptance) was not captured |
