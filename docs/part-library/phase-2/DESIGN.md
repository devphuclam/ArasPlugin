# Part Library Phase 2 Design

**State:** `IN_PROGRESS` (Sprint 2.1 core/backend complete)

## Objective

Convert the completed Phase 1 Part Library core into a complete desktop user experience for library administration, Part selection, revision handling, Vault-backed CAD access, Aras navigation, and detail tabs without regressing existing reuse behavior.

## Explicit Non-Goals

- no redesign of Phase 1 ItemTypes, property names, lifecycle states, or deployed server Methods;
- no direct SQL, package-only schema drift, or speculative live Aras changes;
- no promise of atomic server-side move behavior until an Aras-compatible design is tested and approved;
- no claim that Vault, IronCAD launch, or live permissions work until verified outside local automation;
- no geometry insertion into an active IronCAD scene.

## Baseline and Source of Truth

- Planning baseline commit: `956af6841392b609d9c06df60d484fe5244500c1`
- Phase 1 completion commit: `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c`
- Source package:
  - [Complete UX spec](incoming/Part_Library_Phase_2_Complete_User_Experience.docx)
  - [Execution pack](incoming/Part_Library_Phase_2_Execution_Pack.docx)
  - [Prompt library](incoming/Part_Library_Phase_2_Prompt_Library.md)

Current code and Phase 1 evidence remain authoritative when the package and source disagree.

## Current Code Reality

The current repository already provides:

- `idea_PartLibrary`, `idea_PartLibraryEntry`, and `idea_PartLibraryUsage` Phase 1 support;
- desktop Library browsing, search, detail loading, reuse to current PDM project, publish/deprecate, revision-policy updates, and where-used view;
- `IPartLibraryClient` support for `GetLibrariesAsync`, `SearchEntriesAsync`, `GetEntryAsync`, `AddPartAsync`, `MoveEntryAsync`, `ResolvePartAsync`, `ResolveUsingStoredPolicyAsync`, `UpdateRevisionPolicyAsync`, `PublishEntryAsync`, `DeprecateEntryAsync`, `GetWhereUsedAsync`, and `RecordUsageAsync`;
- server Method deployment artifacts for `idea_AddPartToLibrary`, `idea_RecordPartLibraryUsage`, and `idea_SyncPartLibraryEntryStatus`.

Sprint 2.1 (2026-07-06) added:

- `IPartLibraryClient.CreateLibraryAsync`, `UpdateLibraryAsync`, `ArchiveLibraryAsync` with AML-based create/edit flows and `LibraryMutationResult` responses;
- `IPartLibraryClient.SearchPartsAsync` — paged Aras Part search with keyword (LIKE), lifecycle state, classification, major_rev, and is_current filters, returning `PartPickerSearchResponse` with `PartPickerSearchResultItem` rows;
- `IPartLibraryClient.GetPartPreviewAsync` — single-Part detail (config_id, revision, generation, lifecycle state) with CAD presence and Obsolete-state check for reuse eligibility;
- `IPartLibraryClient.CheckDuplicateEntryAsync` — active duplicate detection by Library + part_config_id (case-insensitive), per D-02;
- `GetLibrariesAsync(LibraryVisibilityFilter)` — optional Archived/All filter (D-03);
- `DuplicateEntryCheckResult` and `AddSelectedPartToLibraryRequest` DTOs.

The current repository does **not** yet expose:

- Library restore flows (soft-unarchive);
- an Aras Part picker UI dialog separate from the current save-to-library workflow;
- Library create/edit/archive UI in the LibraryView;
<｜｜DSML｜｜parameter name="newString" string="true">Sprint 2.1 (2026-07-06) added:

- `IPartLibraryClient.CreateLibraryAsync`, `UpdateLibraryAsync`, `ArchiveLibraryAsync` with AML-based create/edit flows and `LibraryMutationResult` responses;
- `IPartLibraryClient.SearchPartsAsync` — paged Aras Part search with keyword (LIKE), lifecycle state, classification, major_rev, and is_current filters, returning `PartPickerSearchResponse` with `PartPickerSearchResultItem` rows;
- `IPartLibraryClient.GetPartPreviewAsync` — single-Part detail (config_id, revision, generation, lifecycle state) with CAD presence and Obsolete-state check for reuse eligibility;
- `IPartLibraryClient.CheckDuplicateEntryAsync` — active duplicate detection by Library + part_config_id (case-insensitive), per D-02;
- `GetLibrariesAsync(LibraryVisibilityFilter)` — optional Archived/All filter (D-03);
- `DuplicateEntryCheckResult` and `AddSelectedPartToLibraryRequest` DTOs.

The current repository does **not** yet expose:

