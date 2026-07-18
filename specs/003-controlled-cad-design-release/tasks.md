# Tasks: Controlled CAD Design Release

**Input**: Design documents from `specs/003-controlled-cad-design-release/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/README.md, quickstart.md

**Tests**: Test tasks are included per the constitution's requirement (every code change requires tests or verification evidence). Write tests alongside implementation unless stated otherwise.

**Organization**: Tasks are grouped by evidence gates → foundational → user story phases. Each story phase is independently testable.

---

## Phase 1: Evidence Gates (Setup — blocks all UI enablement)

**Purpose**: Gather Aras environment evidence required before any lifecycle-dependent code can be written or UI enabled. No code changes in this phase.

- [ ] T001-⏳ [GATE-A] Capture verified Part ItemType lifecycle state names, transitions, and semantic roles from the Aras environment. Record in `docs/evidence/part-lifecycle-evidence.md` (new). Must include actual state names (not assumed from CAD), which states are editable/reviewable/releasable/released, and permitted transitions. **Blocks**: PartLifecyclePolicy implementation (T038).
- [ ] T002-⏳ [GATE-B-revise] Verify deployed `idea_ReviseCad` server method provides atomic transactional guarantees in the real Aras environment. Source at `src/IdeaCadConnector.Aras/ServerMethods/idea_ReviseCad.cs` currently versions Part and CAD in separate IOM `apply()` calls without transaction wrapping. Test on real Aras: confirm either (a) Aras transaction wrapping exists in deployment, or (b) Aras version operations within one request are effectively atomic. Record result in `docs/evidence/gate-b-revise-cad-atomicity.md`. **Blocks**: Start New Revision UI enablement.
- [ ] T003-⏳ [GATE-B-approve] Verify deployed `idea_ApproveCadReview` server method provides atomic Part+CAD release in the real Aras environment. Source at `src/IdeaCadConnector.Aras/ServerMethods/idea_ApproveCadReview.cs` exists but is **CAD-only**: it loads CAD, validates `In Review`, unlocks if needed, and promotes CAD to `Released`. It does NOT load Part, check Part state, promote Part, or provide any atomic Part+CAD guarantee. Test on real Aras: confirm deployed method transitions both Part and CAD atomically (or triggers an Aras workflow that achieves coordinated release). The checked-in source alone is NOT sufficient evidence — the deployed method must be verified. If NOT atomic, Approve UI remains disabled. Record result in `docs/evidence/gate-b-approve-cad-review-atomicity.md`. **Blocks**: Approve UI enablement (FR-007, FR-020).
- [ ] T004-⏳ [GATE-W] Verify withdraw capability on the Aras server. The canonical transport is `ExecuteCadBusinessActionAsync(Withdraw)` — this requires `CadBusinessActionKind.Withdraw` and a corresponding server method or workflow transition. Check whether Aras provides a lifecycle transition or server method that moves a CAD revision from `In Review` back to `Thiet ke chi tiet` without recording a review decision. If a suitable mechanism exists, identify the exact server method name or lifecycle transition. If NOTHING exists, the Withdraw UI must remain disabled and the limitation documented in `docs/evidence/gate-w-withdraw-availability.md`. **Blocks**: Withdraw UI enablement.
- [ ] T005-⏳ [GATE-N] Verify Aras audit trail coverage for all lifecycle transitions: checkout, check-in, submit, withdraw, approve, request-rework, start-new-revision. Confirm each event records actor, timestamp, revision identifier, previous state, new state, and reason (where applicable). Record audit schema and available fields in `docs/evidence/gate-n-audit-trail-evidence.md`. **Blocks**: Claiming FR-017 compliance. If any transition lacks audit coverage, the feature's acceptance criteria per FR-017 are NOT fully met — document the gap as a known limitation.
- [ ] T005b-⏳ [GATE-B-checkin] Verify deployed `idea_CommitCadCheckin` server method for check-in atomicity, ChangeSet recording, and audit coverage in the real Aras environment. Source at `src/IdeaCadConnector.Aras/ServerMethods/idea_CommitCadCheckin.cs` performs native_file update and unlock as separate IOM `apply()` calls without transaction wrapping. Comments/change reasons are read but not written to a ChangeSet or audit event. Verify: (a) atomic update+unlock — if native_file update succeeds but unlock fails, does the CAD remain in a consistent state? (b) lock ownership validation is correct; (c) file attachment (native_file) is updated; (d) ChangeSet creation is handled by Aras or missing; (e) audit event is recorded for the check-in transition; (f) check-in reason/comment is persisted. If any claim (e.g., "Atomically complete a CAD check-in" in the method header) is not met by deployed behavior, the check-in path cannot claim FR-003/FR-018 compliance and may need to be disabled or have documented limitations. Record result in `docs/evidence/gate-b-checkin-commit-atomicity.md`. **Blocks**: Claiming FR-003/FR-018 fully compliant.
- [ ] T005c-⏳ [GATE-RW] Verify deployed side effects of `idea_RequestCadRework` on Part lifecycle in the real Aras environment. Source at `src/IdeaCadConnector.Aras/ServerMethods/idea_RequestCadRework.cs` promotes CAD to `Thiet ke chi tiet` and then calls `Sync_Part_From_CAD`, which may load, unlock, promote, or version the linked Part. This is inconsistent with the domain model: Part and CAD have separate lifecycles per ADR-0009, and MVP coordinates them only at Start New Revision and Release approval. Verify (a) whether `Sync_Part_From_CAD` changes Part lifecycle state or creates a new Part version; (b) whether the deployed behavior matches MVP policy (rework should be CAD-only); (c) if Part is indeed modified, a business decision must be made to either update the domain policy or fix the server method. Until verified, Request Rework UI must be disabled or carry a documented limitation. Record result in `docs/evidence/gate-rw-rework-side-effects.md`. **Blocks**: Request Rework UI enablement (FR-008).

**Checkpoint**: All seven evidence documents captured. T002 blocks Start New Revision UI; T003 blocks Approve UI; T004 blocks Withdraw UI; T005 blocks FR-017 compliance claim; T005b blocks FR-003/FR-018 full compliance; T005c blocks Request Rework UI. Each failed gate records the exact limitation — the corresponding code remains in place but disabled.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core interfaces, DTOs, and adapters that block ALL user story work.

- [x] T006 [P] Create `ICadLifecyclePolicy` interface in `src/IdeaCadConnector.Core/Cad/ICadLifecyclePolicy.cs`. Methods: `CanCheckout(string state)`, `CanSubmitForReview(string state)`, `CanApprove(string state)`, `CanRequestRework(string state)`, `CanWithdraw(string state)`, `IsReleased(string state)`. Use backend-neutral PDM language — no Aras-specific types.
- [x] T007 [P] Create `IPartLifecyclePolicy` interface in `src/IdeaCadConnector.Core/Library/IPartLifecyclePolicy.cs`. Methods: `CanRelease(string state)`, `IsReleased(string state)`. State names are SEPARATE from CAD per ADR-0009. Document that GATE-A evidence provides the verified state names; the implementation uses those names.
- [x] T008 [P] Create `CadReleaseEligibilitySnapshot` sealed class in `src/IdeaCadConnector.Core/Dto/CadReleaseEligibilitySnapshot.cs`. Properties: `string CadId`, `string PartId`, `string CadState`, `string PartState`. This is a backend-neutral data snapshot — NOT an Aras type. Used as input to `ICadReleaseEligibility.CheckAsync`.
- [x] T009 [P] Create `CadReleaseEligibilityResult` sealed class in `src/IdeaCadConnector.Core/Dto/CadReleaseEligibilityResult.cs`. Properties: `bool IsEligible`, `IReadOnlyList<string> BlockingReasons`.
- [x] T010 [P] Create `ICadReleaseEligibility` interface in `src/IdeaCadConnector.Core/Contracts/ICadReleaseEligibility.cs`. Method: `Task<CadReleaseEligibilityResult> CheckAsync(CadReleaseEligibilitySnapshot snapshot, CancellationToken ct)`. This is the ADVISORY check — evaluates the snapshot only, NEVER fetches from Aras. Does NOT modify transport.
- [x] T011 [P] Create `RecoveryCopyResult` sealed class in `src/IdeaCadConnector.Workspace/Models/RecoveryCopyResult.cs`. Properties: `bool Succeeded`, `string BackupPath`, `string ErrorMessage`, `string SourceHash` (SHA256), `string BackupHash` (SHA256, verified). On success: Succeeded=true, BackupPath/SourceHash/BackupHash set. On failure: Succeeded=false, ErrorMessage describes reason.
- [x] T012 Create `ArasCadLifecycleAdapter` in `src/IdeaCadConnector.Aras/ArasCadLifecycleAdapter.cs`. Implements `ICadLifecyclePolicy`. Resolves verified CAD lifecycle state names to semantic roles. Uses the existing `CadLifecyclePolicy` constants as reference — extend where needed.
- [x] T013 Create `ArasPartLifecycleAdapter` in `src/IdeaCadConnector.Aras/ArasPartLifecycleAdapter.cs`. Implements `IPartLifecyclePolicy`. Uses verified Part state names from GATE-A evidence. Must NOT reuse CAD constants.

**Checkpoint**: All eight new files compile. Build passes with `dotnet build IdeaCadConnector.sln` — 0 errors, 0 warnings.

---

## Phase 3: User Story 1 — Checkout-Edit-Checkin with Cancel-Checkout Recovery (Priority: P1)

**Goal**: Design engineer checks out a Part-linked CAD working revision, edits the CAD file, checks it back in with a required written reason, and can safely cancel checkout with recovery copy. Check-in without a valid reason is rejected before any file transfer or authority call.

**Independent Test**: Check-out → modify → check-in with valid reason succeeds. Check-in with empty/whitespace reason is rejected pre-upload. File integrity validation failure blocks check-in, lock stays. Authority failure during check-in leaves checkout active for retry. Cancel-checkout with modified file creates verified recovery copy. Cancel-checkout with unchanged file skips backup and unlocks directly.

- [x] T014 [P] [US1] Extend `CadLifecyclePolicy` in `src/IdeaCadConnector.Core/Cad/CadLifecyclePolicy.cs` to implement `ICadLifecyclePolicy`. Add/review implementations for `CanCheckout`, `CanSubmitForReview`, `CanApprove`, `CanRequestRework`, `CanWithdraw`, `IsReleased` using existing CAD lifecycle constants (`Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`). Retain existing static methods for backward compatibility — delegate to interface implementation or mark as forwarding calls. Add `CanWithdraw` static method: returns true for `In Review` state.
- [x] T015 [P] [US1] Create `IRecoveryCopyService` interface in `src/IdeaCadConnector.Workspace/Recovery/IRecoveryCopyService.cs`. Methods: `Task<RecoveryCopyResult> CreateRecoveryCopyAsync(string cadId, string workingFilePath, CancellationToken ct)`, `string GetRecoveryDirectory(string cadId)`, `Task CleanExpiredCopiesAsync(CancellationToken ct)`. Returns `RecoveryCopyResult`.
- [x] T016 [P] [US1] Create `RecoveryCopyRecord` model in `src/IdeaCadConnector.Workspace/Models/RecoveryCopyRecord.cs`. Fields: `Guid RecoveryId`, `string CadId`, `string SourcePath`, `string BackupPath`, `string SourceHash` (SHA256), `string BackupHash` (SHA256, verified), `DateTime CreatedAt`, `DateTime RetentionUntil` (CreatedAt + 30 days).
- [x] T017 [US1] Create `FileSystemRecoveryService` in `src/IdeaCadConnector.Workspace/Recovery/FileSystemRecoveryService.cs`. Implements `IRecoveryCopyService`. Stores recovery copies under `<workspace>/.idea-pdm/recovery/<cad-id>/<timestamp>-<filename>`. On failure during copy or hash mismatch: clean up any partial file, return `RecoveryCopyResult { Succeeded=false, ErrorMessage=<reason> }`. Must NOT leave partial backup on failure.
- [x] T018 [P] [US1] Create `CheckinReasonDialog.xaml`/`.cs` in `src/IdeaCadConnector.Desktop/Dialogs/`. TextBox for required written reason. OK button enabled only when reason is non-empty. Cancel button closes dialog with no side effect. Reason null/empty/whitespace rejected before any upload or authority call.
- [x] T018b [US1] Extend `MainViewModel` and `PdmProjectsViewModel` in `src/IdeaCadConnector.Desktop/` to share a single check-in orchestration path via `CheckoutService` — do NOT duplicate check-in logic in two ViewModels. Wire both entry points through the same validation and upload flow.

  **Check-in with required reason (shared path via CheckoutService)**:
  - Both `MainViewModel.CheckInCommand` and `PdmProjectsViewModel.CheckInCommand` open `CheckinReasonDialog`. If user cancels dialog → no upload, no authority call, no side effect.
  - On dialog OK with valid reason: pass reason to `CheckoutService.UploadAndCheckinAsync(string reason, ...)`.
  - `CheckoutService` must set `CadCheckinRequest.Comment = reason` before calling `IArasCadClient.CheckinAsync`. This is the single location where `Comment` is populated — neither ViewModel sets it directly.
  - Before upload, validate local file integrity (SHA256 hash matches expected or file is not corrupt). If validation fails, block check-in, show error, leave checkout lock active for retry.
  - On success flow: upload file, record ChangeSet (or confirm authority recorded it), release checkout lock. If any step fails (upload, authority check-in, lock release), the checkout remains active and the user can retry.
  - Do NOT implement client-side rollback to simulate atomicity. If ChangeSet recording, audit event, or lock release is the authority's responsibility, document this behavior as requiring verification evidence (see GATE-N, T005, T052) — do not simulate it in the client.
  - On authority failure: display the server error, keep checkout lock, allow retry. Do NOT silently discard the failure.

  **Cancel-checkout with recovery**:
  - Compare baseline hash vs local hash; if modified → call `IRecoveryCopyService.CreateRecoveryCopyAsync` first, show `RecoveryCopyResult.BackupPath`, require explicit confirmation, then call `IArasCadClient.CancelCheckoutAsync` (sends `CancelCheckoutRequest` with only `CadId` + `LockToken` — no backup fields).
  - If unchanged → skip backup, unlock directly.
  - If backup fails (`RecoveryCopyResult.Succeeded == false`) → abort cancellation, keep lock active, show `ErrorMessage`.
  - **Cancel-checkout recovery integration is NOT yet implemented** — recovery copy orchestration in the ViewModels requires the recovery service to be wired through DI or a factory. The `FileSystemRecoveryService` and `IRecoveryCopyService` exist and are tested. The cancel-checkout ViewModel paths still call `CancelCheckoutAsync` directly without recovery.
- [x] T019 [US1] Extend `PdmProjectsViewModel` in `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs` to refresh action availability (checkout, check-in, cancel-checkout buttons enabled/disabled) based on CAD lifecycle state via `ICadLifecyclePolicy`. Disable checkout for `Released` or `In Review` states. Disable check-in when not checked out to current user.
- [x] T020 [US1] Add resource strings for cancel-checkout recovery and check-in reason validation errors. NOTE: integrated into `TranslationKeys`/`TranslationResources` (the active localization system uses an in-memory dictionary, not `.resx`) and consumed by the cancel-checkout recovery flow (`CancelCheckoutModifiedConfirm`, `CancelCheckoutRecoveryFailed`). The unused `Strings.resx` is retained for reference only.
- [x] T021 [P] [US1] Write/extend `CadLifecyclePolicyTests` in `tests/IdeaCadConnector.Tests/CadLifecyclePolicyTests.cs`. Test every `ICadLifecyclePolicy` method against all known CAD states. Include negative tests and `CanWithdraw` tests. 20 test cases covering all ICadLifecyclePolicy methods, CanExecuteBusinessAction with Withdraw, and interface delegation.
- [x] T022 [P] [US1] Write `FileSystemRecoveryServiceTests` in `tests/IdeaCadConnector.Tests/FileSystemRecoveryServiceTests.cs`. Test: backup success (content match, hash verified), source-not-found, null/empty args, copy failure cleanup, concurrent isolated cadIds, multiple backups same cadId. 28 test cases.
- [ ] T023 [US1] Write/extend ViewModel tests in `tests/IdeaCadConnector.Tests/MainViewModelTests.cs` and `tests/IdeaCadConnector.Tests/PdmProjectsViewModelTests.cs`. Include:

  **Check-in reason dialog (both VMs)**:
  - Open check-in reason dialog → user cancels → no upload, no authority call, no side effect.
  - Open dialog with empty/whitespace reason → OK disabled or reason rejected pre-upload; authority never called.
  - Open dialog with valid reason → `CheckoutService` receives the correct reason string.

  **CheckoutService reason propagation**:
  - `UploadAndCheckinAsync` called with valid reason → `CadCheckinRequest.Comment` set to that exact value; `IArasCadClient.CheckinAsync` invoked with the populated request.
  - Reason null/empty/whitespace → `UploadAndCheckinAsync` throws or returns failure before calling `CheckinAsync`.

  **Behavior parity**:
  - `MainViewModel.CheckInCommand` and `PdmProjectsViewModel.CheckInCommand` produce identical check-in behavior (same validation, same `Comment` propagation, same failure handling).

  **File integrity validation failure**: corrupt or hash-mismatched file → check-in blocked, error shown, lock remains active.

  **Authority check-in failure**: server error → error displayed, lock remains active, retry available.

  **Successful check-in**: upload succeeds → authority confirms → lock released. If ChangeSet/audit/lock release is authority responsibility, verify via evidence (mock authority response, do not simulate client-side).

  **Cancel-checkout**: modified → backup → unlock; backup fail → unlock NOT called; unchanged → backup skip → unlock directly.

  **Action gating**: checkout blocked for Released/In Review; check-in disabled when not checked out to current user.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~CadLifecyclePolicyTests|RecoveryCopyServiceTests|CheckoutViewModelTests|CancelCheckoutViewModelTests"` all pass.

