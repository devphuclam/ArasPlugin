# Part Library Phase 2 README

**State:** `INTAKE`

## Objective

Plan the complete Part Library user experience that Phase 1 intentionally deferred, while preserving the existing reuse, lifecycle, and usage-tracking behavior that already works.

## Explicit Non-Goals

- no Phase 1 schema rewrite;
- no live Aras deployment claims from local analysis;
- no hidden expansion into unrelated PDM or CAD workflow redesign;
- no production code changes in this intake commit.

## Baseline

- planning baseline: `956af6841392b609d9c06df60d484fe5244500c1`
- depends on Phase 1 completion: `b7f6cf67d0d191ddb71b3e3926064d928ded2c8c`
- package location: [incoming/](incoming/)

## Current Owner

- current phase owner: Codex intake and planning
- implementation owner: not assigned in canonical docs until scope is approved

## Approved and Unresolved Decisions

| ID | Decision | Status | Note |
|---|---|---|---|
| `D-01` | who may create Libraries | `UNRESOLVED` | package recommends Library Manager only |
| `D-02` | duplicate active Entry rule | `UNRESOLVED` | package recommends `Library + part_config_id` |
| `D-03` | archived Library visibility | `UNRESOLVED` | package recommends hidden by default with opt-in view |
| `D-04` | move preserves metadata and lifecycle | `UNRESOLVED` | package recommends preserving both |
| `D-05` | Vault cache model | `UNRESOLVED` | package recommends per-user collision-safe cache |
| `D-06` | IronCAD open mechanism | `UNRESOLVED` | package recommends existing bridge before process launch |

Because these decisions remain unresolved, Phase 2 stays in `INTAKE`.

## Workstreams

| ID | Workstream |
|---|---|
| `WS1` | Library Management |
| `WS2` | Aras Part Picker |
| `WS3` | Move Entry |
| `WS4` | Revision Browser |
| `WS5` | Vault and IronCAD |
| `WS6` | Open in Aras |
| `WS7` | Detail Tabs |
| `WS8` | Filters and UX Hardening |

## Sprint Plan

| Sprint | Scope | Exit dependency |
|---|---|---|
| `2.1` | `WS1` + `WS2` | approve `D-01`, `D-02`, `D-03` |
| `2.2` | `WS3` + `WS4` | approve `D-04` |
| `2.3` | `WS5` + `WS6` + `WS7` | approve `D-05`, `D-06` |
| `2.4` | `WS8` + hardening/UAT prep | earlier sprints verified |

## Canonical Phase Documents

- [Design](DESIGN.md)
- [Deployment](DEPLOYMENT.md)
- [Acceptance](ACCEPTANCE.md)

## Package Intake Outcome

Accepted as source material:

- [Part_Library_Phase_2_Complete_User_Experience.docx](incoming/Part_Library_Phase_2_Complete_User_Experience.docx)

Retained as incoming-only helper material, not canonical:

- [Part_Library_Phase_2_Execution_Pack.docx](incoming/Part_Library_Phase_2_Execution_Pack.docx)
- [Part_Library_Phase_2_Prompt_Library.md](incoming/Part_Library_Phase_2_Prompt_Library.md)

Rejected as durable repository truth:

- agent-specific ownership and prompt workflow as project documentation;
- any suggestion that implementation may start before decision approval;
- any implication that live Aras or Vault behavior is already verified.

## Acceptance Gates

Phase 2 may move to `PLANNED` only after:

- baseline build/test evidence is recorded;
- unresolved decisions that affect the first sprint are approved;
- scope, non-goals, rollback, and live dependencies are explicit;
- no contradictory package content remains outside `incoming/`.

Current baseline evidence on `956af6841392b609d9c06df60d484fe5244500c1`:

- full test project: `117/117` passed;
- solution build: failed in the existing WPF temporary assembly path for `IdeaCadConnector.Desktop`;
- therefore Phase 2 planning may proceed, but implementation should not claim a clean baseline build until that blocker is resolved or explicitly accepted.

## Rollback Considerations

- this intake phase changes documentation only;
- future implementation work must preserve a revert path for desktop code and any new Aras artifacts separately;
- Phase 1 evidence remains closed and must not be rewritten to hide Phase 2 issues.

## Recommended Next Packet

After baseline verification and approval of `D-01`, `D-02`, and `D-03`, the first implementation packet should be:

`Sprint 2.1 core: Library CRUD contracts and paged Aras Part search`

See [Phase Governance Rules](../../PHASE-GOVERNANCE.md).
