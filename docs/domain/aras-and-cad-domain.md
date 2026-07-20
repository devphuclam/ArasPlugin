# Aras and CAD Domain Reference

The backend-neutral IDEA PDM model is defined in [IDEA PDM Domain Model](idea-pdm-domain-model.md). This file records the current Aras and CAD evidence boundary for that model.

## Stable domain knowledge

The application connects local CAD workspaces with Aras Innovator PDM workflows. A Part can be associated with CAD and Document records, and current client workflows also represent BOM, library, and file relationships.

Aras schema names, lifecycle behavior, permissions, server-method deployment, and remote protocol details are facts only when supported by current source or verified environment evidence. Environment snapshots remain evidence, not domain invariants.

## CAD domain

IronCAD scenes and native CAD files are handled through the IronCAD adapter and desktop launch services. CAD workflows include read-only opening, checkout/check-in, revision-related operations, and file transfer where represented by current contracts.

Part, CAD, and Document lifecycle identities remain scoped to their verified Aras ItemTypes and lifecycle maps. The MVP may coordinate a Part revision and linked CAD revision at Start New Revision and Release approval, but it must not infer Part eligibility from CAD state names.

The checked-in source copy of `idea_ReviseCad` demonstrates the intended paired revision operation, but its deployed behavior and transactional guarantees remain environment evidence to verify. Sequential `version`, relationship, or state operations do not by themselves prove atomicity.

## Evidence boundary

The detailed schema and capability evidence remains in archived `docs/archive/legacy-ai-work-kit/docs/ai/04_ARAS_SCHEMA_MAP.md` and `docs/archive/legacy-ai-work-kit/docs/ai/bom/BOM-00-ICAPI-CAPABILITY-REPORT.md`. Unknown behavior must be marked `Not yet established`.

## Current live snapshot versus IDEA target

The 2026-07-20 read-only live snapshot found that the active CAD ItemType uses `Custom CAD Document` and the active Part ItemType uses `Custom Part`. Both active maps contain the core states `Khoi tao`, `Thiet ke chi tiet`, `In Review`, and `Released`; each map also has additional states such as change, superseded, obsolete, or rejected/manufacturing states.

This is an environment configuration gap, not a reason to change the backend-neutral IDEA model. The initial IDEA target uses the same semantic lifecycle roles for Part and CAD, with independent adapter mappings. See [ADR-0011](../adr/0011-configurable-initial-lifecycle-profile.md) and the [live evidence note](../evidence/live-aras-readonly-observations-2026-07-20.md).

The live Server Methods also show why authority operations require separate evidence: approval is currently CAD-only, revision creation performs multiple operations, check-in does not show a custom ChangeSet write, and rework invokes a Part-synchronization helper. These behaviors remain environment facts and are not domain invariants.
