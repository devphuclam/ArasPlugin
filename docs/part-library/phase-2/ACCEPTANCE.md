# Part Library Phase 2 Acceptance

**State:** `LOCALLY_ACCEPTED`

This file records Phase 2 acceptance gates and current implementation evidence. Sprint 2.1 UAT smoke passed, but full Phase 2 is not complete yet.

## Phase Transition History

- Phase 2 moved from `INTAKE` to `PLANNED` on 2026-07-06.
- Phase 2 moved from `PLANNED` to `IN_PROGRESS` when Sprint 2.1 UI implementation was completed locally on 2026-07-06.
- Phase 2 moved from `IN_PROGRESS` to `LOCALLY_ACCEPTED` after Sprint 2.1 UAT smoke evidence was recorded on 2026-07-06.

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
- Sprint 2.2 has not started.

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
- revision policy changes do not mutate data on failed resolve.

### Sprint 2.3

- `VT-01..06`, `OA-01..02`, and `TAB-01..04` implemented;
- no zero-byte or partial CAD cache success path;
- environment-correct Aras navigation and real tab data demonstrated.

### Sprint 2.4

- `FLT-01..04` and `NFR-01..07` complete;
- full regression and manual UAT pass;
- deployment and rollback notes are final.

## Manual and Live UAT Requirements

The following cannot be claimed from local automation alone:

1. real permission behavior with non-admin Library roles;
2. real Vault download behavior;
3. safe IronCAD open behavior from downloaded native files;
4. Open in Aras URL correctness against the intended live server and database;
5. move/revision/CAD-detail behavior against real Aras data volume;
6. no regression of Phase 1 live reuse and usage tracking.

## Current Outcome

Phase 2 is `LOCALLY_ACCEPTED` for Sprint 2.1 only.

Recommended next packet:

`Sprint 2.2: Move Entry + Revision Browser`
