# BR-05 — Pull branch-specific snapshot

## Metadata

- Epic: Remote Branch
- Dependencies: BR-04,PULL-11
- Risk: High
- Status: Not Started

## Goal

Use selected branch as remote source.

## Scope

Pull request/snapshot integration.

## Required preparation

1. Read `docs/ai/01_AI_RUNBOOK.md`.
2. Read `docs/ai/02_PROJECT_STATE.md`.
3. Read `docs/ai/03_ARCHITECTURE_RULES.md`.
4. Read `docs/ai/04_ARAS_SCHEMA_MAP.md` when Aras-related.
5. Verify dependencies are merged.
6. Start from a clean working tree.

## Acceptance criteria

Pull never reads another branch head; stale base shown.

In addition:

- Build/test evidence is recorded.
- No false-success path is introduced.
- No secret is logged or committed.
- Cancellation and rollback are addressed where applicable.
- Reviewer BLOCKER/HIGH findings are resolved.

## Non-goals

No Push.

## AI stop conditions

Stop and report `BLOCKED` if:

- a required Aras logical name or permission is not confirmed;
- the ticket requires destructive data changes not specified here;
- implementation needs more than two major modules or about 15 files;
- a baseline build/test failure prevents verification;
- a dependency ticket is not merged.

## Required final report

- Behavior before/after.
- Files changed.
- Commands and outputs.
- Acceptance criteria mapping.
- Schema/manual test impact.
- Remaining limitations/follow-ups.
