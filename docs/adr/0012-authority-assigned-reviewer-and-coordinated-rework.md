# ADR-0012: Authority-Assigned Reviewer and Coordinated State-Only Rework

**Status**: Accepted  
**Date**: 2026-07-20  
**Decision owners**: IDEA Technology product owner, Codex domain review

## Context

The deployed Aras submit contract accepts the CAD identifier and comment, but
does not accept a reviewer identifier from the client. The product owner also
confirmed that Aras assigns the reviewer through its workflow. The client must
therefore consume an authoritative assignment instead of asking an engineer to
choose or invent a reviewer identity.

The deployed rework path promotes the CAD revision back to `Thiet ke chi tiet`
and invokes `Sync_Part_From_CAD`. The product owner confirmed the business
behavior: the linked Part also returns to `Thiet ke chi tiet`, does not create a
new Part version, and duplicate synchronization is a no-op.

## Decision

1. Reviewer assignment is authority-managed. Aras Assign/workflow assignment
   is the source of truth. The client does not send a reviewer identity in the
   submit request, hard-code a person, or treat checkout ownership as review
   ownership.
2. Reviewer decision actions require an authoritative active assignment for
   the current review task. If that assignment cannot be verified, the client
   fails closed and keeps the action disabled.
3. MVP Request Rework is a coordinated, state-only operation: CAD and its
   linked Part return to `Thiet ke chi tiet`; neither operation creates a new
   engineering revision/version. The existing Aras synchronization path is
   accepted for this behavior, subject to its authority response and audit
   handling.
4. Start New Revision remains the only MVP operation that creates a new
   Part-CAD Revision Pair. Rework must not be modeled as revision creation.

## Consequences

- The reviewer selector may remain visible as explanatory UI, but it is not an
  editable input and is not part of the transport contract.
- The application needs a replaceable authority-assignment read/gate seam; it
  must not infer assignment from a username or checkout lock.
- Request Rework is no longer blocked by uncertainty about Part side effects,
  but full audit and runtime authority evidence remain separate gates.
- Future backends may implement assignment differently behind the same domain
  contract. The domain meaning remains “authority-assigned active reviewer.”

## Evidence

- `docs/evidence/gate-reviewer-assignment.md`
- `docs/evidence/gate-rw-rework-side-effects.md`
- `docs/domain/idea-pdm-domain-model.md`