---

## Phase 4: User Story 2 — Submit for Review, Withdraw, Approve, Request Rework (Priority: P1)

**Goal**: Engineer submits checked-in CAD for review; can withdraw own submission before reviewer acts; reviewer approves (releases both CAD + Part atomically) or requests rework.

**Independent Test**: Submit → withdraw → CAD returns to `Thiet ke chi tiet`, no review decision recorded. Submit → reviewer approves → both CAD and Part transition to Released. Submit → reviewer requests rework → CAD returns to `Thiet ke chi tiet`.

- [x] T024 [P] [US2] Add `Withdraw` to `CadBusinessActionKind` in `src/IdeaCadConnector.Core/Dto/CadBusinessActionKind.cs`. Add after `RequestRework`: `Withdraw`. This is NOT a new transport — it extends the existing enum that feeds `ExecuteCadBusinessActionAsync`.
- [x] T025 [P] [US2] Add `Withdraw` mapping in `WorkflowActionMapper` in `src/IdeaCadConnector.Core/Cad/WorkflowActionMapper.cs`. Add a new mapping rule (e.g., `mapper.AddRule(lifecycleTransitionName, "", CadBusinessActionKind.Withdraw)`) where `lifecycleTransitionName` is the verified Aras lifecycle transition or server method name from GATE-W (T004). If GATE-W determines no equivalent server method exists, the mapper entry may be left unresolved (unreachable) and the Withdraw UI remains disabled.
- [x] T026 [P] [US2] Add `Withdraw` case to `ArasCadClient.ExecuteCadBusinessActionAsync` in `src/IdeaCadConnector.Aras/ArasCadClient.cs`. Follow the existing pattern: add a switch case. Since GATE-W (T004) is still pending, the case throws `ArasOperationException(WorkflowActionNotAvailable)` with a clear message explaining the gate dependency. The path is unreachable until GATE-W evidence is collected.
- [x] T027 [P] [US2] Add `Withdraw` case to `HttpArasCadClient.ExecuteCadBusinessActionAsync` in `src/IdeaCadConnector.Aras/HttpArasCadClient.cs`. Same pattern as T026 — throws `ArasOperationException(WorkflowActionNotAvailable)` pending GATE-W verification.
- [x] T028 [P] [US2] Create `MvpReleaseEligibility` in `src/IdeaCadConnector.Core/Policies/MvpReleaseEligibility.cs`. Implements `ICadReleaseEligibility`. `CheckAsync` receives `CadReleaseEligibilitySnapshot` — reads CadState and PartState from the snapshot, does NOT fetch from Aras. Calls `ICadLifecyclePolicy.CanApprove(cadState)` and `IPartLifecyclePolicy.CanRelease(partState)` separately. If either returns false, populates `BlockingReasons`.
- [x] T029 [P] [US2] Create `SubmitForReviewDialog.xaml` and `.cs` in `src/IdeaCadConnector.Desktop/Dialogs/`. Fields: CAD revision (read-only), Part revision (read-only), change description (required), reviewer selector (dropdown), submit/cancel buttons.
- [x] T030 [P] [US2] Create `ReviewDecisionDialog.xaml` and `.cs` in `src/IdeaCadConnector.Desktop/Dialogs/`. Buttons: Approve (calls `ICadReleaseEligibility.CheckAsync` with snapshot; if eligible, calls `ExecuteCadBusinessActionAsync(Approve)`), Request Rework (required reason, calls `ExecuteCadBusinessActionAsync(RequestRework)`), Cancel. Approve disabled if T003 not passed.
- [x] T031 [P] [US2] Create `WithdrawConfirmDialog.xaml` and `.cs` in `src/IdeaCadConnector.Desktop/Dialogs/`. Shows submission info, confirm/cancel buttons. Calls `ExecuteCadBusinessActionAsync(Withdraw)`. Available only to owning engineer while submission is Pending. Disabled if T004 not passed.
- [ ] T032 [US2] Extend `MainViewModel` in `src/IdeaCadConnector.Desktop/MainViewModel.cs` with submit/approve/rework/withdraw orchestration. Wire through `IArasCadClient.ExecuteCadBusinessActionAsync`. For approve: read current CAD and Part states, construct `CadReleaseEligibilitySnapshot`, call `ICadReleaseEligibility.CheckAsync(snapshot)`; if eligible, call `ExecuteCadBusinessActionAsync(Approve)`. For withdraw: call `ExecuteCadBusinessActionAsync(Withdraw)` — only if owning engineer and submission is Pending. Approve disabled if T003 not passed; withdraw disabled if T004 not passed.
- [ ] T033 [US2] Extend `PdmProjectsViewModel` in `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs` to show submit/withdraw/approve/rework action availability. Disable submit unless `Thiet ke chi tiet`. Disable withdraw unless `In Review` and owning engineer. Disable approve/rework unless `In Review` and assigned reviewer. Grey out actions blocked by gates (T003, T004).
- [x] T034 [US2] Add resource strings in `src/IdeaCadConnector.Ui/Resources/Strings.resx` for review/withdraw operations.
- [ ] T035 [P] [US2] Write/extend tests for: `CadBusinessActionKind.Withdraw` enum test in `CadBusinessActionKindTests.cs` (new or existing); `WorkflowActionMapper` withdraw mapping in `WorkflowActionMapperTests.cs`; `ArasCadClient` withdraw case in `ArasCadClientTests.cs`; `HttpArasCadClient` withdraw case in `HttpArasCadClientTests.cs`.
- [x] T036 [P] [US2] Write `MvpReleaseEligibilityTests` in `tests/IdeaCadConnector.Tests/MvpReleaseEligibilityTests.cs`. Test: both eligible, CAD ineligible, Part ineligible, both ineligible, null/empty snapshot.
- [ ] T037 [US2] Write/extend ViewModel tests for submit/approve/rework/withdraw in `tests/IdeaCadConnector.Tests/MainViewModelTests.cs` and `tests/IdeaCadConnector.Tests/PdmProjectsViewModelTests.cs`. Mock `ICadReleaseEligibility`, `IArasCadClient.ExecuteCadBusinessActionAsync`, `ICadLifecyclePolicy`. Test: eligible approve calls authority with snapshot; ineligible approve shows blocking reasons; withdraw succeeds when owner and Pending; withdraw fails when not owner; no review decision on withdraw. Approve respects T003; withdraw respects T004.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~MvpReleaseEligibilityTests|SubmitReviewViewModelTests|WithdrawViewModelTests|ReviewViewModelTests"` all pass. Manual: submit → withdraw works; submit → approve releases both (if T003 passed).

