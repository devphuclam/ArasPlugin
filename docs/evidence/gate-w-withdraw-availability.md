# GATE-W: Withdraw Availability

**Task**: T004

**Requirement**: Verify withdraw capability on the Aras server — a lifecycle transition or server method that returns CAD from `In Review` to `Thiet ke chi tiet` without recording a review decision.

## Verification Required

- [ ] Identify the server method or lifecycle transition name for withdraw
- [ ] Test: submit a CAD for review, then withdraw it
- [ ] Verify CAD returns to `Thiet ke chi tiet`
- [ ] Verify no review decision record was created (only withdrawal event)
- [ ] If no mechanism exists, document the limitation

## Live Read-Only Observation (2026-07-20)

The inspected `idea_` Method list contained submit, approve, rework, revise, and check-in methods but no verified `idea_` withdraw method. A valid lifecycle transition was not established through read-only inspection. The product owner confirmed that no withdraw mechanism currently exists. Result: **NO MECHANISM AVAILABLE; Withdraw remains disabled.**

## Result

- Mechanism exists? **No**
- Server method or transition name: **None identified**
- Evidence date: **2026-07-20**
- Environment: **IDEA live Aras environment**
- Verified by: **Read-only inspection and product owner confirmation**

## Product Decision

Withdraw is not part of the currently enabled workflow. The client may retain the enum and transport seam for future extension, but the action must remain unavailable until an Aras authority mechanism is deliberately added and verified. This is a closed limitation, not an implementation defect.

**Blocks**: Withdraw UI enablement.
