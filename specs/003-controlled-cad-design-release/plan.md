# Implementation Plan: Controlled CAD Design Release

**Branch**: `003-controlled-cad-design-release` | **Date**: 2026-07-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-controlled-cad-design-release/spec.md`

## Summary

Implement the controlled CAD design release workflow for the IDEA PDM desktop application. The workflow extends existing infrastructure: `IArasCadClient` (both `HttpArasCadClient` and `ArasCadClient`) already handles submit/approve/rework/withdraw via `ExecuteCadBusinessActionAsync`. The `HttpPdmRepositoryClient.ReviseCadAsync` transport method exists and calls the `idea_ReviseCad` server method, but actual Start New Revision behavior remains blocked by **GATE-B-revise** until deployed server atomicity, response shape, Part+CAD creation, linking, and conflict behavior are verified. Client method existence does not prove authority operation correctness. New additions: `ICadReleaseEligibility` (Part + CAD eligibility check via snapshot), `IRecoveryCopyService` in Workspace (safe cancel-checkout backup), Withdraw capability, Desktop orchestration connecting them, UI dialogs, and Part lifecycle verification. Seven evidence gates block UI enablement or compliance claims until Aras server-behavior is verified. US5 (admin configuration) and FR-009 (reviewer reassignment) deferred to post-MVP.

## Technical Context

**Language/Version**: C# 7.3+, .NET Framework 4.8 (net48)

**Primary Dependencies**: Aras IOM (Innovator 12.0+), IronCAD COM interop (ICAPI, version 27.0), WPF, xUnit, Moq

**Existing Infrastructure** (canonical implementations):

| Interface | Aras Implementation(s) | Status |
|-----------|----------------------|--------|
| `IArasCadClient` | `HttpArasCadClient` (HTTP/REST, no IOM), `ArasCadClient` (IOM-based) | Canonical seam for submit, approve, request-rework, and withdraw — all four flow through the same `ExecuteCadBusinessActionAsync` method (no new transport method). `CadBusinessActionKind.Withdraw` added to enum. Both clients extended with Withdraw case. No `ReleaseCadPartPairAsync`, `SubmitForReviewAsync`, or `DecideReviewAsync` created. |
| `IPdmRepositoryClient` | `HttpPdmRepositoryClient` | `ReviseCadAsync` calls `idea_ReviseCad` |
| `IRevisionService` | `GuidanceRevisionService` | Wraps `HttpPdmRepositoryClient.ReviseCadAsync` |

**Storage**: Aras Innovator as PDM authority (Part, CAD ItemTypes with separate lifecycle maps); local filesystem for Workspace metadata and recovery copies

**Testing**: xUnit + Moq; integration tests require dedicated Aras test environment

**Target Platform**: Windows x64, IronCAD 2025 (internal 27.0) + Autodesk Inventor, .NET Framework 4.8

**Performance Goals**: Checkout-edit-checkin under 2 min (controlled fixture — see SC-001); new revision under 10 s (controlled fixture — see SC-004); approval/cancel-checkout under 5 s; Released block within 2 s

**Constraints**: Exclusive checkout; recovery copy before destructive cancel-checkout; Part and CAD lifecycle bindings require verified evidence

**Evidence Gates** (block UI enablement or compliance claims until verified):
1. **GATE-A (Part lifecycle)**: Capture verified Part ItemType lifecycle state names, transitions, and semantic roles from the Aras environment. `PartLifecyclePolicy` must use actual verified Part state names — not assumed from CAD.
2. **GATE-B-revise (Server atomicity — ReviseCad)**: Verify deployed `idea_ReviseCad` server method provides atomic transactional guarantees. Source exists in the repository but versions Part and CAD in separate IOM `apply()` calls. If NOT atomic, Start New Revision UI remains disabled.
3. **GATE-B-approve (Server atomicity — ApproveCadReview)**: Verify deployed `idea_ApproveCadReview` server method provides atomic Part+CAD release. Checked-in source at `src/IdeaCadConnector.Aras/ServerMethods/idea_ApproveCadReview.cs` exists but is **CAD-only**: it promotes CAD to Released without loading, checking, or promoting the linked Part. This does NOT satisfy Q1/FR-007/FR-020. The deployed method must provide coordinated Part+CAD release with atomicity guarantees. If NOT atomic, Approve UI remains disabled.
4. **GATE-B-checkin (Server atomicity — CommitCadCheckin)**: Verify deployed `idea_CommitCadCheckin` server method for atomic update+unlock, ChangeSet creation, audit event recording, and check-in reason persistence. Checked-in source performs native_file update and unlock as separate `apply()` calls without transaction wrapping. Comment is read but not persisted to a ChangeSet or audit event. If atomicity or ChangeSet/audit is missing, FR-003/FR-018 cannot claim full compliance.
5. **GATE-W (Withdraw)**: Verify withdraw capability on the Aras server — a lifecycle transition or server method that returns CAD from `In Review` to `Thiet ke chi tiet` without recording a review decision. If NOT available, Withdraw UI remains disabled.
6. **GATE-RW (Rework side effects)**: Verify deployed `idea_RequestCadRework` server method side effects on Part lifecycle. Checked-in source promotes CAD to `Thiet ke chi tiet` then calls `Sync_Part_From_CAD`, which may load, unlock, promote, or version the linked Part — inconsistent with the domain model (Part and CAD have separate lifecycles; MVP coordinates only at Start New Revision and release). Verify whether Part state/version is actually changed. If side effects contradict MVP policy, Request Rework UI must be disabled or a business decision must update the policy.
7. **GATE-N (Audit trail)**: Verify Aras audit trail covers all lifecycle transitions per FR-017. If any transition lacks audit coverage, FR-017 compliance cannot be claimed — the gap is a documented limitation.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* ✓ Re-checked — the artifact/design checks pass. Runtime evidence gates remain **PENDING** until the seven evidence documents are captured from the real Aras environment; no gated UI action or compliance claim is enabled by this plan alone.

| Gate | Status | Evidence |
|------|--------|----------|
| No guessed Aras ItemTypes, properties, lifecycles, or AML | PASS | Part lifecycle requires verified evidence before UI enablement (GATE-A). No invented state names. |
| Architecture boundaries respected | PASS | `IRecoveryCopyService` in Workspace. `ICadReleaseEligibility` in Core. Canonical `IArasCadClient` implementations not duplicated. |
| No dependency cycles | PASS | Same extension axis. |
| Backend-neutral PDM language | PASS | No Aras-specific types in contracts. |
| No client-side atomicity simulation | PASS | Policy is advisory; authority provides atomic operation. GATE-B gates UI enablement on verified transactional guarantees. |
| No secrets in code/logs | PASS | No secret handling. |
| Recovery in Workspace, not Aras | PASS | `CancelCheckoutRequest` has only `CadId` + `LockToken` (unchanged). |

**No violations — Complexity Tracking section is empty.**

## Project Structure

### Documentation (this feature)

```text
specs/003-controlled-cad-design-release/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Created by /speckit.tasks
```

### Source Code (repository root) — aligned with current repo structure

```text
src/IdeaCadConnector.Core/
├── Cad/
│   ├── ICadLifecyclePolicy.cs                  # NEW: interface — CanCheckout, CanSubmitForReview, CanApprove, CanRequestRework, CanWithdraw, IsReleased
│   └── CadLifecyclePolicy.cs                   # EXTEND: implement ICadLifecyclePolicy; add submit/approve/rework/withdraw helpers (CAD-only)
├── Library/
│   ├── IPartLifecyclePolicy.cs                 # NEW: interface — CanRelease, IsReleased (state names SEPARATE from CAD per ADR-0009)
│   └── PartLifecyclePolicy.cs                  # EXTEND: implement IPartLifecyclePolicy; add MVP release eligibility using VERIFIED Part state names (GATE-A)
├── Contracts/
│   ├── IPdmRepositoryClient.cs                 # UNCHANGED: ReviseCadAsync already defined
│   ├── IRevisionService.cs                     # UNCHANGED
│   ├── IArasCadClient.cs                       # UNCHANGED: ExecuteCadBusinessActionAsync already covers submit/approve/rework/withdraw
│   └── ICadReleaseEligibility.cs               # NEW: policy interface — checks Part + CAD eligibility separately
├── Dto/
│   ├── CancelCheckoutRequest.cs                # UNCHANGED: CadId + LockToken only
│   ├── CadReleaseEligibilitySnapshot.cs        # NEW: CadId, PartId, CadState, PartState (backend-neutral snapshot)
│   └── CadReleaseEligibilityResult.cs          # NEW: IsEligible + BlockingReasons
├── Policies/
│   └── MvpReleaseEligibility.cs                # NEW: ICadReleaseEligibility — checks both policies before approve