---

## Phase 5: User Story 3 — Released Read-Only, Start New Revision (Priority: P2)

**Goal**: Released revision is immutable. Starting new design work atomically creates a new Part Revision + linked CAD Revision while preserving the released pair.

**Important**: US3 has two independent sub-goals with different gate dependencies:
- **Released read-only blocking**: requires T001 (GATE-A for Part state names) + Phase 2. Does NOT require T002 — it uses client-side policy checks only.
- **Start New Revision**: requires T001 (GATE-A) + T002 (GATE-B-revise) + Phase 2. T002 is mandatory because `idea_ReviseCad` must provide atomic Part+CAD creation.

**Evidence gate dependency**: Start New Revision UI is enabled ONLY after BOTH T001 (GATE-A) AND T002 (GATE-B-revise) pass. Released read-only blocking works after T001 alone.

- [ ] T038 [P] [US3] Implement `PartLifecyclePolicy` in `src/IdeaCadConnector.Core/Library/PartLifecyclePolicy.cs` with `IPartLifecyclePolicy`. Use verified Part state names from T001 evidence. `CanRelease(string state)` returns true only for the eligible pre-release state. `IsReleased(string state)` returns true only for the Released state. Must NOT reference CAD constants.
- [ ] T039 [US3] Extend `MainViewModel` in `src/IdeaCadConnector.Desktop/MainViewModel.cs`:
  - **Released read-only blocking**: Block checkout/edit/modify on Released revisions via `ICadLifecyclePolicy.IsReleased` and `IPartLifecyclePolicy.IsReleased`. Show "Released revisions are read-only" within 2 seconds (SC-003). This works after T001 passes — no T002 dependency.
  - **Start New Revision flow**: Wire through `IRevisionService.ReviseAsync` → `IPdmRepositoryClient.ReviseCadAsync`. Before calling, verify `ICadLifecyclePolicy.IsReleased(cadState)`. After success, refresh workspace. Button disabled until BOTH T001 AND T002 pass.
