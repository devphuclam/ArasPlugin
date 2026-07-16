# 06 — Architecture Decision Log

## ADR-001 — SHA256 is content truth

File timestamps and size may optimize scanning but cannot decide equality. SHA256 is the final content identity.

## ADR-002 — Pull uses three-way comparison

Pull compares Base, Local and Remote. A two-way comparison cannot safely distinguish local-only and remote-only changes.

## ADR-003 — No binary auto-merge

`.ics`, `.dwg`, `.pdf` and similar files use Keep Local, Use Server, Save Both, Skip or Cancel.

## ADR-004 — Pull is staged and atomic

Remote files download to `.idea-pdm/temp`, validate, then local files are backed up before apply.

## ADR-005 — Placeholder is compatibility-only

Zero-byte Document placeholders are not the default completed behavior. They remain only for legacy records lacking physical File linkage and must produce a warning.

## ADR-006 — Branch head changes last

A remote branch head is updated only after commit and required file records succeed.

## ADR-007 — One ticket per PR

Large AI-generated changes are difficult to review and rollback. Epics are never implemented in one PR.

## ADR-008 — Schema names are externally verified

An AI may not invent Aras logical names. `04_ARAS_SCHEMA_MAP.md` is the gate.

Add new ADRs; do not silently reverse existing decisions.
