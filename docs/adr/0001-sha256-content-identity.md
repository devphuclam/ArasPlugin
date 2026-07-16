# ADR-0001: SHA256 Is Content Identity

## Status
Accepted

## Context
File timestamps and size can optimize scanning but cannot establish content equality.

## Decision
Use SHA256 as the final content identity for change detection.

## Consequences
Change detection is content-based and can remain stable across timestamp changes. Hashing adds read cost that may be optimized without changing the identity rule.

## Evidence
`docs/ai/06_DECISIONS.md`; Workspace hash/diff source and tests.