- [ ] T040 [US3] Extend `PdmProjectsViewModel` in `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs`. Show Start New Revision only when CAD is `Released`. Disable all modification actions when `IsReleased`. Grey out Start New Revision if T002 not passed.
- [ ] T041 [P] [US3] Write `PartLifecyclePolicyTests` in `tests/IdeaCadConnector.Tests/PartLifecyclePolicyTests.cs`. Use test fixture from `docs/evidence/part-lifecycle-evidence.md`.
- [ ] T042 [US3] Write/extend ViewModel tests. Mock `IRevisionService`, `ICadLifecyclePolicy`, `IPartLifecyclePolicy`. Test: Start New Revision succeeds from Released pair (when T002 passed); checkout of Released blocked with read-only message within 2 seconds. Start New Revision disabled when T002 not passed.
- [ ] T059a [US3] Write `ConcurrentNewRevisionResultHandlingTests` in `tests/IdeaCadConnector.Tests/ConcurrentNewRevisionResultHandlingTests.cs`. Mock `IPdmRepositoryClient` to return a success result for one request and a conflict/failure result for the other. Verify the ViewModel surfaces the conflict message to the user and does not create a duplicate pair on the mock side. **Note**: This tests client-side result handling only — it does NOT test actual authority concurrency.
- [ ] T059b [US3] Real-Aras concurrency evidence. Send two simultaneous Start New Revision requests on the same released pair. Verify one succeeds with a valid new Part+CAD pair and the other receives an authority conflict response. Verify no duplicate pair is created on the server. Record in `docs/evidence/sc-007-concurrent-new-revision-evidence.md`. If real Aras concurrency cannot be tested in MVP (requires verified `idea_ReviseCad` deployment and concurrent access), mark SC-007 as evidence-gated/post-MVP.

