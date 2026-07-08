# Part Library Phase 2 README

**State:** `IN_PROGRESS` (Sprint 2.1 UAT smoke accepted; Sprint 2.2 locally accepted; Sprint 2.3 App UAT accepted; 390/390 tests pass)

## Objective

Complete the desktop Part Library experience on top of the working Phase 1 and Sprint 2.1 backend contracts, without regressing reuse, lifecycle, or usage-tracking behavior.

## Explicit Non-Goals

- no Phase 1 schema rewrite;
- no live Aras deployment claims from local code only;
- no Sprint 2.2+ features inside Sprint 2.1 closeout;
- no direct AML inside WPF views or dialog code-behind.

## Baseline

- planning baseline: `956af6841392b609d9c06df60d484fe5244500c1`
- Sprint 2.1 UI implementation baseline: `0c78e73d8bff2ff610237d65daeb04286a98da7e`
- depends on Phase 1 completion: `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c`
- package location: [incoming/](incoming/)

## Current Owner

- phase owner: Codex local implementation
- current implementation surface: Aras client, desktop client, and tests

## Business Roles

| ID | Persona | Role |
|---|---|---|
| `NVTKC` | Mechanical Designer | Library Contributor |
| `TNTKC` | Mechanical Team Leader | Library Reviewer (inherits Contributor) |
| `TKC_MANAGER` | Mechanical Design Department Manager | Library Manager (inherits Reviewer + Contributor) |
| `PROJECT_VIEWER` | Project Manager | Project and Usage Viewer |
| `MFG_VIEWER` | Mechanical Assembly User | Manufacturing Viewer |
| `CUSTOMER` | Customer | External Viewer |

## Approved Decisions

| ID | Decision | Status | Approved Rule |
|---|---|---|---|
| `D-01` | who may create Libraries | `APPROVED` | Only `TKC_MANAGER` (Library Manager) |
| `D-02` | duplicate active Entry rule | `APPROVED` | Unique on `Library ID + part_config_id`, case-insensitive. Active statuses: Draft, PendingReview, Published. Deprecated is not active. |
| `D-03` | archived Library visibility | `APPROVED` | Hidden by default; explicit Archived or All filter to display. Read-only, no new Entries, not selectable as Move or Part Picker target. |
| `D-04` | move preserves metadata and lifecycle | `APPROVED` | Preserve identity, config_id, policy, pinned info, lifecycle, status, and metadata. Block if lifecycle cannot be preserved safely. |
| `D-05` | Vault cache model | `APPROVED` | Per-user cache keyed by server, database, File ID, and revision/generation. Temp-first download; reject zero-byte; atomic move; clean on failure. |
| `D-06` | IronCAD open mechanism | `APPROVED` | Preferred: existing bridge/connector. Fallback: process launch only with verified local file. |

## Workstreams

| ID | Workstream |
|---|---|
| `WS1` | Library Management |
| `WS2` | Aras Part Picker |
| `WS3` | Move Entry |
| `WS4` | Revision Browser |
| `WS5` | Vault and IronCAD |
| `WS6` | Open in Aras |
| `WS7` | Detail Tabs |
| `WS8` | Filters and UX Hardening |

## Sprint Plan

| Sprint | Scope | Status |
|---|---|---|---|
| `2.1` | `WS1` + `WS2` | UAT smoke accepted |
| `2.2` | `WS3` + `WS4` | locally accepted |
| `2.3` | `WS5` + `WS6` + `WS7` | App UAT accepted |
| `2.4` | `WS8` + hardening/UAT prep | not started |

## Sprint 2.1 Completion Evidence

Local Sprint 2.1 closeout now includes:

- Library visibility filter `Active / Archived / All`
- role-aware Library command state
- Create Library dialog
- Edit Library dialog
- Archive Library flow
- Aras Part Picker search/filter/page flow
- Part preview and add-selected-Part flow
- duplicate Library and duplicate Entry handling
- archived Library target blocking

Phase transition history:

- Phase 2 moved from `IN_PROGRESS` to `LOCALLY_ACCEPTED` after Sprint 2.1 UAT smoke evidence was recorded on 2026-07-06.

Verification on 2026-07-06:

