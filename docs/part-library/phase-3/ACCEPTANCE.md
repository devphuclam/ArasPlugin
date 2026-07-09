# Phase 3 Acceptance

**Phase state:** `IN_PROGRESS`

## Sprint 3.1 Acceptance Checklist

- [x] Release packaging docs created.
- [x] Packaging script created.
- [x] Release zip generated.
- [x] Version file generated.
- [x] Checksums generated.
- [x] Required Aras method included in packaging plan.
- [x] Install/config/UAT/rollback docs included in packaging plan.
- [x] Debug build passed.
- [x] Release build passed.
- [x] Debug tests passed.
- [x] Release tests passed.
- [x] Packaging script passed.
- [x] No source feature changes planned.
- [x] No secrets included in docs or script.
- [x] Phase 3 remains `IN_PROGRESS`.

## Sprint 3.1 Local Verification

Baseline commit:

```text
35494964519e014ee60e573a3db718770668ba8c
```

Commands run:

```powershell
dotnet build .\IdeaCadConnector.sln -c Debug
dotnet build .\IdeaCadConnector.sln -c Release
dotnet test .\IdeaCadConnector.sln -c Debug
dotnet test .\IdeaCadConnector.sln -c Release
powershell -ExecutionPolicy Bypass -File .\tools\release\package-release.ps1 -Version v0.3.0-rc1 -Configuration Release
```

Results:

- Debug build: passed, 0 warnings, 0 errors.
- Release build: passed, 0 warnings, 0 errors.
- Debug tests: passed, 403/403.
- Release tests: passed, 403/403.
- Package script: passed.
- Zip created: `artifacts/release/IdeaCadConnector-v0.3.0-rc1.zip`.
- `VERSION.txt`: generated.
- `checksums/SHA256SUMS.txt`: generated.
- Required Aras method included: `aras/server-methods/idea_GetPrimaryIronCadForPart.cs`.

## Sprint 3.1 Package UAT

**Package:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Package UAT performed** on 2026-07-08.

### Package UAT Evidence

| Area | Result | Evidence |
|---|---|---|
| Extract zip | PASS | Clean folder extraction succeeded |
| VERSION.txt | PASS | File exists in package root |
| SHA256SUMS.txt | PASS | File exists under checksums |
| App launch | PASS | Desktop app launched from clean extracted folder |
| Login Aras | PASS | Login completed successfully |
| Part Library load | PASS | Part Library opened and loaded |
| Aras method included | PASS | `idea_GetPrimaryIronCadForPart.cs` included |
| Docs readable | PASS | INSTALL/CONFIGURATION/UAT/ROLLBACK docs readable |
| Missing DLL check | PASS | No missing DLL error |
| Secret/artifact check | PASS | No secret/artifact issue found |

### Acceptance Decision

Sprint 3.1 Package UAT **accepted**.

Phase 3 remains `IN_PROGRESS`.

Next sprint: **Sprint 3.2 — Environment Configuration Hardening**.

## Not Accepted In Sprint 3.1

- MSI installer;
- ClickOnce;
- auto-update;
- externalized production credential storage;
- live Aras mutation;
- Phase 4 production rollout.
