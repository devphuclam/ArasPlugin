# ADR-0006: Update Branch Head Last

## Status
Accepted

## Context
Advancing a remote branch head before commit and required file records succeed can publish incomplete state.

## Decision
Update the branch head only after commit creation and required file operations succeed.

## Consequences
The branch points only to recoverable completed state; failure leaves the previous head intact.

## Evidence
Archived source: `docs/archive/legacy-ai-work-kit/docs/ai/06_DECISIONS.md`; Workspace commit/branch source and tests.