src/IdeaCadConnector.Aras/
├── HttpArasCadClient.cs                        # EXTEND: add Withdraw case; existing transport interface unchanged
├── ArasCadClient.cs                            # EXTEND: add Withdraw case; existing transport interface unchanged
├── HttpPdmRepositoryClient.cs                  # UNCHANGED: ReviseCadAsync already implemented
├── ArasCadLifecycleAdapter.cs                  # NEW: resolve CAD lifecycle semantic roles from verified state names
└── ArasPartLifecycleAdapter.cs                 # NEW: resolve Part lifecycle semantic roles (GATE-A required)

src/IdeaCadConnector.Workspace/
├── Recovery/
│   ├── IRecoveryCopyService.cs                 # NEW: create/verify/retention/cleanup
│   └── FileSystemRecoveryService.cs            # NEW: .idea-pdm/recovery/<cad-id>/<timestamp>-<file>
├── Models/
│   ├── RecoveryCopyRecord.cs                   # NEW: source hash, backup hash, timestamp, retention
│   └── RecoveryCopyResult.cs                   # NEW: Succeeded, BackupPath, ErrorMessage, SourceHash, BackupHash

src/IdeaCadConnector.Desktop/
├── MainViewModel.cs                            # EXTEND: add submit/approve/rework/withdraw/new-revision/cancel-checkout orchestration; check-in via shared CheckoutService path
├── PdmProjectsViewModel.cs                     # EXTEND: refresh action availability based on Part+CAD eligibility; check-in via shared CheckoutService path (same as MainViewModel)
├── CheckoutService.cs                          # EXTEND: UploadAndCheckinAsync accepts reason parameter; sets CadCheckinRequest.Comment = reason
├── GuidanceRevisionService.cs                  # UNCHANGED: wraps ReviseCadAsync
├── Dialogs/
│   ├── CheckinReasonDialog.xaml/.cs            # NEW: required reason TextBox, OK/Cancel. Cancel = no side effect. Null/empty/whitespace rejected pre-upload.
│   ├── SubmitForReviewDialog.xaml/.cs          # NEW: submit UI
│   ├── ReviewDecisionDialog.xaml/.cs           # NEW: approve/request-rework UI
│   └── WithdrawConfirmDialog.xaml/.cs          # NEW: withdraw confirmation (if GATE-W passes)

