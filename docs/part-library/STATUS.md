# Part Library — Status

## Phase 1 (Complete)

Phase 1 shipped in commits including `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c`. See `docs/part-library/phase-1/FINAL-STATUS.md`.

## Phase 2

| Phase | State | Baseline | Sprint |
|---|---|---|---|
| Planning | `COMPLETED` | `956af6841392b609d9c06df60d484fe5244500c1` | Readiness Gate |
| Sprint 2.1 — Core/Backend | `COMPLETED` | `db8f2c167dd6336c4a522a0d0ec29d16a402a57b` | LM-01..08, PP-01..09 |
| Sprint 2.1 — Closeout | `COMPLETED` | `0c78e73d8bff2ff610237d65daeb04286a98da7e` | Fixes F1–F10 |
| Sprint 2.1 — UI | `COMPLETED` | (HEAD) | UI-01..UI-11 |
| Sprint 2.2 — Core/Backend | `COMPLETED` | `f0db0348e4a6a9a70ff6232d5031304b1ed9c211` | ME-01..06, RV-01..07 |
| Sprint 2.2 — UI | `COMPLETED` | (HEAD) | Move Entry + Revision Browser |
| Sprint 2.3 — Core | `COMPLETED` | (HEAD) | VT-01..06, OA-01..02, TAB-01..04 |
| Sprint 2.3 — UI | `COMPLETED` | (HEAD) | CAD/BOM/Rev/WhereUsed tabs, Open in Aras, Download CAD, Open in IronCAD |
| Sprint 2.3 — App UAT | `ACCEPTED` | `idea_GetPrimaryIronCadForPart deployed` | Live CAD lookup fixed; App UAT smoke accepted |
| Sprint 2.4 — Filter/Sort/Hardening | `ACCEPTED` | (current HEAD) | Entry status/CAD status/text filters, 7-column sort, detail status UX hardening, command state regression, 11 new tests |

### Sprint 2.4 — Filter/Sort/Hardening (accepted)

**Build:** Debug — 0 warnings, 0 errors

**Tests:** 403 total — 0 failed, 0 skipped

**New tests:** 44 (PartLibraryVaultServiceTests: 17, IronCadOpenServiceTests: 12, ArasOpenUrlServiceTests: 15)

#### Implemented

**Core Contracts (`IdeaCadConnector.Core.Library`):**
- `IPartLibraryVaultService` — vault download/cache/open interface
- `IIronCadOpenService` — IronCAD launch interface
- `IArasOpenUrlService` — Open-in-Aras URL builder interface

**Core DTOs (`IdeaCadConnector.Core.Library`):**
- `PartLibraryCadFileInfo` — resolved primary CAD file metadata (now includes `CadNumber`, `Revision`, `Generation`, `FileId`)
- `VaultCacheKey` — collision-safe cache key (server/database/FileId/revision)
- `VaultDownloadResult` — download outcome (success, path, error)
- `PartLibraryEntrySummary`/`PartLibraryEntryDetails` — added `Generation` property
- `PartLibraryCadFileInfo` — added `FileId` mapping from `PrimaryCadFileId`

**Desktop Services (`IdeaCadConnector.Desktop.Services`):**
- `PartLibraryVaultService` — `GetPrimaryCadFileInfoAsync` now maps `FileId`, `CadNumber`, `Revision`, `Generation` from entry; `HasNative` checks both `FileId` and `FileName`
- `ArasOpenUrlService` — added `idea_PartLibraryUsage` to approved item types
- `IronCadOpenService` — (unchanged)

**Aras Client (`IdeaCadConnector.Aras.HttpPartLibraryClient`):**
- `GetPrimaryCadInfoAsync` — now returns `CadNumber` and `FileVersion` (from CAD `generation`)
- Detail tab methods implemented:
  - `GetCadDetailsAsync` — resolves entry CAD through `GetPrimaryCadInfoAsync`
  - `GetBomDetailsAsync` — queries Part BOM children for the entry's part
  - `GetRevisionDetailsAsync` — queries revision history via `SearchPartRevisionsAsync`
  - `GetWhereUsedDetailsAsync` — delegates to existing `GetWhereUsedAsync`
  - `GetDetailBundleAsync` — parallel composition of all four + `GetEntryAsync`

