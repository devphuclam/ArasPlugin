# Research: Controlled CAD Design Release

## 1. Part and CAD Lifecycle Eligibility — Separate Verified Mappings

**Decision**: Part and CAD have separate lifecycle identities per ADR-0009. For the current IDEA profile, the product owner confirmed that both use the same core semantic path: `Khoi tao` → `Thiet ke chi tiet` → `In Review` → `Released`. Matching names are recorded as verified for this profile, but the adapter boundary remains separate so a future backend or Aras configuration can diverge safely. The feature must:

- Define `IPartLifecyclePolicy` (new) and `ICadLifecyclePolicy` (extracted from the existing `CadLifecyclePolicy` pattern) as separate interfaces with verified state mappings.
- Keep an **Aras environment evidence gate** for the remaining Part transition, permission, and immutability details before the Part policy is treated as fully evidenced.
- Use semantic role (e.g., "editable", "review-only", "released") as the policy abstraction; retain the verified Aras state identity and display name behind the adapter.
- The existing `CadLifecyclePolicy.cs` lists `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`; the product owner confirmed the current Part profile uses the same core path. These remain separate adapter mappings and must not be collapsed into one raw lifecycle enum.

**Rationale**:
- ADR-0009 explicitly states: "Keep lifecycle identity scoped to the Aras ItemType and lifecycle map. Matching state labels do not prove identical behavior."
- Part and CAD are different Aras ItemTypes with separate lifecycle maps in the verified environment.

**Alternatives considered**:
- Reusing `CadLifecyclePolicy` constants for Part (rejected — violates ADR-0009; matching Vietnamese display names do not guarantee matching transitions or semantics).
- Creating a single shared lifecycle enum (rejected — Part and CAD retain separate lifecycle identities; a shared enum would collapse them).

## 2. Atomic Authority-Side Operations — No Client-Side Rollback

**Decision**: Both coordinated release (approve) and Start New Revision are authority-side atomic operations. The PDM Authority must provide a verified atomic operation or equivalent transactional guarantee. The client:

- Sends a single request to the authority with all required identifiers.
- Does NOT simulate atomicity by issuing sequential remote transitions and attempting compensating rollback on failure.
- Interprets the authority response: success → both items transitioned; failure → neither item transitioned, no partial state.
- Before calling the authority, the client checks eligibility via policy interfaces (Part + CAD). These checks are advisory — they reduce the chance of authority rejection but do not replace the authority's own validation.

**Rationale**:
- The domain model (Release Policy section) states: "No client-side sequence of independent remote transitions can claim atomic success. The active PDM Authority must provide a verified atomic operation or equivalent transactional guarantee."
- The existing `IPdmRepositoryClient.ReviseCadAsync(PdmReviseRequest)` is an existing client contract with a transport implementation in `HttpPdmRepositoryClient.cs` that calls `idea_ReviseCad`. It follows the one-request, one-result pattern. The deployed Aras server behavior (atomicity, response shape, Part+CAD creation and linking) remains unverified and gated by **GATE-B-revise**. The client implementation alone does not prove authority operation correctness.
- The `idea_ReviseCad` server method source indicates the intended paired operation, but its deployed transactional behavior requires verification.

**Alternatives considered**:
- Client-side rollback (rejected — cannot guarantee rollback success after partial remote state change; network failure after first transition leaves inconsistent state).
- Sequential independent transitions without rollback (rejected — violates domain invariant 4: "Cross-item release and revision creation succeed atomically or leave no partial result").

## 3. Recovery Copy Belongs to Workspace — Desktop Orchestrates, Aras Unlocks Only

**Decision**: Recovery copy creation, hashing, retention, and cleanup are Workspace responsibilities. The Workspace project already owns local file operations, hash verification, and recoverable persistence. The Desktop project orchestrates the user confirmation flow. The Aras adapter's only remote role is releasing the checkout lock.

