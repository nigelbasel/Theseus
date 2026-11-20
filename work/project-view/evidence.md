# Evidence, pull request 1

**Local, 2026-11-20:** `dotnet test Theseus.slnx -p:NuGetAudit=false` on `nigel/project-view`: 1928 of 1928 passed,
0 warnings, run independently of the implementer after the last review fixes. The base was 1762.

**Live:** not checked on a running board. Pull request 1 adds only the Project repositories setting and the Project
menu to the screen; the project card and view are pull request 2.

```theseus-evidence
{
  "kit": "method-v0.2",
  "pipeline": "feature",
  "stage": "verify",
  "result": "pass",
  "repos": [
    { "repo": "Theseus", "build": "pass", "tests": { "passed": 1928, "total": 1928 }, "warnings": 0 }
  ],
  "scenarios": [],
  "gates": [
    { "gate": "spec", "accepted": "2026-11-20" }
  ]
}
```

# Evidence, pull request 2

**Local, 2026-11-20:** `dotnet test Theseus.slnx -p:NuGetAudit=false` on `nigel/project-view`: 1995 of 1995 passed,
0 warnings, run independently of the implementer after the last review fixes.

**Live:** not yet run as an app. Dragging a project card, the double-click, Open spec launching an editor and the
confirmation box are not covered by tests; the owner sees them first on the pre-release.

```theseus-evidence
{
  "kit": "method-v0.2",
  "pipeline": "feature",
  "stage": "verify",
  "result": "pass",
  "repos": [
    { "repo": "Theseus", "build": "pass", "tests": { "passed": 1995, "total": 1995 }, "warnings": 0 }
  ],
  "scenarios": [],
  "gates": [
    { "gate": "spec", "accepted": "2026-11-20" }
  ]
}
```

# Evidence, pull request 3

Written by the implementer, not a verifier: it records the implementer's own run and what is still open, so verify is
not passed here.

**Local, 2026-11-20:** `dotnet test Theseus.slnx -p:NuGetAudit=false` on `nigel/project-view`: 2022 of 2022 passed,
0 warnings, run by the implementer.

**Holdout:** the spec names **resume tomorrow** for this pull request, which changes the window a resumed session opens
in. It has **not been run**: this session cannot reach the holdout scenarios. Verify stays open until a verifier card
has run it and it passes (P18). Nothing here claims it.

**Live:** not run as an app; raising a real terminal window was not tried.

```theseus-evidence
{
  "kit": "method-v0.2",
  "pipeline": "feature",
  "stage": "verify",
  "result": "open",
  "repos": [
    { "repo": "Theseus", "build": "pass", "tests": { "passed": 2022, "total": 2022 }, "warnings": 0 }
  ],
  "scenarios": [
    { "name": "resume tomorrow", "result": "not-run", "by": null }
  ],
  "gates": [
    { "gate": "spec", "accepted": "2026-11-20" }
  ]
}
```


**After the review fixes, 2026-11-20:** 2029 of 2029 passed, 0 warnings, run independently of the implementer. Verify
stays open: **resume tomorrow** has not been run by a verifier card.

```theseus-evidence
{
  "kit": "method-v0.2",
  "pipeline": "feature",
  "stage": "verify",
  "result": "open",
  "repos": [
    { "repo": "Theseus", "build": "pass", "tests": { "passed": 2029, "total": 2029 }, "warnings": 0 }
  ],
  "scenarios": [
    { "name": "resume tomorrow", "result": "not-run", "by": null }
  ],
  "gates": [
    { "gate": "spec", "accepted": "2026-11-20" }
  ]
}
```