**Test Coverage (44 new, existing updated):**
- Vault: cache key equality/name generation, null/empty guard, temp deletion, zero-byte rejection, permission propagation, cancellation, cache-hit shortcut
- IronCAD: executable detection, missing/null path, file not found, not available, adapter invocation, cancellation
- URL: all item types (now includes `idea_PartLibraryUsage`), null/empty guards, encoding, base URI, resource.aspx pattern, database param
- Existing vault test updated: `PrimaryCadFileId` added to stub entry for `HasNative`

#### Not Tested (requires live Aras or IronCAD)

- Real vault download through `IArasCadClient.DownloadNativeFileAsync`
- Real IronCAD process launch
- Real Aras Innovator client URL resolution
- Detail tab methods against live Aras data (CAD/BOM/Revisions/WhereUsed)

#### Completed (Sprint 2.3 — App UAT accepted)

All Sprint 2.3 workstreams completed and UAT accepted:
- `IPartLibraryVaultService` contract and `PartLibraryVaultService` implementation
- `IIronCadOpenService` contract and `IronCadOpenService` implementation
- `IArasOpenUrlService` contract and `ArasOpenUrlService` implementation
- `LibraryServicesFactory` to compose real services from the current session
- `LibraryViewModel` open-target commands for Part, Entry, Library, and CAD navigation
- `BrowserLauncher` validation for safe `http`/`https` URLs
- WPF tab UI implementation with CAD, BOM, Revisions, and Where Used tabs
- 44 focused service tests covering all VT and OA requirements
- UI wiring tests covering routing, service composition, browser launch behavior
- Live CAD lookup fix via server method `idea_GetPrimaryIronCadForPart`
- App UAT smoke accepted: CAD lookup acceptable, Part Library loads, all tabs functional.

#### Completed (Sprint 2.4 — Filters, Sorting, Hardening)

All Sprint 2.4 workstreams implemented:
- **Entry Status filter**: All/Draft/PendingReview/Published/Deprecated
- **CAD Status filter**: All/Available/No CAD/No native file/CAD lookup unavailable
- **Text search**: Filters by item_number/name (already existed, hardened)
- **Sorting**: 7 columns (Item Number, Name, Entry Status, Revision Policy, CAD Status, Usage Count, Last Used On) with Ascending/Descending
- **Detail status UX hardening**: Loading, permission denied, server unavailable, operation cancelled states with localized messages
- **Command state regression**: Verified NVTKC (contributor) cannot Move/Pin; TNTKC (reviewer) can; TPTKC (manager) can manage; viewer blocked
- **Archived Libraries**: Remain hidden by default per D-03
- **Localization**: 25 new keys in en-US, vi-VN, ja-JP
- **New tests**: 11 (filter, sort, command state, detail hardening, regression)
- Debug build passed: 0 warnings, 0 errors
- Release build passed: 0 warnings, 0 errors
- Full tests passed: 403/403

#### Phase 2 Closeout

Phase 2 closed after Sprint 2.4 final live App UAT accepted on 2026-07-08.

Final live UAT performed on actual organization Aras environment. Roles tested: TPTKC (manager), TNTKC (reviewer), NVTKC (contributor), NVLCR (assembly viewer), PM (project viewer). All command states, filters, sorting, tabs, CAD actions, and Aras links verified and accepted. No P0/P1 blocker found.

Final role alignment:
- **TPTKC** — Trưởng phòng thiết kế cơ — Manager — Can manage Libraries, Move Entry, Pin Revision
- **TNTKC** — Trưởng nhóm thiết kế cơ — Reviewer — Can Move Entry, Pin Revision
- **NVTKC** — Nhân viên thiết kế cơ — Contributor — Cannot Move/Pin, can view/use Library
- **NVLCR** — Nhân viên lắp ráp cơ — Assembly viewer — View-only
- **PM** — Quản lý dự án — Project viewer — View-only
- **Khách hàng** — External viewer — View-only, read-only

#### Remaining Known Limitations

- Library restore flows (not in Phase 2 scope)
- Real Download/Open IronCAD depends on local IronCAD install + Vault permissions
- Role mapping is username/config based; future hardening should use Aras Identity membership

