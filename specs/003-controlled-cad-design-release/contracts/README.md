# Contracts: Controlled CAD Design Release

## Overview

This feature introduces or extends interface contracts across three project boundaries. All domain contracts use backend-neutral PDM language. Aras-specific mappings stay in `IdeaCadConnector.Aras`. Recovery belongs in `IdeaCadConnector.Workspace`.

## Canonical Workflow Seam (no new transport methods)

The existing `IArasCadClient.ExecuteCadBusinessActionAsync` is the canonical path for submit, approve, request-rework, and withdraw. Both `HttpArasCadClient` and `ArasCadClient` already implement the seam for the existing submit/approve/request-rework actions; Withdraw is an extension that remains gated by GATE-W and must map to a verified Aras lifecycle transition or server method. `CadBusinessActionKind.Withdraw` is added to the enum and both clients gain a Withdraw case. No new transport interface method is added — no `ReleaseCadPartPairAsync`, `SubmitForReviewAsync`, or `DecideReviewAsync`. This feature adds an advisory eligibility check that runs *before* `ExecuteCadBusinessActionAsync(Approve)`.

Check-in uses the existing `IArasCadClient.CheckinAsync(CadCheckinRequest)` contract. The required written reason is carried by the existing `CadCheckinRequest.Comment` property. Reason validation (reject null/empty/whitespace) and file integrity validation are the caller's responsibility — they happen in the Desktop orchestration layer before any authority call. The transport contract (`CadCheckinRequest`) already supports the comment; no DTO or interface change is needed.

```
# Approve flow (coordinated Part+CAD release):
CadReleaseEligibilitySnapshot snapshot = new(cadId, partId, cadState, partState)
ICadReleaseEligibility.CheckAsync(snapshot)
    → if eligible: ExecuteCadBusinessActionAsync(Approve)
    → server: idea_ApproveCadReview (must provide atomic Part+CAD release; see GATE-B-approve)

# Withdraw flow (CAD-only, no Part coordination):
ExecuteCadBusinessActionAsync(Withdraw)
    → server: [lifecycle transition or server method; see GATE-W]
```

## Server-method evidence boundary

The checked-in Server Method source is evidence for design review, not proof of the deployed Aras behavior. The current source has these known gaps that remain blocked by evidence gates:

- `idea_ApproveCadReview.cs` loads and promotes CAD only; it does not load, validate, or promote the linked Part. Coordinated release must be demonstrated by the deployed authority before `Approve` is enabled (GATE-B-approve).
- `idea_CommitCadCheckin.cs` updates `native_file` and unlocks CAD through separate `apply()` calls. Its `comment` input is read but is not persisted to a ChangeSet or audit record in the checked-in source. Atomicity, ChangeSet, audit, and reason persistence must be verified in the deployed authority before full check-in compliance is claimed (GATE-B-checkin).
- `idea_RequestCadRework.cs` invokes `Sync_Part_From_CAD` after promoting CAD. The deployed side effect on the Part lifecycle/version must be verified before `Request Rework` is enabled (GATE-RW).

These observations must not be converted into assumptions about the live Aras environment. The corresponding evidence documents under `docs/evidence/` are the authority for enabling the actions.

## Core Contracts (Policy + DTOs)

### ICadReleaseEligibility (NEW — Core)

```csharp
/// <summary>
/// Evaluates whether a Part Revision and its primary linked CAD Revision
/// are eligible for coordinated release. Part and CAD eligibility are
/// checked separately per their own lifecycle maps.
///
/// This is an ADVISORY check that operates on a supplied snapshot of
/// current states. It NEVER fetches data from Aras. The orchestration
/// layer (Desktop) reads current states and populates the snapshot.
/// The Aras server method makes the final decision.
/// </summary>
public interface ICadReleaseEligibility
{
    Task<CadReleaseEligibilityResult> CheckAsync(
        CadReleaseEligibilitySnapshot snapshot, CancellationToken ct);
}

/// <summary>
/// Backend-neutral snapshot of current revision states for eligibility evaluation.
/// Populated by the orchestration layer before calling ICadReleaseEligibility.
/// Does NOT contain Aras-specific types.
/// </summary>
public sealed class CadReleaseEligibilitySnapshot
{
    public string CadId { get; init; }
    public string PartId { get; init; }
    public string CadState { get; init; }
    public string PartState { get; init; }
}

public sealed class CadReleaseEligibilityResult
{
    public bool IsEligible { get; init; }
    public IReadOnlyList<string> BlockingReasons { get; init; }
}
```

### ICadLifecyclePolicy (NEW — Core)

```csharp
/// <summary>
/// CAD lifecycle eligibility: answers whether the given CAD lifecycle state
/// permits checkout, submit-for-review, withdraw, approve, request-rework, etc.
/// Uses verified CAD lifecycle state names (Aras evidence).
/// Extracted pattern from the existing static CadLifecyclePolicy.
/// </summary>
public interface ICadLifecyclePolicy
{
    bool CanCheckout(string state);
    bool CanSubmitForReview(string state);
    bool CanApprove(string state);
    bool CanRequestRework(string state);
    bool CanWithdraw(string state);
    bool IsReleased(string state);
}
```

### IPartLifecyclePolicy (NEW — Core)

