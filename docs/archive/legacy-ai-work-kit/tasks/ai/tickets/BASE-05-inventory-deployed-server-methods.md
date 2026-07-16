# BASE-05 — Inventory deployed server methods

## Metadata

- Epic: Baseline
- Dependencies: BASE-04
- Risk: High
- Status: Partially Completed - Live only

## Goal

Know which source methods exist in each environment.

## Scope

Dev/Test/Production method presence and version evidence.

## Required preparation

1. Read `docs/ai/01_AI_RUNBOOK.md`.
2. Read `docs/ai/02_PROJECT_STATE.md`.
3. Read `docs/ai/03_ARCHITECTURE_RULES.md`.
4. Read `docs/ai/04_ARAS_SCHEMA_MAP.md` when Aras-related.
5. Verify dependencies are merged.
6. Start from a clean working tree.

## Acceptance criteria

Deployment table updated with dates and discrepancies.

In addition:

- Build/test evidence is recorded.
- No false-success path is introduced.
- No secret is logged or committed.
- Cancellation and rollback are addressed where applicable.
- Reviewer BLOCKER/HIGH findings are resolved.

## Non-goals

Do not deploy to production.

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

## Live inventory record

- Live inventory completed: 2026-07-10
- Implementation start HEAD: `bda183fbf4952bb697f0dcdb1994d78339371eff`
- Live target: `<innovator-server-url>`, database `<db-name>`
- Evidence: `.ai-work/verification/BASE-05-server-methods/method-inventory.md`
- Build/test evidence: `.ai-work/verification/BASE-05-server-methods/BASE-05-build-debug.log`, `.ai-work/verification/BASE-05-server-methods/BASE-05-build-release.log`, `.ai-work/verification/BASE-05-server-methods/BASE-05-test-debug.trx`
- Secret handling: token read from ignored `.ai-work/live-token.local.txt`; token value was not recorded or committed
- Production deploy: none
- Schema change: none
- Scope limitation: Dev/Test endpoints or tokens were not provided in this ticket run, so Dev/Test method presence and version evidence remain unverified.

## Inventory result

| Method | Live | Source/live compare | Notes |
|---|---|---|---|
| `idea_EnsurePrimaryIronCadPartCad` | CONFIRMED-LIVE | DIFFERS | Live method body differs from source. |
| `idea_CommitCadCheckin` | CONFIRMED-LIVE | DIFFERS | Live method body differs from source. |
| `idea_ReviseCad` | CONFIRMED-LIVE | MATCH | Source/live hash matched. |
| `idea_StartDetailedDesign` | CONFIRMED-LIVE | MATCH | Source/live hash matched. |
| `idea_AddPartToLibrary` | CONFIRMED-LIVE | DIFFERS | Live method body differs from source. |
| `idea_RecordPartLibraryUsage` | CONFIRMED-LIVE | MATCH | Source/live hash matched. |
| `idea_GetPrimaryIronCadForPart` | CONFIRMED-LIVE | MATCH | Source/live hash matched; `METHOD_VERSION` 2026-07-08-A in both source and live. |
| `idea_SyncPartLibraryEntryStatus` | CONFIRMED-LIVE | MATCH | Source/live hash matched. |

Summary:

- 8/8 source methods exist on live.
- 5/8 methods match source by normalized SHA-256.
- 3/8 methods differ and must be reconciled before source/live parity is assumed.

## Remaining BASE-05 work

- Collect Dev method presence and version evidence when a Dev Aras endpoint/token is provided.
- Collect Test method presence and version evidence when a Test Aras endpoint/token is provided.
- Update this ticket to `Completed` only after Dev/Test/Production evidence is recorded or the ticket scope is formally narrowed.

## Verification

- Debug build: Succeeded.
- Release build: Succeeded.
- Debug tests: 419 passed, 0 failed, 0 skipped.
