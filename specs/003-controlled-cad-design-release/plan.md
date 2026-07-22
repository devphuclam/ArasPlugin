# Implementation Plan: Controlled CAD Design Release

**Branch**: `003-controlled-cad-design-release` | **Date**: 2026-07-18 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/003-controlled-cad-design-release/spec.md`

## Summary

Implement the controlled CAD design release workflow for the IDEA PDM desktop application. The workflow extends existing infrastructure: `IArasCadClient` (both `HttpArasCadClient` and `ArasCadClient`) already handles submit/approve/rework/withdraw via `ExecuteCadBusinessActionAsync`. The `HttpPdmRepositoryClient.ReviseCadAsync` transport method exists and calls the `idea_ReviseCad` server method, but actual Start New Revision behavior remains blocked by **GATE-B-revise** until deployed server atomicity, response shape, Part+CAD creation, linking, and conflict behavior are verified. Client method existence does not prove authority operation correctness. New additions: `ICadReleaseEligibility` (Part + CAD eligibility check via snapshot), `IRecoveryCopyService` in Workspace (safe cancel-checkout backup), Withdraw capability, Desktop orchestration connecting them, UI dialogs, and Part lifecycle verification. Nine evidence gates block UI enablement or compliance claims until Aras server-behavior is verified, including reviewer assignment and withdrawal ownership. US5 (admin configuration) and FR-009 (reviewer reassignment) deferred to post-MVP.

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

| # | Gate | Description | Status |
|---|------|-------------|--------|
| 1 | **GATE-A** (Part lifecycle) | Product owner confirmed Part follows `Khoi tao` → `Thiet ke chi tiet` → `In Review` → `Released`. Capture remaining verified Part transition permissions, release immutability, and semantic roles before treating `PartLifecyclePolicy` as fully evidenced. | T001 ✅ |
| 2 | **GATE-B-revise** (ReviseCad atomicity) | Verify deployed `idea_ReviseCad` provides atomic transactional guarantees. Source versions Part and CAD in separate IOM `apply()`. If NOT atomic, Start New Revision UI stays disabled. | T002 ✅ (evidence note: fixture export not retained) |
| 3 | **GATE-B-approve** (ApproveCadReview atomicity) | Verify deployed `idea_ApproveCadReview` provides atomic Part+CAD release. Checked-in source is CAD-only. Deployed method found via CAD `onAfterPromote` → `Sync_Part_From_CAD`. | T003 ✅ |
| 4 | **GATE-B-checkin** (CommitCadCheckin atomicity) | Verify atomic update+unlock, ChangeSet, audit, reason persistence. Source is non-atomic. | T005b ❌ |
| 5 | **GATE-W** (Withdraw) | Verify withdraw method or lifecycle transition on Aras server. | T004 ✅ (no method found; Withdraw stays disabled) |
| 6 | **GATE-RW** (Rework side effects) | Verify deployed `idea_RequestCadRework` result and audit behavior. MVP policy accepted: coordinated state-only, no new revision. | T005c ✅ (policy accepted; audit evidence open) |
| 7 | **GATE-RS** (Reviewer assignment) | Verify Aras Assign/workflow assignment record and replaceable read seam. No client-selected reviewer. | T005d ✅ |
| 8 | **GATE-W-owner** (Withdrawal owner) | Verify submission-owner field/permission. `LockOwnerName` is checkout ownership only. | T005e ✅ (no Withdraw exists; moot) |
| 9 | **GATE-N** (Audit trail) | Verify audit coverage for all 7 transitions per FR-017. | T005 ❌ |

**GATE-RW policy decision (2026-07-20)**: Product owner accepted coordinated state-only rework: CAD and linked Part return to `Thiet ke chi tiet`, no new engineering revision/version is created, and duplicate Sync is a no-op. GATE-RW remains for deployed result/audit evidence, not for a pending business-policy choice.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.* ✓ Re-checked — the artifact/design checks pass. Runtime evidence gates remain **PENDING** until the current nine-gate evidence inventory is captured or explicitly closed as a limitation; no gated UI action or compliance claim is enabled by this plan alone.

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

**Gate inventory correction (2026-07-20)**: The runtime evidence inventory is nine gates. In addition to the seven original authority gates, GATE-RS covers reviewer assignment and GATE-W-owner covers submission-owner authorization for withdrawal.

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
│   ├── SubmitForReviewDialog.xaml/.cs          # EXISTS + GATED: dialog created and wired into WpfWorkflowActionDialogService; runtime submission GATE-RS-blocked.
│   ├── ReviewDecisionDialog.xaml/.cs           # NEW: approve/request-rework UI
│   └── WithdrawConfirmDialog.xaml/.cs          # EXISTS + GATED: dialog created and wired; Withdraw action GATE-W-blocked (no withdraw op in current Aras; implemented-but-disabled, not enabled).

src/IdeaCadConnector.Ui/
├── Resources/
│   └── Strings.resx                            # REFERENCE ONLY: the active localization system uses TranslationResources (in-memory dictionary) consumed via TranslationResources.GetString / LocalizationSource; Strings.resx is retained for reference and is not the runtime source.

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

## Scope Boundaries

The following dependencies are consumed by Feature 003 infrastructure but are **not 003-specific**. They are documented here so reviewers understand they belong to other features:

### PartLibraryStateProvider (`src/IdeaCadConnector.Desktop/Services/PartLibraryStateProvider.cs`)

`PartLibraryStateProvider` implements `IPartStateProvider` by obtaining the current Part lifecycle state from the Part library client. It was split from an earlier `PartLifecycleProvider` refactoring and is consumed by `PdmProjectsViewModel` for the release-eligibility snapshot. The provider itself belongs to **Future/PartLibrary** scope (US5 and Part Library work). It is retained in the 003 codebase because:
- It compiles and passes all tests.
- Part library client infrastructure (`IPartLibraryClient`, `HttpPartLibraryClient`, `SharedPartLibraryClient`) already exists in the solution.
- Removing it would not change 003 behavior but would require a separate refactoring ticket.
- `IPartLibraryClient` returns `null` when the client or authoritative state is unavailable, so callers remain fail-closed (no false positive release eligibility).

### IWorkflowActionDialogService (`src/IdeaCadConnector.Desktop/Workflow/`)

`IWorkflowActionDialogService` and its WPF implementation `WpfWorkflowActionDialogService` provide cross-cutting dialog infrastructure (`ShowGatePending`, `ShowReviewerUnavailable`, `ConfirmSimple`) in addition to the 003-specific review dialogs (`ShowSubmitForReview`, `ShowReviewDecision`, `ShowWithdrawConfirm`). The interface is a general infrastructure concern, not a 003-specific contract. It is retained because:
- Both `MainViewModel` and `PdmProjectsViewModel` inject it as an optional dependency (defaults to `WpfWorkflowActionDialogService`).
- The generic methods (`ShowGatePending`, `ConfirmSimple`) are used by non-003 code paths (e.g., validation-result display).
- Removing it would require extracting the 003-specific dialog methods into a new interface, which is out of scope for this feature.

## Complexity Tracking

*No Constitution Check violations — this section is intentionally empty.*
