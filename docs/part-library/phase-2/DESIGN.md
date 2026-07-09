# Part Library Phase 2 Design

**State:** `COMPLETE` (All sprints accepted; final live App UAT accepted 2026-07-08; role alignment confirmed with actual organization roles)

## Objective

Extend the completed Phase 1 Part Library core into a complete desktop user experience for Library administration, Aras Part intake, revision handling, Vault-backed CAD access, Aras navigation, and richer detail tabs.

## Explicit Non-Goals

- no redesign of Phase 1 ItemTypes, property names, lifecycle states, or deployed server Methods;
- no direct SQL or speculative live Aras changes from local coding;
- no promise of move, Vault, or IronCAD behavior until live UAT proves it;
- no direct AML in WPF view or dialog code.

## Baseline and Source of Truth

- planning baseline commit: `956af6841392b609d9c06df60d484fe5244500c1`
- Phase 1 completion commit: `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c`
- Sprint 2.1 UI implementation baseline: `0c78e73d8bff2ff610237d65daeb04286a98da7e`
- source package:
  - [Complete UX spec](incoming/Part_Library_Phase_2_Complete_User_Experience.docx)
  - [Execution pack](incoming/Part_Library_Phase_2_Execution_Pack.docx)
  - [Prompt library](incoming/Part_Library_Phase_2_Prompt_Library.md)

Current repository code and Phase 1 evidence remain authoritative when package wording and source disagree.

## Current Code Reality

The repository already provides:

- `idea_PartLibrary`, `idea_PartLibraryEntry`, and `idea_PartLibraryUsage` Phase 1 support;
- desktop Library browsing, search, detail loading, reuse to current PDM project, publish/deprecate, revision-policy updates, and where-used view;
- `IPartLibraryClient` support for `GetLibrariesAsync`, `SearchEntriesAsync`, `GetEntryAsync`, `AddPartAsync`, `MoveEntryAsync`, `ResolvePartAsync`, `ResolveUsingStoredPolicyAsync`, `UpdateRevisionPolicyAsync`, `PublishEntryAsync`, `DeprecateEntryAsync`, `GetWhereUsedAsync`, and `RecordUsageAsync`;
- server Method deployment artifacts for `idea_AddPartToLibrary`, `idea_RecordPartLibraryUsage`, and `idea_SyncPartLibraryEntryStatus`.

Sprint 2.1 added:

- `IPartLibraryClient.CreateLibraryAsync`, `UpdateLibraryAsync`, and `ArchiveLibraryAsync`;
- `IPartLibraryClient.SearchPartsAsync`, `GetPartPreviewAsync`, and `CheckDuplicateEntryAsync`;
- `LibraryVisibilityFilter` support in `GetLibrariesAsync`;
- desktop Library management UI:
  - visibility filter `Active / Archived / All`
  - role-aware create/edit/archive command state
  - Create Library dialog
  - Edit Library dialog
  - Archive confirmation flow
- desktop Aras Part Picker UI:
  - search/filter/page through Parts
  - select a Part and load preview
  - duplicate check before add
  - block archived targets, missing `config_id`, and ineligible previews

Sprint 2.2 core support now adds:

- `IPartLibraryClient.MoveLibraryEntryAsync` request/response DTOs and backend support
- safe move validation for source/target libraries, archived targets, duplicates, metadata preservation, and cancellation
- `IPartLibraryClient.SearchPartRevisionsAsync` request/response DTOs and backend support
- revision-history query, paging, sort normalization, and pin eligibility calculation
- focused core tests for the new move and revision-browser paths

The repository now **adds** in Sprint 2.3 core:

- `IPartLibraryVaultService` contract and `PartLibraryVaultService` implementation with temp-first download, zero-byte rejection, atomic cache move, and cleanup (VT-01..06)
- `IIronCadOpenService` contract and `IronCadOpenService` implementation with executable availability check, file validation, and adapter-based launch (D-06)
- `IArasOpenUrlService` contract and `ArasOpenUrlService` implementation with configurable URI + database URL builder (OA-01..02)
- `PartLibraryCadFileInfo`, `VaultCacheKey`, `VaultDownloadResult` DTOs in `Core.Library`
- 44 focused service tests covering all VT and OA requirements

Sprint 2.3 UI wiring now adds:

- `LibraryServicesFactory` to compose real services from the current session and fall back safely when Aras context is missing.
- `LibraryViewModel` open-target commands for Part, Entry, Library, and CAD navigation.
- `LibraryViewModel` detail-state notifications when tabs are cleared or reloaded.
- `BrowserLauncher` validation for safe `http`/`https` URLs.
- localization keys for English, Vietnamese, and Japanese.
- focused UI wiring tests covering routing, service composition, browser launch behavior, and empty-state refreshes.