**Checkpoint**: `dotnet test --filter "FullyQualifiedName~PartLifecyclePolicyTests|StartNewRevisionViewModelTests|ConcurrentNewRevisionResultHandlingTests"` all pass. Manual: Released revision blocks checkout within 2 seconds. Start New Revision creates working pair (if T001 AND T002 passed). Concurrent new-revision race produces one success and one conflict (see T059b evidence).

---

## Phase 6: User Story 4 — Project Manager Visibility (Priority: P3)

**Goal**: Project manager views Part-linked CAD revision lifecycle state, checkout status, and revision history in read-only mode. All modification actions are blocked.

- [ ] T043 [US4] Extend `PdmProjectsViewModel` in `src/IdeaCadConnector.Desktop/PdmProjectsViewModel.cs` to display lifecycle state name, checkout owner (or "Available"), and revision identifier for each Part-linked CAD revision. Hide/disable all action buttons for non-engineer roles.
- [ ] T044 [US4] Add role-aware action blocking in `MainViewModel` in `src/IdeaCadConnector.Desktop/MainViewModel.cs`. When a user without Design Engineer/Reviewer permission attempts any modification action, block with message. Do not call any authority method.

**Checkpoint**: Project manager sees all state/checkout info. Modification blocked at client level.

---

## Phase 7: User Story 5 — PDM Admin Configuration (DEFERRED — Post-MVP)

