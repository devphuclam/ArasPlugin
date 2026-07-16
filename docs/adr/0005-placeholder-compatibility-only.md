# ADR-0005: Zero-Byte Placeholder Is Compatibility-Only

## Status
Accepted

## Context
Some legacy records lack physical File linkage, but a zero-byte placeholder is not a completed document attachment.

## Decision
Keep placeholders only for compatibility with legacy records, emit a warning, and do not treat them as the default completed behavior.

## Consequences
Legacy records remain representable while physical attachment gaps stay visible.

## Evidence
Archived source: `docs/archive/legacy-ai-work-kit/docs/ai/06_DECISIONS.md`; document/file handling source and tests.
