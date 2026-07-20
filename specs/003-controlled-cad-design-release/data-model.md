# Data Model: Controlled CAD Design Release

## Entities

### Part Identity
| Field | Type | Description |
|-------|------|-------------|
| PartNumber | string | Stable engineering identity across revisions. Does not change when a new revision is created. |
| Revisions | Collection<PartRevision> | All revisions of this Part. |

### Part Revision
| Field | Type | Description |
|-------|------|-------------|
| RevisionId | string | Authority identifier for this revision record. |
| PartId | string | Authority identifier linking to the Part identity (stable across revisions). |
| PartNumber | string | Stable Part Number (inherited from Part identity). |
| Revision | string | Revision identifier (e.g., A, B, C). |
| LifecycleState | string | Current lifecycle state in the Part ItemType's lifecycle map. **Requires Aras environment evidence**: state names and transitions differ from CAD lifecycle. |
| PrimaryCadRevisionId | string (nullable) | Authority identifier of the primary linked CAD Revision for this MVP. |
| CreatedDate | DateTime | When this revision was created. |
| CreatedBy | string | User who created this revision. |

**Validation Rules**:
- `PartNumber` + `Revision` must be unique within the authority.
- `LifecycleState` transitions follow the Part ItemType's verified lifecycle map (separate from CAD).
- A released Part Revision cannot be modified (immutable).
- Only one working revision may exist per Part identity at a time (MVP scope).

### CAD Revision
| Field | Type | Description |
|-------|------|-------------|
| CadId | string | Authority identifier for this CAD revision record. |
| CadConfigId | string | Authority identifier linking to the CAD identity (stable across revisions). |
| CadNumber | string | Stable CAD Number (inherited from CAD identity). |
| Revision | string | Revision identifier (e.g., A, B, C). |
| LifecycleState | string | Current lifecycle state in the CAD ItemType's lifecycle map. Known states from existing source: `Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`. |
| LinkedPartRevisionId | string | Authority identifier of the linked Part Revision. |
| NativeContentRef | string | Authority reference to the current native CAD content (not a Vault-specific path). |
| ContentHash | string (SHA256) | Content hash of the current native file. |
| CheckoutOwner | string | User who currently holds the checkout lock (null if not checked out). |
| CheckedOutSince | DateTime? | When the current checkout was started (null if not checked out). |
| LockToken | string | Token required to release the checkout lock. |
| CreatedDate | DateTime | When this revision was created. |
| CreatedBy | string | User who created this revision. |

**Validation Rules**:
- `CadNumber` + `Revision` must be unique within the authority.
- `LifecycleState` transitions follow the CAD ItemType's lifecycle map.
- `CheckoutOwner` must be null if `LifecycleState` is `Released` or `In Review`.
- Only one active checkout allowed per CAD revision at a time.
- Released CAD revision content is immutable.
- `LockToken` is required for check-in or release-checkout operations.

### Part-CAD Revision Pair (MVP)

| Concept | Description |
|---------|-------------|
| Cardinality | One Part Revision linked to one **primary** CAD Revision. This is MVP-specific; a Part Revision may link to zero or more CAD revisions in the future. |
| Creation | Both created atomically via a single authority operation (Start New Revision). |
| Release | Both released atomically via a single authority operation (approval). |
| Mutation | Checkout, check-in, and native-content editing affect only the CAD Revision. The Part Revision is not directly mutated. |
| Lifecycle | Each retains separate lifecycle identities and maps (per ADR-0009). Coordination occurs only at policy-defined operations. |
| Constraints | Only one current working pair per released pair. The authority must reject concurrent duplicate creation. |

### Checkout Session (transient)
| Field | Type | Description |
|-------|------|-------------|
| CadId | string | Authority identifier of the checked-out CAD revision. |
| LockToken | string | Authority-issued lock token. |
| CheckoutTime | DateTime | When checkout began. |
| LocalFilePath | string | Path to local writable copy. |
| BaselineHash | string (SHA256) | Content hash at checkout time (for change detection). |
| Owner | string | User who checked out. |

**Note**: Recovery copy fields are NOT part of the checkout session. Recovery is a Workspace-only concern (see `IRecoveryCopyService` in Workspace).

### Recovery Copy Record (Workspace)
| Field | Type | Description |
|-------|------|-------------|
| RecoveryId | string (GUID) | Unique identifier. |
| CadId | string | Authority identifier of the CAD revision whose file was recovered. |
| SourcePath | string | Path where the working file was at cancellation time. |
| BackupPath | string | Full path to the recovery copy file (under `<workspace>/.idea-pdm/recovery/`). |
| SourceHash | string (SHA256) | Content hash of the source before backup. |
| BackupHash | string (SHA256) | Content hash of the recovery copy after write (verified). |
| CreatedAt | DateTime | When the recovery copy was created. |
| RetentionUntil | DateTime | Date after which this copy may be automatically cleaned (created + 30 days). |

### ChangeSet (existing)
| Field | Type | Description |
|-------|------|-------------|
| ChangeSetId | string | Unique identifier. |
| BaselineRef | string | Reference to workspace baseline. |
| CadRevisionIds | string[] | The CAD revisions included in this change. |
| Author | string | User who checked in. |
| Reason | string | Required human-readable change reason. MUST be persisted to the ChangeSet (not silently discarded). |
| ValidationResult | string | Validation outcome. |
| ContentHashes | string[] | SHA256 hashes of uploaded native content (calculated before upload). |
| Outcome | string | Success / Failure. |
| Timestamp | DateTime | When the ChangeSet was completed. |

