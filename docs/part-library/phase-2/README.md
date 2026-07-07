# Part Library Phase 2 README

**State:** `IN_PROGRESS` (Sprint 2.1 UAT smoke accepted; Sprint 2.2 locally accepted; Sprint 2.3 core implemented)

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
| `2.3` | `WS5` + `WS6` + `WS7` | core implemented |
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

Sprint 2.3 core backend is now implemented:

- `IPartLibraryVaultService` contract + `PartLibraryVaultService` with temp-first download, zero-byte rejection, atomic cache move, temp cleanup on failure
- `IIronCadOpenService` contract + `IronCadOpenService` with executable availability check, file validation, and `ICadApplicationAdapter` launch
- `IArasOpenUrlService` contract + `ArasOpenUrlService` configurable URI + database URL builder
- `PartLibraryCadFileInfo`, `VaultCacheKey`, `VaultDownloadResult` DTOs
- 44 focused service tests covering VT-01..06 and OA-01..02 requirements
- Debug build: 0 warnings, 0 errors
- Full tests: 311/311 passed

Remaining for Sprint 2.3 UI:
- Wire services into `LibraryViewModel` (OpenInIronCad, OpenInAras, DownloadCad)
- Detail tab data queries (CAD, BOM, Revisions, Where Used)
- WPF tab UI implementation (WP-32)

## Next Sprint

`Sprint 2.4: Filters and UX Hardening (WS8)`

Canonical supporting docs:

- [Design](DESIGN.md)
- [Deployment](DEPLOYMENT.md)
- [Acceptance](ACCEPTANCE.md)
