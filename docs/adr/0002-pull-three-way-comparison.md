# ADR-0002: Pull Uses Three-Way Comparison

## Status
Accepted

## Context
A two-way comparison cannot safely distinguish local-only and remote-only changes.

## Decision
Pull compares Base, Local, and Remote states.

## Consequences
Conflict classification requires a retained base and explicit handling of local and remote divergence.

## Evidence
`docs/ai/06_DECISIONS.md`; Workspace diff and conflict tests.