**Pre-upload validation**: Before upload, the local file MUST be confirmed to exist, be readable, and have its SHA-256 calculated. The upload MUST use only the validated file. Any authority-side checksum or integrity behavior requires evidence verification (GATE-B-checkin) — it must not be assumed.

### CadReleaseEligibilitySnapshot (Core — input to ICadReleaseEligibility)

| Field | Type | Description |
|-------|------|-------------|
| CadId | string | Authority identifier of the CAD revision. |
| PartId | string | Authority identifier of the linked Part revision. |
| CadState | string | Current CAD lifecycle state (read by orchestration layer before calling eligibility check). |
| PartState | string | Current Part lifecycle state (read by orchestration layer). |

**Note**: This is NOT an Aras-specific type. It is a plain data snapshot used to keep `ICadReleaseEligibility` backend-neutral. The orchestration layer (Desktop) reads current states and populates this snapshot.

### RecoveryCopyResult (Workspace)

| Field | Type | Description |
|-------|------|-------------|
| Succeeded | bool | True if backup was created and hash-verified. |
| BackupPath | string | Full path to the verified recovery copy (valid only when Succeeded=true). |
| ErrorMessage | string | Human-readable failure reason (valid only when Succeeded=false). |
| SourceHash | string (SHA256) | Content hash of the source file before backup. |
| BackupHash | string (SHA256) | Content hash of the backup file after write (verified equal to source or error). |

### Review Submission
| Field | Type | Description |
|-------|------|-------------|
| SubmissionId | string (GUID) | Unique identifier. |
| CadRevisionId | string | Authority identifier of the submitted CAD revision. |
| PartRevisionId | string | Authority identifier of the linked Part revision. |
| SubmittedBy | string | Engineer who submitted. |
| AssignedReviewer | string | Reviewer assigned to this submission. |
| ChangeDescription | string | Engineer's description of changes. |
| Status | enum (Pending / Approved / ReworkRequested / Withdrawn) | Current status. |
| SubmittedAt | DateTime | When submitted. |
| DecidedAt | DateTime? | When a decision was made. |
| DecisionReason | string | Reviewer's reason for approval or rework request. |

**Authority boundary**: `SubmittedBy` and `AssignedReviewer` are domain fields, but the current authority-neutral operation context does not populate them. Their verified transport/source mapping is required by GATE-RS and GATE-W-owner; checkout lock ownership must not be substituted for `SubmittedBy`.

## State Transition Rules

### CAD Lifecycle (known states from existing source; verified per environment)

```
Khoi tao
  → StartDetailedDesign → Thiet ke chi tiet

Thiet ke chi tiet
  → SubmitForReview → In Review
  → [Checkout/Check-in] → (stays at Thiet ke chi tiet)

In Review
  → Approve → Released   (coordinated with Part — single authority operation)
  → RequestRework → Thiet ke chi tiet
  → Withdraw → Thiet ke chi tiet

Released
  → StartNewRevision → Khoi tao (new Part Revision + new primary CAD Revision — single authority operation)

Released (immutable — no direct transitions out)
```

### Part Lifecycle (requires Aras environment evidence — state names may differ from CAD)

```
[Aras evidence needed]
  → AnyApprovedLifecycleEvent → Released (coordinated with CAD via single authority operation)

Released (immutable — no direct transitions out; Part is released atomically with its primary CAD)
```

**Note**: Part lifecycle state names, transitions, and semantic roles must be captured from the verified Aras environment before implementation (research.md §1, evidence gate). The Part lifecycle exists independently of the CAD lifecycle; the system coordinates only at the policy-defined Start New Revision and Release operations.

## Validation Rules from Spec Requirements

| Rule | FR Source | Description |
|------|-----------|-------------|
| Exclusive checkout | FR-002 | One active checkout per CAD revision at a time. |
| Atomic release | FR-007, FR-020 | Single authority operation releases both CAD and Part; both succeed or neither does. |
| Atomic new revision | FR-011, FR-021 | Single authority operation creates both Part Revision and CAD Revision; both succeed or neither does. |
| Recovery before unlock | FR-004 | If modified, recovery copy must be created and verified before authority unlock. |
| State eligibility | FR-012 | Each action gated by current lifecycle state. Part and CAD have separate state mappings. |
| Released immutable | FR-010 | No modification of Released revisions (either Part or CAD). |
| Audit trail | FR-017 | All lifecycle transitions logged immutably. |
| Role-based action | FR-014, FR-015 | Admin configures which roles perform which actions. |
| Safe failure | FR-018 | Communication failures don't leave partial state. |
| Withdraw before decision | FR-006 | Engineer may withdraw own submission before reviewer acts; no review decision recorded. |
| Reviewer reassignment | FR-009 | PDM Administrator may reassign pending review to another eligible reviewer. |
| Notification on state change | FR-005, FR-007, FR-008 | Authority notifies reviewer on submit, engineer on approve/rework. |
| No unauthorized reassign | FR-009, FR-014 | Only PDM Administrator role may reassign; non-admin attempt is blocked. |
