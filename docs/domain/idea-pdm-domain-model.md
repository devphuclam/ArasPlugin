# IDEA PDM Domain Model

## Purpose

This document defines the backend-neutral business model for IDEA Technology's design-engineering PDM product. Aras Innovator is the current PDM Authority and evidence source; it is not the definition of the domain.

## Identity and Revision Model

### Part Identity

A Part Identity is the stable engineering identity represented by a Part Number. It survives across revisions and may represent a designed or purchased item.

### Part Revision

A Part Revision is one controlled revision of a Part Identity. It owns the product structure valid for that revision and may link to CAD and Document revisions.

### CAD Identity and CAD Revision

A CAD Identity is the stable identity of a controlled design record. A CAD Revision is one revision of that identity and controls its native CAD content, lifecycle, checkout status, and audit history.

A file version created by check-in changes the working content of a CAD Revision; it does not create a new engineering revision.

### Document Identity and Document Revision

A Document Identity is the stable identity of controlled non-native-CAD engineering content. A Document Revision is one controlled revision of that identity.

Document lifecycle, release, and revision propagation are separate policies. They are not copied from Part or CAD behavior without an approved feature and verified authority mapping.

### Part-CAD Revision Pair

The MVP release aggregate is one Part Revision linked to one primary CAD Revision. The two revisions retain separate lifecycle identities.

The pair is coordinated only at explicit policy-defined operations:

- **Start New Revision** creates the new Part Revision and linked CAD Revision together.
- **Release Approval** releases the eligible Part Revision and linked CAD Revision together.

Checkout, file versioning, and native-content editing affect the CAD Revision. They do not silently mutate the linked Part Revision.

## Product Structure

### BOM Snapshot

A BOM Snapshot is the parent-child product structure owned by a specific Part Revision. Releasing the parent freezes that snapshot.

### BOM Line

A BOM Line selects a child Part or child revision policy, quantity, and ordering information within its parent snapshot. Repeated occurrences may share the same child identity while contributing to quantity.

The MVP domain rules are:

- A Released BOM Snapshot is immutable.
- Changing a released parent structure requires a new working Part Revision.
- Binary CAD content and BOM structure are never automatically merged.
- Child selection policy must be explicit, such as pinned revision or latest released; it must not silently drift.
- Where-used queries traverse BOM Lines and other approved usage relationships without changing them.

BOM editing and revision-impact behavior require their own feature specification; this model establishes ownership and invariants only.

## Workspace and Synchronization

### Workspace

A Workspace is an engineer's controlled local working copy. It contains selected engineering files, metadata, references, and local operation history.

### Workspace Baseline

The Workspace Baseline identifies the authoritative remote configuration from which the Workspace was cloned or last synchronized. Local changes are meaningful only relative to this baseline.

### Local Change Status

Local Change Status describes content difference from the baseline: New, Modified, Deleted, or Unchanged. It is independent from checkout ownership, lifecycle, validation, and synchronization outcome.

### ChangeSet

A ChangeSet is the immutable intent and outcome record for one synchronization or check-in operation. It records the baseline, selected changes, validation result, actor, reason, and outcome.

A ChangeSet is not a Git commit exposed as the product model and is not automatically a new Part or CAD Revision. For MVP, standard authority History may be the audit source if it can provide the required fields; a custom ChangeSet record is not assumed to exist in Aras.

### Checkout Session

A Checkout Session connects an exclusive authority lock, its owner, a local writable copy, and the content baseline obtained at checkout.

### Recovery Copy

A Recovery Copy is a verified local copy retained before modified working content is discarded. It does not release the authority lock and is not evidence of a successful check-in.

Safe cancel-checkout ordering is:

1. Compare local content with the checkout baseline.
2. If modified, create and verify a Recovery Copy.
3. Show the recovery location and obtain explicit user confirmation.
4. Release the authority lock.
5. Clean local checkout metadata and working content according to the confirmed action.

If recovery creation or verification fails, cancellation stops and the authority lock remains active.

## Lifecycle and Review

### Lifecycle Identity

Lifecycle Identity is scoped to the authority item type, lifecycle map, and state identity. A matching display name does not establish matching business semantics.

The initial IDEA product profile may use the same semantic lifecycle roles for Part and CAD, but their authority mappings remain separate and replaceable. The profile is not a shared raw-state enum and is not defined by whichever Aras lifecycle map happens to be active in one environment.

### Lifecycle Semantic Role

A Lifecycle Semantic Role expresses business meaning such as initial, detailed-design, review, released, superseded, or obsolete while retaining the authority's verified state identity.

### Review Submission

A Review Submission is the auditable request to evaluate a working Part-CAD Revision Pair. It records the submitting engineer, the reviewer assigned by the PDM authority, change description, status, and decision history. The client does not choose or invent the reviewer identity.

### Release Policy

Release Policy evaluates Part and CAD eligibility separately, then coordinates the authority operation required by the feature. For the MVP, release succeeds for both revisions or for neither.

