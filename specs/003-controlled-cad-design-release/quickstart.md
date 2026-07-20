# Quickstart: Controlled CAD Design Release

## Prerequisites

1. Repository at branch `003-controlled-cad-design-release`
2. Build environment: .NET Framework 4.8 SDK, Visual Studio 2022 (or compatible)
3. IronCAD 2025 (for IronCAD-specific integration tests)
4. Aras Innovator test environment with verified Part and CAD lifecycle maps
5. **Evidence gates** (UI actions or compliance claims remain disabled until their gates pass):
   - **GATE-A**: Part ItemType lifecycle state names, transitions, and semantic roles captured from the Aras environment
   - **GATE-B-revise**: Deployed `idea_ReviseCad` server method verified for atomic transactional guarantees (blocks Start New Revision)
   - **GATE-B-approve**: Deployed `idea_ApproveCadReview` server method verified for atomic Part+CAD release (blocks Approve). **Checked-in source is CAD-only** — see plan.md
   - **GATE-B-checkin**: Deployed `idea_CommitCadCheckin` server method verified for atomic update+unlock, ChangeSet, audit, and reason persistence (blocks FR-003/FR-018 full compliance). **Checked-in source has separate apply() calls without transaction** — see plan.md
   - **GATE-W**: Withdraw capability (lifecycle transition or server method) verified on Aras server (blocks Withdraw)
   - **GATE-RW**: Deployed `idea_RequestCadRework` result and audit behavior verified against the accepted coordinated state-only policy: CAD and Part return to `Thiet ke chi tiet` without a new engineering version (full evidence gate remains open) — see plan.md
   - **GATE-N**: Aras audit trail coverage verified for all lifecycle transitions (blocks FR-017 compliance claim)
   - **GATE-RS**: Authority-managed Aras Assign/workflow assignment and the client's active-assignment read contract verified (blocks reviewer-dependent actions and full FR-005 compliance)
   - **GATE-W-owner**: Authority submission-owner field/permission verified (blocks Withdraw and full FR-006 compliance)
   - See [plan.md §Technical Context](plan.md) for detailed evidence gate requirements

## Setup

```powershell
dotnet build IdeaCadConnector.sln
```

Expected: Build succeeds — 0 errors, 0 warnings.

## Validation Scenarios

### Scenario 1: Unit Tests — Policy Logic

```powershell
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~CadLifecyclePolicyTests"
```

Expected: All CAD lifecycle policy tests pass. Covers: CAD state eligibility for checkout, submit, approve, rework, new revision.

```powershell
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~PartLifecyclePolicyTests"
```

Expected: All Part lifecycle policy tests pass (requires Aras evidence fixture for verified Part state names). Covers: Part state eligibility for release coordination.

Feature 003 MVP lifecycle boundary: the Part policy intentionally stops at
`Khoi tao` -> `Thiet ke chi tiet` -> `In Review` -> `Released`. The accepted
rework edge is `In Review` -> `Thiet ke chi tiet`. States after `Released` are
outside this feature and are not used to enable actions.

```powershell
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~MvpReleasePolicyTests"
```

Expected: All MVP release policy tests pass. Covers: separate Part + CAD eligibility checks, eligibility result types.

```powershell
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~RecoveryCopyServiceTests"
```

Expected: All recovery copy tests pass. Covers: backup creation, SHA256 verification, retention/cleanup, unchanged-file skip, failure handling.

### Scenario 2: Unit Tests — ViewModel Orchestration

```powershell
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~CheckoutViewModelTests"
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~CancelCheckoutViewModelTests"
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~SubmitReviewViewModelTests"
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~ReviewViewModelTests"
dotnet test IdeaCadConnector.sln --filter "FullyQualifiedName~StartNewRevisionViewModelTests"
```

Expected: All pass. ViewModel tests mock `IRecoveryCopyService` (cancel-checkout), `IArasCadClient` (submit/approve/rework/withdraw via `ExecuteCadBusinessActionAsync`), `IPdmRepositoryClient` (revise via `ReviseCadAsync`), and `ICadReleaseEligibility` (advisory eligibility check before approve).

### Scenario 3: Full Test Suite

```powershell
dotnet test IdeaCadConnector.sln
```

Expected: All existing tests pass (0 regressions) plus all new tests pass.

### Scenario 4: End-to-End Workflow (Manual UAT, requires Aras + IronCAD)

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Open desktop app and connect to Aras | Connection succeeds; tree shows Parts and primary linked CADs |
| 2 | Select a working CAD revision (`Thiet ke chi tiet`) and choose Checkout | Checkout succeeds; local writable copy created |
| 3 | Open local file in IronCAD, modify it | File hash differs from baseline |
| 4a | Choose Check-in | `CheckinReasonDialog` opens with reason TextBox |
| 4b | Click Cancel on the dialog | Dialog closes; no upload, no authority call, no side effect; checkout still active |
| 4c | Choose Check-in again, enter a valid written reason, click OK | Reason validated; `CheckoutService.UploadAndCheckinAsync` called with reason; `CadCheckinRequest.Comment` set to the entered text; file uploaded; ChangeSet recorded; checkout lock released; CAD revision updated at same lifecycle state |
| 5 | Select the checked-in CAD, choose Submit for Review, assign a reviewer | Only after GATE-RS passes: submission succeeds; CAD transitions to `In Review`. Before that gate, the action remains disabled. |
| 5b | As the engineer, withdraw the submission before reviewer acts | Only after GATE-W and GATE-W-owner pass: CAD returns to `Thiet ke chi tiet`; no review decision recorded. Before those gates, Withdraw remains disabled. |
| 5c | Re-submit and assign a reviewer | Submission succeeds again; CAD transitions to `In Review` |
| 6 | As the reviewer, open the submission and choose Approve | **Single authority operation**: both CAD and Part transition to `Released` atomically |
| 7 | Attempt to check out the released CAD | Blocked — released revisions are read-only |
| 8 | Choose Start New Revision on the released pair | **Single authority operation**: new Part Revision + new primary CAD Revision created at `Khoi tao`; released pair unchanged |
| 9 | Repeat steps 2–6 on the new working pair | Full lifecycle works on the new pair |