## Phase 3

| Phase | State | Baseline | Sprint |
|---|---|---|---|
| Phase 3 - Deployment and Production Hardening | `COMPLETE` | `35494964519e014ee60e573a3db718770668ba8c` | Sprint 3.1 - Release Packaging Baseline |

### Sprint 3.1 — Release Packaging Baseline

**Status:** `PACKAGE_UAT_ACCEPTED`

**Package tested:** `IdeaCadConnector-v0.3.0-rc1.zip`

Scope implemented and accepted:

- repeatable release package script (`tools/release/package-release.ps1`);
- release zip structure for `IdeaCadConnector v0.3.0-rc1`;
- install, configuration, UAT, rollback, and release notes;
- Aras deployment bundle guidance for `idea_GetPrimaryIronCadForPart`;
- checksum and version metadata generation (`VERSION.txt`, `SHA256SUMS.txt`);
- Debug/Release build and test validation.

Package UAT results (all PASS):

| Area | Result |
|---|---|
| Extract zip | PASS |
| VERSION.txt | PASS |
| SHA256SUMS.txt | PASS |
| App launch from clean folder | PASS |
| Login Aras | PASS |
| Part Library load | PASS |
| Aras method included | PASS |
| Docs readable | PASS |
| Missing DLL check | PASS |
| Secret/artifact check | PASS |

### Sprint 3.2 — Environment Configuration Hardening

**Status:** `CONFIG_PACKAGE_UAT_ACCEPTED`

Sprint 3.2 adds a non-secret environment configuration model for the desktop app.

**Implemented:**

- **Config model**: `EnvironmentConfiguration` and `EnvironmentConfigurationLoader` in `IdeaCadConnector.Core.Configuration`
- **Lookup order**: (1) `IDEA_CAD_CONNECTOR_ENV_CONFIG` env var, (2) next to executable, (3) `%APPDATA%/IdeaCadConnector/`, (4) built-in defaults
- **Validation**: schema version check, malformed JSON detection, secret-like key detection (password, token, secret, cookie, session, credential, passphrase, auth, apikey, api_key)
- **Path expansion**: `%LOCALAPPDATA%`, `%APPDATA%`, `%USERPROFILE%` supported in path fields
- **Fallback**: missing/empty/corrupt config returns defaults with clear diagnostic, never crashes
- **Template**: `IdeaCadConnector.environment.template.json` in docs and release package
- **Packaging**: script validates no active config is included; template only
- **Role defaults**: TPTKC (manager), TNTKC (reviewer), NVTKC (contributor), NVLCR/PM/Khách hàng (read-only)
- **Tests**: 16 new; total 419/419 pass

**Build:** Debug — 0 warnings, 0 errors; Release — 0 warnings, 0 errors

### Sprint 3.3 — Internal Installation/UAT Hardening

**Status:** `INTERNAL_INSTALLATION_PACKAGE_UAT_ACCEPTED`

**Package tested:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Commit tested:** `00d4b70454d7daf438d44385dde3dcebf72fbd0b`

**Decision:** Sprint 3.3 Internal Installation Package UAT accepted.

Sprint 3.3 adds installation hardening docs, a package validation script, a troubleshooting guide, an IT handoff guide, and an internal UAT result template.

**Implemented:**

- **INSTALLATION-HARDENING.md**: Where to extract, how to run, config setup, what not to edit, verify package, rollback
- **MACHINE-READINESS.md**: Windows/.NET 4.8 requirements, network access, role identities, Aras permissions checklist
- **TROUBLESHOOTING.md**: 14 common issues with symptom/cause/check/action/blocker severity
- **INTERNAL-UAT-RESULT-TEMPLATE.md**: Fillable table with 25 test areas, issues table, decision field
- **IT-HANDOFF.md**: What to send/not send, Aras/machine prep, smoke test steps, rollback, escalation path
- **validate-release-package.ps1**: Validates structure, files, docs, config exclusion, forbidden files, secrets; returns exit code
- **Package script updated**: Includes all new docs and validation script in release zip

**Validation:** 23/23 checks PASS, exit code 0

**Build:** Debug — 0 warnings, 0 errors; Release — 0 warnings, 0 errors

