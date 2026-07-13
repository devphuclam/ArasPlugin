# UAT Checklist

## Package Smoke

- [x] Zip extracts without errors.
- [x] `VERSION.txt` exists and matches the expected release candidate.
- [x] `checksums/SHA256SUMS.txt` exists.
- [x] `app/IdeaCadConnector.Desktop.exe` exists.
- [x] `aras/server-methods/idea_GetPrimaryIronCadForPart.cs` exists.
- [x] `docs/INSTALL.md`, `CONFIGURATION.md`, `ROLLBACK.md`, `UAT-CHECKLIST.md`, and `RELEASE-NOTES.md` exist.

## App Smoke

- [x] App launches from extracted `app/` folder.
- [x] User can sign in to target Aras.
- [x] PDM Projects screen opens.
- [x] Part Library screen opens.
- [x] Library list loads for an authorized user.
- [x] Part Picker opens.
- [x] Detail tabs load: CAD, BOM, Revisions, Where Used.
- [x] Open in Aras opens the expected browser URL.
- [x] Download CAD behavior is tested where Vault permissions allow.
- [x] Open in IronCAD behavior is tested where IronCAD is installed.

## Role Smoke

- [x] `ExampleManager` manager behavior is acceptable.
- [x] `ExampleReviewer` reviewer behavior is acceptable.
- [x] `ExampleContributor` contributor behavior is acceptable.
- [x] `ExampleAssemblyViewer` assembly/viewer behavior is acceptable where in scope.
- [x] `ExampleProjectViewer` project viewer behavior is acceptable.
- [x] Customer/unknown user behavior remains conservative and read-only.

## Sprint 3.2 Environment Config Smoke

- [x] Config template exists in package: `docs/templates/IdeaCadConnector.environment.template.json`
- [x] Active config (`IdeaCadConnector.environment.json`) not present in package
- [x] Config model loads without crashing when file is missing
- [x] Config model loads from explicit `IDEA_CAD_CONNECTOR_ENV_CONFIG` path
- [x] Config model loads from `%APPDATA%/IdeaCadConnector/` fallback
- [x] Malformed JSON produces clear error (not crash)
- [x] Secret-like key (`password`, `token`, `secret`) produces warning
- [x] Role defaults work without any config file
- [x] Path expansion works for `%LOCALAPPDATA%` and `%APPDATA%`
- [x] `schemaVersion` other than 1 produces validation error
- [x] Empty `ironCadExecutablePath` does not crash
- [x] App launches without active config
- [x] Login still works
- [x] Part Library still loads
- [x] Active config next to exe does not crash

## Sprint 3.3 Installation Hardening Smoke

- [x] Package validation script runs against zip
- [x] Package validation script returns exit code 0
- [x] Installation hardening doc present: `docs/INSTALLATION-HARDENING.md`
- [x] Machine readiness doc present: `docs/MACHINE-READINESS.md`
- [x] Troubleshooting doc present: `docs/TROUBLESHOOTING.md`
- [x] IT handoff doc present: `docs/IT-HANDOFF.md`
- [x] Internal UAT result template present: `docs/INTERNAL-UAT-RESULT-TEMPLATE.md`
- [x] Validation script present: `tools/validate-release-package.ps1`
- [x] Active config excluded from package
- [x] No secrets in template

## Sprint 3.3 UAT Result

Sprint 3.3 Internal Installation Package UAT accepted on 2026-07-09. Package `IdeaCadConnector-v0.3.0-rc1.zip`. No blockers. See [ACCEPTANCE.md](ACCEPTANCE.md).

## Sprint 3.4 Production Readiness Smoke

- [x] Production readiness doc present: `docs/PRODUCTION-READINESS.md`
- [x] Go/No-Go checklist present: `docs/GO-NO-GO-CHECKLIST.md`
- [x] Release sign-off template present: `docs/RELEASE-SIGNOFF-TEMPLATE.md`
- [x] Release manifest present: `docs/RELEASE-MANIFEST-v0.3.0-rc1.md`
- [x] Known limitations doc present: `docs/KNOWN-LIMITATIONS.md`
- [x] Phase 3 closeout plan present: `docs/PHASE-3-CLOSEOUT-PLAN.md`
- [x] Verification script present: `tools/verify-release-readiness.ps1`
- [x] Active config excluded from package
- [x] No secrets in template

## Sprint 3.4 UAT Result

Sprint 3.4 Production Readiness UAT accepted on 2026-07-09. Package `IdeaCadConnector-v0.3.0-rc1.zip`. No blockers. See [ACCEPTANCE.md](ACCEPTANCE.md).

## Phase 3 Result

**Phase 3 — Deployment and Production Hardening: COMPLETE.** All 4 sprints accepted. See [ACCEPTANCE.md](ACCEPTANCE.md).

## Package Result

- [x] Ready for wider internal UAT.
- [ ] Blocked; blocker recorded in release notes or issue tracker.
