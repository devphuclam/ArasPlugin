# Feature Specification: Controlled CAD Design Release

**Feature Branch**: `003-controlled-cad-design-release`

**Created**: 2026-07-18

**Status**: Draft

**Input**: Create the IDEA PDM controlled CAD design release workflow using docs/product/idea-pdm-product-discovery.md as approved discovery context. A design engineer working in IronCAD or Autodesk Inventor must be able to obtain a Part-linked CAD working revision, check it out for editing, check it in with an auditable reason, submit it for review, and receive a request for rework. An assigned reviewer must be able to approve an eligible design and move it to Released. A Released revision must be read-only; starting further design work must create a new working revision while preserving the released revision. Project managers can observe progress, and PDM administrators configure permissions and lifecycle mappings without bypassing release history. Aras Innovator is the current authority, but requirements must use backend-neutral PDM language and must not expose Git branches as the engineer's revision model. Exclude BOM editing, manufacturing, procurement, receiving, automatic binary merge, and full ECO/ECN processing from this feature.

## Clarifications

### Session 2026-07-18

- Q: Does approval release CAD only, or coordinate separate Part and CAD lifecycles? → A: Coordinated release. Approving an eligible CAD Revision releases both the CAD Revision and its linked Part Revision atomically. Part and CAD retain separate lifecycle identities; the system coordinates their transitions through an explicit MVP release policy rather than copying state names. If either transition is ineligible or fails, neither item is released and the approval reports a clear error. Configurable policy-driven coordination is deferred to post-MVP.
- Q: Does Start New Revision create a new Part Revision only, or both Part and linked CAD Revision? → A: Both created atomically. Start New Revision creates a new Part Revision and a new linked CAD Revision simultaneously, both at `Khoi tao`. The new CAD Revision uses the released CAD content as its initial baseline so the engineer can check it out immediately. Part Number and CAD Number remain stable while revision identifiers increase. The released Part-CAD pair remains immutable. If either creation or linking fails, the whole operation fails without leaving an orphan. BOM and Document revision propagation are deferred to separate approved policies.
- Q: What is the safe cancel-checkout behavior when local content is modified? → A: Backup modified content and require explicit confirmation. If the local CAD content differs from the checkout baseline, Cancel Checkout must first create a recoverable backup, verify the backup was written successfully, show the backup location to the user, and require explicit confirmation before discarding the working copy. The remote checkout lock is released only after backup verification and user confirmation. If backup creation or verification fails, cancellation stops, the local working copy remains untouched, and the checkout lock remains active. If the local content is unchanged from the baseline, the system may cancel checkout and release the lock without creating a backup. The concrete backup path and retention policy are finalized during planning; the recoverable behavior is a spec requirement.

## User Scenarios & Testing

### User Story 1 — Design engineer checks out, edits, and checks in a Part-linked CAD revision (Priority: P1)

A design engineer needs to modify an existing Part-linked CAD design. The engineer opens their local Workspace, selects the target Part-linked CAD working revision, and checks it out. While checked out, the engineer edits the CAD file in IronCAD or Autodesk Inventor. When the edits are complete, the engineer checks the revision back in with a written reason for the change. The system records the change as a completed ChangeSet with an audit trail.

**Why this priority**: The checkout-edit-checkin loop is the fundamental daily PDM action for every design engineer. Without it there is no controlled design workflow at all.

**Independent Test**: An engineer selects any current-revision CAD record in a connected Workspace, checks it out, modifies its content, checks it in with a reason message, and confirms the ChangeSet is recorded and the remote revision is updated. The entire flow succeeds without errors.

**Acceptance Scenarios**:

1. **Given** a connected Workspace containing a current-revision Part-linked CAD record at the `Thiet ke chi tiet` lifecycle state, **When** the engineer selects the record and chooses checkout, **Then** the system marks the record as checked out to the engineer, provides a local writable copy, and no other user can check out the same revision until it is checked in or the checkout is cancelled.

