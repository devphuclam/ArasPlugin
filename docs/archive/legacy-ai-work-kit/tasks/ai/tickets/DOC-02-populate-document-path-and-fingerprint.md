# DOC-02 — Populate Document path and fingerprint

## Metadata

- Epic: Document Vault
- Dependencies: DOC-01
- Risk: Medium
- Status: Completed

## Goal

Analyze/preview each Document physical file deterministically.

## Scope

Workspace analyze/preview mapping, validation, SHA256 service reuse.

## Implementation outcome

- `RelativePath` and deterministic absolute `SourceFilePath` are carried through preview and document request contracts.
- `DocumentFileIdentityService` computes lowercase SHA-256 and byte size from one validated stream.
- Folder and business-structure document sources use the same identity mapping.
- Missing and unreadable files produce blocking preview warnings.
- No Aras upload, schema, or server-method change was made.

## Verification

- Debug and Release solution builds passed with 0 warnings and 0 errors.
- Debug and Release test-project runs passed 433/433.
- Evidence: `.ai-work/verification/DOC-02-build-debug.log`, `.ai-work/verification/DOC-02-build-release.log`, `.ai-work/verification/DOC-02-test-debug.trx`, `.ai-work/verification/DOC-02-test-release.trx`.

## Required preparation

1. Read `docs/ai/01_AI_RUNBOOK.md`.
2. Read `docs/ai/02_PROJECT_STATE.md`.
3. Read `docs/ai/03_ARCHITECTURE_RULES.md`.
4. Read `docs/ai/04_ARAS_SCHEMA_MAP.md` when Aras-related.
5. Verify dependencies are merged.
6. Start from a clean working tree.

## Acceptance criteria

Preview has absolute/relative path, hash, size; missing/unreadable files block readiness.

In addition:

- Build/test evidence is recorded.
- No false-success path is introduced.
- No secret is logged or committed.
- Cancellation and rollback are addressed where applicable.
- Reviewer BLOCKER/HIGH findings are resolved.

## Non-goals

No Aras upload.

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
