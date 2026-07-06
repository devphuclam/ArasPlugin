# Part Library Phase 1 Acceptance

## Automated Evidence

Baseline commit:

```text
08a9986d9cc867a2948afe5a56676730ada54fe4
```

Completion commit:

```text
b7f6cf67d0d191ddb71b3e3926064d928ded2c8c
```

Build command:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'IdeaCadConnector.sln' /t:Restore,Build /p:Configuration=Debug /m
```

Result on 2026-07-06:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Full test command:

```powershell
dotnet test tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj `
  --configuration Debug --no-restore
```

Result:

```text
Total tests: 117
Passed: 117
Failed: 0
```

## Covered Regression Scenarios

- AML parser emits only top-level Items.
- Library count and search use compatible Entry semantics.
- Entry IDs are required and deduplicated case-insensitively.
- Nested Part objects do not become phantom rows.
- A real malformed relationship remains a nonblank diagnostic row.
- resolution-failed and deprecated Entries cannot execute reuse.
- Add dialog read-only bindings are one-way.
- setting `DialogResult` does not call `Close` again.
- no parent, null parent, and invalid quantity are rejected safely.
- valid acceptance writes exactly one workspace reference.
- cancel writes no reference.
- reference-store exceptions become status messages instead of process termination.

## Live Acceptance Record

The project owner declared Phase 1 complete on 2026-07-06. Detailed per-case screenshots were not stored in the repository.

The live acceptance set is:

1. Library count matches actual Entry relationships with no blank rows.
2. Failed `LatestReleased` resolution remains visible and cannot be reused.
3. A valid Entry opens the dialog; Cancel and Add close safely.
4. A valid Add creates exactly one local Library reference.
5. Analyze shows the reference under the selected PDM parent.
6. Push reuses the existing Part and CAD and creates/updates one BOM relationship.
7. Repeated push does not create duplicate Part, CAD, BOM, or Usage.
8. Usage count matches authoritative Usage records.
9. Publish/deprecate state and permission behavior match Aras.
10. Missing analyzed PDM structure produces a clear message without opening the dialog.

## Evidence Policy

Future regressions must add an automated test and a dated Errata entry in this file. Phase 2 must record its evidence separately.