- Debug build: 0 warnings, 0 errors
- Release build: 0 warnings, 0 errors
- focused tests: 81/81 passed
- full tests: 214/214 passed

Sprint 2.2 UAT smoke evidence recorded on 2026-07-07 (state: `UAT_SMOKE_PASSED_WITH_FOLLOW_UP`).

## Sprint 2.2 Complete Packet

Sprint 2.2 core backend + UI are now implemented:

- `MoveLibraryEntryAsync` contract support added to `IPartLibraryClient`
- backend move orchestration added in `HttpPartLibraryClient`
- `SearchPartRevisionsAsync` contract support added to `IPartLibraryClient`
- backend revision-history query and pin eligibility support added in `HttpPartLibraryClient`
- Move Entry dialog (`MoveLibraryEntryDialog`) with target Library selection, archived/current exclusion, duplicate blocking, and backend error handling
- Revision Browser dialog (`PartRevisionBrowserDialog`) with paged revision history grid, page size selection, and Pin Selected Revision action
- role-aware command gating for Move Entry (manager/contributor) and Revision Browser (contributor/reviewer/manager)
- focused Sprint 2.2 UI tests: 30 new (8 Move VM + 11 Revision VM + 11 LibraryViewModel integration)
- Debug build passed
- Release build passed
- full tests passed `261/261`

Sprint 2.2 follow-up patch applied. Move Entry and Pin Revision are now gated on `IsReviewerOrHigher` (TNTKC, lamPM, admin). lamEngineer/NVTKC can view Revision Browser but cannot Move or Pin.

## Sprint 2.1 UAT Smoke Evidence

Recorded closeout evidence:

- Admin smoke test passed.
- `lamEngineer` UAT confirmed contributor behavior, no Library admin commands, and Part Picker usability where Aras permission allows.
- `lamPM` UAT confirmed manager behavior for current UAT, with Create/Edit/Archive Library available.
- Viewer/unknown behavior confirmed conservative read-only behavior.
- Automated verification passed: Debug build, Release build, and full tests `214/214`.

## Sprint 2.2 UAT Smoke Evidence

Recorded closeout evidence:

- Admin smoke test passed.
- `lamEngineer`/NVTKC UAT confirmed contributor behavior, Part Picker usability, Revision Browser view. Move Entry and Pin are correctly blocked for lamEngineer/NVTKC.
- `TNTKC` UAT confirmed reviewer behavior: Move Entry and Pin Revision available.
- `lamPM` UAT confirmed manager behavior: Move Entry and Pin Revision available.
- Viewer/unknown behavior confirmed conservative read-only behavior.
- Automated verification passed: Debug build, Release build, full tests 267/267.

Remaining live limitations:

- role mapping is username/config based for UAT;
- future hardening should use Aras Identity membership;
- full customer/external viewer UAT remains pending unless tested.

## Package Intake Outcome

Accepted as source material:

- [Part_Library_Phase_2_Complete_User_Experience.docx](incoming/Part_Library_Phase_2_Complete_User_Experience.docx)

Retained as incoming-only helper material, not canonical:

- [Part_Library_Phase_2_Execution_Pack.docx](incoming/Part_Library_Phase_2_Execution_Pack.docx)
- [Part_Library_Phase_2_Prompt_Library.md](incoming/Part_Library_Phase_2_Prompt_Library.md)

## Acceptance Gates

Phase 2 is in progress. Full Phase 2 remains open until:

- manual desktop app UAT confirms Sprint 2.1 behavior;
- live Aras UAT confirms permission behavior and backend compatibility;
- Sprint 2.3 and 2.4 workstreams are implemented and accepted.

## Rollback Considerations

- Sprint 2.1 changes are local code and test changes only;
- no live Aras schema or Method change is included in this closeout;
- Phase 1 documents remain closed evidence and must not be rewritten.

## Sprint 2.3 Core Packet

Sprint 2.3 core backend is now implemented (gaps closed 2026-07-07):