**Safe cancel-checkout ordering** (per domain model):

1. Compare local content with the checkout baseline.
2. If modified, create and verify a recovery copy.
3. Show the recovery location and obtain explicit user confirmation.
4. Release the authority lock (Aras adapter: unlock only — no backup paths, no local state in the remote request).
5. Clean local checkout metadata and working content.

If recovery creation or verification fails, cancellation stops and the authority lock remains active.

**Implementation implications**:
- `ICadBackupService` (or `IRecoveryCopyService`) belongs in Workspace, not in Core contracts or Aras.
- The `CancelCheckoutRequest` DTO used for the remote unlock call must NOT include backup paths, backup hashes, or recovery metadata. It contains only `CadId` and `LockToken` (as currently defined).
- The Workspace project's existing `.idea-pdm` metadata directory is the natural recovery storage location.

**Rationale**:
- The domain model Recovery Copy section specifies the five-step ordering and the rule: "If recovery creation or verification fails, cancellation stops and the authority lock remains active."
- The existing `CancelCheckoutRequest` DTO correctly contains only `CadId` and `LockToken` — no backup fields.
- ADR-0010 establishes the boundary: authority owns remote lock; workspace owns local mechanisms.

**Alternatives considered**:
- Backup in the Aras adapter (rejected — the adapter should only unlock remotely; backup paths and local state do not belong in the transport contract).
- Backup in Desktop (rejected — local file operations are a Workspace concern per the solution architecture).

## 4. Lifecycle Policy Separated from Authority Operation

**Decision**: Lifecycle eligibility policy and the authority operation that performs a release are separate concerns:

- `ICadLifecyclePolicy` and `IPartLifecyclePolicy` answer business questions: editable, reviewable, releasable, obsolete, eligible-for-sync. Callers do not compare raw state strings.
- `ICadReleaseEligibility` evaluates a **snapshot** (`CadReleaseEligibilitySnapshot` containing current CAD state and Part state). It NEVER fetches data from Aras. The orchestration layer (Desktop) reads current revision states and populates the snapshot before calling the check.
- The authority adapter (`IArasCadClient.ExecuteCadBusinessActionAsync`) performs the remote transition. The policy does not call the authority; the authority does not evaluate policy.
- Desktop or a service layer reads current states, creates a snapshot, calls `ICadReleaseEligibility.CheckAsync(snapshot)` first (advisory eligibility check), then calls the authority adapter (authoritative atomic operation). If the authority rejects the request, the client reports the authority's error — it does not attempt to interpret or override it.
- `CadReleaseEligibilityResult` (advisory — `IsEligible` + `BlockingReasons`) and the authority operation result (`CadOperationContext` from `ExecuteCadBusinessActionAsync`) are independent types. The authority result must not represent a partial release — if the server method confirms success, both items were released; if it fails, neither was released.
- Withdraw uses `ExecuteCadBusinessActionAsync(Withdraw)` — the same canonical transport seam. No separate eligibility check needed for withdraw (no coordinated Part/CAD operation).

**Rationale**:
- The domain model Release Policy section: "Release Policy evaluates Part and CAD eligibility separately, then coordinates the authority operation required by the feature."
- The existing `CadLifecyclePolicy.CanApproveReview(string state)` checks CAD eligibility and returns bool. `CanStartNewRevision` checks for `Released` state. These are separate from the remote operation.
- Snapshot pattern keeps Core policy pure (no IOM, no Aras dependencies) and makes testing trivial — pass known states, assert expected eligibility.

## 5. Part-CAD Revision Pair — MVP Cardinality

**Decision**: The Part-CAD Revision Pair is defined for this MVP as one Part Revision linked to one primary CAD Revision. This is not the global Part-CAD cardinality — a Part Revision may link to zero or more CAD revisions in the future, but the MVP coordinates only the primary CAD Revision.