2. **Given** a checked-out CAD revision, **When** the engineer modifies the local CAD file and chooses check-in with a reason message, **Then** the system:
   - confirms the local file exists;
   - confirms the local file is readable;
   - calculates the SHA-256 before upload;
   - uploads only the validated file;
   - records a completed ChangeSet with the persisted reason, generates an audit event, and releases the checkout lock — **only when GATE-B-checkin evidence proves the deployed authority provides these**; if the deployed authority does not provide atomic update+unlock, ChangeSet, audit, or reason persistence, the limitation is documented and full compliance is not claimed;
   - the CAD revision now reflects the new content at a new file version while remaining at the same lifecycle state.
   The client does NOT simulate or prove authority-side ChangeSet or audit behavior client-side.

3. **Given** a checked-out CAD revision whose local content differs from the checkout baseline, **When** the engineer chooses cancel checkout, **Then** the system creates a recoverable backup, verifies it was written, shows the backup location to the engineer, and requires explicit confirmation before discarding the working copy. Only after confirmation is the checkout lock released.

4. **Given** a checked-out CAD revision whose local content is unchanged from the checkout baseline, **When** the engineer chooses cancel checkout, **Then** the system releases the checkout lock and discards the working copy without creating a backup, since there are no changes to lose.

5. **Given** a checked-out CAD revision with modified local content, **When** the system fails to create or verify a backup during cancel-checkout, **Then** cancellation stops, the local working copy remains untouched, the checkout lock remains active, and the engineer receives a clear error explaining the backup failure.

6. **Given** an attempt to check out a CAD revision that is already checked out to another user, **When** the engineer selects checkout, **Then** the system informs the engineer that the revision is locked by another user and offers read-only open instead.

---

### User Story 2 — Engineer submits design for review; reviewer approves or requests rework (Priority: P1)

After checking in the completed design, the engineer submits the Part-linked CAD revision for review. The system notifies the designated reviewer. The reviewer opens the design, inspects it, and either approves it (moving it to Released) or requests rework (returning it to the engineer for further changes).

**Why this priority**: Moving a design from working state to Released is the controlled gateway. Without review and release, there is no auditable quality gate, and released revisions lose their authority.

**Independent Test**: A checked-in CAD revision is submitted for review. A reviewer opens the submission, approves it, and the revision transitions to Released. The original engineer can confirm it is now read-only. Separately, a submission is rejected with a rework request, and the revision returns to the design state with the reviewer's comments visible.

**Acceptance Scenarios**:

1. **Given** a checked-in Part-linked CAD revision at the `Thiet ke chi tiet` state, **When** the owning engineer submits it for review with a description of changes, **Then** the revision transitions to `In Review`, the assigned reviewer receives a notification, and the engineer can no longer edit it without withdrawing the submission.

2. **Given** a CAD revision at `In Review` whose linked Part revision is also eligible for release, **When** the assigned reviewer opens the submission, inspects the content, and chooses Approve, **Then** both the CAD Revision and the linked Part Revision transition to `Released` atomically, the engineer is notified of approval, and both revisions become read-only for all users.

3. **Given** a CAD revision at `In Review`, **When** the assigned reviewer chooses Request Rework with a written explanation, **Then** the revision transitions back to `Thiet ke chi tiet`, the engineer is notified with the rework reason, and the revision becomes editable again after a new checkout.

4. **Given** a CAD revision submitted for review, **When** the owning engineer withdraws the submission before the reviewer acts, **Then** the revision returns to `Thiet ke chi tiet` without any review record other than the withdrawal event.

---

### User Story 3 — Released revision is read-only; new design work creates a new revision (Priority: P2)

A Released Part-linked CAD revision must not be edited. When a design change is needed for a released design, the engineer initiates a new revision. The system creates a new working revision (at `Khoi tao`) linked to the same Part identity, while the previous released revision remains immutable and auditable.

**Why this priority**: Immutability of released revisions is a core PDM invariant. Without it, audit trails, BOM baselines, and regulatory compliance are impossible.

**Independent Test**: An engineer attempts to check out a Released CAD revision and is blocked. The same engineer starts a new revision on the same Part, receives a new working revision at `Khoi tao`, checks it out, edits, checks in, and submits it — while the original Released revision remains unchanged and accessible.

**Acceptance Scenarios**:

1. **Given** a Part-linked CAD revision at `Released`, **When** any user attempts to check it out or modify it, **Then** the system rejects the action and explains that released revisions are read-only.