**DTOs:** `VaultCacheKey` (now includes `UserName`, `FileName`, `Extension`; cache file name uses approved extension, not `.cache`; equality includes `UserName` + `Extension`), `VaultDownloadResult` (gains `FileId`, `FileName`, `BytesWritten`, `FromCache`, `CacheKey`), `IronCadOpenRequest`/`IronCadOpenResult`, `ArasOpenUrlRequest`/`ArasOpenUrlResult`, `VaultFileValidator` (approved extension allowlist + path traversal detection), detail tab DTOs (`LibraryEntryCadDetails`, `LibraryEntryBomDetails`, `LibraryEntryRevisionDetails`, `LibraryEntryWhereUsedDetails`, `LibraryEntryDetailBundle`).

**Vault service (D-05):** `GetPrimaryCadFileInfoAsync` now injects `IPartLibraryClient` and maps from `GetEntryAsync` (never throws `NotSupportedException`). `BuildCacheKey` uses injected `serverUrl`/`database` (no hardcoded defaults). `DownloadToCacheAsync` validates extension, rejects path traversal, validates cache hit (exists/readable/size>0/approved extension), returns `FromCache=true` when cached, uses atomic temp-to-cache copy.

**IronCAD open (D-06):** `IronCadOpenRequest`/`IronCadOpenResult` enforce zero-byte rejection, approved extension check, trusted source gating, remote URL rejection; adapter-first before process fallback.

**Aras URL (WS6):** `ArasOpenUrlRequest`/`ArasOpenUrlResult` validate item type against approved list (`Part`, `CAD`, `idea_PartLibrary`, `idea_PartLibraryEntry`); throw `ValidationFailed` on bad input; `BuildUserUrl` removed.

**Detail tabs (WS7):** Backend DTOs + `IPartLibraryClient` methods (`GetCadDetailsAsync`, `GetBomDetailsAsync`, `GetRevisionDetailsAsync`, `GetWhereUsedDetailsAsync`, `GetDetailBundleAsync`) — `HttpPartLibraryClient` throws `NotSupportedException` as placeholder for Sprint 2.3 UI wiring.

**Tests:** 346 total â€” 0 failed, 0 skipped (previous + 29 new: 10 vault, 9 IronCAD, 10 Aras URL covering D-05, D-06, cache key, equality, extension, traversal, cancellation, result properties, `GetPrimaryCadFileInfoAsync` with injected client; plus 6 UI wiring tests for BrowserLauncher, service composition, Aras target routing, and detail-state notifications). All source projects 0w/0e. All test projects 0w/0e.

**Sprint 2.3 UI Wiring Packet**

Completed locally:

- `LibraryServicesFactory` composes real services from the current session context and falls back to safe unavailable services when Aras context is missing.
- `LibraryViewModel` now routes Open in Aras actions for Part, Entry, Library, and CAD targets.
- `LibraryViewModel` now raises detail empty-state notifications when tabs are cleared or reloaded.
- `BrowserLauncher` now validates safe `http`/`https` URLs before launch.
- localization keys were added for English, Vietnamese, and Japanese.
- focused unit tests cover routing, safe launch behavior, and empty-state refresh behavior.

## Sprint 2.3 App UAT Smoke Evidence

Recorded closeout evidence:

- Build: Debug — 0 warnings, 0 errors; Release — 0 warnings, 0 errors.
- Full tests: 390/390 passed.
- Server method `idea_GetPrimaryIronCadForPart` deployed to Aras (read-only C# method, accepts `part_id`, returns CAD/native_file — no mutation).
- Live CAD lookup issue (`CAD lookup unavailable: tried N CAD id candidates; none resolved to a CAD item`) no longer reproduced after method deployment.
- App UAT smoke performed: CAD lookup acceptable, Part Library loads.
- Sprint 2.3 UI features confirmed: CAD tab, BOM tab, Revisions tab, Where Used tab, Open in Aras, Download CAD, Open in IronCAD.

Remaining live limitations:

- real Download/Open IronCAD depends on local IronCAD install + Vault permissions;
- method must exist in target Aras database;
- connector user needs Execute Method + Get Part/CAD/Part CAD/File permissions.

## Next Sprint

`Sprint 2.4: Filters and UX Hardening (WS8)`

Canonical supporting docs:

- [Design](DESIGN.md)
- [Deployment](DEPLOYMENT.md)
- [Acceptance](ACCEPTANCE.md)
