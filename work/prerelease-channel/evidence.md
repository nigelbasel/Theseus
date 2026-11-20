# Evidence

**Local, 2026-11-20:** `dotnet test Theseus.slnx -p:NuGetAudit=false` on `nigel/prerelease-channel`: 1759 of 1759
passed, 0 warnings (log: the workstream log), after the review fixes. Twelve tests are new.

**CI:** not run yet. AC 1 and 2 are proved by the first push of `nigel/theseus` after the merge: a pre-release named
"(pre-release from nigel/theseus)", and `releases/latest` unchanged. Recorded here when it has run.

**Live:** AC 3 to 5 need the owner: tick the setting, Check for updates, and the pre-release is offered.

```theseus-evidence
{
  "kit": "method-v0.1",
  "pipeline": "feature",
  "stage": "verify",
  "result": "open",
  "repos": [
    { "repo": "Theseus", "build": "pass", "tests": { "passed": 1759, "total": 1759 }, "warnings": 0 }
  ],
  "scenarios": [],
  "gates": [
    { "gate": "spec", "accepted": "2026-11-20" }
  ]
}
```