2. **Given** a released Part-CAD Revision Pair that needs design changes, **When** the engineer chooses Start New Revision, **Then** the system atomically creates a new Part Revision and a new linked CAD Revision, both at `Khoi tao` state, with new revision identifiers. The released Part-CAD pair remains unchanged at its current version. Only this new Part-CAD Revision Pair may be worked on as the current working pair.

3. **Given** a new Part-CAD Revision Pair created from a released pair, **When** the engineer works on the new pair through checkout-edit-checkin-submit-approve, **Then** only the new pair transitions through the lifecycle; the released pair is unaffected.

---

### User Story 4 — Project manager observes design progress and release status (Priority: P3)

A project manager views the design status of Part-linked CAD revisions in the Workspace or PDM tree. They can see which revisions are in design, in review, or released, who owns each checkout, and the revision history. They cannot bypass engineering controls, modify content, or change lifecycle state.

**Why this priority**: Project visibility enables planning and coordination without compromising engineering control boundaries.

**Independent Test**: A project manager opens the PDM tree, sees multiple Part-linked CAD revisions with their lifecycle states, checkout ownership, and revision history. The manager attempts to check out or modify a revision and is blocked.

**Acceptance Scenarios**:

1. **Given** a project manager viewing a Part with multiple CAD revision, **When** the manager inspects the revision list, **Then** each revision shows its lifecycle state (Khoi tao, Thiet ke chi tiet, In Review, Released), checkout status (checked out to whom or available), and revision identifier.

2. **Given** a project manager viewing the design tree, **When** the manager attempts to check out, check in, submit, approve, or modify a CAD revision, **Then** the system blocks the action and explains that the current user role does not have engineering modification permissions.

3. **Given** a project manager who needs to see a released design, **When** the manager opens it, **Then** the system opens it read-only (the same as any non-owner user opening a released or locked revision).

---

### User Story 5 — PDM administrator configures permissions and lifecycle mappings (Priority: P3) **(DEFERRED post-MVP)**

A PDM administrator assigns which user roles (Design Engineer, Reviewer, Project Manager, PDM Administrator) can perform which lifecycle actions on Part and CAD records. The administrator also maps the approved lifecycle states (Khoi tao, Thiet ke chi tiet, In Review, Released, and the rework transition) to the configured PDM authority. The administrator cannot bypass release history, modify released data, or rewrite audit records.

**Why this priority**: Permissions and lifecycle configuration are prerequisites for the controlled workflow to function in a real organization. Without an admin, the system cannot adapt to different team structures.

**Independent Test**: An administrator configures a lifecycle map where only users in the Reviewer group can approve submissions. A non-reviewer attempts to approve and is blocked. The same administrator attempts to modify a released revision's state directly and is blocked.

**Acceptance Scenarios**:

1. **Given** a PDM administrator in the configuration interface, **When** the administrator assigns the Approve action to the Reviewer role only, **Then** only users with the Reviewer role can approve submitted designs; engineers and project managers cannot.

2. **Given** a PDM administrator, **When** the administrator attempts to change the lifecycle state of a released revision, delete a ChangeSet, or rewrite audit history, **Then** the system blocks the action — administration does not override immutability.

3. **Given** a PDM administrator, **When** the administrator configures which states are editable (Khoi tao, Thiet ke chi tiet), which are reviewable (In Review), and which are read-only (Released), **Then** the system enforces those policies for all subsequent actions.

### Edge Cases