```csharp
/// <summary>
/// Part lifecycle eligibility for MVP coordinated release.
/// State names are SEPARATE from CAD per ADR-0009 and require
/// verified Aras environment evidence (GATE-A).
/// </summary>
public interface IPartLifecyclePolicy
{
    bool CanRelease(string state);
    bool IsReleased(string state);
}
```

### Existing Contracts (unchanged)

```csharp
// IArasCadClient — the canonical transport seam for submit/approve/rework/withdraw (no new interface methods):
public interface IArasCadClient : IDisposable
{
    Task<CadOperationContext> ExecuteCadBusinessActionAsync(
        ExecuteCadBusinessActionRequest request, CancellationToken ct);
    // ... other operations unchanged (checkout, checkin, cancel-checkout, etc.)
}

// CancelCheckoutRequest — only CadId + LockToken (no backup fields):
public sealed class CancelCheckoutRequest
{
    public string CadId { get; set; }
    public string LockToken { get; set; }
}

// IPdmRepositoryClient — ReviseCadAsync unchanged:
public interface IPdmRepositoryClient : IDisposable
{
    Task<PdmReviseResult> ReviseCadAsync(PdmReviseRequest request, CancellationToken ct);
    // ... other existing operations unchanged
}

// IRevisionService — unchanged
public interface IRevisionService
{
    Task<PdmRevisePreconditionResult> CheckPreconditionsAsync(...);
    Task<PdmReviseResult> ReviseAsync(PdmReviseRequest request, CancellationToken ct);
}
```

## Workspace Contracts (Recovery)

### IRecoveryCopyService (NEW — Workspace)

```csharp
/// <summary>
/// Creates, verifies, retains, and cleans recovery copies.
/// Workspace owns these operations per the domain model and solution architecture.
/// Desktop orchestrates the user confirmation flow; the Aras adapter only unlocks
/// remotely via CancelCheckoutRequest (CadId + LockToken only).
/// </summary>
public interface IRecoveryCopyService
{
    /// <summary>Creates a verified recovery copy of the given file. Returns RecoveryCopyResult.</summary>
    Task<RecoveryCopyResult> CreateRecoveryCopyAsync(string cadId, string workingFilePath, CancellationToken ct);

    /// <summary>Gets the recovery copy directory path for display to the user.</summary>
    string GetRecoveryDirectory(string cadId);

    /// <summary>Cleans recovery copies past their retention period.</summary>
    Task CleanExpiredCopiesAsync(CancellationToken ct);
}

/// <summary>
/// Result of a recovery copy creation attempt.
/// On success: Succeeded=true, BackupPath, SourceHash, BackupHash are set.
/// On failure: Succeeded=false, ErrorMessage describes the failure.
/// Backup is always hash-verified before Succeeded is set to true.
/// If creation or verification fails, the source file is untouched.
/// </summary>
public sealed class RecoveryCopyResult
{
    public bool Succeeded { get; init; }
    public string BackupPath { get; init; }
    public string ErrorMessage { get; init; }
    public string SourceHash { get; init; }   // SHA256 of source before copy
    public string BackupHash { get; init; }   // SHA256 of backup after write (verified)
}
```

## Contract Dependency Flow

```
[Desktop ViewModel]
    │
    ├─ IRecoveryCopyService.CreateRecoveryCopyAsync  ──→ (Workspace: backup before cancel)
    ├─ IArasCadClient.CancelCheckoutAsync             ──→ (Aras: CadId+LockToken only)
    ├─ IRevisionService.ReviseAsync                   ──→ (HttpPdmRepositoryClient.ReviseCadAsync)
    ├─ ICadReleaseEligibility.CheckAsync(snapshot)    ──→ (Core: advisory, reads snapshot only)
    │     snapshot populated by Desktop from current selection
    ├─ IArasCadClient.ExecuteCadBusinessActionAsync   ──→ (Aras: submit/approve/rework/withdraw — canonical transport)
    │
    ▼
 Policy (Core): ICadLifecyclePolicy + IPartLifecyclePolicy (separate per ADR-0009)
```

## Referenced Source Files

| File | Project |
|------|---------|
| `IArasCadClient.cs` | `src/IdeaCadConnector.Core/Contracts/` |
| `IPdmRepositoryClient.cs` | `src/IdeaCadConnector.Core/Contracts/` |
| `IRevisionService.cs` | `src/IdeaCadConnector.Core/Contracts/` |
| `CadLifecyclePolicy.cs` | `src/IdeaCadConnector.Core/Cad/` |
| `CadBusinessActionKind.cs` | `src/IdeaCadConnector.Core/Dto/` |
| `CancelCheckoutRequest.cs` | `src/IdeaCadConnector.Core/Dto/` |
| `RecoveryCopyResult.cs` | `src/IdeaCadConnector.Workspace/Models/` |
| `WorkflowActionMapper.cs` | `src/IdeaCadConnector.Core/Cad/` |
| `HttpArasCadClient.cs` | `src/IdeaCadConnector.Aras/` |
| `ArasCadClient.cs` | `src/IdeaCadConnector.Aras/` |
| `HttpPdmRepositoryClient.cs` | `src/IdeaCadConnector.Aras/` |
| `idea_ReviseCad.cs` | `src/IdeaCadConnector.Aras/ServerMethods/` |
| `GuidanceRevisionService.cs` | `src/IdeaCadConnector.Desktop/` |
| `MainViewModel.cs` | `src/IdeaCadConnector.Desktop/` |
| `PdmProjectsViewModel.cs` | `src/IdeaCadConnector.Desktop/` |
