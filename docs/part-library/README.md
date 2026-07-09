# Part Library Documentation

This directory is the only active documentation root for Part Library.

## Phase Status

| Phase | State | Baseline | Completion | Evidence |
|---|---|---|---|---|
| Phase 1 - Core governance and reuse | `COMPLETE` | `08a9986d9cc867a2948afe5a56676730ada54fe4` | `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c` | Build: 0 warnings/errors; tests: 117/117; owner accepted 2026-07-06 |
| Phase 2 - Complete Library UX | `COMPLETE` | `956af6841392b609d9c06df60d484fe5244500c1` | Sprint 2.1–2.4 all accepted; final live App UAT accepted 2026-07-08; role alignment confirmed with actual organization roles | All 4 sprints implemented and accepted. Sprint 2.3 live CAD lookup fixed via `idea_GetPrimaryIronCadForPart`. Sprint 2.4 filters, sorting, detail UX hardening, command state hardening, localization. Official roles: TPTKC (manager), TNTKC (reviewer), NVTKC (contributor), NVLCR (assembly viewer), PM (project viewer), Khách hàng (external viewer). Debug/Release build passed; tests 403/403 pass |
| Phase 3 - Deployment and Production Hardening | `IN_PROGRESS` | `35494964519e014ee60e573a3db718770668ba8c` | Sprint 3.1 release packaging accepted; Sprint 3.2 config package UAT accepted; Sprint 3.3 internal install/UAT hardening next | Release packaging, environment config model with safe loader/validation/template, Aras deployment checklist, rollback, checksums. Sprint 3.2: config template in package, active config excluded, secret detection, role defaults, no-crash with/without config. Package UAT all PASS. |

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
