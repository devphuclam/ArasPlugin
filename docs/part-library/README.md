# Part Library Documentation

This directory is the only active documentation root for Part Library.

## Phase Status

| Phase | State | Baseline | Completion | Evidence |
|---|---|---|---|---|
| Phase 1 - Core governance and reuse | `COMPLETE` | `08a9986d9cc867a2948afe5a56676730ada54fe4` | `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c` | Build: 0 warnings/errors; tests: 117/117; owner accepted 2026-07-06 |
| Phase 2 - Complete Library UX | `IN_PROGRESS` | `956af6841392b609d9c06df60d484fe5244500c1` | Sprint 2.1 UAT smoke accepted; Sprint 2.2 locally accepted; Sprint 2.3 App UAT accepted; Sprint 2.4 filter/hardening/sort implemented | Admin, lamEngineer, lamPM, and viewer smoke evidence recorded; Sprint 2.2 core backend + UI + follow-up permission patch; Sprint 2.3 core implementation + UI wiring + live CAD lookup fix via server method `idea_GetPrimaryIronCadForPart`; App UAT smoke accepted: CAD lookup acceptable, Part Library loads, CAD/BOM/Rev/WhereUsed tabs, Open in Aras, Download CAD, Open in IronCAD; Sprint 2.4: entry status/CAD status/text filters, 7-column sort, detail status UX hardening, command state regression, 11 new tests; Debug/Release build passed; tests 403/403 pass |
| Phase 3 - Deployment and Production Hardening | `IN_PROGRESS` | `35494964519e014ee60e573a3db718770668ba8c` | Sprint 3.1 release packaging baseline in progress | Release packaging, environment config guidance, Aras deployment checklist, rollback, checksums, and internal package UAT for `v0.3.0-rc1` |

## Read Phase 1

- [Design and business rules](phase-1/DESIGN.md)
- [Aras deployment](phase-1/DEPLOYMENT.md)
- [Acceptance evidence](phase-1/ACCEPTANCE.md)
- [Final status](phase-1/FINAL-STATUS.md)

## Prepare Phase 2

- [Phase 2 intake boundary](phase-2/README.md)
- [Phase 2 design and work breakdown](phase-2/DESIGN.md)
- [Phase 2 deployment assumptions](phase-2/DEPLOYMENT.md)
- [Phase 2 acceptance gates](phase-2/ACCEPTANCE.md)

## Prepare Phase 3

- [Phase 3 release packaging boundary](phase-3/README.md)
- [Phase 3 release packaging design](phase-3/DESIGN.md)
- [Phase 3 deployment guidance](phase-3/DEPLOYMENT.md)
- [Phase 3 acceptance gates](phase-3/ACCEPTANCE.md)
- [Release packaging process](phase-3/RELEASE-PACKAGING.md)
- [Environment configuration guidance](phase-3/ENVIRONMENT-CONFIGURATION.md)
- [Rollback guidance](phase-3/ROLLBACK.md)
- [UAT checklist](phase-3/UAT-CHECKLIST.md)

## Shared References

- [UI mockups](references/mockups/)
- [Schemas and examples](references/schemas/)

## Governance

All future phases follow [Phase Governance Rules](../PHASE-GOVERNANCE.md).

Phase 1 documents are closed evidence. Phase 2 work must not modify them except through a dated Errata entry. Phase 3 starts release packaging after Phase 2 functional/live UAT acceptance reported by the project owner, without rewriting Phase 2 history.
