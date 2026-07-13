# Part Library Documentation

This directory is the only active documentation root for Part Library.

## Phase Status

| Phase | State | Baseline | Completion | Evidence |
|---|---|---|---|---|
| Phase 1 - Core governance and reuse | `COMPLETE` | `08a9986d9cc867a2948afe5a56676730ada54fe4` | `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c` | Build: 0 warnings/errors; tests: 117/117; owner accepted 2026-07-06 |
| Phase 2 - Complete Library UX | `COMPLETE` | `956af6841392b609d9c06df60d484fe5244500c1` | Sprint 2.1–2.4 all accepted; final live App UAT accepted 2026-07-08; role alignment confirmed with actual organization roles | All 4 sprints implemented and accepted. Sprint 2.3 live CAD lookup fixed via `idea_GetPrimaryIronCadForPart`. Sprint 2.4 filters, sorting, detail UX hardening, command state hardening, localization. Official roles: ExampleManager (manager), ExampleReviewer (reviewer), ExampleContributor (contributor), ExampleAssemblyViewer (assembly viewer), ExampleProjectViewer (project viewer), Khách hàng (external viewer). Debug/Release build passed; tests 403/403 pass |
| Phase 3 - Deployment and Production Hardening | `COMPLETE` | `35494964519e014ee60e573a3db718770668ba8c` | Sprint 3.1 release packaging accepted; Sprint 3.2 config package UAT accepted; Sprint 3.3 internal installation package UAT accepted; Sprint 3.4 production readiness UAT accepted; Phase 3 complete | Release packaging, environment config model, installation hardening docs, validation/verification scripts, troubleshooting guide, IT handoff guide, UAT result template, production readiness docs, go/no-go checklist, release sign-off template, release manifest, known limitations, closeout plan. Final package: IdeaCadConnector-v0.3.0-rc1.zip. Debug/Release 0w/0e, 419/419 tests pass. |

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
- [Installation hardening](phase-3/INSTALLATION-HARDENING.md)
- [Machine readiness](phase-3/MACHINE-READINESS.md)
- [Troubleshooting](phase-3/TROUBLESHOOTING.md)
- [IT handoff guide](phase-3/IT-HANDOFF.md)
- [Internal UAT result template](phase-3/INTERNAL-UAT-RESULT-TEMPLATE.md)
- [Production readiness](phase-3/PRODUCTION-READINESS.md)
- [Go/No-Go checklist](phase-3/GO-NO-GO-CHECKLIST.md)
- [Release sign-off template](phase-3/RELEASE-SIGNOFF-TEMPLATE.md)
- [Release manifest](phase-3/RELEASE-MANIFEST-v0.3.0-rc1.md)
- [Known limitations](phase-3/KNOWN-LIMITATIONS.md)
- [Phase 3 closeout plan](phase-3/PHASE-3-CLOSEOUT-PLAN.md)

## Shared References

- [UI mockups](references/mockups/)
- [Schemas and examples](references/schemas/)

## Governance

All future phases follow [Phase Governance Rules](../PHASE-GOVERNANCE.md).

Phase 1 documents are closed evidence. Phase 2 work must not modify them except through a dated Errata entry. Phase 3 starts release packaging after Phase 2 functional/live UAT acceptance reported by the project owner, without rewriting Phase 2 history.
