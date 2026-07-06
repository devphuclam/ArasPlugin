# Part Library Phase 2 Intake

**State:** `NOT STARTED`

Phase 2 has not been imported, approved, or implemented. This folder exists only to receive and evaluate the next package without mixing it with completed Phase 1 evidence.

## Intended Product Boundary

The previously deferred UX candidates are:

- Create, edit, and archive Library;
- Aras Part picker;
- Move Entry target dialog;
- revision browser and explicit `LatestCurrent` UI;
- Vault CAD download and open;
- Open in Aras;
- CAD, BOM, and Revisions tab content;
- additional Entry filtering and production UX hardening.

This list is an intake boundary, not an approved commitment. The Phase 2 package must be compared with current code before scope is accepted.

## Package Destination

Place incoming files under:

```text
docs/part-library/phase-2/incoming/
```

Do not copy incoming status, task, or handoff files into the documentation root.

## Entry Criteria

Phase 2 may move to `PLANNED` only after:

- the package is inventoried;
- assumptions are checked against commit `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c` or its verified successor;
- duplicate and obsolete documents are rejected;
- scope and non-goals are approved;
- build/test baseline is recorded;
- acceptance criteria and live Aras dependencies are explicit.

## Isolation Rules

- Do not edit `phase-1/` to track Phase 2 work.
- Do not change Phase 1 ItemTypes or Methods merely to match a new document.
- Do not claim a Phase 2 feature exists until code and evidence confirm it.
- Keep temporary package files under `incoming/` and remove them when Phase 2 closes.

See [Phase Governance Rules](../../PHASE-GOVERNANCE.md).
