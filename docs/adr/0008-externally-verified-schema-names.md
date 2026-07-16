# ADR-0008: Schema Names Require External Verification

## Status
Accepted

## Context
AI-generated guesses about Aras logical names can produce incorrect or destructive integrations.

## Decision
Do not invent Aras logical names. Use current schema evidence as the gate for schema-dependent work.

## Consequences
Unknown schema facts block dependent work until verified, but reduce unsafe remote changes.

## Evidence
`docs/ai/06_DECISIONS.md`; `docs/ai/04_ARAS_SCHEMA_MAP.md`.