### Scenario 5: Cancel-Checkout with Recovery Copy

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Modify local CAD file (hash differs from baseline) | — |
| 2 | Choose Cancel Checkout | Recovery copy dialog appears showing backup path (Workspace-owned); no backup metadata in remote request |
| 3 | Confirm cancellation | Recovery copy written and verified (SHA256); authority unlock released |
| 4 | Navigate to `<workspace>/.idea-pdm/recovery/<cad-id>/` | Recovery file exists with matching hash |
| 5 | Repeat with unmodified file | Cancel succeeds without recovery copy prompt; unlock-only request sent to authority |

### Scenario 6: Check-in with Required Written Reason

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Select a checked-out CAD and choose Check-in | `CheckinReasonDialog` opens with reason TextBox |
| 2 | Click Cancel on the dialog | Dialog closes; no upload, no authority call, no side effect; checkout remains active |
| 3 | Choose Check-in again, enter empty string as reason, click OK | OK button disabled or reason rejected before upload; no authority call |
| 4 | Enter whitespace only as reason | Same rejection — empty/whitespace blocked pre-upload |
| 5 | Enter a valid reason, start check-in with a corrupted local file | File integrity validation fails; check-in blocked; checkout lock remains active; error describes the validation failure |
| 6 | Enter a valid reason, file integrity OK, but Aras server returns an error (e.g., network timeout, server fault) | Check-in failure reported; `CheckoutService` error propagated; checkout lock remains active; user can retry |
| 7 | Enter a valid reason, file integrity OK, authority call succeeds | Check-in succeeds; `CheckoutService.UploadAndCheckinAsync` received the reason; `CadCheckinRequest.Comment` contains the reason; file uploaded; ChangeSet recorded; audit event created; checkout lock released |

### Scenario 7: Error Handling — Atomic Authority Operations

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Try to approve a CAD at `In Review` whose Part is at an ineligible state | Eligibility check blocks with clear message; no authority call made |
| 2 | Try to start a new revision on a released pair while another engineer's request is concurrent | Authority handles race: one succeeds, other receives conflict message |
| 3 | Authority operation fails during approve (e.g., server error) | Client reports failure; CAD and Part remain at `In Review` — no partial state |
| 4 | Cancel checkout with insufficient disk space for recovery copy | Recovery copy creation fails; cancellation aborted; lock remains active; error reported |

### Scenario 8: Performance Baseline (SC-001, SC-004)

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Set up controlled test fixture: record CAD file size, environment specs (CPU/RAM/disk/OS), network conditions (latency/bandwidth) | Fixture parameters documented in evidence record |
| 2 | Execute checkout-edit-checkin cycle and measure each phase (download, edit window, upload) | Duration recorded; if > 2 min, mark SC-001 post-MVP; `docs/evidence/sc-001-checkout-edit-checkin-performance.md` created |
| 3 | Execute Start New Revision and measure duration | Duration recorded; if > 10 s, mark SC-004 post-MVP; `docs/evidence/sc-004-start-new-revision-performance.md` created |

### Scenario 9: Admin Configuration — Post-MVP Verification (SC-006)

**⛔ Post-MVP only.** Admin configuration UI is deferred. This scenario verifies Aras-side behavior for future reference.

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | PDM Administrator configures role-to-action permissions in Aras Innovator UI | Configuration saved; non-reviewer cannot approve |
| 2 | Administrator attempts to modify released CAD/Part data or audit records | Blocked — administration does not override immutability |
| 3 | Record findings | `docs/evidence/admin-permission-config-evidence.md` |

### Scenario 10: Concurrent New-Revision Race (SC-007)

| Step | Action | Expected Result |
|------|--------|-----------------|
| 1 | Send two simultaneous Start New Revision requests on the same released pair | One succeeds with valid new Part+CAD pair; other receives conflict. No duplicate pair exists |
| 2 | Record findings | `docs/evidence/sc-007-concurrent-new-revision-evidence.md` |

## Reference

- [Data Model](data-model.md) — entities, fields, validation rules, separate Part/CAD state transitions
- [Contracts](contracts/README.md) — interface contracts with Workspace/Core/Aras boundary ownership
- [Spec](spec.md) — feature requirements and acceptance scenarios
- [Research](research.md) — design decisions including Part/CAD lifecycle separation, atomic authority operations, Workspace-owned recovery
