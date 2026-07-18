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
