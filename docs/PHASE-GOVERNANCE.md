# Phase Governance Rules

This file defines how project phases are created, tracked, completed, and handed over. It applies to every future package, regardless of which agent or developer performs the work.

## 1. One Source of Truth

- `docs/<feature>/README.md` is the only navigation and status entry point for a feature.
- Every fact belongs to one canonical document. Do not copy status tables, task lists, setup instructions, or acceptance results into multiple files.
- Code and live-system evidence override old planning documents when they disagree.
- Agent-named handoff files are forbidden. Context must be written as project state, not as a conversation between tools.

## 2. Phase States

Only these states are allowed:

| State | Meaning |
|---|---|
| `NOT STARTED` | No approved scope or implementation work exists. |
| `INTAKE` | A package has arrived and is isolated for review. |
| `PLANNED` | Scope, design, risks, and acceptance criteria are approved. |
| `IN PROGRESS` | Implementation is active. |
| `VERIFICATION` | Implementation is frozen while build, tests, and live checks run. |
| `BLOCKED` | Work cannot continue; the blocker and owner are recorded. |
| `COMPLETE` | Code, evidence, documentation, and owner acceptance are present. |

A phase may move backward when verification fails. It may not skip from `NOT STARTED` to `COMPLETE`.

## 3. Opening a Phase

Before a phase becomes `IN PROGRESS`, its README must record:

- objective and explicit non-goals;
- baseline commit;
- package/source location;
- affected systems and expected files;
- dependencies on previous phases;
- acceptance criteria;
- rollback or recovery considerations;
- current owner.

Incoming material goes under `phase-N/incoming/`. It must not overwrite completed-phase documentation or production code before review.

## 4. Working in a Phase

- Keep one active task list inside the phase README or approved implementation plan.
- Update status only when evidence changes.
- Record decisions, not chat transcripts.
- Do not create separate files for each prompt, agent, review pass, or temporary report.
- Temporary diagnostics belong in Git history or issue tracking and must be removed before phase closure.
- New scope discovered during implementation is moved to a later phase unless it is required to satisfy the approved acceptance criteria.

## 5. Closing a Phase

A phase can be marked `COMPLETE` only when all are present:

- exact completion commit;
- build command and successful result;
- focused and full-test commands with actual counts;
- live/manual checks, or an explicit statement of which checks were not performed;
- final supported scope and known limitations;
- deployment/configuration instructions where applicable;
- project-owner acceptance.

At closure, consolidate the phase to at most:

```text
phase-N/
  DESIGN.md
  DEPLOYMENT.md
  ACCEPTANCE.md
  FINAL-STATUS.md
```

Delete stale task lists, prompt copies, duplicate reports, temporary plans, and agent handoffs after their durable information is merged.

## 6. Closed-Phase Immutability

- Do not rewrite a completed phase to make a later phase look complete.
- Later phases may reference completed documents but may not mix their status or task lists into them.
- Corrections use a clearly dated `Errata` section and identify the correcting commit.
- A regression reopens the affected phase as `IN PROGRESS` or creates a stabilization phase; it is never hidden by editing old evidence.

## 7. Status Format

Every feature README uses one table:

| Phase | State | Baseline | Completion | Evidence |
|---|---|---|---|---|

No second status table may exist elsewhere for the same feature.

## 8. Package Intake Rule

When a new phase package arrives:

1. Place it under the matching `phase-N/incoming/` folder.
2. Inventory its files and compare its assumptions with current code.
3. Reject duplicate, stale, or contradictory documents.
4. Approve one design and one implementation plan.
5. Move durable information into canonical phase files.
6. Delete `incoming/` when the phase closes.

## 9. Naming and Encoding

- Use phase numbers consistently: `phase-1`, `phase-2`, and so on.
- Use descriptive project names, never model/vendor names in handoff filenames.
- Markdown is UTF-8.
- Keep filenames stable after publication so links remain valid.
- Avoid dates in canonical filenames; dates belong inside evidence sections.

## 10. Enforcement Checklist

Before merging documentation changes:

- exactly one feature README exists;
- phase states use the allowed values;
- no stale test counts or completion claims remain;
- all relative links resolve;
- no credentials, tokens, local connection data, or Vault downloads are present;
- completed-phase evidence is untouched unless an Errata entry explains why;
- temporary package and agent files are removed.
