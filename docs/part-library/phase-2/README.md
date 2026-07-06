# Part Library Phase 2 README

**State:** `PLANNED`

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

## Business Roles

| ID | Persona | Role |
|---|---|---|
| NVTKC | Mechanical Designer | Library Contributor |
| TNTKC | Mechanical Team Leader | Library Reviewer |
| Trưởng phòng thiết kế cơ | Mechanical Design Department Manager | Library Manager |
| Quản lý dự án | Project Manager | Project and Usage Viewer |
| Nhân viên lắp ráp cơ | Mechanical Assembly User | Manufacturing Viewer |
| Khách hàng | Customer | External Viewer |

## Approved Permissions

### NVTKC (Library Contributor)

May:
- view permitted Active Libraries
- search Parts
- add Draft Entries
- edit permitted Draft Entry metadata
- submit Entries for review
- reuse valid approved Library Parts in PDM Projects
- view permitted CAD, BOM, revisions, and Where Used

May not:
- create, edit, archive, or restore a Library
- publish or deprecate an Entry unless separately assigned
- manage permissions

### TNTKC (Library Reviewer)

May:
- perform all NVTKC actions
- review Entries
- publish Entries
- request rework
- manage Entries in the assigned team scope
- move Entries when permitted

May not:
- archive or restore a Library
- modify global Library permissions

### Trưởng phòng thiết kế cơ (Library Manager)

May:
- create, edit, archive, and restore Libraries
- move and remove Entries
- publish and deprecate Entries
- manage Library content and business exceptions

Only Trưởng phòng thiết kế cơ may create, edit, archive, or restore a Library.

### Quản lý dự án (Project and Usage Viewer)

Read-only for:
- permitted Libraries
- Published Entries
- BOM
- Usage
- Where Used
- revision and project-impact information

### Nhân viên lắp ráp cơ (Manufacturing Viewer)

May view only:
- Published production-relevant Parts
- released BOM
- approved drawings and assembly documents
- approved CAD or neutral files where permission allows

### Khách hàng (External Viewer)

May view only:
- explicitly shared Published data
- approved revisions
- approved drawings and delivery documents

Must not see:
- Draft or PendingReview data
- internal Usage
- internal notes
- source_project
- source_commit
- unrestricted native CAD
- other projects' or customers' data

### Admin Account

| Field | Value |
|---|---|
| Username | `admin` |
| Password | `innovator` |
| Permission | Full — bypasses all role-based restrictions |

## Approved Decisions

| ID | Decision | Status | Approved Rule |
|---|---|---|---|
| `D-01` | who may create Libraries | `APPROVED` | Only Trưởng phòng thiết kế cơ (Library Manager) |
| `D-02` | duplicate active Entry rule | `APPROVED` | Unique on `Library ID + part_config_id`, case-insensitive. Active statuses: Draft, PendingReview, Published. Deprecated is not active. |
| `D-03` | archived Library visibility | `APPROVED` | Hidden by default; explicit Archived or All filter to display. Read-only, no new Entries, not selectable as Move or Part Picker target. |
| `D-04` | move preserves metadata and lifecycle | `APPROVED` | Preserve related Part identity, part_config_id, revision_policy, pinned_part_id, pinned_revision, lifecycle state, entry_status, category, tags, note, source_project, source_commit. Block with clear error if lifecycle cannot be preserved safely. |
| `D-05` | Vault cache model | `APPROVED` | Per-Windows-user cache keyed by Aras server, database, File ID, and revision/generation. Download to temp first; reject zero-byte; validate extension; move atomically; clean temp on failure. |
| `D-06` | IronCAD open mechanism | `APPROVED` | Preferred: existing bridge/connector. Fallback: process launch only when local file exists, readable, >0 bytes, approved extension, from verified Vault download or trusted workspace. |

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
| `2.1` | `WS1` + `WS2` | D-01, D-02, D-03 APPROVED |
| `2.2` | `WS3` + `WS4` | D-04 APPROVED |
| `2.3` | `WS5` + `WS6` + `WS7` | D-05, D-06 APPROVED |
| `2.4` | `WS8` + hardening/UAT prep | earlier sprints verified |

All decisions D-01 through D-06 are APPROVED as of 2026-07-06. Each sprint may begin as soon as its predecessor is complete.

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

Phase 2 moved to `PLANNED` on 2026-07-06 after:

- baseline build/test evidence recorded and verified;
- all decisions D-01 through D-06 are APPROVED;
- scope, non-goals, rollback, and live dependencies are explicit;
- no contradictory package content remains outside `incoming/`.

Current baseline evidence on `956af6841392b609d9c06df60d484fe5244500c1`:

- Debug solution build: 0 warnings, 0 errors
- full test project: `117/117` passed;
- WPF temporary assembly build blocker diagnosed and fixed (stale `.g.i.cs` cache — see TASK 2 in implementation logs);
- regression protection added for WPF build configuration.

## Rollback Considerations

- this intake phase changes documentation only;
- future implementation work must preserve a revert path for desktop code and any new Aras artifacts separately;
- Phase 1 evidence remains closed and must not be rewritten to hide Phase 2 issues.

## Recommended Next Packet

All decisions D-01 through D-06 are APPROVED. The first implementation packet is:

`Sprint 2.1 core: Library CRUD contracts and paged Aras Part search`

See [Phase Governance Rules](../../PHASE-GOVERNANCE.md).
