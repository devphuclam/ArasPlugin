# Part Library Phase 2 Acceptance

**State:** `PLANNED`

This file records Phase 2 acceptance gates and the planning baseline evidence. It does not mark any Phase 2 implementation complete.

## Phase Transition Gate

Phase 2 moved from `INTAKE` to `PLANNED` on 2026-07-06.

All gate criteria satisfied:

- the package has been inventoried and isolated under `incoming/`;
- canonical `README`, `DESIGN`, `DEPLOYMENT`, and `ACCEPTANCE` files exist;
- decisions `D-01` through `D-06` are approved;
- baseline build/test evidence recorded and verified;
- scope, non-goals, dependencies, and rollback considerations are explicit.

## Baseline Verification Commands

Solution build:

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe' `
  'IdeaCadConnector.sln' /t:Restore,Build /p:Configuration=Debug /m
```

Full test project:

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

Current Phase 2 state:

```text
PLANNED
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

WPF build blocker root cause:

```text
Stale .g.i.cs cache files from the WPF markup compilation pipeline.
Microsoft.NET.Sdk.WindowsDesktop does not remove .g.i.cs files during
incremental rebuild. When XAML files are added or modified, the cached
.g.i.cs can become stale relative to code-behind, causing the temporary
WPF assembly to fail with CS0103 (InitializeComponent not found in
partial class) and CS5001 (no Main in temp assembly as cascading failure).

Fix: dotnet clean removes the stale cache, allowing regeneration of
matching .g.i.cs files on the next build.
```

Full test result:

```text
Passed: 117
Failed: 0
Skipped: 0
Total: 117
```

## Acceptance Gates by Sprint

### Sprint 2.1

- `LM-01..08` and `PP-01..09` implemented without regressing save/reuse flows
- CRUD and Part picker tests pass
- archived visibility, duplicate rules, and permission feedback are demonstrated

### Sprint 2.2

- `ME-01..06` and `RV-01..07` implemented with failure-safe behavior
- move rollback or block behavior is evidenced
- revision policy changes do not mutate data on failed resolve

### Sprint 2.3

- `VT-01..06`, `OA-01..02`, and `TAB-01..04` implemented
- no zero-byte or partial CAD cache success path
- environment-correct Aras navigation and real tab data are demonstrated

### Sprint 2.4

- `FLT-01..04` and `NFR-01..07` complete
- full regression and manual UAT pass
- deployment and rollback notes are final

## Manual and Live UAT Requirements

The following checks cannot be claimed from local automation alone:

1. real permission behavior with non-admin Library roles;
2. real Vault download behavior;
3. safe IronCAD open behavior from a downloaded native file;
4. Open in Aras URL correctness against the intended live server and database;
5. move/revision/CAD-detail behavior against real Aras data volume;
6. no regression of Phase 1 live reuse and usage tracking.

### Role-Based UAT Expectations

| Role | UAT Scope |
|---|---|
| NVTKC (Library Contributor) | Add Draft Entry, edit metadata, submit for review, reuse approved Part in PDM Project, view CAD/BOM/Where Used |
| TNTKC (Library Reviewer) | Review Entry, publish, request rework, manage Entries in team scope, move Entry |
| Trưởng phòng thiết kế cơ (Library Manager) | Create, edit, archive, restore Library; move/remove Entry; publish/deprecate; manage content exceptions |
| Quản lý dự án (Project Viewer) | Read-only access to Libraries, Published Entries, BOM, Usage, Where Used, project impact |
| Nhân viên lắp ráp cơ (Manufacturing Viewer) | View only Published production Parts, released BOM, approved drawings and CAD |
| Khách hàng (External Viewer) | View only explicitly shared Published data, approved revisions/drawings. Must not see Draft, internal Usage, notes, source_project/commit, unrestricted CAD |

## Implementation Readiness Outcome

Phase 2 is `PLANNED`.

Readiness gate completed 2026-07-06:

- all decisions D-01 through D-06 are APPROVED;
- baseline build/test evidence recorded: Debug 0/0, Release 0/0, tests 117/117;
- WPF build blocker diagnosed and verified fixed;
- regression protection added for WPF configuration;
- no Sprint 2.1 production code was implemented in this gate.

Next step: begin Sprint 2.1 implementation (Library CRUD contracts and paged Aras Part search).