The repository still does **not** yet provide:

- Library restore flows;
- real CAD/BOM/Revisions detail tabs against live Aras data (`WS7`).

The repository now **adds** in Sprint 2.4:

- Entry Status filter: All / Draft / PendingReview / Published / Deprecated
- CAD Status filter: All / Available / No CAD / No native file / CAD lookup unavailable
- Text search hardening (existing)
- Sorting: 7 columns with Ascending/Descending
- Detail status UX hardening (loading, permission denied, server unavailable, cancelled)
- Command state regression verification
- 25 new localization keys (en-US, vi-VN, ja-JP)
- 11 new focused tests

## Sprint 2.1 UAT Closeout Evidence

The following evidence has been recorded against the current design baseline:

- Admin smoke test passed.
- `lamEngineer` UAT confirmed contributor behavior, no Library admin commands, and Part Picker usability where Aras permission allows.
- `lamPM` UAT confirmed manager behavior for current UAT, with Create/Edit/Archive Library available.
- Viewer/unknown behavior confirmed conservative read-only behavior.
- Automated verification passed: Debug build, Release build, and full tests `214/214`.

## Sprint 2.2 Verification

Local Sprint 2.2 core + UI packet has been verified:

- Debug build passed: 0 warnings, 0 errors
- Release build passed: 0 warnings, 0 errors
- focused Sprint 2.2 core tests passed
- focused Sprint 2.2 UI tests passed: 30 new (8 Move VM + 11 Revision VM + 11 LibraryViewModel integration)
- full tests passed `261/261`

Sprint 2.2 UI now includes:

- Move Entry dialog with target Library selection (excludes current Library, Archived Libraries, non-contributable Libraries)
- role-aware Move command gating (manager can move, contributor/reviewer allowed, viewer cannot)
- Revision Browser dialog with paged revision history, page size selection, and Pin Selected Revision
- Pin calls `UpdateRevisionPolicyAsync` with `Pinned` policy and selected `PartId`
- backend error handling: permission denied, validation failed, server errors displayed clearly
- `CanPin=false` revisions disable Pin button with reason
- duplicate active Entry in target Library blocks move

## Sprint 2.2 Follow-Up Patch

Follow-up patch applied with commit `f0db0348e4a6a9a70ff6232d5031304b1ed9c211`. Authorization model extended:

- `ILibraryAuthorizationService` gained `IsReviewerOrHigher`, `CanMoveEntries`, `CanPinRevisions`.
- `LibraryAuthorizationRules` gained `ReviewerUsers` collection and `IsReviewer()` method.
- Default reviewer users: tntkc, lampm, tptkc, truongphongthietkeco, admin, innovatoradmin (original UAT usernames; mapped to official roles below).
- `CanExecuteMoveEntry` now gates on `CanMoveEntries` (reviewer-or-higher) instead of `IsContributorOrHigher`.
- `PartRevisionBrowserViewModel` accepts `canPinRevisions` parameter; `CanPin` gates on it.
- NVTKC: `IsReviewerOrHigher=false` → `CanMoveEntries=false`, `CanPinRevisions=false`.
- TNTKC: `IsReviewerOrHigher=true` → `CanMoveEntries=true`, `CanPinRevisions=true`.
- TPTKC: `IsReviewerOrHigher=true` → `CanMoveEntries=true`, `CanPinRevisions=true`.
- viewer/unknown: conservative read-only, no Move/Pin.

No backend `MoveLibraryEntryAsync`, `SearchPartRevisionsAsync`, or `UpdateRevisionPolicyAsync` behavior was changed.

Remaining live limitations:

- role mapping is username/config based for UAT;
- future hardening should use Aras Identity membership;
- full customer/external viewer UAT remains pending unless tested;
- Sprint 2.3 App UAT accepted: CAD lookup acceptable, Part Library loads, all tabs functional.

## Approved Decisions

| ID | Decision | Approved Rule |
|---|---|---|
| `D-01` | who may create Libraries | Only `TKC_MANAGER` (Library Manager) |
| `D-02` | duplicate active Entry rule | Unique on `Library ID + part_config_id`, case-insensitive. Active: Draft, PendingReview, Published. |
| `D-03` | archived Library visibility | Hidden by default; explicit Archived/All filter; read-only; no new Entries; not a Move or Part Picker target. |
| `D-04` | move preserves metadata and lifecycle | Preserve identity, config_id, policy, pinned info, lifecycle, status, and metadata. Block if lifecycle cannot be preserved safely. |
| `D-05` | Vault cache model | Per-user cache keyed by server/database/File ID/revision. Temp-first download; reject zero-byte; atomic move; clean on failure. |
| `D-06` | IronCAD open mechanism | Preferred: existing bridge/connector. Process-launch fallback only with verified local file. |

