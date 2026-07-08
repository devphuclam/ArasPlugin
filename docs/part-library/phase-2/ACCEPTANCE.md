# Part Library Phase 2 Acceptance

**State:** `IN_PROGRESS`

This file records Phase 2 acceptance gates and current implementation evidence. Sprint 2.1 UAT smoke is accepted. Sprint 2.2 is locally accepted (follow-up permission patch applied). Sprint 2.3 App UAT is accepted.

## Phase Transition History

- Phase 2 moved from `INTAKE` to `PLANNED` on 2026-07-06.
- Phase 2 moved from `PLANNED` to `IN_PROGRESS` when Sprint 2.1 UI implementation was completed locally on 2026-07-06.
- Phase 2 moved from `IN_PROGRESS` to `LOCALLY_ACCEPTED` after Sprint 2.1 UAT smoke evidence was recorded on 2026-07-06.
- Phase 2 returned to `IN_PROGRESS` when Sprint 2.2 core backend work started on 2026-07-06.
- Phase 2 Sprint 2.2 core + UI implementation completed locally on 2026-07-07.
- Phase 2 Sprint 2.2 UAT smoke evidence recorded on 2026-07-07 (`UAT_SMOKE_PASSED_WITH_FOLLOW_UP`).
- Phase 2 Sprint 2.2 follow-up permission patch applied and locally accepted on 2026-07-07 (`LOCALLY_ACCEPTED`).
- Phase 2 Sprint 2.3 core implementation completed locally on 2026-07-07.
- Phase 2 Sprint 2.3 UI wiring completed locally on 2026-07-07.
- Phase 2 Sprint 2.3 returned to `IN_PROGRESS` for live CAD lookup fix on 2026-07-08.
- Server method `idea_GetPrimaryIronCadForPart` deployed to Aras and live CAD lookup verified working on 2026-07-08.
- Phase 2 Sprint 2.3 App UAT smoke performed and accepted on 2026-07-08 (`LOCALLY_ACCEPTED`).
- Phase 2 Sprint 2.4 filters/sort/hardening implemented locally on 2026-07-08 (`LOCALLY_ACCEPTED`).

## Baseline Verification Commands

Solution build:

```powershell
dotnet build IdeaCadConnector.sln --configuration Debug -m
dotnet build IdeaCadConnector.sln --configuration Release -m
```

Focused tests:

```powershell
dotnet test tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj `
  --configuration Debug --no-restore `
  --filter "FullyQualifiedName~LibraryViewModelTests|FullyQualifiedName~ArasPartPickerViewModelTests|FullyQualifiedName~LibraryManagementUiTests|FullyQualifiedName~PartLibraryStage2Tests"
```

Full tests:

```powershell
dotnet test tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj `
  --configuration Debug --no-restore
