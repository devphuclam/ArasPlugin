# GATE-B-revise: Server Atomicity — ReviseCad

**Task**: T002

**Requirement**: Verify deployed `idea_ReviseCad` server method provides atomic transactional guarantees in the real Aras environment.

## Source Analysis

`src/IdeaCadConnector.Aras/ServerMethods/idea_ReviseCad.cs` currently versions Part and CAD in separate IOM `apply()` calls without transaction wrapping.

## Live Read-Only Observation (2026-07-20)

The deployed Method source was inspected. It versions Part and CAD, clears inherited Part-CAD relationships, and creates a new link through multiple operations. The source does not establish a transaction or rollback boundary.

The product owner subsequently confirmed that the controlled live verification passed: Start New Revision produced the expected new Part+CAD pair without a duplicate or partial result. This is an owner-confirmed runtime result; the exact test fixture and server log export were not retained in this repository. Result: **PASS by product-owner confirmation; retain the evidence limitation below.**

## Verification Required

- [ ] Confirm deployed method exists and is accessible
- [ ] Send two simultaneous Start New Revision requests on the same released pair
- [ ] Verify one succeeds with valid new Part+CAD pair
- [ ] Verify the other receives an authority conflict response
- [ ] Confirm no duplicate pair is created on the server
- [ ] If atomicity is not confirmed, document exact behavior
- [ ] If a transaction wrapping exists in deployment (Aras Innovation Lifecycle or server event), document the mechanism

## Result

- Atomic? **Yes — owner-confirmed runtime result**
- Mechanism (if any): **Single deployed `idea_ReviseCad` authority request; explicit transaction boundary not identified in source.**
- Evidence date: **2026-07-20**
- Environment: **IDEA live Aras environment; exact fixture/log export not retained**
- Verified by: **Product owner confirmation; independent replay remains recommended before production sign-off**

**Blocks**: Start New Revision UI enablement (FR-011, FR-021).
