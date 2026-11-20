# Evidence

**Local, 2026-11-20:** `dotnet test Theseus.slnx -p:NuGetAudit=false` on `nigel/prerelease-channel`: 1759 of 1759
passed, 0 warnings (log: the workstream log), after the review fixes. Twelve tests are new.

**CI, 2026-11-20:** the first push of `nigel/theseus` after the merge built green and published pre-release
`v2026.11.20.16249`, titled "(pre-release from nigel/theseus)"; `releases/latest` stayed `v2026.11.19.23452`, from
`main` (AC 1, 2). **Live:** Nigel ticked the setting and installed it through Check for updates (AC 3 to 5).

**Live:** AC 3 to 5 need the owner: tick the setting, Check for updates, and the pre-release is offered.

```theseus-evidence
{
  "kit": "method-v0.1",
  "pipeline": "feature",
  "stage": "verify",
  "result": "pass",
  "repos": [
    { "repo": "Theseus", "build": "pass", "tests": { "passed": 1759, "total": 1759 }, "warnings": 0 }
  ],
  "scenarios": [],
  "gates": [
    { "gate": "spec", "accepted": "2026-11-20" }
  ]
}
```
