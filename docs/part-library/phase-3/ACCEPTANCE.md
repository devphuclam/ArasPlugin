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

## Sprint 3.2 Local Verification

Baseline commit:

```text
09688754cf0db4a0ef350d84409371b51765ea5c
```

Sprint 3.2 implements the environment configuration model, config loader, validation, template, packaging safeguards, and tests.

Results:

- Debug build: passed, 0 warnings, 0 errors.
- Release build: passed, 0 warnings, 0 errors.
- Debug tests: passed, 419/419 (403 existing + 16 new).
- Release tests: passed, 419/419.
- Package script: passed.
- Template included in package: `docs/templates/IdeaCadConnector.environment.template.json`
- Active config excluded: validated by script.

### Sprint 3.2 Acceptance

Sprint 3.2 is locally accepted when all checklist items met:

- [x] environment config model exists (`EnvironmentConfiguration`, `EnvironmentConfigurationLoader` in `Core.Configuration`);
- [x] safe config loader exists with lookup order, path expansion, validation;
- [x] missing config does not crash (returns defaults);
- [x] template exists in `docs/templates/`;
- [x] package includes template only (validated by script);
- [x] no secrets committed;
- [x] official role defaults remain correct (TPTKC/TNTKC/NVTKC/NVLCR/PM/Khách hàng);
- [x] tests pass (419/419);
- [x] package script passes;
- [x] docs updated;
- [x] Phase 3 remains `IN_PROGRESS`;
- [x] Sprint 3.3 not started.

### Sprint 3.2 Config Package UAT

**Package tested:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Commit tested:** `f95b58b9bfa154a5e661eb98b3e7f1d5439b3435`

**Config Package UAT accepted** on 2026-07-08.

| Area | Result | Evidence |
|---|---|---|
| Package script rerun | PASS | `package-release.ps1` completed |
| Template included | PASS | `docs/templates/IdeaCadConnector.environment.template.json` exists in package |
| Active config excluded | PASS | `IdeaCadConnector.environment.json` not packaged |
| Template secret scan | PASS | No password/token/secret/cookie/session values in template |
| Role defaults | PASS | Official role defaults present (TPTKC, TNTKC, NVTKC, NVLCR, PM, KhachHang/Customer) |
| Launch without config | PASS | App opened without active config |
| Login | PASS | Aras login still worked |
| Part Library load | PASS | Part Library still loaded |
| Optional active config | PASS | Copied config next to executable |
| Active config safety | PASS | App did not crash with active config present |

### Acceptance Decision

Sprint 3.2 Config Package UAT **accepted**.

Phase 3 remains `IN_PROGRESS`.

Next sprint: **Sprint 3.3 — Internal Installation/UAT Hardening**.

## Not Accepted In Sprint 3.1 or Sprint 3.2

- MSI installer;
- ClickOnce;
- auto-update;
- externalized production credential storage;
- live Aras mutation;
- Phase 4 production rollout.
