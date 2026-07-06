# Part Library Documentation Cleanup Design

## Goal

Prepare a small, unambiguous documentation surface for the incoming Part Library Phase 2 package while preserving the completed Phase 1 decisions and evidence.

## Canonical Location

`IdeaCadConnector/docs/part-library` becomes the only active Part Library documentation root.

The duplicate package under `ARAS-Plugin/docs/part-library` is migration input only and will be removed after its valuable content is consolidated.

## Final Structure

```text
docs/part-library/
  README.md
  phase-1/
    DESIGN.md
    DEPLOYMENT.md
    ACCEPTANCE.md
    FINAL-STATUS.md
  phase-2/
    README.md
  references/
    mockups/
    schemas/
```

The broader PDM source of truth at `docs/core/IDEA-PDM-DESIGN-MASTER.md` remains unchanged.

## Consolidation Rules

- `README.md` is the only navigation entry point and clearly labels Phase 1 as completed and Phase 2 as not yet imported.
- Phase 1 status records commit `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c`, clean build, and `117/117` passing tests.
- `DESIGN.md` retains authoritative business rules, architecture decisions, data model, contracts, workspace integration, risks, and deferred scope.
- `DEPLOYMENT.md` retains the actual ItemTypes, properties, Methods, permissions, lifecycle setup, and deployment cautions.
- `ACCEPTANCE.md` retains automated evidence and the live/manual acceptance checklist.
- `FINAL-STATUS.md` replaces historical phase/task trackers with one concise completed-state snapshot.
- Mockups and CSV/JSON schemas remain references and are not mixed with active instructions.
- Phase 2 receives only an intake boundary document. No speculative Phase 2 implementation details are presented as completed work.

## Removal Rules

Remove documents that are duplicated, obsolete, encoding-corrupted, or merely historical execution logs:

- numbered Part Library files superseded by the four consolidated Phase 1 documents;
- stale `STATUS.md` and implementation task lists;
- duplicate Stage 1 deployment guides;
- obsolete repository audits, handoffs, expansion plans, and pilot checklists after any still-valid rule is incorporated;
- this temporary cleanup design after the cleanup is committed, because Git history preserves it.

## Verification

- Search active docs for stale test counts such as `42/42` and `68/68`.
- Search for contradictory Phase 1/Phase 2 completion claims.
- Verify every relative Markdown link resolves.
- Verify exactly one active Part Library documentation root remains.
- Confirm no production source, Aras Method, mockup, or schema is modified by the cleanup.
