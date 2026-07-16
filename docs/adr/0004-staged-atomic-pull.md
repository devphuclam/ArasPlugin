# ADR-0004: Pull Is Staged and Atomic

## Status
Accepted

## Context
Applying remote files directly can leave a partially updated local workspace.

## Decision
Download to a temporary location, validate, back up local files, then apply the change.

## Consequences
Failures preserve recoverable state and make rollback possible, at the cost of temporary storage and additional validation.

## Evidence
Archived source: `docs/archive/legacy-ai-work-kit/docs/ai/06_DECISIONS.md`; Workspace package and output-safety source/tests.