- What happens when the network connection is lost during checkout, check-in, submit, or approve? The operation must fail safely without leaving a partially-applied state. The user must be informed and able to retry once connectivity is restored.
- What happens when an engineer tries to check in a CAD file that fails content validation (corrupt file, wrong format, missing metadata)? The system must reject the check-in, explain the validation failure, and leave the checkout intact.
- What happens when an engineer has multiple checkouts and tries to submit only one for review? The system must allow per-revision submission independently of other checkouts.
- What happens when the reviewer is unavailable or leaves the organization? The PDM administrator must be able to reassign the review to another eligible reviewer.
- What happens when two engineers simultaneously attempt to start a new revision on the same released Part? The system must handle the race condition, allowing only one to succeed (only one current working Part-CAD pair per released pair) and informing the other of the concurrent attempt.
- What happens when Start New Revision creates the Part Revision but the CAD Revision creation fails? The entire operation must fail, the orphan Part Revision must be rolled back, and the error must be reported.
- What happens when cancel-checkout backup creation fails due to disk space, permissions, or I/O error? The cancellation must abort, the local working copy must remain untouched, the checkout lock must remain active, and the user must receive a clear error identifying the failure reason.
- What happens when a revision is at `In Review` and the engineer attempts to start editing again without withdrawing? The system must block editing until the submission is withdrawn or the reviewer requests rework.
- What happens when approval succeeds for the CAD revision but the linked Part revision is ineligible (e.g., wrong lifecycle state)? The system must fail the entire approval, release neither revision, and report which transition was ineligible and why.

## Requirements

### Functional Requirements

- **FR-001**: The system MUST allow a Design Engineer to check out an eligible (current working revision at `Khoi tao` or `Thiet ke chi tiet`) Part-linked CAD revision, producing a local writable copy and recording the checkout ownership in the authority.
- **FR-002**: The system MUST allow only one user to hold an active checkout on a given CAD revision at a time. Attempting to check out a revision that is already checked out to another user MUST present a clear explanation and offer read-only open instead.
- **FR-003**: The system MUST allow the checkout owner to check in the modified CAD file with a required written reason. Check-in MUST validate the local file (exists, readable, SHA-256 calculated before upload), upload the validated file, record a completed ChangeSet with the persisted reason, generate an audit event, and release the checkout lock. Any authority-side checksum or content-integrity behavior requires evidence verification — it must not be assumed. **GATE-B-checkin blocks full compliance**: if the deployed authority does not provide atomic update+unlock, a completed ChangeSet with the persisted comment, and an audit event, FR-003 compliance cannot be claimed. The limitation is documented in `docs/evidence/gate-b-checkin-commit-atomicity.md` and the check-in path is gated accordingly.
- **FR-004**: The system MUST allow the checkout owner to cancel their checkout. If the local CAD content differs from the checkout baseline, the system MUST first create a recoverable backup of the modified file, verify the backup was written successfully, show the backup location to the user, and require explicit confirmation before discarding the working copy. The remote checkout lock MUST be released only after backup verification and user confirmation. If backup creation or verification fails, cancellation MUST stop, the local working copy MUST remain untouched, and the checkout lock MUST remain active. If the local content is unchanged from the baseline, the system MAY cancel checkout and release the lock without creating a backup.
- **FR-005**: The system MUST allow the engineer who owns the checked-in CAD revision to submit it for review with a change description. Submission MUST transition the revision to `In Review` and notify the assigned reviewer.
- **FR-006**: The system MUST allow the engineer to withdraw a submitted revision before the reviewer acts. Withdrawal MUST return the revision to `Thiet ke chi tiet` without recording a review decision.
- **FR-007**: The system MUST allow an assigned Reviewer to approve a submitted CAD revision at `In Review` whose linked Part revision is also eligible for release. Approval MUST atomically transition both the CAD Revision and the linked Part Revision to `Released`, making both read-only for all users, and notify the submitting engineer. **Note**: Checked-in `idea_ApproveCadReview.cs` is CAD-only — it does not load, check, or promote the linked Part and therefore does NOT satisfy FR-007. **GATE-B-approve** blocks Approve UI until deployed authority behavior proves coordinated Part+CAD release. The client MUST NOT simulate atomicity (no sequential independent transitions, no compensating rollback).
- **FR-008**: The system MUST allow an assigned Reviewer to request rework on a submitted CAD revision with a written explanation. MVP Request Rework semantics are **CAD-only**: the CAD Revision transitions back to `Thiet ke chi tiet` and the engineer is notified. The linked Part lifecycle and version MUST NOT be changed implicitly (per ADR-0009, Part and CAD have separate lifecycle identities; MVP coordinates them only at Start New Revision and Release). **Note**: Checked-in `idea_RequestCadRework.cs` calls `Sync_Part_From_CAD`, which may modify Part state or create a new Part version — inconsistent with CAD-only semantics. **GATE-RW** blocks Request Rework UI until deployed behavior is verified. If the deployed method changes Part, a business-policy decision is required; silent acceptance is prohibited.
- **FR-009**: The PDM Administrator MUST be able to reassign a review to another eligible reviewer when needed. **(DEFERRED post-MVP)**
- **FR-010**: A Released Part-linked CAD revision MUST be read-only. The system MUST block any checkout, edit, state transition, or content modification on a released revision.
- **FR-011**: The system MUST allow a Design Engineer to start a new working revision for a released Part. Starting a new revision MUST atomically create a new Part Revision and a new linked CAD Revision, both at `Khoi tao` state. The new CAD Revision MUST use the released CAD content as its initial baseline. The Part Number and CAD Number remain stable; only their revision identifiers increment. The released Part-CAD pair MUST remain immutable and unchanged. Only one current working Part-CAD pair may be created from the same released pair.
- **FR-012**: The system MUST enforce lifecycle state eligibility for each action. Only a revision at an eligible state may undergo checkout, check-in, submit, review, approval, or rework.
- **FR-013**: The system MUST provide a Project Manager role that can view Part-linked CAD revision states, checkout status, lifecycle history, and released content in read-only mode but cannot perform engineering modifications.
- **FR-014**: The system MUST provide a PDM Administrator role that can configure which roles (Design Engineer, Reviewer, Project Manager, PDM Administrator) are permitted to perform each lifecycle action. **(DEFERRED post-MVP)**
- **FR-015**: The system MUST provide a PDM Administrator role that can map the approved lifecycle states and transitions for Part and CAD ItemTypes in the PDM authority. **(DEFERRED post-MVP)**
- **FR-016**: The PDM Administrator MUST NOT be able to modify, delete, or rewrite a released revision, a completed ChangeSet, or any audit record.
- **FR-017**: Every lifecycle transition (checkout, check-in, submit, withdraw, approve, request rework, start new revision) MUST be recorded as an auditable event with timestamp, actor identity, revision identifier, previous and new state, and any user-provided reason.
- **FR-018**: Communication failures during any operation MUST NOT leave the system in a partially-applied state. The user MUST be informed of the failure and allowed to retry after connectivity is restored.
- **FR-019**: All requirements MUST be expressed in backend-neutral PDM language. The Git branch model MUST NOT be exposed as the engineer's revision model.
- **FR-020**: The authority MUST provide atomic cross-item operations: if either item's transition is ineligible or fails, neither item transitions. The system MUST report a clear error identifying which transition failed and why. This cross-cutting principle governs both coordinated release (FR-007) and Start New Revision (FR-021). The client MUST NOT simulate atomicity through sequential transitions or compensating rollback.
- **FR-021**: Start New Revision MUST be atomic: if creation of either the Part Revision or the CAD Revision fails, or if the linking between them fails, the entire operation MUST fail without leaving an orphan revision. The system MUST report a clear error.