- Library restore flows (soft-unarchive);
- an Aras Part picker UI dialog separate from the current save-to-library workflow;
- Library create/edit/archive UI in the LibraryView;
- revision-history browsing for all revisions of a `config_id`;
- primary CAD download/open services for library entries;
- explicit Open in Aras URL generation from Library details;
- complete CAD, BOM, Revisions, and Where Used tabs backed by dedicated data contracts;
- production-grade entry filters for CAD availability or resolution state.
- revision-history browsing for all revisions of a `config_id`;
- primary CAD download/open services for library entries;
- explicit Open in Aras URL generation from Library details;
- complete CAD, BOM, Revisions, and Where Used tabs backed by dedicated data contracts;
- production-grade entry filters for CAD availability or resolution state.

## Package Inventory Decision

### Accepted as durable input

- the UX scope areas `WS1` through `WS8`;
- the requirement IDs `LM`, `PP`, `ME`, `RV`, `VT`, `OA`, `TAB`, `FLT`, and `NFR`;
- the overall four-sprint structure `2.1` through `2.4`;
- the rule that Phase 1 behavior and evidence must remain intact.

### Rejected as canonical repository content

- agent-specific ownership and handoff instructions;
- any implication that Phase 2 is already ready for coding without decision approval;
- duplicate prompt packs as a second source of truth;
- any suggestion that live Aras deployment or Vault validation has already happened.

## Decision Status

| ID | Decision | Approved Rule |
|---|---|---|
| `D-01` | who may create Libraries | Only Trưởng phòng thiết kế cơ (Library Manager) |
| `D-02` | duplicate active Entry rule | Unique on `Library ID + part_config_id`, case-insensitive. Active: Draft, PendingReview, Published. |
| `D-03` | archived Library visibility | Hidden by default; explicit Archived/All filter; read-only; no new Entries; not a Move or Part Picker target. |
| `D-04` | move preserves metadata and lifecycle | Preserve identity, config_id, policy, pinned info, lifecycle, status, metadata. Block if lifecycle cannot be preserved safely. |
| `D-05` | Vault cache model | Per-Windows-user cache keyed by server/db/File ID/revision. Temp-first download; reject zero-byte; validate extension; atomic move; clean on failure. |
| `D-06` | IronCAD open mechanism | Preferred: existing bridge/connector. Process launch fallback only with verified local file. |

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
|---|---|---|---|---|
| `2.1` | `WS1`, `WS2` | Library CRUD planning, Aras Part picker contracts/UI | D-01, D-02, D-03 APPROVED |
| `2.2` | `WS3`, `WS4` | move safety and revision browser | D-04 APPROVED |
| `2.3` | `WS5`, `WS6`, `WS7` | Vault/CAD services, Aras links, populated detail tabs | D-05, D-06 APPROVED |
| `2.4` | `WS8` + hardening | advanced filters, regression closure, UAT prep | previous sprints verified |

All decisions D-01 through D-06 are APPROVED as of 2026-07-06.

## Requirement Mapping

| Group | Implementation focus | Primary verification focus |
|---|---|---|
| `LM-01..08` | new Library contracts, permission-aware CRUD, archived visibility rules | CRUD tests, duplicate rules, refresh-state tests |
| `PP-01..09` | Part search DTOs, paging, filters, preview, duplicate-preventing add flow | client query tests, UI validation tests, no-mutation-on-failure tests |
| `ME-01..06` | target eligibility, duplicate prevention, rollback-safe move orchestration | success/failure rollback tests, permission tests |
| `RV-01..07` | revision history queries, stored policy updates, drift reporting | revision ordering and safe-policy tests |
| `VT-01..06` | primary CAD resolution, Vault download/cache/open preparation | no-zero-byte, cleanup, permission/network tests |
| `OA-01..02` | environment-correct Aras URL building | URL composition tests, manual UAT |
| `TAB-01..04` | dedicated data services for CAD, BOM, Revisions, Where Used | selection-driven loading tests, manual data checks |
| `FLT-01..04` | entry filters, sorting, persisted safe UI state | UI/filter-state tests |
| `NFR-01..07` | exception boundaries, batching, localization, no sensitive logs, regression control | full automated suite + manual/live gate |

## Architecture Guidance

- Keep `Desktop` presentation-only; no direct AML or Vault protocol logic in views or dialogs.
- Extend `Core` contracts minimally and only when a real use case lacks an existing DTO.
- Keep `HttpPartLibraryClient` as the Aras boundary for library-related AML, Method calls, and permission/error classification.
- Prefer selection-driven or batched loading over per-row detail queries.
- Preserve the top-level AML parsing fix and all Phase 1 safety boundaries.
- Treat live Aras and Vault validation as deployment/UAT evidence, not local inference.

## Recommended First Implementation Packet

All decisions D-01 through D-06 are APPROVED and Phase 2 is moved to `PLANNED`.

The first recommended packet is:

`Sprint 2.1 core planning check -> Library CRUD contracts + paged Aras Part search`