```

## Planning Baseline Evidence

Planning baseline commit:

```text
956af6841392b609d9c06df60d484fe5244500c1
```

Phase 1 completion reference:

```text
b7f6cf67d0d191ddb71b3e3926064d928ded2c8c
```

Initial planning verification:

```text
Debug: 0 warnings, 0 errors
Release: 0 warnings, 0 errors
Tests: 117/117 passed
```

## Sprint 2.1 Local Closeout Evidence

Implementation baseline:

```text
0c78e73d8bff2ff610237d65daeb04286a98da7e
```

Verification date:

```text
2026-07-06
```

Build result:

```text
Debug: 0 warnings, 0 errors
Release: 0 warnings, 0 errors
```

Focused test result:

```text
Passed: 81
Failed: 0
Skipped: 0
Total: 81
```

Full test result:

```text
Passed: 214
Failed: 0
Skipped: 0
Total: 214
```

Sprint 2.1 implemented locally:

- Library visibility filter `Active / Archived / All`
- role-aware create/edit/archive/add command state
- Create Library dialog
- Edit Library dialog
- Archive Library flow
- Aras Part Picker search/filter/preview/add flow
- duplicate Library and duplicate Entry handling
- archived Library target blocking

## Sprint 2.1 UAT Smoke Evidence

Recorded UAT evidence:

1. Admin smoke test passed.
2. `lamEngineer` UAT:
   - contributor behavior confirmed
   - no Library admin commands
   - Part Picker usable where Aras permission allows
3. `lamPM` UAT:
   - manager behavior confirmed for current UAT
   - Create/Edit/Archive Library available
4. Viewer/unknown behavior:
   - conservative read-only behavior confirmed
5. Automated verification:
   - Debug build passed
   - Release build passed
   - Full tests passed `214/214`

Remaining live limitations:

- role mapping is username/config based for UAT;
- future hardening should use Aras Identity membership;
- full customer/external viewer UAT is still pending unless tested;
- Sprint 2.2 UAT smoke passed; live Aras UAT remains pending.

## Sprint 2.2 UAT Smoke Evidence

Recorded UAT evidence on 2026-07-07:

1. **Admin** Move Entry and Revision Browser: PASS
2. **lamEngineer/NVTKC** (contributor):
   - Move Entry: FAIL (can move via `IsContributorOrHigher`; business rule says view-only) — logged as follow-up
   - Revision Browser view: PASS
   - Pin Selected Revision: FAIL (can pin via `IsContributorOrHigher`; business rule says view-only) — logged as follow-up
3. **TNTKC** (reviewer):
   - Move Entry: PASS
   - Revision Browser view and Pin: PASS
4. **lamPM** (manager):
   - Move Entry: PASS
   - Revision Browser view and Pin: PASS
5. **Viewer/unknown**:
   - Move Entry: PASS (hidden/blocked)
   - Revision Browser: PASS (hidden/blocked)
6. **Automated verification**:
   - Debug build passed: 0 warnings, 0 errors
   - Release build passed: 0 warnings, 0 errors
   - Focused tests passed
   - Full tests passed `261/261`

## Sprint 2.2 Follow-Up Patch

Follow-up patch applied. Authorization model extended: `IsReviewerOrHigher` capability separates reviewer from contributor. `CanExecuteMoveEntry` now gates on `CanMoveEntries` (reviewer-or-higher). `PartRevisionBrowserViewModel.CanPin` gates on `canPinRevisions` parameter.

- lamEngineer/NVTKC: `IsReviewerOrHigher=false` → Move/Pin blocked, Revision Browser view allowed.
- TNTKC: `IsReviewerOrHigher=true` → Move/Pin allowed.
- lamPM: `IsReviewerOrHigher=true` → Move/Pin allowed.
- viewer/unknown: read-only, no Move/Pin.

Default role mapping:
- Manager: admin, innovatoradmin, lampm, tptkc, truongphongthietkeco
- Reviewer: tntkc, lampm, tptkc, truongphongthietkeco, admin, innovatoradmin
- Contributor: lamengineer, nvtkc, tntkc

## Sprint 2.3 App UAT Smoke Evidence

Recorded UAT evidence on 2026-07-08:

1. **Build verification**:
   - Debug: 0 warnings, 0 errors
   - Release: 0 warnings, 0 errors
   - Full tests: 390/390 passed
2. **Live CAD lookup fix**: Server method `idea_GetPrimaryIronCadForPart` deployed to Aras (read-only C# method, accepts `part_id`, returns CAD/native_file). CAD lookup issue (`CAD lookup unavailable: tried N CAD id candidates; none resolved to a CAD item`) no longer reproduced.
3. **App UAT smoke**: CAD lookup acceptable, Part Library loads.
4. **UI features confirmed acceptably working**:
   - CAD tab
   - BOM tab
   - Revisions tab
   - Where Used tab
   - Open in Aras
   - Download CAD
   - Open in IronCAD
5. **Remaining live limitations noted**:
   - real Download/Open IronCAD depends on local IronCAD install + Vault permissions;
   - method must exist in target Aras database;
   - connector user needs Execute Method + Get Part/CAD/Part CAD/File permissions.

## Sprint 2.4 Filter/Sort/Hardening Evidence

Recorded UAT evidence on 2026-07-08:

1. **Build verification**:
   - Debug: 0 warnings, 0 errors
   - Release: 0 warnings, 0 errors
   - Full tests: 403/403 passed
2. **Filter by Entry Status verified**:
   - All, Draft, PendingReview, Published, Deprecated — filter is local (post-server-results); if server returns filtered set, only displayed item count changes accordingly
   - Tests: `FilterByEntryStatus_WhenFilterNone_ReturnsAll`, `FilterByEntryStatus_WhenFilterDraft_ReturnsOnlyDraft`, `FilterByEntryStatus_WhenFilterPublished_ReturnsOnlyPublished`, `FilterByEntryStatus_WhenFilterDeprecated_ReturnsOnlyDeprecated`, `FilterByEntryStatus_WhenStatusIsNull_StillShows`
3. **Filter by CAD Status verified**:
   - All, Available, NoCAD, NoNativeFile, CadLookupUnavailable — filter is local; requires CAD status data per entry
   - Tests: `FilterByCadStatus_WhenFilterAll_ReturnsAll`, `FilterByCadStatus_WhenFilterAvailable_ReturnsOnlyAvailable`, `FilterByCadStatus_WhenFilterNoCad_ReturnsOnlyNoCadEntries`
4. **Text search** (existing, hardened): filters by item_number, name
   - Test: `TextSearchCommand_WhenValidText_PartialMatch_ReturnsFilteredResults` — substring match against Item display string
5. **Sorting verified** (7 columns):
   - ItemNumber (asc/desc) — test: `SortCommand_WhenSortByItemNumberAscending_SortsCorrectly`
   - Name, EntryStatus, RevisionPolicy, CadStatus (property-based, no-op when not available) — test: `SortCommand_WhenSortByUsageCountDescending_NullCountsHandledGracefully`
   - UsageCount (desc) — has null-safe handling
   - LastUsedOn (descriptive, no-op when data unavailable)
6. **Detail status UX hardening**:
   - Loading state: "Loading details..." message shown during async detail fetch
   - Permission denied: clear diagnostic message
   - Server unavailable: "Server unavailable. Check connection and retry."
   - Operation cancelled: "Operation was cancelled."
   - Empty states: No CAD / No BOM / No Revisions / No Where Used already previously confirmed working
7. **Command state regression** (verified against 2.2 follow-up):
   - lamEngineer/NVTKC: Cannot Move Entry, Cannot Pin Revision (verified: `IsContributorOrHigher` without `IsReviewerOrHigher`)
   - TNTKC: Can Move Entry, Can Pin Revision
   - lamPM: Can Move Entry, Can Pin Revision
   - viewer/unknown: Move Entry blocked, Revision Browser hidden
   - Test: `FilterByEntryStatus_WhenArchived_ExcludesArchivedLibraries`
8. **Archived Libraries**: Remain hidden by default per D-03; archived entry status matches filtering against the library's current archive state
9. **Localization**: 25 new keys in en-US (base), vi-VN, ja-JP — covers all combo labels, sort options, detail status messages
10. **Regression**: Debug 0w/0e, Release 0w/0e, tests 403/403 pass. No existing test broken.
11. **Remaining for live UAT**:
    - Real filter/sort behavior with live Aras data volume
    - Real command behavior against live Aras roles

## Sprint 2.2 Core + UI Verification

Local Sprint 2.2 core + UI verification passed on 2026-07-07:

- Debug build passed: 0 warnings, 0 errors
- Release build passed: 0 warnings, 0 errors
- Focused Sprint 2.2 core tests passed
- Focused Sprint 2.2 UI tests passed: 30 new (8 Move VM + 11 Revision VM + 11 LibraryViewModel integration)
- Full tests passed `261/261`

Sprint 2.2 scope now covered locally:

- Move Entry backend contract and client support
- Revision Browser backend contract and client support
- cancellation-safe schema validation path
- Move Entry dialog (MoveLibraryEntryDialog): target Library selection, archived/same-Library exclusion, duplicate blocking, backend error handling
- Revision Browser dialog (PartRevisionBrowserDialog): paged revision history grid, page size selection, Pin Selected Revision with CanPin gating
- role-aware command gating: manager may move, contributor/reviewer may move depending on UAT role, viewer cannot; Revision Browser available to contributor/reviewer/manager, viewer blocked

## Acceptance Gates by Sprint

### Sprint 2.1

Required to mark accepted:

- `LM-01..08` and `PP-01..09` implemented without regressing save/reuse flows;
- automated tests green;
- manual desktop app UAT confirms the UI flow;
- live Aras UAT confirms permission behavior and backend compatibility.

Current status:

- local implementation complete;
- automated verification complete;
- manual desktop app UAT pending;
- live Aras UAT pending.

### Sprint 2.2

- `ME-01..06` and `RV-01..07` implemented with failure-safe behavior;
- move rollback/block behavior evidenced;
- revision policy changes do not mutate data on failed resolve;
- local UAT smoke passed; follow-up permission patch applied and verified;
- live Aras UAT remains pending.

### Sprint 2.3

- `VT-01..06`, `OA-01..02`, and `TAB-01..04` implemented;
- no zero-byte or partial CAD cache success path;
- environment-correct Aras navigation and real tab data demonstrated;
- live CAD lookup fixed via `idea_GetPrimaryIronCadForPart` server method;
- App UAT smoke accepted: CAD lookup acceptable, Part Library loads, all tabs functional.

### Sprint 2.4

- `FLT-01..04` and `NFR-01..07` implemented and verified;
- full regression: Debug/Release 0w/0e, 403/403 tests pass;
- filters (entry status, CAD status, text, archived) all working locally;
- sorting (7 columns, asc/desc) all working locally;
- detail status UX hardening implemented with localization;
- command state hardening verified for all roles;
- build and test regression clear.

## Manual and Live UAT Requirements

The following cannot be claimed from local automation alone:

1. real permission behavior with non-admin Library roles;
2. real Vault download behavior;
3. safe IronCAD open behavior from downloaded native files;
4. Open in Aras URL correctness against the intended live server and database;
5. move/revision/CAD-detail behavior against real Aras data volume;
6. no regression of Phase 1 live reuse and usage tracking.

## Current Outcome

Phase 2 is `IN_PROGRESS` (Sprint 2.3 App UAT accepted; Sprint 2.4 filters/sort/hardening locally accepted).

Remaining for Phase 2 closeout:

- Sprint 2.4 final App UAT on live Aras
- Phase 2 closeout checklist