**Rationale**:
- The domain model: "The MVP release aggregate is one Part Revision linked to one primary CAD Revision."
- This matches the domain model's "Part-CAD Revision Pair" section and the coordinated operations.

## 6. Authority-Assigned Reviewer and Coordinated Rework

**Decision**: Aras Assign/workflow assignment is authoritative for reviewer selection. The engineer does not choose a reviewer and the client does not send a reviewer identity through the current submit contract. Reviewer actions require a verified active assignment read seam; a missing assignment fails closed.

Request Rework is accepted as coordinated state-only behavior for MVP: the CAD Revision and linked Part Revision return to `Thiet ke chi tiet), no new engineering revision/version is created, and duplicate `Sync_Part_From_CAD` work is a no-op. The remaining evidence work concerns deployed result/audit coverage, not an unresolved business-policy choice.

**Rationale**:

- Product owner decision on 2026-07-20: “Aras Assign.”
- Product owner decision on 2026-07-20: Part returns to `Thiet ke chi tiet` without increasing version; duplicate Sync is a no-op.
- The client must not infer review ownership from checkout lock ownership.

## 8. Authority-Neutral Identities — No Leaked Aras Terminology

**Decision**: Backend-neutral domain contracts use authority-neutral identity types (plain `string` identifiers, domain entity references). Aras-specific mappings (Aras ID, config_id, Vault metadata, IOM references) stay in the adapter layer (`IdeaCadConnector.Aras`). Specifically:

- `CadId` → `string` (authority-neutral documented as "authority identifier of the CAD revision")
- `PartId` → `string` (authority-neutral documented as "authority identifier of the Part revision")
- No `config_id`, `item_number`, `major_rev`, `lock_token` terminology in domain contracts
- File references use domain terms like "native content reference" or "file hash", not "Vault file ID"

**Rationale**:
- The domain model PDM Authority section: "The domain must not require AML, IOM, Vault identifiers, Aras ItemType names, or Git branch names."
- The existing `PdmReviseRequest` already uses abstract names (`PartId`, `CadId`, `PartNumber`, `CadNumber`, `Reason`) — no Aras-specific terms. This pattern is correct and should be extended.

## 9. Withdraw Submission — Same Canonical Transport Seam

**Decision**: Withdraw submission uses the same `IArasCadClient.ExecuteCadBusinessActionAsync` with `CadBusinessActionKind.Withdraw`. This is NOT a new transport method.

**Source evidence**: `CadBusinessActionKind` currently contains `SubmitForReview`, `Approve`, `RequestRework` — but NOT `Withdraw`. `WorkflowActionMapper` has no withdraw mapping. Neither `ArasCadClient` nor `HttpArasCadClient` has a withdraw case. All four files must be extended.

**Implementation requirements**:
- Add `Withdraw` to `CadBusinessActionKind` enum.
- Add withdraw lifecycle mapping in `WorkflowActionMapper`.
- Add `Withdraw` switch case + `ExecuteWithdrawAsync` in `ArasCadClient`.
- Add `Withdraw` switch case + `ExecuteWithdrawHttpAsync` in `HttpArasCadClient`.
- Add `CanWithdraw(string state)` to `ICadLifecyclePolicy` and `CadLifecyclePolicy` (returns true for `In Review`).
- All follow the existing `SubmitForReview`/`Approve`/`RequestRework` patterns exactly.

**Evidence gate (GATE-W)**: Verify the deployed server method or lifecycle transition for withdraw exists and behaves correctly. If no atomic mechanism exists to return CAD from `In Review` to `Thiet ke chi tiet` without recording a review decision, the Withdraw UI remains disabled.

**GATE-W-owner extension**: Availability of a withdraw transition alone is insufficient. The authority operation context must expose the submission owner (or an equivalent verified authorization result). `LockOwnerName` is checkout ownership and must not be used as a substitute. Until this is verified, the client fails closed.

## 10. Reviewer Assignment Evidence Boundary

The checked-in `idea_SubmitCadForReview` method accepts `cad_id` and `comment`; it has no reviewer input because the product owner confirmed that Aras Assign/workflow assignment selects the reviewer. The current `CadOperationContext` likewise has no submission-owner or active-reviewer read contract. The live workflow contains runtime review activities assigned by the authority; this is evidence for a provider/authority-assignment seam, not permission to hard-code an identity. Therefore the client must not add guessed AML/property names, put reviewer identities into comments, or show a selector whose value is discarded. The initial UX stays simple behind a replaceable authority-assignment provider; GATE-RS remains required before submission/reviewer decisions can claim verified active-assignment behavior.

## 11. Reviewer Reassignment — FR-009 (Deferred to Post-MVP)

**Decision**: Reviewer reassignment is deferred to post-MVP. FR-009 is not part of the current feature implementation. In MVP, Aras Assign/workflow performs reviewer assignment at submission time; engineer-selected reviewer input is not part of the contract.

**Design for future implementation**:
- Define `IReviewReassignmentService` in Core contracts with `ReassignAsync(ReviewReassignmentRequest)`.
- Implement in Aras adapter using `IArasCadClient.ExecuteCadBusinessActionAsync(ReassignReviewer)` where `ReassignReviewer` is a new `CadBusinessActionKind` value.
- Add `ReassignReviewer` to `CadBusinessActionKind`, `WorkflowActionMapper`, both Aras clients.
- Do NOT simulate reassignment client-side. The authority must validate the new reviewer.
- Only PDM Administrator role may reassign.
- **Evidence gate (GATE-REASSIGN)** required before UI enablement.

**Rationale**:
- FR-009 is P3. MVP scope (US1 + US2) does not require admin reassignment.
- The initial reviewer mechanism is deliberately simple and replaceable: consume an authority-assigned reviewer or a configured provider. Do not make the engineer's client selection authoritative until the server contract is verified.
- Reassignment requires a verified server method or authority capability (GATE-REASSIGN).

## 12. Notifications and Audit — Authority Responsibility

**Decision**: Notifications (engineer notified on approve/rework, reviewer notified on submit) and audit events are the authority's responsibility. The client does NOT implement its own notification or audit system.

**Rationale**:
- FR-005: "notify the assigned reviewer" — the authority is the system of record.
- FR-007: "notify the submitting engineer" — same.
- FR-017: "Every lifecycle transition MUST be recorded as an auditable event" — Aras Innovator already records audit trails for item transitions. The client must not duplicate or fake this.
- **Evidence gate (GATE-N)**: Verify the Aras environment records and surfaces audit events for all lifecycle transitions (checkout, check-in, submit, withdraw, approve, request-rework, start-new-revision). Record the audit schema and available fields (actor, timestamp, revision, previous state, new state, reason). If audit coverage is incomplete, document the gap.

## 13. Check-in Atomicity — idea_CommitCadCheckin (GATE-B-checkin)

**Decision**: The check-in server method `idea_CommitCadCheckin` must provide atomic update+unlock, ChangeSet recording, audit event creation, and reason persistence. The checked-in source at `src/IdeaCadConnector.Aras/ServerMethods/idea_CommitCadCheckin.cs` falls short: it updates native_file and unlocks in separate `apply()` calls without transaction wrapping. The `comment` property is read from input but not written to any ChangeSet or audit record. The method header claims "Atomically complete a CAD check-in" but the source does not implement atomicity.

**Implementation requirements**:
- Verify deployed `idea_CommitCadCheckin` behavior — the checked-in source may differ from deployment.
- Confirm lock ownership validation works correctly.
- Confirm native_file attachment is updated on the CAD record.
- Confirm unlock follows successful update without partial state risk.
- Confirm ChangeSet creation (either server-side or by an Aras workflow/event).
- Confirm audit event records actor, timestamp, revision, previous/new state, and reason/comment.
- If deployed method is not atomic or does not record ChangeSet/audit, document limitations and disable claims of FR-003/FR-018 full compliance.

**Evidence gate (GATE-B-checkin)**: Verify all above conditions in the real Aras environment. Record in `docs/evidence/gate-b-checkin-commit-atomicity.md`.

## 14. Request Rework Side Effects — Sync_Part_From_CAD (GATE-RW)

**Decision**: The `idea_RequestCadRework` server method at `src/IdeaCadConnector.Aras/ServerMethods/idea_RequestCadRework.cs` promotes CAD to `Thiet ke chi tiet` and then calls `Sync_Part_From_CAD`. The product owner confirmed that the linked Part also returns to `Thiet ke chi tiet`, does not receive a new engineering version, and duplicate Sync work is a no-op. Request Rework is therefore accepted as coordinated state-only behavior for MVP.

**Implementation requirements**:
- Verify deployed behavior of `Sync_Part_From_CAD` — does it change Part lifecycle state? Does it create a new Part version?
- Confirm that the deployed result matches the product-owner decision: both states return to `Thiet ke chi tiet`, no new Part version is created, and duplicate synchronization is a no-op.
- Keep audit/result verification separate; this decision does not fabricate transition evidence or open a runtime gate by itself.

**Evidence gate (GATE-RW)**: Verify deployed rework side effects on Part lifecycle in the real Aras environment. Record in `docs/evidence/gate-rw-rework-side-effects.md`.

## 15. Existing Infrastructure Summary (Updated)

- `IPdmRepositoryClient.ReviseCadAsync(PdmReviseRequest)` — Client code exists in `HttpPdmRepositoryClient.cs` and calls `idea_ReviseCad`. Real Start New Revision behavior remains blocked by GATE-B-revise until deployed server atomicity and response behavior are verified. The existence of the client method does not prove the authority operation works.
- `IRevisionService.CheckPreconditionsAsync` — Already defined; checks CAD state, lock token, Part/CAD IDs.
- `IRevisionService.ReviseAsync` — Already defined; delegates to `IPdmRepositoryClient.ReviseCadAsync`.
- `CadLifecyclePolicy.CanStartNewRevision` — Already checks for `Released` state; must NOT be reused for Part.
- `CadLifecyclePolicy.GetStartNewRevisionMessage` — Currently returns "this desktop app does not create new revisions" — must be replaced with Start New Revision flow.
- `WorkflowActionMapper` — Maps `SubmitForReview`, `Approve`, `RequestRework` to `CadBusinessActionKind`. Already deployed in production. These map to the CAD workflow only.
- `CadBusinessActionKind` already has `SubmitForReview`, `Approve`, `RequestRework` — CAD-only workflow actions. Part workflow actions may require a separate Part action kind enum.
- `TranslationKeys.ConfirmSubmitForReview` — Already exists.
- `CancelCheckoutRequest` currently contains `CadId` + `LockToken` only — correct per the revised design. No backup fields needed.
- `WorkflowActionMapper` must be extended to include `Withdraw` (if not already present) and verify the server method exists (GATE-W).
- **Evidence gate GATE-A**: Before implementing Part lifecycle transitions, capture verified Part ItemType lifecycle state names, transitions, and semantic roles from the Aras environment.
- **Evidence gate GATE-B-revise**: Before enabling Start New Revision UI, verify the deployed `idea_ReviseCad` server method behavior and atomic transactional guarantees.
- **Evidence gate GATE-B-approve**: Before enabling Approve UI, verify the deployed `idea_ApproveCadReview` server method provides coordinated Part+CAD release. **Checked-in source is CAD-only** — deployed behavior may differ.
- **Evidence gate GATE-B-checkin**: Before claiming FR-003/FR-018 compliance, verify deployed `idea_CommitCadCheckin` provides atomic update+unlock, ChangeSet, audit, and reason persistence. **Checked-in source has separate apply() calls without transaction**.
- **Evidence gate GATE-W**: Before enabling Withdraw UI, verify the deployed withdraw capability (lifecycle transition or server method) exists and behaves correctly.
- **Evidence gate GATE-RW**: Before claiming full Request Rework compliance, verify deployed `idea_RequestCadRework` result and audit behavior against the accepted coordinated state-only policy. **Checked-in source may modify Part lifecycle through the intended Sync path**.
- **Evidence gate GATE-N**: Before claiming FR-017 compliance, verify Aras audit trail covers all lifecycle transitions with required fields (actor, timestamp, revision, previous state, new state, reason).

## 14. Product Decision Addendum — Same Initial Semantic Profile, Replaceable Mapping

**Decision**: IDEA's initial business profile should be simple and use the same semantic lifecycle roles for Part and CAD. This does **not** create one shared raw state enum. Each ItemType keeps an independent mapping/policy so the backend and live Aras configuration can change later.

**Live evidence**: On 2026-07-20, the active CAD ItemType used `Custom CAD Document` and the active Part ItemType used `Custom Part`. A corrected query of the active Part map found `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `Che tao`, `Nhan hang`, `In Change`, `Superseded`, and `Obsolete`. The four core IDEA states are therefore present in both maps; their additional states and transition graphs still require separate policy handling.