### Key Entities

- **Part identity**: A stable engineering identifier (Part Number) that persists across revisions. A Part may have many revisions.
- **Part Revision**: A controlled version of a Part, representing one design iteration (e.g., revision A, B, C). It has a lifecycle state and can link to CAD revisions. Start New Revision creates a new Part Revision and linked CAD Revision as a coordinated pair.
- **Part-CAD Revision Pair**: A matched set of one Part Revision and one linked CAD Revision. They retain separate lifecycle identities; the system coordinates their transitions atomically only at policy-defined MVP operations: Start New Revision (both created together) and Release approval (both released together). Only one current working pair may exist per released pair.
- **CAD Revision**: A revision-controlled design record associated with native CAD file content (IronCAD .ics, Inventor .ipt/.iam). Linked to exactly one Part Revision. Has its own lifecycle state, checkout status, file versions, and audit history.
- **Working Revision**: A CAD or Part revision whose lifecycle state is before `Released`, eligible for editing through checkout/check-in.
- **Release Policy**: An explicit MVP policy that coordinates Part and CAD lifecycle transitions during approval. When a CAD Revision is approved, the system atomically releases both the CAD Revision and its linked Part Revision. Configurable policy-driven coordination is deferred to post-MVP.
- **Released Revision**: A CAD or Part revision at the `Released` lifecycle state, immutable and read-only. Cannot be checked out, edited, or transitioned further.
- **Checkout**: A temporary exclusive claim on a working CAD revision, enabling the checkout owner to modify the local file. Recorded in the authority with owner identity and timestamp.
- **ChangeSet**: An immutable record of one completed check-in operation. Includes its baseline reference, changed files, validation result, author identity, reason message, and outcome.
- **Review Submission**: A request from a Design Engineer to move a checked-in CAD revision from `Thiet ke chi tiet` through `In Review` toward `Released`. Includes change description, submitting engineer identity, and assigned reviewer.
- **Lifecycle State**: A named stage in the approved CAD/Part lifecycle: `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`. Each state has defined permitted transitions and role-based action eligibility.
- **User Role**: A named permission group — Design Engineer, Reviewer, Project Manager, PDM Administrator — defining which lifecycle actions the user may perform.

