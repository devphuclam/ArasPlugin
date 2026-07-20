# GATE-B-approve: Server Atomicity — ApproveCadReview

**Task**: T003

**Requirement**: Verify deployed `idea_ApproveCadReview` server method provides atomic Part+CAD release.

## Source Analysis

`src/IdeaCadConnector.Aras/ServerMethods/idea_ApproveCadReview.cs` is **CAD-only**: it loads CAD, validates `In Review`, unlocks if needed, and promotes CAD to `Released`. It does NOT load Part, check Part state, promote Part, or provide any atomic Part+CAD guarantee.

## Live Read-Only Observation (2026-07-20)

The deployed Method record and Method source were inspected. `idea_ApproveCadReview` requires CAD `In Review` and promotes CAD to `Released`. The active CAD ItemType also has a `Server Event` for `onAfterPromote` linked to `Sync_Part_From_CAD`, so the approval path indirectly coordinates the linked Part. Product owner confirmation states that this live path satisfies the required atomicity behavior. Result: **PASS by product owner confirmation; formal fixture identifiers and execution logs are not recorded in this note.**

## Verification Required

- [ ] Confirm deployed method exists and is accessible
- [ ] Test: submit a CAD+Part pair for review, approve it
- [ ] Verify Part was also promoted to Released (or remains unchanged)
- [ ] If Part was NOT promoted, check whether an Aras workflow or server event achieves coordinated release
- [ ] Verify atomicity: if approval fails mid-way, does any partial state remain?
- [ ] If deployed method is NOT atomic and does NOT coordinate with Part, document the exact behavior

## Result

- Atomic Part+CAD? **Yes — owner-confirmed runtime result. Coordination is achieved through the CAD `onAfterPromote` Server Event calling `Sync_Part_From_CAD`, not through a single atomic server method.**
- Mechanism (if any): **CAD `idea_ApproveCadReview` promotes CAD to `Released`; the CAD ItemType's `onAfterPromote` Server Event invokes `Sync_Part_From_CAD`, which loads the linked Part and coordinates its state.**
- Evidence date: **2026-07-20**
- Environment: **IDEA live Aras environment; exact fixture/log export not retained**
- Verified by: **Product owner confirmation; independent replay remains recommended before production sign-off**

**Blocks**: Approve UI enablement (FR-007, FR-020). Client MUST NOT simulate atomicity.
