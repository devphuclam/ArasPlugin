# Part Library Phase 2 Acceptance

**State:** `INTAKE`

This file records Phase 2 acceptance gates and the planning baseline evidence. It does not mark any Phase 2 implementation complete.

## Required Phase Transition Gate

Phase 2 may move from `INTAKE` to `PLANNED` only when:

- the package has been inventoried and isolated under `incoming/`;
- canonical `README`, `DESIGN`, `DEPLOYMENT`, and `ACCEPTANCE` files exist;
- unresolved decisions `D-01` through `D-06` are either approved or explicitly deferred with no impact on the next sprint;
- baseline build/test evidence is recorded;
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
INTAKE
```

Verification date:

```text
2026-07-06
```

Build result:

```text
FAILED
```

Observed build blocker:

```text
IdeaCadConnector.Desktop temporary WPF assembly build fails with:
- CS5001: Program does not contain a static 'Main' method suitable for an entry point
- CS0103: InitializeComponent does not exist in AddLibraryPartToProjectDialog.xaml.cs
- CS0103: InitializeComponent does not exist in SaveToLibraryDialog.xaml.cs
- CS0103: InitializeComponent does not exist in PublishLibraryEntryDialog.xaml.cs
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

## Known Planning Blockers

- `D-01` through `D-06` are unresolved;
- no approved canonical owner acceptance yet for the Phase 2 scope;
- the current baseline does not have a clean solution build because of the existing Desktop WPF temporary assembly failure.

## Implementation Readiness Outcome

Phase 2 is **not** ready to claim `IN PROGRESS`.

The earliest acceptable next step is:

1. record baseline build/test evidence;
2. approve `D-01`, `D-02`, and `D-03`;
3. move the phase to `PLANNED`;
4. start the first implementation packet for Sprint `2.1`.