No client-side sequence of independent remote transitions can claim atomic success. The active PDM Authority must provide a verified atomic operation or equivalent transactional guarantee.

### Revision Policy

Start New Revision preserves stable Part and CAD numbers, creates new revision identities, copies the released CAD content as the new baseline, links the new pair, and leaves the released pair unchanged.

Only one current working pair may be created from the same released pair under the MVP policy. The authority must reject concurrent duplicate creation.

## History Dimensions

| History | Meaning | Creates engineering revision? |
|---|---|---|
| Local save | CAD application writes local content | No |
| File version/check-in | New controlled content within a working CAD Revision | No |
| ChangeSet | Audit record of a sync/check-in attempt and outcome | No |
| Lifecycle event | Auditable business transition | No |
| Start New Revision | New Part Revision and linked CAD Revision | Yes |
| Release approval | Freezes the eligible revision pair | No |

These histories must remain queryable without collapsing them into a single status or sequence number.

## Roles and Capabilities

| Role | Primary capabilities | Explicit limits |
|---|---|---|
| Design Engineer | Checkout, edit, check-in, submit review, withdraw eligible submission, start eligible new revision | Cannot approve own submission by default or modify Released content |
| Reviewer | Read submitted content, approve, request rework | Cannot edit reviewed CAD content during review |
| Project Manager | Observe lifecycle, checkout ownership, revision history, and Released content | Cannot bypass engineering controls or modify content by default |
| PDM Administrator | Configure users, role-action permissions, lifecycle mappings, numbering, and reviewer reassignment | Cannot rewrite Released revisions, completed ChangeSets, or audit events |

Permission is evaluated separately from lifecycle eligibility. Possessing a role never makes an otherwise ineligible transition valid.

## PDM Authority and Adapters

The PDM Authority owns remote identity, lifecycle, permissions, released revisions, concurrency, and audit history. Aras currently fills this role.

The domain speaks in business operations and semantic capabilities. An authority Adapter maps them to verified Aras ItemTypes, lifecycle states, relationships, Vault operations, and server methods. A future IDEA Adapter may map the same meaning to a different backend.

The domain must not require AML, IOM, Vault identifiers, Aras ItemType names, or Git branch names.

Reviewer assignment is an authority concern. Aras Assign/workflow assignment is the current mechanism. The client consumes a verified active assignment through a replaceable provider or adapter seam; it never hard-codes a person or sends an arbitrary client-only reviewer value.

## Domain Invariants

1. Released Part, CAD, Document, and BOM revisions are immutable within their approved policies.
2. Further work on a Released Part-CAD pair creates a new working pair.
3. Part and CAD retain separate lifecycle identities even when operations coordinate them.
4. Cross-item release and revision creation succeed atomically or leave no partial result.
5. Request Rework is state-only coordination: CAD and linked Part return to `Thiet ke chi tiet` without creating a new engineering revision/version.
6. Workspace changes are evaluated against an explicit baseline.
7. Modified local content is never silently discarded.
8. Recovery succeeds before remote unlock during destructive cancel-checkout.
9. Local save, file version, ChangeSet, lifecycle event, and revision are distinct histories.
10. Local change, checkout, lifecycle, validation, permission, and synchronization outcome are distinct dimensions.
11. Binary engineering content is not automatically merged.
12. BOM child revision selection is explicit and does not silently drift.
13. Document lifecycle and revision propagation are not inferred from Part or CAD rules.
14. Permission and lifecycle eligibility must both allow an action.
15. Authority schema and transition behavior require verified evidence before implementation.

## Domain Scenarios

### Normal Working Check-In

An engineer checks out CAD Revision A, changes the native file, and checks it in. The CAD file version and ChangeSet history advance; Part Revision A and CAD Revision A remain the same engineering revisions.

### Coordinated Release

A reviewer approves an eligible Part-CAD Revision Pair. The authority releases both revisions atomically. If either revision is ineligible, neither transitions.

### New Revision

An engineer starts work from Released pair A. The authority creates working Part Revision B and CAD Revision B, links them, copies released CAD content as the new baseline, and preserves pair A unchanged.

### Failed Cancel-Checkout Recovery

The local CAD file differs from the checkout baseline, but recovery storage is unavailable. Cancellation stops; the working content and authority lock remain unchanged.

### Released BOM Change

An engineer requests a quantity change in the BOM of Released Part Revision A. The system rejects direct mutation and requires a new working parent Part Revision under the future BOM feature policy.

### Document Change

A drawing linked to a Released design needs correction. The system does not assume whether Part or CAD must revise; a Document-specific policy and impact decision are required.

## Evidence Boundaries

- Current source and tests are authoritative for implemented behavior.
- Aras lifecycle, schema, permission, relationship, and server-method behavior require verified environment evidence.
- The checked-in source of a server method is reference code until deployment and transactional behavior are verified.
- IronCAD and Inventor capabilities must be validated independently; requirement parity does not prove adapter parity.