**Recommended Aras improvement**: No new Part lifecycle is required solely to obtain the four core states; verify the existing transition graph and permissions first. Keep `PartLifecyclePolicy` separate from CAD policy because the maps contain additional states and may diverge later. See ADR-0011 and `docs/evidence/part-lifecycle-evidence.md`.

## 15. Recommended Authority Operations for Real PDM Behavior

The current live methods are useful learning references but do not yet prove the business guarantees required by the feature:

- **Approval**: `idea_ApproveCadReview` textually promotes CAD only, but live ItemType configuration adds `CAD.onAfterPromote → Sync_Part_From_CAD`, so the deployed approval path indirectly coordinates Part. This is the correct caller path to preserve in the model. The remaining question is transaction/failure behavior: test whether a Part sync failure rolls back the CAD promotion. Do not replace this with two client calls.
- **Start New Revision**: `idea_ReviseCad` is one client request but performs multiple server-side operations. Keep the UI gated until a deployed failure and concurrency test proves no orphan Part, orphan CAD, or duplicate pair remains.
- **Rework**: `idea_RequestCadRework` calls `Sync_Part_From_CAD`, which can change or version Part. This is acceptable only if the business explicitly defines rework as coordinated Part-CAD behavior. Otherwise, remove the hidden Part side effect. In either case, make the authority operation explicit and testable.
- **Check-in/audit**: Standard Aras `History` exists in the live environment. For MVP, use it as the first audit candidate and verify actor/time/item/version/reason coverage. Add a custom ChangeSet only if History cannot support the required structured synchronization or recovery use case.
- **Reviewer**: Keep the initial client simple behind `IReviewerProvider`; consume a verified active Aras assignment when available. Do not hard-code a reviewer or add a selector whose value is not accepted by the authority.

These are recommendations for the Aras administrator and remain unimplemented until approved and evidenced.

## 16. Live Inspection Lessons

The durable lessons from this investigation are recorded in `docs/development/aras-live-evidence-and-ai-lessons.md`. The critical rules are: query the active ItemType lifecycle binding, separate semantic roles from raw state names, distinguish source/deployment/transaction evidence, inspect helper call graphs for cross-item side effects, and never mark an evidence gate complete from source inspection alone.

## 17. Follow-up Live Confirmation

The product owner confirmed that the deployed CAD `onAfterPromote → Sync_Part_From_CAD` path satisfies the required coordinated atomicity behavior. The product owner also confirmed that the second Sync invocation observed in the rework path is a no-op. The evidence gate records this confirmation without inventing missing fixture identifiers or logs.