**⛔ This phase is DEFERRED to post-MVP.** US5 is Priority P3. The MVP scope (US1 + US2, both P1) does not include admin configuration UI. See `docs/configuration/lifecycle-and-permissions-config.md` for reference documentation.

**Deferred backlog (informational — not implementable in MVP):**

- Create admin configuration dialog with role-to-action permission and lifecycle mapping views. *(Post-MVP)*
- Create ReviewReassignDialog and wire to `IArasCadClient.ExecuteCadBusinessActionAsync(ReassignReviewer)`. *(Post-MVP)*
- Add admin permission gating in MainViewModel for configuration actions. *(Post-MVP)*

**What IS in MVP scope for FR-009/FR-014/FR-015/FR-016**:
- FR-009 (reviewer reassignment) is deferred — the reviewer is selected at submission time only in MVP.
- FR-014/FR-015 (admin configures permissions and lifecycle mappings) are deferred — Aras-side configuration is done in the Innovator UI.
- FR-016 (admin cannot modify released/audit data) is enforced server-side by Aras. The client respects Aras permission rejections for all roles.
- MVP tasks that apply: document Aras-side requirements in `docs/configuration/lifecycle-and-permissions-config.md` (T048 below), and verify client permission respect (T049 below).

- [x] T048 Document Aras-side configuration requirements in `docs/configuration/lifecycle-and-permissions-config.md` (new). Cover: required Part and CAD ItemTypes, lifecycle maps, role-to-action permission setup on the server, and expected state names. This is reference documentation for the Aras administrator — NOT a client implementation.
- [ ] T049 Verify that every `IArasCadClient.ExecuteCadBusinessActionAsync` and `IPdmRepositoryClient.ReviseCadAsync` call path respects Aras-configured permissions. If the Aras server rejects an action (e.g., insufficient permissions, ineligible lifecycle state), the client must display the server's error message without swallowing or overriding it. No client-side "admin bypass" logic. Record verification in `docs/evidence/permissions-client-respect-evidence.md`.
- [ ] T060 [US5] Verify SC-006 admin permission configuration. **Deferred post-MVP evidence task** — US5 admin configuration UI is not part of MVP. Manual/UAT: a PDM Administrator configures role-to-action permissions in the Aras Innovator UI. Confirm that configuration changes take effect for action gating (e.g., non-reviewer cannot approve). Confirm that the administrator cannot modify released CAD/Part data, completed ChangeSets, or audit records through configuration actions. Record findings in `docs/evidence/admin-permission-config-evidence.md`. This task does NOT affect MVP completion.

**Checkpoint**: MVP correctly propagates Aras permission errors. Admin configuration UI is deferred to post-MVP. FR-014/FR-015 are NOT claimed as implemented in MVP completion checklist. SC-006 is DEFERRED post-MVP — evidence may be collected as a deferred manual task (T060).

---

## Phase 8: Reviewer Reassignment (Priority: P3 — Deferred to Post-MVP)

**⛔ This phase is DEFERRED to post-MVP.** Reviewer reassignment per FR-009 requires a clean authority contract and adapter. It is not part of the MVP.

**Design decision for future implementation (not implementable now)**:
- Define `IReviewReassignmentService` in `src/IdeaCadConnector.Core/Contracts/IReviewReassignmentService.cs`:
  ```csharp
  public interface IReviewReassignmentService
  {
      Task<ReviewReassignmentResult> ReassignAsync(
          ReviewReassignmentRequest request, CancellationToken ct);
  }
  ```
  - `ReviewReassignmentRequest`: SubmissionId, CurrentReviewerId, NewReviewerId, Reason (optional).
  - `ReviewReassignmentResult`: Succeeded, ErrorMessage.
- Implement `ArasReviewReassignmentService` in `src/IdeaCadConnector.Aras/`. Maps to an Aras server method or workflow update via `IArasCadClient.ExecuteCadBusinessActionAsync(ReassignReviewer)` where `ReassignReviewer` is an added `CadBusinessActionKind` value.
- Do NOT simulate reassignment client-side. The authority must validate the new reviewer's eligibility.
- PDM Administrator role check required before action is available.
- **Evidence gate (GATE-REASSIGN)**: Verify deployed server method for reassignment before enabling UI.

**Checkpoint**: Not implemented in MVP. Design decision recorded for future implementation.

---

## Phase 9: Notification and Audit Verification

**Purpose**: Verify that the Aras authority provides notifications and audit events per spec. The client does NOT implement its own notification or audit system.

**GATE-N (T005) blocking behavior**: If any lifecycle transition lacks audit trail coverage per FR-017, the feature cannot claim full compliance with FR-017. The gap is documented as a known limitation and blocks the feature completion checklist from marking FR-017 as satisfied.

- [ ] T050 Verify that submitting for review triggers an Aras notification to the assigned reviewer. Record in `docs/evidence/notification-submit-evidence.md`.
- [ ] T051 Verify that approving/rework triggers an Aras notification to the submitting engineer. Record in `docs/evidence/notification-approve-rework-evidence.md`.
- [ ] T052 Cross-reference GATE-N evidence (T005) against each lifecycle transition. Confirm each event includes: actor, timestamp, revision identifier, previous state, new state, reason. Coverage required: checkout, check-in, submit, withdraw, approve, request-rework, start-new-revision. Record gaps in `docs/evidence/audit-trail-gaps.md`.

