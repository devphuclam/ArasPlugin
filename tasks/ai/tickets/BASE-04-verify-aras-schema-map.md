# BASE-04 — Verify Aras schema map

## Metadata

- Epic: Baseline
- Dependencies: BASE-01
- Risk: Critical
- Status: Partially verified - remaining live permission/version blockers

## Goal

Confirm exact live/test logical names and permissions.

## Scope

Document→File link, PDM Commit schema, Part Library, methods, lifecycle/permissions.

## Required preparation

1. Read `docs/ai/01_AI_RUNBOOK.md`.
2. Read `docs/ai/02_PROJECT_STATE.md`.
3. Read `docs/ai/03_ARCHITECTURE_RULES.md`.
4. Read `docs/ai/04_ARAS_SCHEMA_MAP.md` when Aras-related.
5. Verify dependencies are merged.
6. Start from a clean working tree.

## Acceptance criteria

04_ARAS_SCHEMA_MAP has CONFIRMED-LIVE evidence or explicit blockers.

In addition:

- Build/test evidence is recorded.
- No false-success path is introduced.
- No secret is logged or committed.
- Cancellation and rollback are addressed where applicable.
- Reviewer BLOCKER/HIGH findings are resolved.

## Non-goals

Do not create schema yet.

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

## BASE-04 execution note - 2026-07-10

Planner scope was approved exactly as written. Implementation updated
`docs/ai/04_ARAS_SCHEMA_MAP.md` with explicit live-verification blockers.

No source code or Aras schema was changed.

Live Aras evidence:

- Read-only AML evidence collected from `https://<innovator-server-url>/InnovatorServer/`, database `SampleDatabase`.
- Evidence files are under `.ai-work/verification/BASE-04-live-schema/`.
- Token was used only from `.ai-work/live-token.local.txt` and is not recorded in evidence.

Verification:

- `dotnet build IdeaCadConnector.sln --configuration Debug --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet build IdeaCadConnector.sln --configuration Release --no-restore`: passed, 0 warnings, 0 errors.
- `dotnet test .\tests\IdeaCadConnector.Tests\IdeaCadConnector.Tests.csproj --configuration Debug --no-restore --no-build`: passed, 419/419.

Live confirmed:

- Core ItemTypes and relationships currently used by clone/push.
- `Document File` relationship exists and links `Document` to `File`.
- Part Library ItemTypes and key properties exist.
- All eight listed server methods exist on live.

Live confirmed absent:

- `PDM Commit`.
- `PDM Commit File`.
- `PDM Branch`.

Blocked items still remaining:

- Document File write/version/permission behavior.
- Role-specific permission matrix.
- Lifecycle transition map details.
- Any ticket that requires server-side PDM Branch/Commit schema.

Required to unblock:

- decode live permission and lifecycle configuration; or
- perform approved non-destructive UAT for Document File add/version behavior; and
- plan/deploy the missing PDM Branch/Commit schema before Branch/Commit server-side tickets.
