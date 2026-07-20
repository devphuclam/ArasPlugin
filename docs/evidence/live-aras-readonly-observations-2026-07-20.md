# Live Aras Read-Only Observations — 2026-07-20

## Scope

This note records observations retrieved through read-only OData GET requests from the configured Aras Innovator Community Edition environment. No promote, edit, add, delete, version, or business Server Method operation was invoked during this inspection. Credentials and bearer tokens are intentionally excluded.

These observations are evidence for specification decisions; they are not permission to enable gated runtime behavior.

## ItemTypes and lifecycle maps

- `CAD` exists as a versionable ItemType (`is_versionable=1`, manual versioning enabled).
- `Part` exists as a versionable ItemType.
- The active `CAD` ItemType is associated with the lifecycle map `Custom CAD Document`.
- The active `Part` ItemType is associated with the lifecycle map `Custom Part`.
- The default lifecycle maps named `CAD` and `Part` also exist, but they are not the maps currently assigned to the active ItemTypes.

### Active custom CAD states

`Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `Loai bo`, `In Change`, and `Superseded`.

### Active custom Part states

`Khoi tao`, `Thiet ke chi tiet`, `In Review`, `Released`, `Che tao`, `Nhan hang`, `In Change`, `Superseded`, and `Obsolete`.

The corrected live query confirms that the active custom Part map contains the same four core design/review/release state names used by the active custom CAD map. The maps still have additional states and separate transition graphs, so the application should retain separate policies rather than assume every state has identical meaning.

## Live CAD workflow methods

The following `idea_` Server Methods were found: `idea_StartDetailedDesign`, `idea_SubmitCadForReview`, `idea_ApproveCadReview`, `idea_RequestCadRework`, `idea_ReviseCad`, and `idea_CommitCadCheckin`, among others.

Observed behavior from the method source:

- `idea_StartDetailedDesign`: CAD-only promotion from `Khoi tao` to `Thiet ke chi tiet`.
- `idea_SubmitCadForReview`: CAD-only promotion from `Thiet ke chi tiet` to `In Review`; it does not accept a reviewer identity.
- `idea_ApproveCadReview`: requires CAD `In Review` and promotes CAD to `Released`. The active CAD ItemType has a `Server Event` row for `onAfterPromote` whose related Method is `Sync_Part_From_CAD`; therefore the approval path indirectly synchronizes/promotes Part after the CAD promotion.
- `idea_CommitCadCheckin`: validates the CAD lock owner, updates the CAD file and metadata, unlocks CAD, and returns the CAD; no ChangeSet creation or explicit custom audit record was observed in the method source.
- `idea_RequestCadRework`: promotes CAD back to `Thiet ke chi tiet` and then invokes `Sync_Part_From_CAD`.
- `idea_ReviseCad`: versions Part and CAD separately, clears inherited Part-CAD relationships, then creates the new link. The method source does not establish a single transaction or rollback boundary.

`Sync_Part_From_CAD` is an existing coordination helper. It can promote Part toward the CAD state and, when CAD is `Thiet ke chi tiet` while Part is in another state, it can create a new Part version before promoting that version. Approval reaches it indirectly through CAD `onAfterPromote`. Rework also calls it explicitly after CAD promotion, so rework may reach the helper through both the Server Event and the explicit method call; this should be verified for idempotency and duplicate side effects.

The Part ItemType has its own `onAfterPromote` event list, including `BOM_Auto_Promote_Parent`, but the inspected Part event list does not contain `Sync_Part_From_CAD`. The synchronization direction observed is therefore CAD promote → CAD `onAfterPromote` → Part sync.

The project also contains server-owned primary-CAD methods. `idea_EnsurePrimaryIronCadPartCad` identifies an IronCAD `Mechanical/Part` CAD, reuses or creates/links one when needed, and `idea_GetPrimaryIronCadForPart` selects a linked CAD by a deterministic priority. These methods reduce client-side ambiguity. `Sync_Part_From_CAD` itself still reads the first `Part CAD` relationship row, so its direct selection behavior should remain aligned with the primary-CAD rule.

## Part synchronization side effect

`Sync_Part_From_CAD` is the deployed coordination helper used by the rework path. Product owner confirmation establishes the accepted business result for MVP: Part returns to `Thiet ke chi tiet`, no new Part version is created, and duplicate Sync invocation is a no-op. The application models this as coordinated state-only rework while retaining separate Part/CAD lifecycle policies.

## Review workflow and assignments

The live `CAD Approval Workflow` contains these relevant activity nodes:

- `NVTKC_Submit`
- `TNTKC_Review`
- `Phat_hanh`
- `Thiet_ke_chi_tiet` (labelled as a rework request)
- `Auto To In Review`

Read-only inspection of active `TNTKC_Review` activity instances showed assignment to the `TNTKC` identity. This supports deriving reviewer authority from the active Aras workflow assignment rather than using a hard-coded reviewer name or selecting an arbitrary reviewer in the client.

## History and ChangeSet observation

- The standard `History` ItemType exists and contains immutable-looking historical entries with actions such as `Add` and `Update`, item id, item state, and item version.
- `CAD Changes` and `Part Changes` relationship ItemTypes exist, but no rows were returned in the sampled collection query.
- No ItemType named `ChangeSet` was found in the inspected ItemType names, and no obvious custom `idea_` ChangeSet method was found in the inspected method list.
- This does not prove that no ChangeSet mechanism exists elsewhere; it means the current evidence is insufficient to require a custom ChangeSet record in the feature contract.

## Consequences for Feature 003

Before the product-owner decisions, the live evidence conflicted with these assumptions if they remained unconditional:

1. Part and CAD have identical complete lifecycle maps.
2. CAD approval has no Part coordination at all.
3. Check-in always creates a ChangeSet.
4. Rework changes only CAD.
5. Start New Revision is atomic across Part and CAD.
6. The client may choose the reviewer independently of the Aras workflow assignment.

The product owner resolved the reviewer-assignment and rework-policy questions. Remaining authority evidence gates still control runtime enablement and compliance claims.

## Decisions and recommendations after product discussion

- `Custom Part` is not yet the official IDEA product contract, but its live map contains the same core IDEA states as `Custom CAD Document`. IDEA wants to keep that initial profile while retaining configurable per-ItemType mappings. See ADR-0011.
- Reviewer handling should stay simple and replaceable. The client should consume an authority assignment/provider and must not hard-code a person or invent a reviewer property.
- The current approval path already has paired coordination through `CAD.onAfterPromote → Sync_Part_From_CAD`. Recommendation: preserve this design if desired, but failure-test whether the CAD promote and the Server Event are one atomic authority transaction; do not infer rollback from the event name alone.
- The current rework method explicitly calls `Sync_Part_From_CAD` after promoting CAD, while the same CAD `onAfterPromote` event also calls it. Product owner confirmation says the duplicate path is a no-op; retain this as a regression check for future server changes.
- The current revision method is a single request from the client but not proven atomic internally. Recommendation: add a deployed failure/concurrency test and, if needed, implement an explicit authority transaction boundary before enabling it.
- The product owner explicitly accepts the rework side effect as coordinated state-only behavior for MVP; no new Part version is created. Full deployed result and audit evidence remain separate work.
- Standard Aras `History` is the recommended first audit source for MVP. A custom ChangeSet should be added only if History cannot provide the required structured reason, baseline, and synchronization outcome.

## Follow-up confirmation

- Product owner confirmed that the CAD `onAfterPromote` → `Sync_Part_From_CAD` path satisfies the required atomicity behavior for coordinated Part/CAD synchronization.
- Product owner confirmed that the duplicate Sync invocation during rework is a no-op on the live flow.
- Product owner confirmed that Request Rework returns Part to `Thiet ke chi tiet` without increasing the Part version.
- Product owner confirmed that Aras Assign/workflow assigns the reviewer; the client must not select or hard-code a reviewer.