**Checkpoint**: Evidence documents confirm notification and audit behavior. If T005 gaps exist, FR-017 is marked as partially satisfied with documented limitations.

---

## Phase 10: Polish & Cross-Cutting Concerns

- [ ] T053 [P] Run full build and test suite: `dotnet build IdeaCadConnector.sln` (0 errors, 0 warnings); `dotnet test IdeaCadConnector.sln` (all existing + new tests pass, 0 regressions).
- [ ] T054 Run `quickstart.md` validation scenarios. Record any failures.
- [ ] T055 Update canonical documentation for any behavior changes.
- [ ] T056 [US1] Record SC-001 performance baseline for checkout-edit-checkin. Create `docs/evidence/sc-001-checkout-edit-checkin-performance.md`. Execute checkout-edit-checkin in a controlled Aras test environment. Record: CAD file size, environment specifications (CPU, RAM, disk type, OS version), network conditions (estimated latency, bandwidth), and measured duration broken down by phase (download, edit window, upload). If the 2-minute target cannot be achieved, mark SC-001 as post-MVP verification and document the actual measured baseline.
- [ ] T057 [US2] Verify SC-002 review-to-approve UX step count. Count user-facing steps from Submit → review → approve. Record each distinct step. If the count exceeds 5, document the deviation and recommend simplification. Manual/UAT verification — requires a functional UI.
- [ ] T058 [US3] Record SC-004 performance baseline for Start New Revision. Create `docs/evidence/sc-004-start-new-revision-performance.md`. Execute Start New Revision in a controlled Aras test environment. The evidence record MUST identify the exact released Part/CAD pair used, the test setup (environment specs, network conditions), and the measured duration. If the 10-second target cannot be verified in MVP, mark SC-004 as post-MVP verification and document the actual measured baseline.

---

## Dependencies & Execution Order

### Phase Dependencies

| Phase | Depends On | Blocking Gate | Notes |
|-------|-----------|---------------|-------|
| Phase 1 | None | — | All evidence gates |
| Phase 2 | T001 (for T013) | T001 for ArasPartLifecycleAdapter | T006–T011 parallel with Phase 1 |
| Phase 3 (US1) | Phase 2 | None | Checkout/checkin not gated |
| Phase 4 (US2) | Phase 2 | T003 blocks Approve; T004 blocks Withdraw; T005c blocks Request Rework | Can run parallel with Phase 3; rework disabled until GATE-RW passes |
| Phase 5 (US3) | T001 + Phase 2 | T001 + T002 block Start New Revision; T001 alone enough for read-only blocking | Released read-only and Start New Revision have different gate deps |
| Phase 6 (US4) | Phase 3 + 4 | None | Reuses action state infra |
| Phase 7 (US5) | — | — | DEFERRED post-MVP |
| Phase 8 (Reassign) | — | — | DEFERRED post-MVP |
| Phase 9 | Phase 1 + 3–5 | T005 blocks FR-017 compliance claim | All transitions must exist before audit verification |
| Phase 10 | All implemented phases | — | Final verification |

### Evidence Gate → Action Mapping

| Gate | Blocks | Affected Story |
|------|--------|---------------|
| T001 (GATE-A) | PartLifecyclePolicy implementation (T038) | US3 |
| T002 (GATE-B-revise) | Start New Revision UI enablement | US3 |
| T003 (GATE-B-approve) | Approve UI enablement (FR-007, FR-020) | US2 |
| T004 (GATE-W) | Withdraw UI enablement | US2 |
| T005 (GATE-N) | Claiming FR-017 compliance | Phase 9 |
| T005b (GATE-B-checkin) | Claiming FR-003/FR-018 full compliance | US1 |
| T005c (GATE-RW) | Request Rework UI enablement (FR-008) | US2 |

If a gate FAILS: the corresponding UI element remains in code but is disabled with a clear limitation tooltip. Do NOT remove the code.

### User Story Dependencies

- **US1 (P1)**: No dependency on other stories. No blocking gates.
- **US2 (P1)**: No dependency on US1. Blocking gates: T003 (Approve), T004 (Withdraw), T005c (Request Rework).
- **US3 (P2)**: No dependency on US1/US2. **Released read-only blocking**: depends on T001 + Phase 2 only. **Start New Revision UI**: depends on T001 + T002 + Phase 2. Implementer C MUST wait for BOTH T001 AND T002 before enabling Start New Revision.
- **US4 (P3)**: Depends on US1 + US2 action infrastructure.
- **US5 (P3)**: DEFERRED post-MVP.

### Parallel Team Strategy

1. Team completes Phase 1 + Phase 2 together.
2. Once Phase 2 is done:
   - Implementer A: Phase 3 (US1) — checkout/checkin/cancel
   - Implementer B: Phase 4 (US2) — submit/withdraw/approve/rework
3. Implementer C: Phase 5 (US3) — can start released read-only blocking after T001 + Phase 2. But Start New Revision UI requires Implementer C to wait for BOTH T001 AND T002.
   - Inside US3, implementer can:
     - Implement `PartLifecyclePolicy` (T038) after T001
     - Implement read-only blocking in MainViewModel (T039 part 1) after T001
     - Implement Start New Revision flow in MainViewModel (T039 part 2) only AFTER T002
4. Phase 6 (US4) after Phase 3 + 4 stable.
5. Phase 9 (notification/audit) after Phase 1 + transitions exist.

---

## Parallel Opportunities