src/IdeaCadConnector.Ui/
├── Resources/
│   └── Strings.resx                            # EXTEND: submit/review/withdraw/backup/evidence-gate messages

tests/IdeaCadConnector.Tests/
├── CadLifecyclePolicyTests.cs                  # EXTEND: add CanWithdraw tests
├── PartLifecyclePolicyTests.cs                 # NEW (requires Aras evidence fixture for GATE-A)
├── MvpReleaseEligibilityTests.cs               # NEW
├── FileSystemRecoveryServiceTests.cs           # NEW
├── CadBusinessActionKindTests.cs               # NEW: enum completeness
├── WorkflowActionMapperTests.cs                # EXTEND: withdraw mapping
├── ArasCadClientTests.cs                       # EXTEND: withdraw case
├── HttpArasCadClientTests.cs                   # EXTEND: withdraw case
├── MainViewModelTests.cs                       # EXTEND: cancel-checkout recovery, submit/approve/rework/withdraw
└── PdmProjectsViewModelTests.cs                # EXTEND: action state refresh

**Note**: US5 (Admin Configuration) and FR-009 (Reviewer Reassignment) are deferred to post-MVP.
MVP configuration is handled through the Aras Innovator UI directly.
```

**Key design rule**: Do NOT duplicate workflow logic. `IArasCadClient.ExecuteCadBusinessActionAsync` is the canonical path for submit/approve/rework/withdraw — no new transport methods. The new `ICadReleaseEligibility` is an *advisory* gate that runs *before* calling `ExecuteCadBusinessActionAsync(Approve)`. It evaluates a `CadReleaseEligibilitySnapshot` only and never fetches from Aras. `IRecoveryCopyService` belongs in Workspace; `CancelCheckoutRequest` stays `CadId`+`LockToken` only. Withdraw extends the existing enum, mapper, and both adapter implementations — no new transport interface.

**Check-in orchestration**: Both `MainViewModel` and `PdmProjectsViewModel` share a single check-in orchestration path through `CheckoutService.UploadAndCheckinAsync`. The ViewModels open `CheckinReasonDialog`, pass the validated reason to `CheckoutService`, and `CheckoutService` alone sets `CadCheckinRequest.Comment = reason`. This prevents duplicate workflow logic across the two entry points. UI reason collection (dialog) is separated from orchestration (service) which is separated from transport (client).

## Complexity Tracking

*No Constitution Check violations — this section is intentionally empty.*