## Success Criteria

### Measurable Outcomes

- **SC-001**: A Design Engineer can complete the checkout-edit-checkin cycle in under 2 minutes, including file transfer time. Measured against a **controlled test fixture** — the evidence record must state the CAD file size, environment specifications, and network conditions. If the 2-minute target cannot be verified during MVP due to environment or performance limitations, it is marked as post-MVP verification and the evidence record documents the measured baseline instead.
- **SC-002**: A Review Submission through approval to Released completes with fewer than 5 user-facing steps for both the engineer submitting and the reviewer approving.
- **SC-003**: Any attempt to check out, edit, or modify a Released revision is blocked within 2 seconds with a clear explanation.
- **SC-004**: Starting a new revision from a Released Part creates the new revision in under 10 seconds, and the original released revision remains fully accessible and unchanged.
- **SC-005**: A Project Manager can view the lifecycle state, checkout status, and revision history of any Part-linked CAD revision without being able to perform any modification action.
- **SC-006**: A PDM Administrator can configure role-to-action permissions and lifecycle state mappings without being able to modify released data or audit history. **DEFERRED post-MVP** — US5 admin configuration is not part of the MVP. Evidence may be collected as a deferred manual task (see T060).
- **SC-007**: The system correctly handles concurrent new-revision requests: one succeeds, the other receives a clear conflict message.
- **SC-008**: All auditable events are recorded with timestamp, actor identity, revision identifier, previous and new state, and reason where applicable — and cannot be deleted or modified by any user role including PDM Administrator.

## Assumptions

- **Checkout exclusivity**: The standard PDM exclusive checkout model is assumed. Simultaneous editing of the same CAD revision by multiple engineers is out of scope for MVP.
- **Reviewer assignment**: The engineer selects an eligible reviewer from a list at submission time. The list of eligible reviewers is determined by role-based permission configuration.
- **Notification mechanism**: The system notifies users (engineer when review is decided, reviewer when submission arrives) through in-application notifications. Email or external notification integration is out of scope for MVP.
- **Existing Workspace model**: The engineer already has a connected Workspace with a baseline from the PDM authority. The Workspace clone/pull operations that obtain content from the authority are separate features and assumed available.
- **Lifecycle state names**: The lifecycle states `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, and the rework transition are used as configured in the PDM authority. The feature does not assume these exact Vietnamese display names are hardcoded — they are the configured values in the reference environment.
- **File transfer**: Checkout downloads the current CAD file version; check-in uploads the modified file. File transfer protocols are handled by the authority adapter and are not specified at the feature level.
- **CAD application integration**: The feature assumes the system can launch the appropriate CAD application (IronCAD, Autodesk Inventor) to open the checked-out file. The launch mechanism is handled by existing Desktop services.
- **IronCAD and Inventor parity**: The feature treats both CAD applications equivalently at the requirement level. Application-specific differences (file format, API capabilities) are implementation details addressed during planning.
- **No batch operations**: Checkout, check-in, submit, approve, and new-revision actions operate on one CAD revision at a time. Batch or multi-select operations are out of scope for MVP.
- **No offline mode**: The feature requires network connectivity to the PDM authority for all operations. Offline editing is out of scope for MVP.
