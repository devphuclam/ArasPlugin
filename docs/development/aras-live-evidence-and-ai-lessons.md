# Aras Live Evidence and AI Lessons

This document is a durable handoff for Codex, OpenCode, and future Spec Kit sessions. It records mistakes that previously produced unsafe assumptions and the checks required to avoid repeating them.

## Lessons from the live inspection

1. **Never infer the active lifecycle from default Aras maps.** Query the ItemType-to-lifecycle relationship first. The live CAD and Part ItemTypes use `Custom CAD Document` and `Custom Part`, while default `CAD` and `Part` maps also exist.
2. **Never use one shared raw state enum for Part and CAD.** The IDEA target may use the same semantic roles, but each ItemType needs an independent mapping and policy.
3. **Server Method source is not transaction evidence.** A method that calls `apply()` several times, or has an “atomic” comment, does not prove rollback or all-or-nothing behavior. Verify failure behavior in the deployed environment.
4. **A single REST request is not automatically an atomic business operation.** `idea_ReviseCad` is one method call from the client but performs multiple server-side item operations. Its atomicity must be demonstrated separately.
5. **Do not confuse Aras History with a custom ChangeSet.** Standard History exists in the live system. A custom ChangeSet is an additional product concept and should be required only if the audit/reconciliation use case needs structured data that History cannot provide.
6. **Workflow-map templates and runtime activities are different evidence.** Reviewer assignment must be read from the active runtime Activity/Assignment or an authority contract, not guessed from a map node or hard-coded in the client.
7. **A helper method can hide cross-item side effects.** `Sync_Part_From_CAD` is called by CAD rework and can promote or version Part. Review the whole call graph before describing an operation as CAD-only.
8. **The complete caller path includes ItemType Server Events.** `idea_ApproveCadReview` does not call `Sync_Part_From_CAD` textually, but CAD `onAfterPromote` invokes it. Always inspect ItemType Server Events in addition to Server Method source.
9. **Do not mark a gate complete from source inspection alone.** Record source evidence, deployed behavior, test setup, observed result, and remaining uncertainty separately.
10. **Validate OData filters and relationship paths before drawing lifecycle conclusions.** The first Part lifecycle query was read from the wrong result set; the corrected query followed `Part ItemType → ItemType Life Cycle → Custom Part → Life Cycle State` and showed that the core states are present. A single malformed or mis-scoped query must never become a domain decision.

11. **Role configuration is not identity inference.** Resolve Design Engineer, Reviewer, Project Manager, and PDM Administrator only from an explicit configured role source. An unknown or multiply-matched user must fail closed; never promote a username to a role because it looks familiar or because an old test used it.
12. **Aras Administrator is not automatically the business owner.** On the controlled
    fixture, `Create New Revision` was rejected with `You must be a member of the Owner
    identity to perform this action.` Preserve this authority rejection. The desktop may
    expose the explicitly configured PDM Administrator's development review override when
    no reviewer assignment exists, but that override must never simulate Aras ownership or
    suppress an authority rejection.
13. **Do not confuse ItemType versioning discipline with the PDM revision policy.** The
    live `Part` ItemType is Automatic while `CAD` is Manual. Editing the Part produced
    Part B while the linked CAD remained A. Automatic/Manual is an authority setting;
    paired revision creation must remain an explicit operation that creates both sides.
14. **Manual versioning is not the same as immutability.** A Manual ItemType may avoid
    automatic revision advancement on save, but Released update/lock permissions still
    need to block direct edits. The product must enforce Released immutability separately.

## Required Spec Kit handoff procedure

Before `/speckit.plan` or `/speckit.implement`:

1. Read `AGENTS.md`, `CONTEXT.md`, the applicable `docs/domain/*`, ADRs, and the feature's `research.md`, `spec.md`, `plan.md`, and `tasks.md`.
2. Read the live observation note and every evidence gate referenced by the feature.
3. Treat `Not yet established`, `PENDING`, and `PARTIAL` as blockers for claims and UI enablement, not as permission to guess.
4. Keep semantic lifecycle policy separate from Aras state names and transport operations.
5. Use one writer at a time: OpenCode implements approved tasks; Codex reviews and records findings.
6. After a live change, re-query the deployed Server Method and lifecycle configuration. Do not assume repository source and live deployment are synchronized.
7. Never place credentials, bearer tokens, or environment secrets in evidence, prompts, commits, or logs.

## Current recommendations

- Configure or explicitly map the live Part lifecycle to the IDEA target profile before enabling Part-dependent actions.
- Replace CAD-only approval with a dedicated authority operation that validates and releases the Part-CAD pair atomically.
- Make rework policy explicit: either coordinate Part and CAD intentionally, or remove the Part synchronization side effect. Do not leave it as an accidental helper call.
- Keep Start New Revision behind a deployed failure/concurrency test proving no partial Part-CAD pair is left behind.
- Use standard Aras History as the initial audit source; add a structured ChangeSet only after identifying a concrete audit or synchronization requirement it must satisfy.
- Keep the reviewer mechanism simple behind `IReviewerProvider`: use the active Aras assignment when available, without hard-coded identity or premature reassignment UI.
- Refresh an inactive Aras item after a server-side side effect. During the
  controlled release fixture test, the Part form initially still displayed
  `Khoi tao` after CAD reached `Released`; an explicit Part Refresh then
  showed `Released`. A stale item form is not evidence that a server event
  failed.
- Test the complete event path, not only the called method. The controlled
  fixture confirmed `CAD In Review -> Released`, followed by CAD
  `onAfterPromote`, `Sync_Part_From_CAD`, and refreshed Part `Released`.
- Keep the bounded Feature 003 lifecycle at
  `Khoi tao -> Thiet ke chi tiet -> In Review -> Released`; do not infer or
  enable post-`Released` states from the broader Aras map.
