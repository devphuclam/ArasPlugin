# GATE-W-owner: Withdrawal Owner Authorization

**Task**: T005e

**Requirement**: Verify withdrawal-owner authorization if a deployed Withdraw operation exists. If no Withdraw operation exists, record the limitation and keep the action disabled.

## Source Analysis

The current `CadOperationContext` contains a `LockOwnerName` field that identifies the checkout lock owner. The `Withdraw` code path in both ViewModels uses explicit fail-closed implementations that do NOT reference `LockOwnerName`:

- `PdmProjectsViewModel.IsWithdrawOwnerAvailable()`: hard-coded `return false` with comment "LockOwnerName identifies the checkout lock owner, not the submitter/withdrawal owner. The current authority contract has no authoritative submitter field, so Withdraw must fail closed."
- `MainViewModel.CanExecuteAction` for `Withdraw`: hard-coded `return false` with comment "No authoritative submitter/owner field is present in the current CAD operation contract."

No `SubmissionOwnerName`, `SubmittedById`, or equivalent authoritative submitter field exists in any DTO or transport contract in the current codebase.

## Live Read-Only Observation (2026-07-20)

The `CadOperationContext` and related authority operation contracts were inspected. Only `LockOwnerName` (checkout ownership) is available. No submission-owner or withdrawal-authorization field exists. The `idea_` method list contained no withdraw mechanism (see GATE-W, T004), so the question of owner authorization for withdrawal is currently moot — there is no operation to authorize.

## Verification Required

- [x] Confirm whether a Withdraw operation exists
- [ ] Identify the authority operation context field that identifies the submission owner (not applicable while no Withdraw operation exists)
- [ ] Verify the authority enforces owner-only withdrawal (not applicable while no Withdraw operation exists)
- [x] Document the limitation when no Withdraw operation exists

## Result

- Submission owner field exposed in operation context? **Not applicable while no Withdraw operation exists; no such field exists in the current contract**
- `LockOwnerName` authoritative for withdrawal? **NO — checkout ownership only**
- Mechanism (if any): **None identified**
- Evidence date: **2026-07-20**
- Environment: **IDEA live Aras environment**
- Verified by: **Read-only inspection; product owner confirmed no withdraw mechanism exists (T004)**

**Blocks**: Withdraw UI enablement and full FR-006 compliance. `LockOwnerName` is checkout ownership only and MUST NOT be treated as review ownership. Both ViewModels enforce this by returning `false` unconditionally for Withdraw. This is a closed availability limitation from T004, not permission to infer or fabricate a submission owner.
