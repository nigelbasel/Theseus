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