## Workstreams

| ID | Workstream | Delivery boundary |
|---|---|---|
| `WS1` | Library Management | create, edit, archive, and permission-aware Library administration |
| `WS2` | Aras Part Picker | search/filter/preview real Aras Parts before adding Entries |
| `WS3` | Move Entry | safe retargeting with duplicate and permission checks |
| `WS4` | Revision Browser | inspect revision history and update stored resolution policy safely |
| `WS5` | Vault and IronCAD | resolve primary CAD, download native files, validate cache, open safely |
| `WS6` | Open in Aras | generate navigation URLs for Part, CAD, Library, and Entry |
| `WS7` | Detail Tabs | CAD, BOM, Revisions, and Where Used backed by real data |
| `WS8` | Filters and UX Hardening | status, CAD, resolution filters, sorting, and failure-safe polish |

## Sprint Plan

| Sprint | Scope | Planned output | Dependency gate |
|---|---|---|---|
| `2.1` | `WS1`, `WS2` | Library management UI and Aras Part Picker UI | D-01, D-02, D-03 approved |
| `2.2` | `WS3`, `WS4` | Move Entry and Revision Browser | D-04 approved |
| `2.3` | `WS5`, `WS6`, `WS7` | Vault/CAD services, Aras links, populated detail tabs | D-05, D-06 approved |
| `2.3-core` | `WS5`, `WS6` (backend) | Service contracts, Desktop vault service, IronCAD service, URL builder, DTOs, tests | VT-01..06, OA-01..02 |
| `2.3-ui` | `WS7`, WS5/WS6 wiring | Wired ViewModel commands, detail tab data queries, WPF tab UI | `2.3-core` |
| `2.4` | `WS8` | advanced filters, regression closure, UAT prep | previous sprints verified |

## Requirement Mapping

| Group | Implementation focus | Primary verification focus |
|---|---|---|
| `LM-01..08` | permission-aware CRUD, archived visibility rules | CRUD tests, duplicate-name handling, refresh-state tests |
| `PP-01..09` | Part search, paging, filters, preview, duplicate-preventing add flow | request-shape tests, UI validation tests, no-mutation-on-failure tests |
| `ME-01..06` | target eligibility, duplicate prevention, rollback-safe move orchestration | move success/failure tests |
| `RV-01..07` | revision history queries, stored policy updates, drift reporting | revision ordering and policy tests |
| `VT-01..06` | primary CAD resolution, Vault download/cache/open preparation | no-zero-byte, cleanup, permission/network tests |
| `OA-01..02` | environment-correct Aras URL building | URL composition tests and live UAT |
| `TAB-01..04` | dedicated data services for CAD, BOM, Revisions, Where Used | selection-driven loading tests and manual checks |
| `FLT-01..04` | entry filters, sorting, persisted safe UI state | UI/filter-state tests |
| `NFR-01..07` | exception boundaries, localization, no sensitive logs, regression control | full automated suite plus manual/live gate |

## Architecture Guidance

- keep `Desktop` presentation-only; no direct AML or Vault protocol logic in dialogs or views;
- extend `Core` contracts only when a real Phase 2 use case needs a new DTO or enum;
- keep `HttpPartLibraryClient` as the Aras boundary for library AML, Method calls, and error classification;
- prefer selection-driven loading and explicit guard checks over optimistic mutation;
- preserve all Phase 1 safety boundaries and regression tests.

## Final Role Matrix

| ID | Official Title | Capability | Move Entry | Pin Revision | Library Admin |
|---|---|---|---|---|---|
| `TPTKC` | Trưởng phòng thiết kế cơ | Manager | Yes | Yes | Yes |
| `TNTKC` | Trưởng nhóm thiết kế cơ | Reviewer | Yes | Yes | No |
| `NVTKC` | Nhân viên thiết kế cơ | Contributor | No | No | No |
| `NVLCR` | Nhân viên lắp ráp cơ | Assembly viewer | No | No | No |
| `PM` | Quản lý dự án | Project viewer | No | No | No |
| `Khách hàng` | Customer | External viewer | No | No | No |

## Phase 2 Closeout

Phase 2 closed after Sprint 2.4 final live App UAT accepted on 2026-07-08.

**Build:** Debug — 0 warnings, 0 errors; Release — 0 warnings, 0 errors
**Tests:** 403/403 pass
**Server method deployed:** `idea_GetPrimaryIronCadForPart` (read-only, live CAD lookup)
**Live UAT result:** Accepted. All roles tested, no P0/P1 blocker.

## Next Implementation Packet

`Phase 3: Deployment and Production Hardening — Sprint 3.1 (release packaging) and beyond`
