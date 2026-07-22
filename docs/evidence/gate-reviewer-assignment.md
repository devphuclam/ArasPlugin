# GATE-RS: Reviewer Assignment

**Task**: T005d

**Requirement**: Verify how the deployed authority assigns and records the reviewer for a submitted CAD revision. Engineer-selected reviewer input is not required for MVP.

## Source Analysis

`src/IdeaCadConnector.Aras/ServerMethods/idea_SubmitCadForReview.cs` and the corresponding transport seam `IArasCadClient.ExecuteCadBusinessActionAsync(SubmitForReview)` accept only:
- `cad_id` (the CAD revision identifier)
- `comment` (change description)

No reviewer identifier, reviewer list, or assignment field exists in the current submit request DTO or server method signature.

## Live Read-Only Observation and Product Decision (2026-07-20)

The inspected `idea_` method list contained a submit method whose signature accepts only the CAD identifier and an optional comment. The product owner confirmed that Aras Assign/workflow assignment selects the reviewer; therefore no engineer-selected reviewer field is expected in the submit request.

The client `SubmitForReviewDialog` contains a reviewer ComboBox and a `SetAvailableReviewers` method, but both are hard-disabled (`IsEnabled="False"`) because the authority contract does not support reviewer selection. The reviewer identity is NOT sent to the authority.

## Verification Required

- [x] Confirm reviewer assignment is authority-managed by Aras Assign/workflow
- [ ] Identify the read contract that exposes the authoritative active reviewer assignment to the client
- [ ] Verify the authority validates the reviewer's eligibility (role, permissions, active assignments)
- [ ] Verify the authority records the assignment on the submission/workflow record

## Result

- Reviewer assignment supported? **Authority-managed assignment confirmed; client read contract NOT PROVEN**
- Mechanism: **Aras Assign/workflow assignment**
- Evidence date: **2026-07-20**
- Environment: **IDEA live Aras environment**
- Verified by: **Product owner confirmation plus source/read-only inspection**

**Blocks**: Runtime reviewer-dependent action enablement and full FR-005 compliance until the active assignment read contract is verified. The client ComboBox remains informational/disabled. The client MUST NOT invent an Aras property, encode a reviewer identity into the `comment` field, or send a reviewer.