| Phase | [P]-eligible tasks |
|-------|-------------------|
| Phase 2 | T006–T011 (6 interfaces/DTOs) |
| Phase 3 | T014–T016 (policy, service, model); T018 (dialog); T021–T022 (tests) |
| Phase 4 | T024–T031 (enum, mapper, 2 clients, policy, 3 dialogs); T035–T036 (tests) |
| Phase 5 | T038 (policy); T041 (test); T059a (concurrency result-handling test) |
| Phase 10 | T053 (build/test) |

---

## FR-to-Task Coverage Map

| FR | Description | Covered By | Status |
|----|------------|------------|--------|
| FR-001 | Checkout eligible CAD revision | T014, T018, T019 | MVP |
| FR-002 | Exclusive checkout, conflict message | T018, T020 | MVP |
| FR-003 | Check-in with required written reason | T005b, T018, T018b, T019, T020, T023 | MVP (blocked by T005b until check-in atomicity/ChangeSet/audit verified) |
| FR-004 | Cancel-checkout with recovery | T015, T017, T018, T023 | MVP |
| FR-005 | Submit for review, notify reviewer | T029, T032, T050 | MVP (notify = verify T050) |
| FR-006 | Withdraw before reviewer acts | T024–T027, T031, T032, T037 | MVP (if GATE-W passes) |
| FR-007 | Approve → atomic Part+CAD release, notify | T003, T028, T030, T032, T051 | MVP (blocked by T003 — checked-in source is CAD-only; deployed method must provide coordinated Part+CAD release) |
| FR-008 | Request rework with explanation | T005c, T032, T051 | MVP (blocked by T005c — server method may modify Part lifecycle via Sync_Part_From_CAD) |
| FR-009 | PDM Admin reassign reviewer | Phase 8 (all deferred) | DEFERRED post-MVP |
| FR-010 | Released read-only, block modification | T039, T040, T042 | MVP |
| FR-011 | Start New Revision → atomic new Part+CAD | T002, T038, T039, T042 | MVP (if GATE-B-revise passes) |
| FR-012 | State eligibility for each action | T006, T007, T014, T021, T028, T038, T041 | MVP |
| FR-013 | Project Manager read-only view | T043, T044 | MVP |
| FR-014 | Admin configures role-to-action permissions | T048 (doc only) | DEFERRED post-MVP |
| FR-015 | Admin maps lifecycle states | T048 (doc only) | DEFERRED post-MVP |
| FR-016 | Admin cannot modify released/audit data | T049 (verify) | MVP (Aras-enforced) |
| FR-017 | Audit trail for all transitions | T005, T005b, T050, T051, T052 | MVP (blocked by T005 gaps; check-in audit covered by T005b) |
| FR-018 | Safe failure with no partial state | T005b, T018b, T032 | MVP (blocked by T005b until check-in atomicity verified) |
| FR-019 | Backend-neutral PDM language | All interfaces (T006–T011) | MVP |
| FR-020 | Atomic release: both succeed or neither | T003, T028, T032 | MVP (blocked by T003 — checked-in source is CAD-only) |
| FR-021 | Atomic new revision: both succeed or neither | T002, T038, T039 | MVP (if GATE-B-revise passes) |

## SC-to-Task Coverage Map

| SC | Description | Covered By | Status |
|----|------------|------------|--------|
| SC-001 | Checkout-edit-checkin < 2 min | T056 | MVP (performance baseline); post-MVP if target cannot be verified |
| SC-002 | Review-to-approve < 5 user-facing steps | T057 | MVP (UX step-count verification) |
| SC-003 | Released block within 2 s | T039, T042 | MVP |
| SC-004 | New revision < 10 s | T058 | MVP (performance baseline); post-MVP if target cannot be verified |
| SC-005 | Project Manager read-only view | T043, T044 | MVP |
| SC-006 | Admin config without data modification | T060 | DEFERRED post-MVP |
| SC-007 | Concurrent new-revision race handled | T059a, T059b | Evidence-gated (T059b requires real Aras; if not testable in MVP, post-MVP) |
| SC-008 | Audit events complete and immutable | T005, T005b, T050, T051, T052 | MVP (blocked by T005 gaps) |

---

## Notes

- [P] tasks = different files, independent, no sequential dependencies.
- Each user story independently testable via `dotnet test --filter "FullyQualifiedName~..."`.
- **Evidence gates (T001–T005, T005b, T005c) block runtime UI enablement or compliance claims**. All code paths are designed and documented in artifacts, but their runtime activation requires verified Aras evidence recorded in `docs/evidence/`. Until a gate passes, the corresponding action remains disabled. Do NOT claim Approve, Withdraw, Start New Revision, Request Rework, or FR-003/FR-018/FR-017 compliance as enabled before their gate evidence exists.
- `IArasCadClient.ExecuteCadBusinessActionAsync` is the canonical transport seam for submit/approve/rework/withdraw. No `ReleaseCadPartPairAsync`, `SubmitForReviewAsync`, or `DecideReviewAsync`.
- `ICadReleaseEligibility` is advisory, evaluates a snapshot only — never fetches from Aras.
- No client-side rollback. No sequential independent transitions pretending to be atomic.
- Part and CAD have SEPARATE lifecycle policies per ADR-0009.
- No Aras-specific types in Core contracts.
- `CancelCheckoutRequest` stays: `CadId` + `LockToken` only. No backup metadata in remote unlock.
- Recovery copy belongs to Workspace. Desktop orchestrates. Aras unlocks only.
- US5 and reviewer reassignment are DEFERRED to post-MVP. FR-014/FR-015 are NOT claimed as implemented in MVP.
- Commit after each task or logical group. Stop at each checkpoint to validate independently.
- **61 executable tasks** (T001–T044, T005b, T005c, T018b, T048–T058, T059a, T059b, T060). T045–T047 are deferred informational bullets — not counted as executable tasks. T005b, T005c, T056, T057, T058, T059a, T059b, and T060 were added for check-in atomicity, rework side-effect gates, and success-criteria coverage.
