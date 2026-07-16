# ADR-0003: Binary CAD Files Are Not Auto-Merged

## Status
Accepted

## Context
CAD, drawing, and document binaries do not have a safe generic text merge strategy.

## Decision
Binary conflicts use explicit choices such as Keep Local, Use Server, Save Both, Skip, or Cancel.

## Consequences
Conflict handling is explicit and avoids silent data loss.

## Evidence
Archived source: `docs/archive/legacy-ai-work-kit/docs/ai/06_DECISIONS.md`; Workspace conflict behavior and tests.