**UAT Results:** Package script rerun PASS, validation on zip PASS, clean extraction PASS, validation on extracted folder PASS, new docs included PASS, validation script included PASS, machine readiness doc usable PASS, troubleshooting doc usable PASS, UAT result template usable PASS, IT handoff doc usable PASS, active config excluded PASS, secret scan PASS, app launch PASS, login Aras PASS, Part Library load PASS. Known issues: none.

**Sprint 3.3 accepted** on 2026-07-09.

### Sprint 3.4 — Production Release Readiness

**Status:** `PRODUCTION_READINESS_UAT_ACCEPTED`

**Package tested:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Commit tested:** `dc4fe6c17d041a6618662187c209532e7fd0be0a`

**Decision:** Sprint 3.4 Production Readiness UAT accepted.

Sprint 3.4 adds final release-readiness documentation, checklists, and a release verification script.

**Implemented:**

- **PRODUCTION-READINESS.md**: Package identity, build/test baseline, Aras/machine prerequisites, package contents, no-secrets policy, rollback, escalation, final decision form
- **GO-NO-GO-CHECKLIST.md**: 10-section checklist (build/test, package integrity, Aras readiness, role readiness, machine readiness, security, rollback, known limitations, business sign-off, IT sign-off)
- **RELEASE-SIGNOFF-TEMPLATE.md**: Fillable sign-off form with release info, tester info, decision, accepted limitations, required follow-up, sign-off table
- **RELEASE-MANIFEST-v0.3.0-rc1.md**: Complete manifest of version, package, source commit, checksums, app, Aras method, docs, scripts, exclusions, build/test baseline, sprint acceptance summary
- **KNOWN-LIMITATIONS.md**: Installer, config wiring, IronCAD, CAD download, permissions, restore flow, production rollout, role mapping
- **PHASE-3-CLOSEOUT-PLAN.md**: Remaining steps before Phase 3 COMPLETE (UAT, validation, go/no-go, closeout, optional tag)
- **verify-release-readiness.ps1**: Runs build, test, package, validate in sequence; prints PASS/FAIL summary; returns exit code
- **Package script updated**: Includes all Sprint 3.4 docs and verification script
- **Validation script updated**: Checks for Sprint 3.4 docs and verification script

**Build:** Debug — 0 warnings, 0 errors; Release — 0 warnings, 0 errors

**Tests:** Debug — 419/419 pass; Release — 419/419 pass

**Package validation:** 30/30 checks PASS, exit code 0

**Release verification:** 4/4 checks PASS, exit code 0

**UAT Results:** Production readiness doc usable PASS, go/no-go checklist usable PASS, release sign-off template usable PASS, release manifest PASS, known limitations accepted PASS, closeout plan PASS, verification script PASS. No P0/P1 blocker reported. Sprint 3.4 accepted on 2026-07-09.

## Phase 3 Closeout

**Phase 3 — Deployment and Production Hardening:** `COMPLETE`

Phase 3 completed after all 4 sprints accepted:

| Sprint | Scope | Status |
|---|---|---|
| 3.1 | Release Packaging Baseline | `PACKAGE_UAT_ACCEPTED` |
| 3.2 | Environment Configuration Hardening | `CONFIG_PACKAGE_UAT_ACCEPTED` |
| 3.3 | Internal Installation/UAT Hardening | `INTERNAL_INSTALLATION_PACKAGE_UAT_ACCEPTED` |
| 3.4 | Production Release Readiness | `PRODUCTION_READINESS_UAT_ACCEPTED` |

**Final package:** `IdeaCadConnector-v0.3.0-rc1.zip`

**Final build/test baseline:** Debug 0w/0e, Release 0w/0e, 419/419 tests pass.

**Required Aras method:** `idea_GetPrimaryIronCadForPart`

**Official roles:** TPTKC (manager), TNTKC (reviewer), NVTKC (contributor), NVLCR (assembly viewer), PM (project viewer), Khách hàng (external viewer).

**Known limitations:** Documented in [KNOWN-LIMITATIONS.md](phase-3/KNOWN-LIMITATIONS.md).

**Recommended next action:** Optional Git tag `v0.3.0-rc1` (not created in this commit).
